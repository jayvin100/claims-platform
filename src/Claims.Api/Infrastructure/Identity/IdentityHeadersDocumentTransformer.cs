using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Claims.Api.Infrastructure.Identity;

/// <summary>
/// Declares X-User-Id and X-Role in the OpenAPI document, so Swagger UI shows an "Authorize"
/// button where a reviewer enters them once and every call sends them.
/// </summary>
public class IdentityHeadersDocumentTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        var requirement = new OpenApiSecurityRequirement();
        foreach (var header in DemoHeaderAuthenticationHandler.Headers)
        {
            document.Components.SecuritySchemes[header] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.ApiKey,
                In = ParameterLocation.Header,
                Name = header,
                Description = "Demo identity: see GET /api/users for valid values.",
            };
            requirement[new OpenApiSecuritySchemeReference(header, document)] = [];
        }
        // Applies to every operation; AnonymousOperationTransformer clears it where it doesn't apply.
        document.Security = [requirement];

        return Task.CompletedTask;
    }
}

/// <summary>Shows [AllowAnonymous] endpoints (GET /api/users) as needing no headers in Swagger.</summary>
public class AnonymousOperationTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        if (context.Description.ActionDescriptor.EndpointMetadata.OfType<IAllowAnonymous>().Any())
            operation.Security = []; // an empty list overrides the document-wide requirement

        return Task.CompletedTask;
    }
}
