using Dawning.AgentOS.Domain.Core;
using Microsoft.AspNetCore.Http;

namespace Dawning.AgentOS.Api.Results;

/// <summary>
/// Maps <see cref="Result{T}"/> values to <see cref="IResult"/> values per
/// the rules fixed by ADR-023 §4.
/// </summary>
/// <remarks>
/// <list type="bullet">
///   <item>
///     <description>
///       <c>Success</c> → <c>200 OK</c> + DTO body.
///     </description>
///   </item>
///   <item>
///     <description>
///       <c>Failure</c> with at least one <see cref="DomainError.Field"/>
///       set → <c>400 Bad Request</c> ProblemDetails (validation).
///     </description>
///   </item>
///   <item>
///     <description>
///       <c>Failure</c> with no field-level errors → <c>422 Unprocessable
///       Entity</c> ProblemDetails (business rule violation).
///     </description>
///   </item>
/// </list>
/// <para>
/// Other status codes (e.g. <c>404 Not Found</c>) are returned by the
/// endpoint explicitly when it knows the semantic; per ADR-023 V0 has no
/// not-found use case yet.
/// </para>
/// </remarks>
public static class ResultHttpExtensions
{
    /// <summary>
    /// Converts a <see cref="Result{T}"/> into an <see cref="IResult"/>.
    /// </summary>
    /// <typeparam name="T">The success-payload type.</typeparam>
    /// <param name="result">The application-layer result.</param>
    /// <returns>An <see cref="IResult"/> ready to return from a Minimal API endpoint.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="result"/> is null.</exception>
    public static IResult ToHttpResult<T>(this Result<T> result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.IsSuccess)
        {
            return TypedResults.Ok(result.Value);
        }

        var errors = result.Errors;
        var hasFieldError = errors.Any(e => !string.IsNullOrWhiteSpace(e.Field));
        var statusCode = hasFieldError
            ? StatusCodes.Status400BadRequest
            : StatusCodes.Status422UnprocessableEntity;
        var title = hasFieldError ? "Validation failed" : "Business rule violation";
        var detail = errors.Length > 0 ? errors[0].Message : null;

        // Use TypedResults rather than the unqualified Results identifier:
        // this file's own namespace `Dawning.AgentOS.Api.Results` shadows
        // the BCL `Microsoft.AspNetCore.Http.Results` static class for any
        // file inside `Dawning.AgentOS.Api.*`.
        return TypedResults.Problem(
            statusCode: statusCode,
            title: title,
            detail: detail,
            extensions: new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["errors"] = errors
                    .Select(e => new ProblemErrorEntry(e.Code, e.Message, e.Field))
                    .ToArray(),
            }
        );
    }

    /// <summary>
    /// Serializable shape of a single <see cref="DomainError"/> used in the
    /// <c>errors</c> extension of an RFC 7807 ProblemDetails response.
    /// </summary>
    /// <param name="Code">The stable machine-readable error code.</param>
    /// <param name="Message">The human-readable message.</param>
    /// <param name="Field">The optional field path, when applicable.</param>
    private sealed record ProblemErrorEntry(string Code, string Message, string? Field);
}
