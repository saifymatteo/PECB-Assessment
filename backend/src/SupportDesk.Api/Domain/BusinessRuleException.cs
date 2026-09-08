namespace SupportDesk.Api.Domain;

/// <summary>Base for business-rule violations; mapped to RFC 7807 ProblemDetails by the API layer.</summary>
public abstract class BusinessRuleException(string message, string errorCode) : Exception(message)
{
    /// <summary>Stable, machine-readable code (e.g. INVALID_TRANSITION) surfaced to API clients.</summary>
    public string ErrorCode { get; } = errorCode;
}

/// <summary>Rule 2: the requested status move is not in the transition graph.</summary>
public sealed class InvalidTicketTransitionException(TicketStatus from, TicketStatus to)
    : BusinessRuleException(
        $"Cannot change ticket status from '{from}' to '{to}'.",
        "INVALID_TRANSITION")
{
    public TicketStatus From { get; } = from;
    public TicketStatus To { get; } = to;
    public IReadOnlyList<TicketStatus> AllowedTargets { get; } = Ticket.AllowedTransitions[from];
}

/// <summary>Rules 5/6/7-context: the ticket is Closed and therefore read-only.</summary>
public sealed class TicketClosedException()
    : BusinessRuleException("This ticket is closed and can no longer be modified.", "TICKET_CLOSED");

/// <summary>Rule 4: an inactive agent cannot take assignments.</summary>
public sealed class InactiveAgentException(string agentName)
    : BusinessRuleException(
        $"Agent '{agentName}' is inactive and cannot be assigned to tickets.",
        "AGENT_INACTIVE");

/// <summary>Rule 3: entering In Progress requires an assigned agent.</summary>
public sealed class AgentAssignmentRequiredException()
    : BusinessRuleException(
        "The ticket must be assigned to an active agent before it can move to In Progress.",
        "AGENT_ASSIGNMENT_REQUIRED");
