using Microsoft.EntityFrameworkCore;
using SupportDesk.Api.Contracts;
using SupportDesk.Api.Domain;
using SupportDesk.Api.Features;
using SupportDesk.Api.Infrastructure;
using SupportDesk.Api.Infrastructure.ErrorHandling;

namespace SupportDesk.Api.Features;

public interface ITicketService
{
    Task<PagedResult<TicketListItemDto>> ListAsync(TicketListQuery query, CancellationToken cancellationToken);
    Task<TicketDetailDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<TicketDetailDto> CreateAsync(CreateTicketRequest request, CancellationToken cancellationToken);
    Task<TicketDetailDto> UpdateAsync(Guid id, UpdateTicketRequest request, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
    Task<TicketDetailDto> AssignAgentAsync(Guid id, Guid? agentId, CancellationToken cancellationToken);
    Task<TicketDetailDto> ChangeStatusAsync(Guid id, TicketStatus status, CancellationToken cancellationToken);
    Task<TicketCommentDto> AddCommentAsync(Guid id, AddCommentRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// Orchestrates persistence around the domain rules; the rules themselves live on
/// <see cref="Ticket"/> (ADR 0002). No business decisions are made here.
/// </summary>
public class TicketService(AppDbContext db, TicketReferenceGenerator referenceGenerator) : ITicketService
{
    public async Task<PagedResult<TicketListItemDto>> ListAsync(TicketListQuery query, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        var tickets = db.Tickets.AsNoTracking().Include(t => t.AssignedAgent).AsQueryable();

        if (query.Status is { } status)
            tickets = tickets.Where(t => t.Status == status);

        if (query.Priority is { } priority)
            tickets = tickets.Where(t => t.Priority == priority);

        if (query.AgentId is { } agentId)
            tickets = tickets.Where(t => t.AssignedAgentId == agentId);

        // Rule 7 evaluated in the database so overdue-only filtering is server-side.
        if (query.Overdue == true)
            tickets = tickets.Where(t =>
                t.DueDate < now &&
                t.Status != TicketStatus.Resolved &&
                t.Status != TicketStatus.Closed);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = $"%{query.Search.Trim()}%";
            tickets = tickets.Where(t =>
                EF.Functions.ILike(t.Reference, pattern) ||
                EF.Functions.ILike(t.Title, pattern) ||
                EF.Functions.ILike(t.CustomerName, pattern));
        }

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var total = await tickets.CountAsync(cancellationToken);
        var items = await tickets
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return PagedResult<TicketListItemDto>.Create(
            items.Select(t => t.ToListItemDto(now)).ToList(), page, pageSize, total);
    }

    public async Task<TicketDetailDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var ticket = await db.Tickets
            .AsNoTracking()
            .Include(t => t.AssignedAgent)
            .Include(t => t.Comments)
            .SingleOrDefaultAsync(t => t.Id == id, cancellationToken);

        return ticket?.ToDetailDto(DateTime.UtcNow);
    }

    public async Task<TicketDetailDto> CreateAsync(CreateTicketRequest request, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        var reference = await referenceGenerator.NextReferenceAsync(now, cancellationToken);
        var ticket = Ticket.Create(reference, request.Title, request.Description,
            request.CustomerName, request.CustomerEmail, request.Priority, now);

        db.Tickets.Add(ticket);
        await db.SaveChangesAsync(cancellationToken);

        return ticket.ToDetailDto(now);
    }

    public async Task<TicketDetailDto> UpdateAsync(Guid id, UpdateTicketRequest request, CancellationToken cancellationToken)
    {
        var ticket = await LoadTracked(id, cancellationToken);

        ticket.UpdateDetails(request.Title, request.Description, request.CustomerName,
            request.CustomerEmail, request.Priority, DateTime.UtcNow);

        await db.SaveChangesAsync(cancellationToken);
        return ticket.ToDetailDto(DateTime.UtcNow);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var ticket = await LoadTracked(id, cancellationToken);

        // Assumption D7a: a closed ticket is read-only, which includes deletion.
        if (ticket.IsClosed)
            throw new TicketClosedException();

        db.Tickets.Remove(ticket);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<TicketDetailDto> AssignAgentAsync(Guid id, Guid? agentId, CancellationToken cancellationToken)
    {
        var ticket = await LoadTracked(id, cancellationToken);

        if (agentId is { } value)
        {
            var agent = await db.Agents.SingleOrDefaultAsync(a => a.Id == value, cancellationToken)
                ?? throw new NotFoundException("Agent", value);

            ticket.AssignAgent(agent, DateTime.UtcNow); // rule 4 enforced in the entity
        }
        else
        {
            ticket.AssignAgent(null, DateTime.UtcNow); // unassignment allowed while not closed (D7b)
        }

        await db.SaveChangesAsync(cancellationToken);
        return ticket.ToDetailDto(DateTime.UtcNow);
    }

    public async Task<TicketDetailDto> ChangeStatusAsync(Guid id, TicketStatus status, CancellationToken cancellationToken)
    {
        var ticket = await LoadTracked(id, cancellationToken);

        ticket.TransitionTo(status, DateTime.UtcNow); // rules 2, 3, 6 enforced in the entity

        await db.SaveChangesAsync(cancellationToken);
        return ticket.ToDetailDto(DateTime.UtcNow);
    }

    public async Task<TicketCommentDto> AddCommentAsync(Guid id, AddCommentRequest request, CancellationToken cancellationToken)
    {
        var ticket = await LoadTracked(id, cancellationToken);

        var comment = ticket.AddComment(request.AuthorName, request.Body, DateTime.UtcNow); // rule 5 in the entity

        // Explicit state: a nav-discovered entity whose GUID PK is already set would be
        // attached as Modified (an UPDATE against a row that does not exist yet).
        db.Entry(comment).State = EntityState.Added;

        await db.SaveChangesAsync(cancellationToken);
        return comment.ToCommentDto();
    }

    private async Task<Ticket> LoadTracked(Guid id, CancellationToken cancellationToken)
        => await db.Tickets
            .Include(t => t.AssignedAgent)
            .Include(t => t.Comments)
            .SingleOrDefaultAsync(t => t.Id == id, cancellationToken)
            ?? throw new NotFoundException("Ticket", id);
}
