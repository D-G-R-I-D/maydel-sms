using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi;

namespace Maydel.Api.Common;

/// <summary>Shapes the OpenAPI document so the generated TypeScript client matches what the API really sends.</summary>
public static class OpenApiSetup
{
    public static IServiceCollection AddMaydelOpenApi(this IServiceCollection services) => services.AddOpenApi(o =>
    {
        o.AddDocumentTransformer((doc, _, _) =>
        {
            doc.Info.Title = "Maydel SMS API";
            doc.Info.Description = "Called server-to-server by the Next.js BFF. Errors are RFC 9457 problem details with `code` and `traceId`.";
            return Task.CompletedTask;
        });

        // Query binding is case-insensitive; document the camelCase names clients should send.
        o.AddOperationTransformer((operation, _, _) =>
        {
            foreach (var parameter in operation.Parameters ?? [])
                if (parameter is OpenApiParameter { In: ParameterLocation.Query, Name: { Length: > 0 } name } query)
                    query.Name = JsonNamingPolicy.CamelCase.ConvertName(name);
            return Task.CompletedTask;
        });

        // Every problem response carries these extensions (see HttpResults.ToProblem and AddProblemDetails).
        o.AddSchemaTransformer((schema, context, _) =>
        {
            var type = context.JsonTypeInfo.Type;
            if (type != typeof(ProblemDetails) && type != typeof(HttpValidationProblemDetails)) return Task.CompletedTask;

            schema.Properties ??= new Dictionary<string, IOpenApiSchema>();
            schema.Properties["code"] = new OpenApiSchema
            {
                Type = JsonSchemaType.String | JsonSchemaType.Null,
                Description = "Stable machine-readable error code, e.g. auth.invalid_credentials. Absent on framework-generated problems.",
            };
            schema.Properties["traceId"] = new OpenApiSchema { Type = JsonSchemaType.String, Description = "Quote this to support." };
            return Task.CompletedTask;
        });
    });
}
