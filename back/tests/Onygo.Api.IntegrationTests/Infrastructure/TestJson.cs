using System.Text.Json;
using System.Text.Json.Serialization;

namespace Onygo.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Mêmes options JSON que l'API (enums en texte).
/// </summary>
public static class TestJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };
}
