namespace ElectronicStore.Services;

/// <summary>
/// Why an order operation failed. Controllers map this to an HTTP response instead of
/// guessing from an exception type or a message string.
/// </summary>
public enum OrderErrorCode
{
    None = 0,

    /// <summary>Caller sent something wrong — redisplay the form.</summary>
    ValidationFailed,

    /// <summary>The order, product or address does not exist.</summary>
    NotFound,

    /// <summary>The row exists but belongs to another account.</summary>
    Forbidden,

    /// <summary>State moved underneath the caller: out of stock, status already changed.</summary>
    Conflict,

    /// <summary>Infrastructure failure. The details are logged, never shown to the user.</summary>
    Unexpected
}

/// <summary>
/// Outcome of an order operation. Preferred over throwing for expected business failures
/// ("out of stock" is not exceptional), while real infrastructure faults are still logged
/// and surfaced as <see cref="OrderErrorCode.Unexpected"/> rather than swallowed.
/// </summary>
public sealed class OrderResult<T>
{
    private OrderResult(bool succeeded, T? value, OrderErrorCode errorCode, IReadOnlyList<string> errors)
    {
        Succeeded = succeeded;
        Value = value;
        ErrorCode = errorCode;
        Errors = errors;
    }

    public bool Succeeded { get; }

    /// <summary>Only meaningful when <see cref="Succeeded"/> is true.</summary>
    public T? Value { get; }

    public OrderErrorCode ErrorCode { get; }

    /// <summary>User-facing messages, safe to render. Never contains SQL or stack traces.</summary>
    public IReadOnlyList<string> Errors { get; }

    public string FirstError => Errors.Count > 0 ? Errors[0] : string.Empty;

    public static OrderResult<T> Ok(T value) => new(true, value, OrderErrorCode.None, []);

    public static OrderResult<T> Fail(OrderErrorCode errorCode, params string[] errors) =>
        new(false, default, errorCode, errors);

    public static OrderResult<T> Fail(OrderErrorCode errorCode, IEnumerable<string> errors) =>
        new(false, default, errorCode, errors.ToArray());

    /// <summary>Carries a failure across result types without restating the messages.</summary>
    public static OrderResult<T> FailLike<TOther>(OrderResult<TOther> other) =>
        new(false, default, other.ErrorCode, other.Errors);
}
