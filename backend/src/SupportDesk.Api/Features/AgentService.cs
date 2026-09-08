using Microsoft.EntityFrameworkCore;
using SupportDesk.Api.Contracts;
using SupportDesk.Api.Domain;
using SupportDesk.Api.Features;
using SupportDesk.Api.Infrastructure;
using SupportDesk.Api.Infrastructure.ErrorHandling;

namespace SupportDesk.Api.Features;

public interface IAgentService
{
    Task<IReadOnlyList<AgentDto>> ListAsync(string? search, CancellationToken cancellationToken);
    Task<AgentDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<AgentDto> CreateAsync(CreateAgentRequest request, CancellationToken cancellationToken);
    Task<AgentDto> SetActiveAsync(Guid id, bool active, CancellationToken cancellationToken);
}

public class AgentService(AppDbContext db) : IAgentService
{
    public async Task<IReadOnlyList<AgentDto>> ListAsync(string? search, CancellationToken cancellationToken)
    {
        var agents = db.Agents.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            agents = agents.Where(a =>
                EF.Functions.ILike(a.FullName, pattern) ||
                EF.Functions.ILike(a.Email, pattern));
        }

        var list = await agents
            .OrderBy(a => a.FullName)
            .ToListAsync(cancellationToken);

        return [.. list.Select(a => a.ToAgentDto())];
    }

    public Task<AgentDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        => db.Agents.AsNoTracking()
            .Where(a => a.Id == id)
            .Select(a => a.ToAgentDto())
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<AgentDto> CreateAsync(CreateAgentRequest request, CancellationToken cancellationToken)
    {
        var emailTaken = await db.Agents.AnyAsync(a => a.Email == request.Email, cancellationToken);
        if (emailTaken)
            throw new DuplicateEmailException(request.Email);

        var agent = new Agent(request.FullName, request.Email, request.Department, request.Active);
        db.Agents.Add(agent);
        await db.SaveChangesAsync(cancellationToken);

        return agent.ToAgentDto();
    }

    public async Task<AgentDto> SetActiveAsync(Guid id, bool active, CancellationToken cancellationToken)
    {
        var agent = await db.Agents.SingleOrDefaultAsync(a => a.Id == id, cancellationToken)
            ?? throw new NotFoundException("Agent", id);

        agent.SetActive(active);
        await db.SaveChangesAsync(cancellationToken);

        return agent.ToAgentDto();
    }
}
