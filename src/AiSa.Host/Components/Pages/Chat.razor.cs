namespace AiSa.Host.Components.Pages;

public partial class Chat
{
    private List<ChatMessage> messages = new();
    private string currentMessage = string.Empty;
    private bool isSending = false;

    private void HandleSend()
    {
        if (string.IsNullOrWhiteSpace(currentMessage) || isSending)
            return;

        // Add user message to list
        var userMessage = currentMessage.Trim();
        messages.Add(new ChatMessage
        {
            Role = "User",
            Text = userMessage,
            CssClass = "user-message"
        });

        currentMessage = string.Empty;

        // TODO: In T01.F, this will call the API and add assistant response
        // For now, this is UI only - no API call yet
    }
}

public class ChatMessage
{
    public string Role { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string? CorrelationId { get; set; }
    public string CssClass { get; set; } = string.Empty;
}

