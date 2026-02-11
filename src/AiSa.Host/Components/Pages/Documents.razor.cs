using System.Net.Http.Json;
using AiSa.Host.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.FluentUI.AspNetCore.Components;
using Icons = Microsoft.FluentUI.AspNetCore.Components.Icons;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace AiSa.Host.Components.Pages;

public partial class Documents
{
    private bool heroDismissed = false;
    private bool isDragOver = false;
    private bool isUploading = false;
    private bool isRefreshing = false;
    private bool isTriggeringFileInput = false;
    private IBrowserFile? selectedFile = null;
    private List<DocumentItem>? documents = null;

    [Inject]
    private HttpClient Http { get; set; } = default!;

    [Inject]
    private ILoadingService LoadingService { get; set; } = default!;

    [Inject]
    private IToastNotificationService ToastService { get; set; } = default!;

    [Inject]
    private ILogger<Documents> Logger { get; set; } = default!;

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        await RefreshDocuments();
    }

    private void DismissHero()
    {
        heroDismissed = true;
    }


    private void HandleFileSelected(InputFileChangeEventArgs e)
    {
        var file = e.File;
        if (file != null && file.Name.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
        {
            selectedFile = file;
            StateHasChanged();
        }
        else
        {
            ToastService.ShowWarning("Please select a .txt file", "Invalid file type");
        }
    }

    private void ClearSelectedFile()
    {
        selectedFile = null;
        StateHasChanged();
    }

    private async Task TriggerFileInput()
    {
        if (isTriggeringFileInput)
            return;

        isTriggeringFileInput = true;
        try
        {
            await JSRuntime.InvokeVoidAsync("eval", "document.getElementById('file-input')?.click()");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error triggering file input");
        }
        finally
        {
            // Reset after a short delay to allow the file dialog to open
            await Task.Delay(100);
            isTriggeringFileInput = false;
        }
    }


    private void HandleDragOver(DragEventArgs e)
    {
        isDragOver = true;
        StateHasChanged();
    }

    private void HandleDragLeave(DragEventArgs e)
    {
        isDragOver = false;
        StateHasChanged();
    }

    private void HandleDrop(DragEventArgs e)
    {
        isDragOver = false;
        // Note: Drag & drop file handling requires JS interop
        // For now, we'll show a message to use the file input
        ToastService.ShowInfo("Please use the 'Choose File' button to upload files", "Drag & Drop");
        StateHasChanged();
    }

    private async Task HandleUpload()
    {
        if (selectedFile == null || isUploading)
            return;

        isUploading = true;
        StateHasChanged();

        await LoadingService.ExecuteWithLoadingAsync(async cancellationToken =>
        {
            try
            {
                using var content = new MultipartFormDataContent();
                using var fileStream = selectedFile.OpenReadStream(maxAllowedSize: 10 * 1024 * 1024); // 10MB max
                content.Add(new StreamContent(fileStream), "file", selectedFile.Name);

                var response = await Http.PostAsync("/api/documents", content, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<UploadResult>(cancellationToken: cancellationToken);
                    if (result != null)
                    {
                        if (result.status == "completed")
                        {
                            ToastService.ShowSuccess(
                                $"Document '{result.sourceName}' ingested successfully with {result.chunkCount} chunks",
                                "Upload Successful");
                        }
                        else
                        {
                            ToastService.ShowError(
                                result.errorMessage ?? "Document ingestion failed",
                                "Upload Failed");
                        }
                    }

                    // Clear selected file and refresh list
                    selectedFile = null;
                    await RefreshDocuments();
                }
                else
                {
                    var problemDetails = await response.Content.ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ProblemDetails>(cancellationToken: cancellationToken);
                    var errorMessage = problemDetails?.Detail ?? "Upload failed. Please try again.";
                    ToastService.ShowError(errorMessage, "Upload Error");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error uploading document. FileName: {FileName}", selectedFile.Name);
                ToastService.ShowError("An error occurred while uploading the document. Please try again.", "Upload Error");
            }
            finally
            {
                isUploading = false;
            }
        }, key: "document-upload");
    }

    private async Task RefreshDocuments()
    {
        isRefreshing = true;
        StateHasChanged();

        await LoadingService.ExecuteWithLoadingAsync(async cancellationToken =>
        {
            try
            {
                var response = await Http.GetFromJsonAsync<List<DocumentItem>>("/api/documents", cancellationToken);
                documents = response ?? new List<DocumentItem>();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error loading documents list");
                ToastService.ShowError("Failed to load documents. Please try again.", "Error");
                documents = new List<DocumentItem>();
            }
            finally
            {
                isRefreshing = false;
            }
        }, key: "documents-refresh");
    }

    private int totalDocuments => documents?.Count ?? 0;
    private int totalChunks => documents?.Sum(d => d.chunkCount) ?? 0;
    private int completedDocuments => documents?.Count(d => d.status == "completed") ?? 0;
    private int failedDocuments => documents?.Count(d => d.status == "failed") ?? 0;

    private string FormatFileSize(long bytes)
    {
        if (bytes < 1024)
            return $"{bytes} B";
        else if (bytes < 1024 * 1024)
            return $"{bytes / 1024.0:F1} KB";
        else
            return $"{bytes / (1024.0 * 1024.0):F1} MB";
    }

    private string FormatRelativeTime(DateTimeOffset timestamp)
    {
        var now = DateTimeOffset.UtcNow;
        var diff = now - timestamp;

        if (diff.TotalSeconds < 60)
        {
            var seconds = (int)diff.TotalSeconds;
            return seconds == 0 ? "just now" : $"{seconds}s ago";
        }
        else if (diff.TotalMinutes < 60)
        {
            var minutes = (int)diff.TotalMinutes;
            return $"{minutes}m ago";
        }
        else if (diff.TotalHours < 24)
        {
            var hours = (int)diff.TotalHours;
            return $"{hours}h ago";
        }
        else
        {
            var days = (int)diff.TotalDays;
            return $"{days}d ago";
        }
    }

    private Appearance GetStatusAppearance(string status)
    {
        return status.ToLowerInvariant() switch
        {
            "completed" => Appearance.Accent,
            "failed" => Appearance.Neutral,
            _ => Appearance.Neutral
        };
    }


    private Microsoft.FluentUI.AspNetCore.Components.Icon GetStatusIcon(string status)
    {
        return status.ToLowerInvariant() switch
        {
            "completed" => new Icons.Regular.Size16.CheckmarkCircle(),
            "failed" => new Icons.Regular.Size16.Alert(),
            _ => new Icons.Regular.Size16.Clock()
        };
    }

    private string GetStatusText(string status)
    {
        return status.ToLowerInvariant() switch
        {
            "completed" => "Completed",
            "failed" => "Failed",
            _ => "Unknown"
        };
    }
}

public class DocumentItem
{
    public string documentId { get; set; } = string.Empty;
    public string sourceName { get; set; } = string.Empty;
    public int chunkCount { get; set; }
    public DateTimeOffset indexedAt { get; set; }
    public string status { get; set; } = string.Empty;
}

public class UploadResult
{
    public string documentId { get; set; } = string.Empty;
    public string sourceName { get; set; } = string.Empty;
    public string status { get; set; } = string.Empty;
    public int chunkCount { get; set; }
    public DateTimeOffset indexedAt { get; set; }
    public string? errorMessage { get; set; }
}
