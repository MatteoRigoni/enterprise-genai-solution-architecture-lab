using AiSa.Host.Services;

namespace AiSa.Host.Handlers;

/// <summary>
/// HTTP message handler that adds x-ui-session-id header to all /api/* requests.
/// </summary>
public class UiSessionHeaderHandler : DelegatingHandler
{
    private const string UiSessionHeaderName = "x-ui-session-id";
    private readonly IUiSession _uiSession;

    public UiSessionHeaderHandler(IUiSession uiSession)
    {
        _uiSession = uiSession ?? throw new ArgumentNullException(nameof(uiSession));
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (request.RequestUri != null && request.RequestUri.AbsolutePath.StartsWith("/api", StringComparison.OrdinalIgnoreCase))
        {
            request.Headers.Add(UiSessionHeaderName, _uiSession.SessionId);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}

