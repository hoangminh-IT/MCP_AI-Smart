namespace SmartInventory.Application.Common;

public sealed class AppValidationException : Exception
{
    public AppValidationException(string message,
        IReadOnlyDictionary<string, string[]>? errors = null) : base(message)
    {
        Errors = errors ?? new Dictionary<string, string[]>();
    }

    public IReadOnlyDictionary<string, string[]> Errors { get; }
}

public sealed class AppConflictException(string message) : Exception(message);
public sealed class AppNotFoundException(string message) : Exception(message);
public sealed class AppUnauthorizedException(string message) : Exception(message);
public sealed class AppForbiddenException(string message) : Exception(message);
public sealed class AppServiceUnavailableException(string message) : Exception(message);
