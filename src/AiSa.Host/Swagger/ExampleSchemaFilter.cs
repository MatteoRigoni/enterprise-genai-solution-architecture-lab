using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace AiSa.Host.Swagger;

/// <summary>
/// Swagger schema filter to add examples to request/response models.
/// </summary>
public class ExampleSchemaFilter : ISchemaFilter
{
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        if (context.Type == typeof(AiSa.Application.Models.ChatRequest))
        {
            schema.Example = new OpenApiObject
            {
                ["message"] = new OpenApiString("What is the storage limit on the free plan?")
            };
        }
        else if (context.Type == typeof(AiSa.Application.Models.ChatResponse))
        {
            schema.Example = new OpenApiObject
            {
                ["response"] = new OpenApiString("The free plan includes 5 GB of total storage."),
                ["correlationId"] = new OpenApiString("01234567-89ab-cdef-0123-456789abcdef"),
                ["messageId"] = new OpenApiString("msg-12345678-1234-1234-1234-123456789abc"),
                ["citations"] = new OpenApiArray
                {
                    new OpenApiObject
                    {
                        ["sourceName"] = new OpenApiString("faq.txt"),
                        ["chunkId"] = new OpenApiString("chunk-001"),
                        ["score"] = new OpenApiDouble(0.95)
                    }
                }
            };
        }
        else if (context.Type == typeof(AiSa.Application.Models.Citation))
        {
            schema.Example = new OpenApiObject
            {
                ["sourceName"] = new OpenApiString("faq.txt"),
                ["chunkId"] = new OpenApiString("chunk-001"),
                ["score"] = new OpenApiDouble(0.95)
            };
        }
    }
}
