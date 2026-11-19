using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace HomeschoolAI.Api.OpenApi;

/// <summary>
/// Documenta los enums como cadenas (p. ej. "Completed") en el Swagger/OpenAPI, en
/// lugar de sus valores numéricos, para que el contrato generado del frontend pueda
/// comparar estados por nombre. Va de la mano de <see cref="System.Text.Json.Serialization.JsonStringEnumConverter"/>.
/// </summary>
public sealed class EnumStringSchemaFilter : ISchemaFilter
{
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        if (!context.Type.IsEnum)
        {
            return;
        }

        schema.Type = "string";
        schema.Format = null;
        schema.Enum = Enum.GetNames(context.Type)
            .Select(name => (IOpenApiAny)new OpenApiString(name))
            .ToList();
    }
}
