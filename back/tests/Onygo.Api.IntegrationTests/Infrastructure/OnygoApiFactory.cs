using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Onygo.Api.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace Onygo.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Démarre l'API sur un vrai PostgreSQL (Testcontainers), partagé par tous les tests de la collection « Api ».
/// Chaque test crée ses propres utilisateurs avec une adresse unique : aucun état n'est partagé entre tests.
/// </summary>
public sealed class OnygoApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public FakeEmailSender EmailSender { get; } = new();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<OnygoDbContext>();
        await dbContext.Database.MigrateAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Onygo", _postgres.GetConnectionString());
        builder.ConfigureTestServices(services =>
            services.AddSingleton<IEmailSender<IdentityUser>>(EmailSender));
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
        await _postgres.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<OnygoApiFactory>
{
    public const string Name = "Api";
}
