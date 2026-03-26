using System.Diagnostics;
using System.Text.Json;
using AiSa.Application;
using AiSa.Application.Eval;
using AiSa.Application.Models;
using AiSa.Domain.Eval;
using Microsoft.AspNetCore.Mvc;

namespace AiSa.Host.Endpoints;

internal static class EvalEndpoints
{
    public static IEndpointRouteBuilder MapEvalEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/eval");

        api.MapPost("/run", async (
                IEvalService evalService,
                IChatService chatService,
                ActivitySource activitySource,
                IWebHostEnvironment environment,
                CancellationToken cancellationToken) =>
            {
                var repoRoot = Path.GetFullPath(Path.Combine(environment.ContentRootPath, "..", ".."));
                var datasetPath = Path.Combine(repoRoot, "eval", "datasets", "base.json");
                if (!File.Exists(datasetPath))
                {
                    return Results.Problem(
                        statusCode: StatusCodes.Status500InternalServerError,
                        title: "Eval dataset not found",
                        detail: $"Expected dataset at '{datasetPath}'.");
                }

                var datasetJson = await File.ReadAllTextAsync(datasetPath, cancellationToken);
                var dataset = JsonSerializer.Deserialize<EvalDataset>(datasetJson);
                if (dataset is null || dataset.Questions.Count == 0)
                {
                    return Results.Problem(
                        statusCode: StatusCodes.Status500InternalServerError,
                        title: "Eval dataset invalid",
                        detail: "Dataset is empty or malformed.");
                }

                const int smokeQuestionCount = 5;
                var questions = dataset.Questions.Take(smokeQuestionCount).ToList();

                var results = new List<EvalResult>(questions.Count);
                var stopwatch = Stopwatch.StartNew();

                using var activity = activitySource.StartActivity("eval.run", ActivityKind.Internal);
                activity?.SetTag("eval.dataset.name", dataset.Name);
                activity?.SetTag("eval.dataset.version", dataset.Version);
                activity?.SetTag("eval.question.count", questions.Count);

                foreach (var question in questions)
                {
                    var questionStopwatch = Stopwatch.StartNew();

                    ChatResponse? response;
                    try
                    {
                        response = await chatService.ProcessChatAsync(
                            new ChatRequest { Message = question.Question },
                            cancellationToken);
                    }
                    catch (Exception)
                    {
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

                var reportsDirectory = Path.Combine(repoRoot, "eval", "reports");
                Directory.CreateDirectory(reportsDirectory);

                var timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmm");
                var reportPath = Path.Combine(reportsDirectory, $"{timestamp}.json");

                var reportJson = JsonSerializer.Serialize(report, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                await File.WriteAllTextAsync(reportPath, reportJson, cancellationToken);

                activity?.SetTag("eval.metrics.answeredRate", metrics.AnsweredRate);
                activity?.SetTag("eval.metrics.citationPresenceRate", metrics.CitationPresenceRate);
                activity?.SetTag("eval.metrics.citationAccuracyRate", metrics.CitationAccuracyRate);
                activity?.SetTag("eval.metrics.hallucinationRate", metrics.HallucinationRate);
                activity?.SetTag("eval.metrics.p95LatencyMs", metrics.P95LatencyMs);
                activity?.SetStatus(ActivityStatusCode.Ok);

                return Results.Ok(report);
            })
            .WithName("RunEval")
            .WithSummary("Run smoke evaluation")
            .WithDescription("""
                Runs a small smoke evaluation against the chat service using the base eval dataset.
                Executes a subset of questions (default: first 5) in-process and returns an EvalReport.
                """)
            .WithTags("Eval")
            .Produces<EvalReport>(StatusCodes.Status200OK, "application/json")
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        api.MapGet("/reports/latest", async (
                IWebHostEnvironment environment,
                CancellationToken cancellationToken) =>
            {
                var repoRoot = Path.GetFullPath(Path.Combine(environment.ContentRootPath, "..", ".."));
                var reportsDirectory = Path.Combine(repoRoot, "eval", "reports");
                if (!Directory.Exists(reportsDirectory))
                {
                    return Results.NotFound(new ProblemDetails
                    {
                        Status = StatusCodes.Status404NotFound,
                        Title = "No eval reports found",
                        Detail = "The eval reports directory does not exist."
                    });
                }

                var files = Directory.GetFiles(reportsDirectory, "*.json", SearchOption.TopDirectoryOnly);
                if (files.Length == 0)
                {
                    return Results.NotFound(new ProblemDetails
                    {
                        Status = StatusCodes.Status404NotFound,
                        Title = "No eval reports found",
                        Detail = "No eval report files were found."
                    });
                }

                var latest = files.OrderBy(f => f).Last();
                var json = await File.ReadAllTextAsync(latest, cancellationToken);
                var report = JsonSerializer.Deserialize<EvalReport>(json);

                if (report is null)
                {
                    return Results.Problem(
                        statusCode: StatusCodes.Status500InternalServerError,
                        title: "Failed to read eval report",
                        detail: "The latest eval report file could not be deserialized.");
                }

                return Results.Ok(report);
            })
            .WithName("GetLatestEvalReport")
            .WithSummary("Get latest eval report")
            .WithDescription("""
                Returns the most recent eval report from the eval/reports directory.
                """)
            .WithTags("Eval")
            .Produces<EvalReport>(StatusCodes.Status200OK, "application/json")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        return app;
    }
}

