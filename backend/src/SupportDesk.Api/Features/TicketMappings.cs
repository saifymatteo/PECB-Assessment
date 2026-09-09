using SupportDesk.Api.Contracts;
using SupportDesk.Api.Domain;

namespace SupportDesk.Api.Features;

/// <summary>Manual DTO mapping - six endpoints do not justify a mapper dependency.</summary>
public static class TicketMappings
{
    public static TicketListItemDto ToListItemDto(this Ticket ticket, DateTime now) => new(
        ticket.Id,
        ticket.Reference,
        ticket.Title,
        ticket.CustomerName,
        ticket.Priority.ToString(),
        ticket.Status.ToString(),
        ticket.AssignedAgentId,
        ticket.AssignedAgent?.FullName,
        ticket.CreatedAt,
        ticket.DueDate,
        ticket.IsOverdue(now));

    public static TicketDetailDto ToDetailDto(this Ticket ticket, DateTime now) => new(
        ticket.Id,
        ticket.Reference,
        ticket.Title,
        ticket.Description,
        ticket.CustomerName,
        ticket.CustomerEmail,
        ticket.Priority.ToString(),
        ticket.Status.ToString(),
        ticket.AssignedAgentId,
        ticket.AssignedAgent?.FullName,
        ticket.CreatedAt,
        ticket.LastModifiedAt,
        ticket.ResolvedAt,
        ticket.ClosedAt,
        ticket.DueDate,
        ticket.IsOverdue(now),
        [.. Ticket.AllowedTransitions[ticket.Status].Select(s => s.ToString())],
        [.. ticket.Comments
            .OrderBy(c => c.CreatedAt)
            .Select(c => c.ToCommentDto())]);

    public static TicketCommentDto ToCommentDto(this Comment comment) => new(
        comment.Id,
        comment.AuthorName,
        comment.Body,
        comment.CreatedAt);

    public static AgentDto ToAgentDto(this Agent agent) => new(
        agent.Id,
        agent.FullName,
        agent.Email,
        agent.Department.ToString(),
        agent.Active);
}
