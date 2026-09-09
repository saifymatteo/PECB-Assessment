using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using SupportDesk.Api.Domain;

namespace SupportDesk.Api.Infrastructure.ErrorHandling;

/// <summary>
/// Maps business-rule violations to RFC 7807 ProblemDetails with a machine-readable
/// errorCode (and allowedTransitions for invalid status moves). Anything not handled here
/// falls through to the default ProblemDetails 500 handler - clients never see stack traces.
/// </summary>
public sealed class BusinessRuleExceptionHandler(ILogger<BusinessRuleExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not BusinessRuleException rule)
            return false;

        var (status, title) = rule switch
        {
            InvalidTicketTransitionException => (StatusCodes.Status409Conflict, "Invalid status transition"),
            TicketClosedException => (StatusCodes.Status409Conflict, "Ticket is closed"),
            AgentAssignmentRequiredException => (StatusCodes.Status409Conflict, "Agent assignment required"),
            InactiveAgentException => (StatusCodes.Status422UnprocessableEntity, "Agent is inactive"),
            _ => (StatusCodes.Status409Conflict, "Business rule violation"),
        };

        logger.LogWarning("Business rule {ErrorCode} rejected {Method} {Path}: {Message}",
            rule.ErrorCode, context.Request.Method, context.Request.Path, rule.Message);

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = rule.Message,
            Instance = context.Request.Path,
        };

        problem.Extensions["errorCode"] = rule.ErrorCode;

        if (rule is InvalidTicketTransitionException invalidTransition)
            problem.Extensions["allowedTransitions"] =
                invalidTransition.AllowedTargets.Select(t => t.ToString()).ToArray();

        problem.Extensions["traceId"] = context.TraceIdentifier;

        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(problem, cancellationToken);
        return true;
    }
}
