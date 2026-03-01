namespace WfpTrafficControl.Shared;

/// <summary>
/// Represents an error with a code and message.
/// </summary>
public sealed class Error
{
    /// <summary>
    /// Error code for programmatic handling.
    /// </summary>
    public string Code { get; }

    /// <summary>
    /// Human-readable error message.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Optional inner exception if this error wraps an exception.
    /// </summary>
    public Exception? Exception { get; }

    public Error(string code, string message, Exception? exception = null)
    {
        Code = code ?? throw new ArgumentNullException(nameof(code));
        Message = message ?? throw new ArgumentNullException(nameof(message));
        Exception = exception;
    }

    public override string ToString() => $"[{Code}] {Message}";
}

/// <summary>
/// Standard error codes used across the project.
/// </summary>
public static class ErrorCodes
{
    public const string Unknown = "UNKNOWN";
    public const string InvalidArgument = "INVALID_ARGUMENT";
    public const string NotFound = "NOT_FOUND";
    public const string PermissionDenied = "PERMISSION_DENIED";
    public const string AccessDenied = "ACCESS_DENIED";
    public const string InvalidPolicy = "INVALID_POLICY";
    public const string InvalidState = "INVALID_STATE";
    public const string WfpError = "WFP_ERROR";
    public const string ServiceError = "SERVICE_ERROR";
    public const string ServiceUnavailable = "SERVICE_UNAVAILABLE";
    public const string IpcError = "IPC_ERROR";
    public const string NetworkError = "NETWORK_ERROR";
}

/// <summary>
/// Represents the result of an operation that can either succeed with a value or fail with an error.
/// </summary>
/// <typeparam name="T">The type of the success value.</typeparam>
public readonly struct Result<T>
{
    private readonly T? _value;
    private readonly Error? _error;

    /// <summary>
    /// True if the operation succeeded.
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// True if the operation failed.
    /// </summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>
    /// Gets the success value. Throws if the result is a failure.
    /// </summary>
    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException($"Cannot access Value on a failed result: {_error}");

    /// <summary>
    /// Gets the error. Throws if the result is a success.
    /// </summary>
    public Error Error => IsFailure
        ? _error!
        : throw new InvalidOperationException("Cannot access Error on a successful result.");

    private Result(T value)
    {
        IsSuccess = true;
        _value = value;
        _error = null;
    }

    private Result(Error error)
    {
        IsSuccess = false;
        _value = default;
        _error = error ?? throw new ArgumentNullException(nameof(error));
    }

    /// <summary>
    /// Creates a successful result with the given value.
    /// </summary>
    public static Result<T> Success(T value) => new(value);

    /// <summary>
    /// Creates a failed result with the given error.
    /// </summary>
    public static Result<T> Failure(Error error) => new(error);

    /// <summary>
    /// Creates a failed result with the given code and message.
    /// </summary>
    public static Result<T> Failure(string code, string message) => new(new Error(code, message));

    /// <summary>
    /// Implicit conversion from T to Result&lt;T&gt; (success case).
    /// </summary>
    public static implicit operator Result<T>(T value) => Success(value);

    /// <summary>
    /// Implicit conversion from Error to Result&lt;T&gt; (failure case).
    /// </summary>
    public static implicit operator Result<T>(Error error) => Failure(error);

    /// <summary>
    /// Gets the value if successful, or the default value if failed.
    /// </summary>
    public T GetValueOrDefault(T defaultValue = default!) => IsSuccess ? _value! : defaultValue;

    /// <summary>
    /// Executes one of the provided functions based on success or failure.
    /// </summary>
    public TResult Match<TResult>(Func<T, TResult> onSuccess, Func<Error, TResult> onFailure)
    {
        return IsSuccess ? onSuccess(_value!) : onFailure(_error!);
    }

    public override string ToString() => IsSuccess ? $"Success({_value})" : $"Failure({_error})";
}

/// <summary>
/// Represents the result of an operation that can either succeed (with no value) or fail with an error.
/// </summary>
public readonly struct Result
{
    private readonly Error? _error;

    /// <summary>
    /// True if the operation succeeded.
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// True if the operation failed.
    /// </summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>
    /// Gets the error. Throws if the result is a success.
    /// </summary>
    public Error Error => IsFailure
        ? _error!
        : throw new InvalidOperationException("Cannot access Error on a successful result.");

    private Result(bool isSuccess, Error? error)
    {
        IsSuccess = isSuccess;
        _error = error;
    }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static Result Success() => new(true, null);

    /// <summary>
    /// Creates a failed result with the given error.
    /// </summary>
    public static Result Failure(Error error) => new(false, error ?? throw new ArgumentNullException(nameof(error)));

    /// <summary>
    /// Creates a failed result with the given code and message.
    /// </summary>
    public static Result Failure(string code, string message) => new(false, new Error(code, message));

    /// <summary>
    /// Implicit conversion from Error to Result (failure case).
    /// </summary>
    public static implicit operator Result(Error error) => Failure(error);

    /// <summary>
    /// Executes one of the provided actions based on success or failure.
    /// </summary>
    public TResult Match<TResult>(Func<TResult> onSuccess, Func<Error, TResult> onFailure)
    {
        return IsSuccess ? onSuccess() : onFailure(_error!);
    }

    public override string ToString() => IsSuccess ? "Success" : $"Failure({_error})";
}
