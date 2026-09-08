using System.ComponentModel.DataAnnotations;
using SupportDesk.Api.Domain;

namespace SupportDesk.Api.Contracts;

public record TicketListItemDto(
    Guid Id,
    string Reference,
    string Title,
    string CustomerName,
    string Priority,
    string Status,
    Guid? AssignedAgentId,
    string? AssignedAgentName,
    DateTime CreatedAt,
    DateTime DueDate,
    bool IsOverdue);

public record TicketCommentDto(Guid Id, string AuthorName, string Body, DateTime CreatedAt);

public record TicketDetailDto(
    Guid Id,
    string Reference,
    string Title,
    string Description,
    string CustomerName,
    string CustomerEmail,
    string Priority,
    string Status,
    Guid? AssignedAgentId,
    string? AssignedAgentName,
    DateTime CreatedAt,
    DateTime LastModifiedAt,
    DateTime? ResolvedAt,
    DateTime? ClosedAt,
    DateTime DueDate,
    bool IsOverdue,
    /// <summary>Server-side truth about which moves are legal right now — never client-calculated alone.</summary>
    IReadOnlyList<string> AllowedTransitions,
    IReadOnlyList<TicketCommentDto> Comments);

public class CreateTicketRequest
{
    [Required, MaxLength(200)]
    public string Title { get; init; } = null!;

    [Required, MaxLength(4000)]
    public string Description { get; init; } = null!;

    [Required, MaxLength(200)]
    public string CustomerName { get; init; } = null!;

    [Required, MaxLength(320), EmailAddress]
    public string CustomerEmail { get; init; } = null!;

    /// <summary>Due date is derived from this; no due-date field is accepted from clients (rule 1).</summary>
    [Required, EnumDataType(typeof(TicketPriority))]
    public TicketPriority Priority { get; init; }
}

public class UpdateTicketRequest
{
    [Required, MaxLength(200)]
    public string Title { get; init; } = null!;

    [Required, MaxLength(4000)]
    public string Description { get; init; } = null!;

    [Required, MaxLength(200)]
    public string CustomerName { get; init; } = null!;

    [Required, MaxLength(320), EmailAddress]
    public string CustomerEmail { get; init; } = null!;

    [Required, EnumDataType(typeof(TicketPriority))]
    public TicketPriority Priority { get; init; }
}

public class ChangeStatusRequest
{
    [Required, EnumDataType(typeof(TicketStatus))]
    public TicketStatus Status { get; init; }
}

public class AddCommentRequest
{
    [Required, MaxLength(200)]
    public string AuthorName { get; init; } = null!;

    [Required, MaxLength(4000)]
    public string Body { get; init; } = null!;
}

/// <summary>Null AgentId unassigns; a value assigns (active agents only, rule 4).</summary>
public class AssignAgentRequest
{
    public Guid? AgentId { get; init; }
}

public class TicketListQuery
{
    [Range(1, int.MaxValue)] public int Page { get; init; } = 1;
    [Range(1, 100)] public int PageSize { get; init; } = 20;
    public string? Search { get; init; }
    public TicketStatus? Status { get; init; }
    public TicketPriority? Priority { get; init; }
    public Guid? AgentId { get; init; }

    /// <summary>true = only tickets past their due date that are not Resolved/Closed (rule 7).</summary>
    public bool? Overdue { get; init; }
}

public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalItems)
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalItems / (double)PageSize);

    public static PagedResult<T> Create(IReadOnlyList<T> items, int page, int pageSize, int totalItems)
        => new(items, page, pageSize, totalItems);
}
