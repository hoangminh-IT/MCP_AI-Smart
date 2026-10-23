using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SmartInventory.Application.Common;
using SmartInventory.Application.Ai;

namespace SmartInventory.Api;

public sealed class ApiExceptionHandler(IProblemDetailsService problems, ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && context.RequestAborted.IsCancellationRequested) return true;
        var (status, title, detail) = exception switch
        {
            AppValidationException => (400, "Validation failed", exception.Message),
            AppUnauthorizedException => (401, "Unauthorized", exception.Message),
            AppForbiddenException => (403, "Forbidden", exception.Message),
            AppNotFoundException => (404, "Not found", exception.Message),
            AppConflictException => (409, "Conflict", exception.Message),
            AppServiceUnavailableException => (503, "Service unavailable", exception.Message),
            AiUnavailableException => (503, "AI unavailable", exception.Message),
            AiProviderException => (502, "AI provider error", exception.Message),
            DbUpdateConcurrencyException => (409, "Conflict", "The record changed during this request. Reload it and try again."),
            DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } } =>
                (409, "Conflict", "A record with the same unique value already exists."),
            DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation } } =>
                (409, "Conflict", "The record is referenced by another record, or a referenced record no longer exists."),
            DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.CheckViolation } } =>
                (400, "Validation failed", "A value violates a database constraint."),
            BadHttpRequestException request => (request.StatusCode, "Invalid request", "The request body is invalid or exceeds the allowed size."),
            _ => (500, "Server error", "The request could not be completed. Try again later.")
        };
        if (status >= 500)
            // Deliberately omit exception messages and database detail, which may contain submitted values.
            logger.LogError("Request failed with {ExceptionType}; trace {TraceId}", exception.GetType().Name, context.TraceIdentifier);
        var problem = new ProblemDetails { Status = status, Title = title, Detail = detail };
        if (exception is AppValidationException validation && validation.Errors.Count > 0)
            problem.Extensions["errors"] = validation.Errors;
        context.Response.StatusCode = status;
        await problems.WriteAsync(new ProblemDetailsContext { HttpContext = context, ProblemDetails = problem });
        return true;
    }
}
