using AiSa.Application;
using AiSa.Application.Models;
using Microsoft.AspNetCore.Mvc;

namespace AiSa.Host.Endpoints;

/// <summary>
/// Feedback API endpoints.
/// </summary>
internal static class FeedbackEndpoints
{
    /// <summary>
    /// Maps feedback endpoints to the API group.
    /// </summary>
    public static IEndpointRouteBuilder MapFeedbackEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api");

        // POST /api/feedback - Submit feedback
        api.MapPost("/feedback", async (
                FeedbackRequest request,
                IFeedbackService feedbackService,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                // Validate input
                if (string.IsNullOrWhiteSpace(request.MessageId))
                {
                    return Results.Problem(
                        statusCode: StatusCodes.Status400BadRequest,
                        title: "Bad Request",
                        detail: "MessageId is required.");
                }

                if (string.IsNullOrWhiteSpace(request.Rating) ||
                    (request.Rating != "positive" && request.Rating != "negative"))
                {
                    return Results.Problem(
                        statusCode: StatusCodes.Status400BadRequest,
                        title: "Bad Request",
                        detail: "Rating must be 'positive' or 'negative'.");
                }

                try
                {
                    await feedbackService.SubmitFeedbackAsync(request);
                    return Results.Ok(new { success = true });
                }
                catch (Exception ex)
                {
                    return Results.Problem(
                        statusCode: StatusCodes.Status500InternalServerError,
                        title: "Internal Server Error",
                        detail: "Failed to submit feedback.");
                }
            })
            .WithName("SubmitFeedback")
            .WithSummary("Submit feedback for a chat message")
            .WithDescription("""
                Submits feedback (thumbs up/down) for a chat message response.
                
                **Rating Values:**
                - `positive`: Thumbs up - response was helpful/accurate
                - `negative`: Thumbs down - response was unhelpful/inaccurate
                
                **Example Request:**
                ```json
                {
                  "messageId": "01234567-89ab-cdef-0123-456789abcdef",
                  "rating": "positive",
                  "comment": "Very helpful answer!"
                }
                ```
                """)
            .WithTags("Feedback")
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .Accepts<FeedbackRequest>("application/json");

        return app;
    }
}
