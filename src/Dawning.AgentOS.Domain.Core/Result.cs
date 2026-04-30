using System.Collections.Immutable;

namespace Dawning.AgentOS.Domain.Core;

/// <summary>
/// Outcome of a use case or domain operation that has no return value on success.
/// Business failures are conveyed via <see cref="Result"/> / <see cref="Result{T}"/>
/// rather than thrown exceptions; only invariant violations (programming errors)
/// throw.
/// </summary>
public class Result
{
    /// <summary><c>true</c> when the operation succeeded.</summary>
    public bool IsSuccess { get; }

    /// <summary><c>true</c> when the operation failed; convenience inverse of <see cref="IsSuccess"/>.</summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>
    /// Accumulated business-level errors. Empty on success.
    /// Multi-field validation failures are represented as multiple entries.
    /// </summary>
    public ImmutableArray<DomainError> Errors { get; }

    /// <summary>Creates a successful result.</summary>
    public static Result Success() => new(true, ImmutableArray<DomainError>.Empty);

    /// <summary>Creates a failed result from one or more errors.</summary>
    /// <exception cref="ArgumentException">Thrown when no errors are provided.</exception>
    public static Result Failure(params DomainError[] errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        if (errors.Length == 0)
        {
            throw new ArgumentException(
                "A failure result must contain at least one error.",
                nameof(errors));
        }

        return new Result(false, errors.ToImmutableArray());
    }

    /// <summary>Creates a failed result from a single error code and message.</summary>
    public static Result Failure(string code, string message, string? field = null)
        => Failure(new DomainError(code, message, field));

    private protected Result(bool isSuccess, ImmutableArray<DomainError> errors)
    {
        IsSuccess = isSuccess;
        Errors = errors;
    }
}

/// <summary>
/// Outcome of a use case or domain operation that returns a value on success.
/// </summary>
/// <typeparam name="T">Type of the success value.</typeparam>
public sealed class Result<T> : Result
{
    private readonly T? _value;

    /// <summary>
    /// The success value. Throws when accessed on a failed result; callers must
    /// check <see cref="Result.IsSuccess"/> first or pattern-match.
    /// </summary>
    /// <exception cref="InvalidOperationException">When the result is a failure.</exception>
    public T Value
    {
        get
        {
            if (!IsSuccess)
            {
                throw new InvalidOperationException(
                    "Cannot access Value of a failed result. Inspect Errors instead.");
            }

            return _value!;
        }
    }

    /// <summary>Creates a successful result carrying <paramref name="value"/>.</summary>
    public static Result<T> Success(T value) => new(true, value, ImmutableArray<DomainError>.Empty);

    /// <summary>Creates a failed result from one or more errors.</summary>
    public static new Result<T> Failure(params DomainError[] errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        if (errors.Length == 0)
        {
            throw new ArgumentException(
                "A failure result must contain at least one error.",
                nameof(errors));
        }

        return new Result<T>(false, default, errors.ToImmutableArray());
    }

    /// <summary>Creates a failed result from a single error code and message.</summary>
    public static new Result<T> Failure(string code, string message, string? field = null)
        => Failure(new DomainError(code, message, field));

    private Result(bool isSuccess, T? value, ImmutableArray<DomainError> errors)
        : base(isSuccess, errors)
    {
        _value = value;
    }
}
