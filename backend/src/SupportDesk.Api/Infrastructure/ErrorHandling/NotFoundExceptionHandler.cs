using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace SupportDesk.Api.Infrastructure.ErrorHandling;

/// <summary>Reference to an entity id that does not exist.</summary>
public sealed class NotFoundException(string entity, object key)
    : Exception($"{entity} '{key}' was not found.")
{
    public string Entity { get; } = entity;
    public object Key { get; } = key;
}

/// <summary>Maps <see cref="NotFoundException"/> to a 404 ProblemDetails with errorCode NOT_FOUND.</summary>
public sealed class NotFoundExceptionHandler(ILogger<NotFoundExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not NotFoundException notFound)
            return false;

        logger.LogWarning("404 on {Method} {Path}: {Message}",
            context.Request.Method, context.Request.Path, notFound.Message);

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status404NotFound,
            Title = "Resource not found",
            Detail = notFound.Message,
            Instance = context.Request.Path,
        };
        problem.Extensions["errorCode"] = "NOT_FOUND";
        problem.Extensions["traceId"] = context.TraceIdentifier;

        context.Response.StatusCode = StatusCodes.Status404NotFound;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(problem, cancellationToken);
        return true;
    }
}
