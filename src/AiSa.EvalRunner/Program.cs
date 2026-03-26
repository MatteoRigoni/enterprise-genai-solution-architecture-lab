using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using AiSa.Application.Models;
using AiSa.Application.Eval;
using AiSa.Domain.Eval;

var (datasetPath, baseUrl, outputDirectory) = ParseArgs(args);

Console.WriteLine("AiSa EvalRunner");
Console.WriteLine($"Dataset : {datasetPath}");
Console.WriteLine($"Base URL: {baseUrl}");
Console.WriteLine($"Output  : {outputDirectory}");
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

Directory.CreateDirectory(outputDirectory);

using var http = new HttpClient { BaseAddress = new Uri(baseUrl) };
var evalService = new EvalService();

var results = new List<EvalResult>(dataset.Questions.Count);
var stopwatch = Stopwatch.StartNew();

foreach (var question in dataset.Questions)
{
    var questionStopwatch = Stopwatch.StartNew();

    ChatResponse? response;
    try
    {
        var httpResponse = await http.PostAsJsonAsync("/api/chat", new ChatRequest { Message = question.Question });
        response = await httpResponse.Content.ReadFromJsonAsync<ChatResponse>();
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

return 0;

static (string DatasetPath, string BaseUrl, string OutputDirectory) ParseArgs(string[] arguments)
{
    var dataset = "eval/datasets/base.json";
    var baseUrl = "http://localhost:5000";
    var output = "eval/reports";

    for (var i = 0; i < arguments.Length - 1; i++)
    {
        switch (arguments[i])
        {
            case "--dataset":
                dataset = arguments[i + 1];
                break;
            case "--base-url":
                baseUrl = arguments[i + 1];
                break;
            case "--output":
                output = arguments[i + 1];
                break;
        }
    }

    return (dataset, baseUrl, output);
}
