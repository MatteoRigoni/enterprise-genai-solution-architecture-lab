using System.Net.Http.Json;
using AiSa.Application;
using AiSa.Application.Models;
using AiSa.Host.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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

    [Inject]
    private IOptions<StreamingOptions> StreamingOptions { get; set; } = default!;

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
        StateHasChanged(); // Force UI update to clear the input field immediately

        // Use centralized loading service with a specific key for chat operations
        await LoadingService.ExecuteWithLoadingAsync(async cancellationToken =>
        {
            try
            {
                // Check if streaming is enabled via configuration
                var useStreaming = StreamingOptions.Value.Enabled;

                if (useStreaming)
                {
                    Logger.LogDebug("Using streaming chat endpoint");
                    await HandleStreamingChat(messageToSend, cancellationToken);
                }
                else
                {
                    Logger.LogDebug("Using non-streaming chat endpoint (cache enabled)");
                    await HandleNonStreamingChat(messageToSend, cancellationToken);
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
        if (e.Value != null)
        {
            currentMessage = e.Value.ToString() ?? string.Empty;
            StateHasChanged();
        }
    }

    private async Task HandleNonStreamingChat(string messageToSend, CancellationToken cancellationToken)
    {
        Logger.LogDebug("Handling non-streaming chat request. MessageLength: {MessageLength}", messageToSend.Length);
        
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
                Logger.LogDebug("Non-streaming chat response received. ResponseLength: {ResponseLength}, CorrelationId: {CorrelationId}",
                    chatResponse.Response?.Length ?? 0, chatResponse.CorrelationId);
                
                // Add assistant response to message list
                messages.Add(new ChatMessage
                {
                    Role = "Assistant",
                    Text = chatResponse.Response,
                    CorrelationId = chatResponse.CorrelationId,
                    MessageId = chatResponse.MessageId,
                    CssClass = "assistant-message",
                    Timestamp = DateTimeOffset.Now
                });
                StateHasChanged();
            }
        }
        else
        {
            // Handle error response (ProblemDetails)
            var problemDetails = await response.Content.ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ProblemDetails>(cancellationToken: cancellationToken);
            
            Logger.LogError(
                "Chat API error. StatusCode: {StatusCode}, Path: /api/chat, Detail: {Detail}, CorrelationId: {CorrelationId}",
                response.StatusCode,
                problemDetails?.Detail ?? "Unknown error",
                problemDetails?.Extensions?.TryGetValue("correlationId", out var corrId) == true ? corrId : null);
            
            errorMessage = problemDetails?.Detail ?? "Unable to send message. Please try again later.";
        }
    }

    private async Task HandleStreamingChat(string messageToSend, CancellationToken cancellationToken)
    {
        // Create assistant message placeholder
        var assistantMessage = new ChatMessage
        {
            Role = "Assistant",
            Text = "",
            CssClass = "assistant-message",
            Timestamp = DateTimeOffset.Now
        };
        messages.Add(assistantMessage);
        StateHasChanged();

        // Track chunks received for partial recovery (declared outside try for catch access)
        var chunksReceived = 0;
        var partialTextReceived = false;
        
        // Setup timeout for ReadLineAsync (declared outside try for catch access)
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(StreamingOptions.Value.TimeoutSeconds));

        try
        {
            var request = new ChatRequest { Message = messageToSend };
            var jsonContent = System.Text.Json.JsonSerializer.Serialize(request);
            var content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");

            var response = await Http.PostAsync("/api/chat/stream", content, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                var problemDetails = await response.Content.ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ProblemDetails>(cancellationToken: cancellationToken);
                errorMessage = problemDetails?.Detail ?? "Unable to send message. Please try again later.";
                messages.Remove(assistantMessage);
                StateHasChanged();
                return;
            }

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new System.IO.StreamReader(stream);

            string? line;
            while ((line = await reader.ReadLineAsync()) != null && !timeoutCts.Token.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data: "))
                    continue;

                var jsonData = line.Substring(6); // Remove "data: " prefix
                if (string.IsNullOrWhiteSpace(jsonData) || jsonData == "[DONE]")
                    continue;

                try
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(jsonData);
                    var root = doc.RootElement;

                    if (root.TryGetProperty("type", out var type))
                    {
                        var messageType = type.GetString();
                        
                        if (messageType == "metadata" && root.TryGetProperty("data", out var metadata))
                        {
                            if (metadata.TryGetProperty("correlationId", out var corrId))
                                assistantMessage.CorrelationId = corrId.GetString();
                            if (metadata.TryGetProperty("messageId", out var msgId))
                                assistantMessage.MessageId = msgId.GetString();
                        }
                        else if (messageType == "chunk" && root.TryGetProperty("data", out var chunkData))
                        {
                            var chunk = chunkData.GetString() ?? "";
                            if (!string.IsNullOrEmpty(chunk))
                            {
                                assistantMessage.Text += chunk;
                                chunksReceived++;
                                partialTextReceived = true;
                                StateHasChanged(); // Update UI incrementally
                            }
                        }
                        else if (messageType == "done")
                        {
                            break; // Stream complete
                        }
                        else if (messageType == "error" && root.TryGetProperty("error", out var errorData))
                        {
                            errorMessage = errorData.GetString() ?? "An error occurred.";
                            break;
                        }
                    }
                }
                catch (System.Text.Json.JsonException ex)
                {
                    Logger.LogWarning(ex, "Failed to parse SSE chunk. Line: {Line}", line);
                }
            }
        }
        catch (OperationCanceledException) when (timeoutCts.Token.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            // Timeout during streaming - preserve partial chunks if any
            Logger.LogWarning("Streaming timeout after {ChunksReceived} chunks. PartialTextLength: {PartialLength}", 
                chunksReceived, assistantMessage.Text.Length);
            
            if (StreamingOptions.Value.EnableAutomaticFallback && partialTextReceived)
            {
                // Preserve partial text and complete with fallback
                Logger.LogInformation("Preserving {PartialLength} characters of partial response, completing with fallback", 
                    assistantMessage.Text.Length);
                
                try
                {
                    // Get complete response from non-streaming endpoint
                    var completeResponse = await GetCompleteResponseAsync(messageToSend, cancellationToken);
                    
                    if (completeResponse != null && !string.IsNullOrEmpty(completeResponse.Response))
                    {
                        // If complete response starts with partial text, use complete
                        // Otherwise append completion indicator
                        if (completeResponse.Response.StartsWith(assistantMessage.Text, StringComparison.Ordinal))
                        {
                            // Complete response includes partial - replace with full
                            assistantMessage.Text = completeResponse.Response;
                        }
                        else
                        {
                            // Different response - append completion indicator
                            assistantMessage.Text += "\n\n[Response completed via fallback]";
                        }
                        
                        // Update metadata if available
                        if (!string.IsNullOrEmpty(completeResponse.CorrelationId))
                            assistantMessage.CorrelationId = completeResponse.CorrelationId;
                        if (!string.IsNullOrEmpty(completeResponse.MessageId))
                            assistantMessage.MessageId = completeResponse.MessageId;
                        
                        StateHasChanged();
                        return; // Success
                    }
                }
                catch (Exception fallbackEx)
                {
                    Logger.LogError(fallbackEx, "Fallback completion failed");
                    // Keep partial text - better than nothing
                }
            }
            else if (StreamingOptions.Value.EnableAutomaticFallback)
            {
                // No partial text - full fallback
                if (messages.Contains(assistantMessage))
                {
                    messages.Remove(assistantMessage);
                    StateHasChanged();
                }
                await HandleNonStreamingChat(messageToSend, cancellationToken);
                return;
            }
            else
            {
                // Fallback disabled - show timeout error
                errorMessage = "Response timed out. Partial response shown above.";
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Streaming chat failed. ExceptionType: {ExceptionType}, Message: {Message}, ChunksReceived: {ChunksReceived}", 
                ex.GetType().Name, ex.Message, chunksReceived);
            
            // Automatic fallback to non-streaming if enabled
            if (StreamingOptions.Value.EnableAutomaticFallback)
            {
                Logger.LogInformation("Attempting automatic fallback to non-streaming endpoint. PartialTextLength: {PartialLength}", 
                    assistantMessage.Text.Length);
                
                if (partialTextReceived && !string.IsNullOrEmpty(assistantMessage.Text))
                {
                    // Preserve partial text and try to complete
                    try
                    {
                        var completeResponse = await GetCompleteResponseAsync(messageToSend, cancellationToken);
                        
                        if (completeResponse != null && !string.IsNullOrEmpty(completeResponse.Response))
                        {
                            // Use complete response if it's better
                            if (completeResponse.Response.Length > assistantMessage.Text.Length)
                            {
                                assistantMessage.Text = completeResponse.Response;
                            }
                            else
                            {
                                // Keep partial + indicator
                                assistantMessage.Text += "\n\n[Response completed via fallback]";
                            }
                            
                            if (!string.IsNullOrEmpty(completeResponse.CorrelationId))
                                assistantMessage.CorrelationId = completeResponse.CorrelationId;
                            if (!string.IsNullOrEmpty(completeResponse.MessageId))
                                assistantMessage.MessageId = completeResponse.MessageId;
                            
                            StateHasChanged();
                            return;
                        }
                    }
                    catch (Exception fallbackEx)
                    {
                        Logger.LogError(fallbackEx, "Fallback completion failed, keeping partial text");
                        // Keep partial text - better than nothing
                        return;
                    }
                }
                else
                {
                    // No partial text - remove placeholder and do full fallback
                    if (messages.Contains(assistantMessage))
                    {
                        messages.Remove(assistantMessage);
                        StateHasChanged();
                    }
                    
                    try
                    {
                        await HandleNonStreamingChat(messageToSend, cancellationToken);
                        return;
                    }
                    catch (Exception fallbackEx)
                    {
                        Logger.LogError(fallbackEx, "Fallback to non-streaming also failed");
                        // Continue to show error message below
                    }
                }
            }
            
            // If fallback disabled or fallback also failed, show error
            if (!partialTextReceived)
            {
                errorMessage = "An error occurred while receiving the response. Please try again.";
                if (messages.Contains(assistantMessage))
                {
                    messages.Remove(assistantMessage);
                }
            }
            else
            {
                // Keep partial text - better than nothing
                Logger.LogInformation("Keeping partial response with {Length} characters", assistantMessage.Text.Length);
            }
        }
        finally
        {
            StateHasChanged();
        }
    }

    private async Task<ChatResponse?> GetCompleteResponseAsync(string message, CancellationToken cancellationToken)
    {
        try
        {
            var request = new ChatRequest { Message = message };
            var response = await Http.PostAsJsonAsync("/api/chat", request, cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<ChatResponse>(cancellationToken: cancellationToken);
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to get complete response for fallback");
        }
        
        return null;
    }

    private async Task HandleFeedback(string messageId, string rating)
    {
        try
        {
            var feedbackRequest = new FeedbackRequest
            {
                MessageId = messageId,
                Rating = rating
            };

            var response = await Http.PostAsJsonAsync("/api/feedback", feedbackRequest);
            
            if (response.IsSuccessStatusCode)
            {
                // Update message feedback state
                var message = messages.FirstOrDefault(m => m.MessageId == messageId);
                if (message != null)
                {
                    message.FeedbackRating = rating;
                    StateHasChanged();
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error submitting feedback. MessageId: {MessageId}, Rating: {Rating}", messageId, rating);
        }
    }

}

public class ChatMessage
{
    public string Role { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string? CorrelationId { get; set; }
    public string? MessageId { get; set; }
    public string? FeedbackRating { get; set; }
    public string CssClass { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.Now;
}

