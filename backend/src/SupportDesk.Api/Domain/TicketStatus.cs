namespace SupportDesk.Api.Domain;

/// <summary>Lifecycle of a ticket. Transitions are constrained; see <see cref="Ticket.AllowedTransitions"/>.</summary>
public enum TicketStatus
{
    New = 1,
    InProgress = 2,
    Resolved = 3,
    Closed = 4,
}
