using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Onygo.Api.Auth;
using Onygo.Api.Infrastructure.Email;
using Onygo.Api.Infrastructure.OpenApi;
using Onygo.Api.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Chaîne lue à la résolution et non au démarrage, pour que les tests d'intégration puissent la remplacer.
builder.Services.AddDbContext<OnygoDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Onygo")
        ?? throw new InvalidOperationException("Connection string 'Onygo' is missing.")));

builder.Services.AddOnygoAuth(builder.Configuration);

// Le fournisseur d'e-mails n'est pas encore choisi (Brevo ou Resend) : hors développement,
// l'envoi échoue explicitement au lieu de perdre silencieusement les e-mails.
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddSingleton<IEmailSender<IdentityUser>, DevelopmentEmailSender>();
}
else
{
    builder.Services.AddSingleton<IEmailSender<IdentityUser>, UnconfiguredEmailSender>();
}

builder.Services.AddOpenApi(options => options.AddDocumentTransformer<BearerSecuritySchemeTransformer>());
builder.Services.AddHealthChecks();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "Onygo API"));
}

app.UseRateLimiter();

// Appelés explicitement, après Swagger UI : sinon ASP.NET les place en tête du pipeline
// et la politique de repli bloque les fichiers de Swagger UI, qui ne sont pas des routes.
app.UseAuthentication();
app.UseAuthorization();

app.MapAuthEndpoints();
app.MapHealthChecks("/health").AllowAnonymous();

app.Run();

public partial class Program;
