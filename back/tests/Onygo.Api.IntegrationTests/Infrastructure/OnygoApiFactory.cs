using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
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

    /// <summary>Date du jour à Paris vue par l'API pendant les tests (horloge figée à 10 h).</summary>
    public static readonly DateOnly Today = new(2026, 6, 15);

    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(Today, new TimeOnly(10, 0), TimeSpan.FromHours(2)));

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
        // Tous les tests partagent la même « adresse IP » : sans limites hautes, ils se bloqueraient entre eux.
        // Les limites réelles sont testées dans RateLimitingTests.
        builder.UseSetting("RateLimiting:Auth:SignInPermitsPerMinute", "100000");
        builder.UseSetting("RateLimiting:Auth:EmailPermitsPerHour", "100000");
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IEmailSender<IdentityUser>>(EmailSender);
            services.AddSingleton<TimeProvider>(_clock);
        });
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
