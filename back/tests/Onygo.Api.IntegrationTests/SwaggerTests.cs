using System.Net;
using Microsoft.AspNetCore.Hosting;
using Onygo.Api.IntegrationTests.Infrastructure;

namespace Onygo.Api.IntegrationTests;

[Collection(ApiCollection.Name)]
public class SwaggerTests(OnygoApiFactory factory)
{
    [Fact]
    public async Task SwaggerUi_InDevelopment_IsServed()
    {
        var client = CreateDevelopmentClient();

        var response = await client.GetAsync("/swagger/index.html");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task OpenApiDocument_InDevelopment_DeclaresBearerScheme()
    {
        var client = CreateDevelopmentClient();

        var document = await client.GetStringAsync("/openapi/v1.json");

        Assert.Contains("\"scheme\": \"bearer\"", document);
    }

    [Fact]
    public async Task SwaggerUi_OutsideDevelopment_IsNotExposed()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/swagger/index.html");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private HttpClient CreateDevelopmentClient() =>
        factory.WithWebHostBuilder(builder => builder.UseEnvironment("Development")).CreateClient();
}
