using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using AiSa.Application.Eval;
using AiSa.Application.Models;
using AiSa.Domain.Eval;

var (datasetPath, baseUrl, outputDirectory, maxQuestions, thresholds) = ParseArgs(args);

Console.WriteLine("AiSa EvalRunner");
Console.WriteLine($"Dataset : {datasetPath}");
Console.WriteLine($"Base URL: {baseUrl}");
Console.WriteLine($"Output  : {outputDirectory}");
if (maxQuestions is { } lim && lim > 0)
    Console.WriteLine($"Limit   : {lim} questions");
Console.WriteLine();

if (!File.Exists(datasetPath))
{
    Console.Error.WriteLine($"Dataset file not found: {datasetPath}");
    return 1;
}

var datasetJson = await File.ReadAllTextAsync(datasetPath);
var dataset = JsonSerializer.Deserialize<EvalDataset>(datasetJson);
if (dataset is null || dataset.Questions.Count == 0)
{
    Console.Error.WriteLine("Dataset is empty or invalid.");
    return 1;
}

var questions = dataset.Questions;
if (maxQuestions is { } cap && cap > 0 && cap < questions.Count)
{
    questions = questions.Take(cap).ToList();
}

Directory.CreateDirectory(outputDirectory);

using var http = new HttpClient { BaseAddress = new Uri(baseUrl) };
var evalService = new EvalService();

var results = new List<EvalResult>(questions.Count);
var stopwatch = Stopwatch.StartNew();

foreach (var question in questions)
{
    var questionStopwatch = Stopwatch.StartNew();

    ChatResponse? response;
    try
    {
        var httpResponse = await http.PostAsJsonAsync("/api/chat", new ChatRequest { Message = question.Question });
        if (!httpResponse.IsSuccessStatusCode)
        {
            Console.Error.WriteLine($"HTTP {(int)httpResponse.StatusCode} from /api/chat.");
            response = null;
        }
        else
        {
            response = await httpResponse.Content.ReadFromJsonAsync<ChatResponse>();
        }
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Error calling /api/chat: {ex.Message}");
        response = null;
    }

    questionStopwatch.Stop();

    var responseText = response?.Response ?? string.Empty;
    var citations = response?.Citations ?? Array.Empty<Citation>();

    var answered = !string.IsNullOrWhiteSpace(responseText) &&
                   !responseText.Contains("I don't know", StringComparison.OrdinalIgnoreCase);

    var citationsPresent = citations.Count > 0;

    bool? citationAccurate = null;
    if (question.ExpectedDocIds is { Count: > 0 } expectedDocIds && citationsPresent)
    {
        var expectedSet = new HashSet<string>(expectedDocIds, StringComparer.OrdinalIgnoreCase);
        var citedSources = citations.Select(c => c.SourceName);
        citationAccurate = citedSources.Any(s => expectedSet.Contains(s));
    }

    bool? hallucinationDetected = null;
    if (question.ExpectedKeyFacts.Count > 0 && !string.IsNullOrWhiteSpace(responseText))
    {
        var missingFacts = question.ExpectedKeyFacts.Count(f =>
            !responseText.Contains(f, StringComparison.OrdinalIgnoreCase));
        hallucinationDetected = missingFacts > 0;
    }

    results.Add(new EvalResult
    {
        Question = question.Question,
        ActualResponse = responseText,
        Answered = answered,
        CitationsPresent = citationsPresent,
        CitationAccurate = citationAccurate,
        HallucinationDetected = hallucinationDetected,
        LatencyMs = questionStopwatch.ElapsedMilliseconds
    });
}

stopwatch.Stop();

var metrics = evalService.ComputeMetrics(results);
var report = new EvalReport
{
    DatasetName = dataset.Name,
    DatasetVersion = dataset.Version,
    Metrics = metrics,
    Results = results,
    RunTimestamp = DateTimeOffset.UtcNow,
    RunDurationMs = stopwatch.ElapsedMilliseconds
};

var timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmm");
var reportPath = Path.Combine(outputDirectory, $"{timestamp}.json");

var reportJson = JsonSerializer.Serialize(report, new JsonSerializerOptions
{
    WriteIndented = true
});
await File.WriteAllTextAsync(reportPath, reportJson);

Console.WriteLine("Eval run completed.");
Console.WriteLine($"Questions           : {metrics.TotalQuestions}");
Console.WriteLine($"Answered rate       : {metrics.AnsweredRate:P1}");
Console.WriteLine($"Citation presence   : {metrics.CitationPresenceRate:P1}");
Console.WriteLine($"Citation accuracy   : {metrics.CitationAccuracyRate:P1}");
Console.WriteLine($"Hallucination rate  : {metrics.HallucinationRate:P1}");
Console.WriteLine($"Avg latency (ms)    : {metrics.AvgLatencyMs:F0}");
Console.WriteLine($"p95 latency (ms)    : {metrics.P95LatencyMs:F0}");
Console.WriteLine($"Run duration (ms)   : {report.RunDurationMs}");
Console.WriteLine($"Report written to   : {reportPath}");

if (thresholds is not null &&
    (thresholds.MinAnsweredRate is not null || thresholds.MinCitationPresenceRate is not null))
{
    var failure = EvalThresholdGate.GetFailureReason(metrics, thresholds);
    if (failure is not null)
    {
        Console.Error.WriteLine(failure);
        return 3;
    }
}

return 0;

static (string DatasetPath, string BaseUrl, string OutputDirectory, int? MaxQuestions, EvalThresholdOptions? Thresholds)
    ParseArgs(string[] arguments)
{
    var dataset = "eval/datasets/base.json";
    var baseUrl = "http://localhost:5000";
    var output = "eval/reports";
    int? maxQuestions = null;
    double? minAnswered = null;
    double? minCitationPresence = null;

    for (var i = 0; i < arguments.Length; i++)
    {
        switch (arguments[i])
        {
            case "--dataset" when i + 1 < arguments.Length:
                dataset = arguments[++i];
                break;
            case "--base-url" when i + 1 < arguments.Length:
                baseUrl = arguments[++i];
                break;
            case "--output" when i + 1 < arguments.Length:
                output = arguments[++i];
                break;
            case "--max-questions" when i + 1 < arguments.Length && int.TryParse(arguments[++i], out var maxQ):
                maxQuestions = maxQ;
                break;
            case "--min-answered-rate" when i + 1 < arguments.Length && double.TryParse(
                arguments[++i],
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var minA):
                minAnswered = minA;
                break;
            case "--min-citation-presence-rate" when i + 1 < arguments.Length && double.TryParse(
                arguments[++i],
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var minC):
                minCitationPresence = minC;
                break;
        }
    }

    EvalThresholdOptions? thresholds = minAnswered is not null || minCitationPresence is not null
        ? new EvalThresholdOptions
        {
            MinAnsweredRate = minAnswered,
            MinCitationPresenceRate = minCitationPresence
        }
        : null;

    return (dataset, baseUrl, output, maxQuestions, thresholds);
}
