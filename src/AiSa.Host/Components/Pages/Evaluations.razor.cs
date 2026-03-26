using System.Net.Http.Json;
using AiSa.Domain.Eval;
using AiSa.Host.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace AiSa.Host.Components.Pages;

public partial class Evaluations
{
    private bool heroDismissed = false;
    private bool isRunning = false;
    private EvalReport? latestReport = null;

    [Inject]
    private HttpClient Http { get; set; } = default!;

    [Inject]
    private IToastNotificationService ToastService { get; set; } = default!;

    [Inject]
    private ILogger<Evaluations> Logger { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        await LoadLatestReport();
    }

    private void DismissHero()
    {
        heroDismissed = true;
    }

    private async Task LoadLatestReport()
    {
        try
        {
            latestReport = await Http.GetFromJsonAsync<EvalReport>("/api/eval/reports/latest");
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            latestReport = null;
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to load latest eval report");
            latestReport = null;
        }
    }

    private async Task RunSmokeEval()
    {
        if (isRunning)
            return;

        isRunning = true;
        StateHasChanged();

        try
        {
            var response = await Http.PostAsync("/api/eval/run", null);
            if (response.IsSuccessStatusCode)
            {
                latestReport = await response.Content.ReadFromJsonAsync<EvalReport>();
                ToastService.ShowSuccess("Smoke eval completed. Metrics updated.", "Eval Done");
            }
            else
            {
                ToastService.ShowError("Eval run failed. Check that the dataset file exists and the API is healthy.", "Eval Error");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error running smoke eval");
            ToastService.ShowError("An error occurred while running the eval.", "Eval Error");
        }
        finally
        {
            isRunning = false;
            StateHasChanged();
        }
    }

    private string GetMetricStatus(string metric, double value) => metric switch
    {
        "answered"      => value >= 0.80 ? "metric-green" : value >= 0.60 ? "metric-yellow" : "metric-red",
        "citation"      => value >= 0.70 ? "metric-green" : value >= 0.50 ? "metric-yellow" : "metric-red",
        "accuracy"      => value >= 0.70 ? "metric-green" : value >= 0.50 ? "metric-yellow" : "metric-red",
        "hallucination" => value <= 0.10 ? "metric-green" : value <= 0.25 ? "metric-yellow" : "metric-red",
        "avglatency"    => value <= 5000  ? "metric-green" : value <= 8000  ? "metric-yellow" : "metric-red",
        "p95latency"    => value <= 8000  ? "metric-green" : value <= 12000 ? "metric-yellow" : "metric-red",
        _               => "metric-green"
    };
}
