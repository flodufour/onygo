using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Onygo.Api.Infrastructure.Persistence;
using Onygo.Api.IntegrationTests.Infrastructure;

namespace Onygo.Api.IntegrationTests.Auth;

[Collection(ApiCollection.Name)]
public class TokenKeysTests(OnygoApiFactory factory)
{
    private readonly AuthApiClient _auth = new(factory);

    // Les clés qui chiffrent les jetons doivent survivre à un redémarrage ou un redéploiement de l'API,
    // sinon tous les utilisateurs sont déconnectés. En local elles retomberaient sur le disque de la machine,
    // ce qui masquerait le problème : on vérifie donc directement qu'elles sont en base.
    [Fact]
    public async Task Login_StoresTokenEncryptionKeysInDatabase()
    {
        await _auth.LoginConfirmedUserAsync();

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<OnygoDbContext>();
        Assert.True(await dbContext.DataProtectionKeys.AnyAsync());
    }
}
