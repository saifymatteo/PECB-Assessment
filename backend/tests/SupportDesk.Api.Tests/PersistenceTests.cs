using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SupportDesk.Api.Contracts;
using SupportDesk.Api.Domain;
using SupportDesk.Api.Features;
using SupportDesk.Api.Infrastructure;
using Xunit;

namespace SupportDesk.Api.Tests;

/// <summary>
/// Service-level persistence tests against a real (in-memory) relational database.
/// These catch EF-specific traps that pure domain tests cannot, e.g. the entity state
/// of nav-discovered child entities.
/// </summary>
public class PersistenceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;
    private readonly TicketService _tickets;

    public PersistenceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;
        _db = new AppDbContext(options);
        _db.Database.EnsureCreated();
        _tickets = new TicketService(_db, new TicketReferenceGenerator(_db));
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private async Task<Guid> SeedOpenTicketAsync()
    {
        var agent = new Agent("Dana", $"dana-{Guid.NewGuid():N}@example.com", AgentDepartment.Technical);
        _db.Agents.Add(agent);

        var created = await _tickets.CreateAsync(new CreateTicketRequest
        {
            Title = "T", Description = "D", CustomerName = "C",
            CustomerEmail = "c@example.com", Priority = TicketPriority.Low,
        }, CancellationToken.None);

        await _tickets.AssignAgentAsync(created.Id, agent.Id, CancellationToken.None);
        await _tickets.ChangeStatusAsync(created.Id, TicketStatus.InProgress, CancellationToken.None);
        return created.Id;
    }

    [Fact]
    public async Task Add_comment_through_service_persists()
    {
        var ticketId = await SeedOpenTicketAsync();

        var comment = await _tickets.AddCommentAsync(ticketId,
            new AddCommentRequest { AuthorName = "Dana", Body = "Investigating." },
            CancellationToken.None);

        Assert.NotEqual(Guid.Empty, comment.Id);
        Assert.Equal(1, (await _tickets.GetByIdAsync(ticketId, CancellationToken.None))!.Comments.Count);
    }

    [Fact]
    public async Task Created_tickets_get_increasing_references()
    {
        var first = await _tickets.CreateAsync(new CreateTicketRequest
        {
            Title = "T1", Description = "D", CustomerName = "C", CustomerEmail = "c@example.com",
            Priority = TicketPriority.Low,
        }, CancellationToken.None);

        var second = await _tickets.CreateAsync(new CreateTicketRequest
        {
            Title = "T2", Description = "D", CustomerName = "C", CustomerEmail = "c2@example.com",
            Priority = TicketPriority.Low,
        }, CancellationToken.None);

        Assert.Matches(@"^TCK-\d{4}-\d{4}$", first.Reference);
        Assert.Equal(int.Parse(first.Reference[^4..]) + 1, int.Parse(second.Reference[^4..]));
    }

    [Fact]
    public async Task Deleting_an_open_ticket_cascades_its_comments()
    {
        var ticketId = await SeedOpenTicketAsync();
        await _tickets.AddCommentAsync(ticketId,
            new AddCommentRequest { AuthorName = "A", Body = "b" }, CancellationToken.None);

        await _tickets.DeleteAsync(ticketId, CancellationToken.None);

        Assert.Equal(0, await _db.Comments.CountAsync(c => c.TicketId == ticketId));
        Assert.Null(await _db.Tickets.FindAsync(ticketId));
    }

    [Fact]
    public async Task Deleting_a_closed_ticket_is_rejected()
    {
        var ticketId = await SeedOpenTicketAsync();
        await _tickets.ChangeStatusAsync(ticketId, TicketStatus.Resolved, CancellationToken.None);
        await _tickets.ChangeStatusAsync(ticketId, TicketStatus.Closed, CancellationToken.None);

        await Assert.ThrowsAsync<TicketClosedException>(
            () => _tickets.DeleteAsync(ticketId, CancellationToken.None));

        Assert.NotNull(await _db.Tickets.FindAsync(ticketId)); // still there
    }
}
