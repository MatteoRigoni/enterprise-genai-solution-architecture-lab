using System.Net.Http.Json;
using AiSa.Application.Models;
using AiSa.Host.Services;
using Microsoft.AspNetCore.Components;

namespace AiSa.Host.Components.Pages;

public partial class Chat
{
    private List<ChatMessage> messages = new();
    private string currentMessage = string.Empty;
    private bool isSending = false;
    private string? errorMessage = null;

    [Inject]
    private HttpClient Http { get; set; } = default!;

    [Inject]
    private ILoadingService LoadingService { get; set; } = default!;

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
            CssClass = "user-message"
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
                            CssClass = "assistant-message"
                        });
                    }
                }
                else
                {
                    // Handle error response (ProblemDetails)
                    errorMessage = $"Error: {response.StatusCode}";
                    var problemDetails = await response.Content.ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ProblemDetails>(cancellationToken: cancellationToken);
                    if (problemDetails?.Detail != null)
                    {
                        errorMessage = problemDetails.Detail;
                    }
                }
            }
            catch (HttpRequestException ex)
            {
                errorMessage = $"Network error: {ex.Message}";
            }
            catch (Exception ex)
            {
                errorMessage = $"An error occurred: {ex.Message}";
            }
            finally
            {
                isSending = false;
            }
        }, key: "chat-send");
    }
}

public class ChatMessage
{
    public string Role { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string? CorrelationId { get; set; }
    public string CssClass { get; set; } = string.Empty;
}

