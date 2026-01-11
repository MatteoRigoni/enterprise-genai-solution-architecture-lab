using System.Diagnostics;
using AiSa.Application;
using AiSa.Application.Models;
using Microsoft.AspNetCore.Mvc;

namespace AiSa.Host.Endpoints;

/// <summary>
/// Chat API endpoints.
/// </summary>
internal static class ChatEndpoints
{
    /// <summary>
    /// Maps chat endpoints to the API group.
    /// </summary>
    public static IEndpointRouteBuilder MapChatEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api");

        api.MapPost("/chat", async (
                ChatRequest request,
                IChatService chatService,
                ActivitySource activitySource,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                // Minimal input validation as a 400 (ProblemDetails is still RFC7807)
                if (string.IsNullOrWhiteSpace(request.Message))
                {
                    return Results.Problem(
                        statusCode: StatusCodes.Status400BadRequest,
                        title: "Bad Request",
                        detail: "The request is invalid. Please check your input and try again.");
                }

                // Child span for application work; the incoming HTTP span already exists
                using var activity = activitySource.StartActivity("chat.handle", ActivityKind.Internal);

                // Low-cardinality metadata only (avoid logging raw prompts)
                activity?.SetTag("chat.message.length", request.Message.Length);

                var response = await chatService.ProcessChatAsync(request, cancellationToken);

                activity?.SetTag("chat.response.length", response.Response?.Length ?? 0);
                activity?.SetStatus(ActivityStatusCode.Ok);

                return Results.Ok(response);
            })
            .WithName("ChatApi")
            .WithSummary("Send a chat message")
            .WithDescription("Processes a chat message and returns a response from the LLM. The message is validated, processed by the chat service, and tracked via OpenTelemetry.")
            .WithTags("Chat")
            .Produces<ChatResponse>(StatusCodes.Status200OK, "application/json")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .Accepts<ChatRequest>("application/json");

        return app;
    }
}


