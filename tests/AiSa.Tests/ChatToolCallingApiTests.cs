using System.Net;
using System.Net.Http.Json;
using AiSa.Application;
using AiSa.Application.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AiSa.Tests;

/// <summary>
/// Tool calling enabled via config; mock LLM emits &lt;tool_call&gt; for order/ticket phrases (T05.A).
/// </summary>
public class ChatToolCallingApiTests : IClassFixture<WebApplicationFactory<AiSa.Host.Program>>
{
    private readonly HttpClient _client;

    public ChatToolCallingApiTests(WebApplicationFactory<AiSa.Host.Program> factory)
    {
        var configured = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ToolCalling:Enabled"] = "true",
                    ["ToolCalling:MaxToolCallsPerRequest"] = "1"
                });
            });
            builder.ConfigureServices(services =>
            {
                services.Remove(services.FirstOrDefault(s => s.ServiceType == typeof(IRetrievalService))!);
                services.AddScoped<IRetrievalService, MockRetrievalServiceWithResults>();
            });
        });

        _client = configured.CreateClient();
    }

    [Fact]
    public async Task PostChat_ToolCallingEnabled_OrderPhrase_ExecutesMockTool()
    {
        var request = new ChatRequest { Message = "Please check order 12345 for me" };

        var response = await _client.PostAsJsonAsync("/api/chat", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var chatResponse = await response.Content.ReadFromJsonAsync<ChatResponse>();
        Assert.NotNull(chatResponse);
        Assert.Contains("12345", chatResponse!.Response, StringComparison.Ordinal);
        Assert.Contains("Shipped", chatResponse.Response, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PostChat_ToolCallingEnabled_SupportTicketPhrase_ExecutesMockTool()
    {
        var request = new ChatRequest { Message = "Please create a support ticket: cannot log in" };

        var response = await _client.PostAsJsonAsync("/api/chat", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var chatResponse = await response.Content.ReadFromJsonAsync<ChatResponse>();
        Assert.NotNull(chatResponse);
        Assert.Contains("TCK-0001", chatResponse!.Response, StringComparison.Ordinal);
    }
}
