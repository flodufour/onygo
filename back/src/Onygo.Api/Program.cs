using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Onygo.Api.Infrastructure.Email;
using Onygo.Api.Infrastructure.OpenApi;
using Onygo.Api.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Chaîne lue à la résolution et non au démarrage, pour que les tests d'intégration puissent la remplacer.
builder.Services.AddDbContext<OnygoDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Onygo")
        ?? throw new InvalidOperationException("Connection string 'Onygo' is missing.")));

// Toute route exige un utilisateur connecté, sauf celles marquées explicitement AllowAnonymous.
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
builder.Services
    .AddIdentityApiEndpoints<IdentityUser>(options =>
    {
        options.Password.RequiredLength = 8;
        options.User.RequireUniqueEmail = true;
        options.SignIn.RequireConfirmedEmail = true;
    })
    .AddEntityFrameworkStores<OnygoDbContext>();

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

// Appelés explicitement, après Swagger UI : sinon ASP.NET les place en tête du pipeline
// et la politique de repli bloque les fichiers de Swagger UI, qui ne sont pas des routes.
app.UseAuthentication();
app.UseAuthorization();

// MapIdentityApi ne marque pas ses routes publiques (/login, /register…) comme anonymes :
// on ouvre celles qui n'ont aucune exigence d'autorisation, /manage/* garde la sienne.
app.MapGroup("/auth")
    .WithTags("Auth")
    .MapIdentityApi<IdentityUser>()
    .Finally(endpoint =>
    {
        if (!endpoint.Metadata.OfType<IAuthorizeData>().Any())
        {
            endpoint.Metadata.Add(new AllowAnonymousAttribute());
        }
    });
app.MapHealthChecks("/health").AllowAnonymous();

app.Run();

public partial class Program;
