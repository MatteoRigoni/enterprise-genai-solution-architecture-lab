using System.Net.Http.Json;
using AiSa.Application.Models;
using AiSa.Host.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;

namespace AiSa.Host.Components.Pages;

public partial class Chat
{
    private List<ChatMessage> messages = new();
    private string currentMessage = string.Empty;
    private bool isSending = false;
    private string? errorMessage = null;
    private bool heroDismissed = false;

    [Inject]
    private HttpClient Http { get; set; } = default!;

    [Inject]
    private ILoadingService LoadingService { get; set; } = default!;

    [Inject]
    private ILogger<Chat> Logger { get; set; } = default!;

    private async Task HandleSend()
    {
        if (string.IsNullOrWhiteSpace(currentMessage) || isSending)
            return;

        // Add user message to list immediately
        var userMessage = currentMessage.Trim();
        messages.Add(new ChatMessage
        {
            Role = "User",
            Text = userMessage,
            CssClass = "user-message",
            Timestamp = DateTimeOffset.Now
        });

        var messageToSend = currentMessage;
        currentMessage = string.Empty;
        errorMessage = null;
        isSending = true;

        // Use centralized loading service with a specific key for chat operations
        await LoadingService.ExecuteWithLoadingAsync(async cancellationToken =>
        {
            try
            {
                // Call API endpoint
                var request = new ChatRequest
                {
                    Message = messageToSend
                };

                var response = await Http.PostAsJsonAsync("/api/chat", request, cancellationToken);
                
                if (response.IsSuccessStatusCode)
                {
                    var chatResponse = await response.Content.ReadFromJsonAsync<ChatResponse>(cancellationToken: cancellationToken);
                    
                    if (chatResponse != null)
                    {
                        // Add assistant response to message list
                        messages.Add(new ChatMessage
                        {
                            Role = "Assistant",
                            Text = chatResponse.Response,
                            CorrelationId = chatResponse.CorrelationId,
                            CssClass = "assistant-message",
                            Timestamp = DateTimeOffset.Now
                        });
                    }
                }
                else
                {
                    // Handle error response (ProblemDetails)
                    // Log technical details internally, show user-friendly message
                    var problemDetails = await response.Content.ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ProblemDetails>(cancellationToken: cancellationToken);
                    
                    Logger.LogError(
                        "Chat API error. StatusCode: {StatusCode}, Path: /api/chat, Detail: {Detail}, CorrelationId: {CorrelationId}",
                        response.StatusCode,
                        problemDetails?.Detail ?? "Unknown error",
                        problemDetails?.Extensions?.TryGetValue("correlationId", out var corrId) == true ? corrId : null);
                    
                    // Show user-friendly message only
                    errorMessage = "Unable to send message. Please try again later.";
                }
            }
            catch (HttpRequestException ex)
            {
                // Log technical details internally
                Logger.LogError(ex, "Network error while sending chat message. Message: {Message}", ex.Message);
                
                // Show user-friendly message only
                errorMessage = "Network error. Please check your connection and try again.";
            }
            catch (Exception ex)
            {
                // Log technical details internally
                Logger.LogError(ex, "Unexpected error while sending chat message. ExceptionType: {ExceptionType}, Message: {Message}", 
                    ex.GetType().Name, ex.Message);
                
                // Show user-friendly message only
                errorMessage = "An unexpected error occurred. Please try again later.";
            }
            finally
            {
                isSending = false;
            }
        }, key: "chat-send");
    }

    private void DismissHero()
    {
        heroDismissed = true;
    }

    private async Task HandleKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Enter" && !e.ShiftKey)
        {
            await HandleSend();
        }
    }

    private void HandleInput(ChangeEventArgs e)
    {
        // Update immediately on every keystroke for reactive button state
        currentMessage = e.Value?.ToString() ?? string.Empty;
        StateHasChanged();
    }
}

public class ChatMessage
{
    public string Role { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string? CorrelationId { get; set; }
    public string CssClass { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.Now;
}

