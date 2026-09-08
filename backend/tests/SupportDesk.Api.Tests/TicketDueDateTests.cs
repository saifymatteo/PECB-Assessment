using SupportDesk.Api.Domain;
using Xunit;

namespace SupportDesk.Api.Tests;

/// <summary>Rules 1 and 7: due-date derivation, recalculation, and the overdue definition.</summary>
public class TicketDueDateTests
{
    private static readonly DateTime CreatedAt = new(2026, 1, 10, 9, 0, 0, DateTimeKind.Utc);

    private static Ticket NewTicket(TicketPriority priority)
        => Ticket.Create("TCK-2026-0009", "Refund missing", "Customer never got it",
            "Alice", "alice@example.com", priority, CreatedAt);

    [Theory]
    [InlineData(TicketPriority.Critical, 4)]
    [InlineData(TicketPriority.High, 24)]
    [InlineData(TicketPriority.Normal, 72)]
    [InlineData(TicketPriority.Low, 168)]
    public void Due_date_is_derived_from_priority_at_creation(TicketPriority priority, double expectedHours)
    {
        var ticket = NewTicket(priority);

        Assert.Equal(CreatedAt.AddHours(expectedHours), ticket.DueDate);
    }

    [Fact]
    public void Priority_change_while_open_recalculates_due_date_from_original_creation_date()
    {
        var ticket = NewTicket(TicketPriority.Normal); // due = created + 3 days
        var changedAt = CreatedAt.AddDays(2);          // halfway through the SLA

        ticket.UpdateDetails("Refund missing", "Customer never got it", "Alice",
            "alice@example.com", TicketPriority.Critical, changedAt);

        // recomputed from CreatedAt, NOT from changedAt
        Assert.Equal(CreatedAt.AddHours(4), ticket.DueDate);
    }

    [Fact]
    public void Recalculated_due_date_can_make_an_open_ticket_overdue()
    {
        var ticket = NewTicket(TicketPriority.Normal);
        ticket.UpdateDetails("Refund missing", "Customer never got it", "Alice",
            "alice@example.com", TicketPriority.Critical, CreatedAt.AddDays(2)); // due now created+4h
        var now = CreatedAt.AddDays(2);

        Assert.True(ticket.IsOverdue(now)); // due date passed while still open
    }

    [Fact]
    public void Priority_unchanged_means_due_date_untouched()
    {
        var ticket = NewTicket(TicketPriority.High);

        ticket.UpdateDetails("New title", "New description", "Alice",
            "alice@example.com", TicketPriority.High, CreatedAt.AddDays(1));

        Assert.Equal(CreatedAt.AddHours(24), ticket.DueDate);
    }

    [Fact]
    public void Priority_can_be_changed_while_resolved_and_recalculates_due_date()
    {
        var ticket = NewTicket(TicketPriority.Normal);
        var agent = new Agent("Evan", "evan@example.com", AgentDepartment.Billing);
        ticket.AssignAgent(agent, CreatedAt);
        ticket.TransitionTo(TicketStatus.InProgress, CreatedAt);
        ticket.TransitionTo(TicketStatus.Resolved, CreatedAt.AddDays(1));

        ticket.UpdateDetails("Refund missing", "Customer never got it", "Alice",
            "alice@example.com", TicketPriority.Critical, CreatedAt.AddDays(1));

        Assert.Equal(TicketStatus.Resolved, ticket.Status);
        Assert.Equal(CreatedAt.AddHours(4), ticket.DueDate); // D7c: allowed, recalculated
    }

    [Theory]
    [InlineData(TicketStatus.New, true)]
    [InlineData(TicketStatus.InProgress, true)]
    [InlineData(TicketStatus.Resolved, false)]
    [InlineData(TicketStatus.Closed, false)]
    public void Overdue_requires_passed_due_date_and_non_terminal_status(TicketStatus status, bool expectedOverdue)
    {
        var ticket = NewTicket(TicketPriority.Normal); // due = created + 3 days
        var agent = new Agent("Evan", "evan@example.com", AgentDepartment.Billing);
        ticket.AssignAgent(agent, CreatedAt);

        var now = CreatedAt.AddDays(4); // due date passed
        switch (status)
        {
            case TicketStatus.InProgress: ticket.TransitionTo(TicketStatus.InProgress, CreatedAt); break;
            case TicketStatus.Resolved:
                ticket.TransitionTo(TicketStatus.InProgress, CreatedAt);
                ticket.TransitionTo(TicketStatus.Resolved, CreatedAt);
                break;
            case TicketStatus.Closed:
                ticket.TransitionTo(TicketStatus.InProgress, CreatedAt);
                ticket.TransitionTo(TicketStatus.Resolved, CreatedAt);
                ticket.TransitionTo(TicketStatus.Closed, CreatedAt);
                break;
        }

        Assert.Equal(expectedOverdue, ticket.IsOverdue(now));
    }

    [Fact]
    public void Not_overdue_while_due_date_is_in_the_future()
    {
        var ticket = NewTicket(TicketPriority.Normal);

        Assert.False(ticket.IsOverdue(CreatedAt.AddDays(2)));
        Assert.False(ticket.IsOverdue(CreatedAt)); // boundary: exactly at due date is not overdue either
    }
}
