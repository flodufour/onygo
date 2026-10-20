using Microsoft.AspNetCore.Http.HttpResults;

namespace Onygo.Api.Common;

/// <summary>
/// Erreur métier au format ProblemDetails, avec un champ « code » que le front traduit.
/// </summary>
public static class BusinessProblem
{
    public static ProblemHttpResult Forbidden(string code) => WithCode(StatusCodes.Status403Forbidden, code);

    public static ProblemHttpResult Conflict(string code) => WithCode(StatusCodes.Status409Conflict, code);

    private static ProblemHttpResult WithCode(int statusCode, string code) =>
        TypedResults.Problem(
            statusCode: statusCode,
            title: code,
            extensions: new Dictionary<string, object?> { ["code"] = code });
}
