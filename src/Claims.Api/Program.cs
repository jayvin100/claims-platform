using Claims.Api.Claims;
using Claims.Api.Infrastructure.Data;
using Claims.Api.Infrastructure.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Enums travel as names ("SG", "Motor"), and a number in their place is refused rather than guessed at.
builder.Services.AddControllers().AddJsonOptions(options =>
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false)));
builder.Services.AddOpenApi(options => options
    .AddDocumentTransformer<IdentityHeadersDocumentTransformer>()
    .AddOperationTransformer<AnonymousOperationTransformer>());
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Claims")));

// The Angular dev server calls the API from another origin.
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins("http://localhost:4200")
    .AllowAnyMethod()
    .WithHeaders(["Content-Type", .. DemoHeaderAuthenticationHandler.Headers])));

// Demo identity (ADR-0006): this scheme is the one line to swap for AddJwtBearer later.
builder.Services
    .AddAuthentication(DemoHeaderAuthenticationHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, DemoHeaderAuthenticationHandler>(DemoHeaderAuthenticationHandler.SchemeName, null);
// Every endpoint needs a known user unless it opts out with [AllowAnonymous].
builder.Services.AddAuthorization(options =>
    options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<CurrentUser>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ClaimExceptionHandler>();

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    // Bring the schema and demo users up to date before serving any request.
    using var scope = app.Services.CreateScope();
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();

    app.MapOpenApi().AllowAnonymous();
    // Swagger UI is static files served by middleware, so it must sit before the auth check.
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "Claims API"));
}

// CORS answers browser preflight (OPTIONS) requests itself, before the auth check sees them.
// Only the Angular dev server needs it, so it is on in Development only (ticket #10).
if (app.Environment.IsDevelopment())
    app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
