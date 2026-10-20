using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;

namespace Onygo.Api.IntegrationTests.Infrastructure;

public static class ProblemAssertions
{
    /// <summary>Vérifie le statut HTTP et le champ « code » d'une erreur métier.</summary>
    public static async Task AssertBusinessErrorAsync(
        this HttpResponseMessage response,
        HttpStatusCode expectedStatus,
        string expectedCode)
    {
        Assert.Equal(expectedStatus, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal(expectedCode, problem!.Extensions["code"]!.ToString());
    }
}
