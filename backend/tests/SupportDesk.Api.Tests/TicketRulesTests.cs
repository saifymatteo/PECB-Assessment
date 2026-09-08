using SupportDesk.Api.Domain;
using Xunit;

namespace SupportDesk.Api.Tests;

/// <summary>Rule 2 + rule 3: the status transition graph and its preconditions.</summary>
public class TicketTransitionTests
{
    private static readonly DateTime Now = new(2026, 1, 10, 9, 0, 0, DateTimeKind.Utc);

    private static Ticket NewTicket(TicketPriority priority = TicketPriority.Normal)
        => Ticket.Create("TCK-2026-0001", "Printer on fire", "Smells bad",
            "Alice Customer", "alice@example.com", priority, Now);

    private static Agent ActiveAgent() => new("Dana Support", "dana@example.com", AgentDepartment.Technical);

    /// <summary>Drives a fresh ticket into the requested legal status (New → In Progress → Resolved → Closed).</summary>
    private static Ticket TicketInStatus(TicketStatus target, Agent? agent = null)
    {
        var ticket = NewTicket();
        if (agent is not null) ticket.AssignAgent(agent, Now);
        if (target >= TicketStatus.InProgress) ticket.TransitionTo(TicketStatus.InProgress, Now);
        if (target >= TicketStatus.Resolved) ticket.TransitionTo(TicketStatus.Resolved, Now);
        if (target == TicketStatus.Closed) ticket.TransitionTo(TicketStatus.Closed, Now);
        return ticket;
    }

    [Theory]
    [InlineData(TicketStatus.New, TicketStatus.InProgress)]
    [InlineData(TicketStatus.InProgress, TicketStatus.Resolved)]
    [InlineData(TicketStatus.Resolved, TicketStatus.Closed)]
    public void Legal_transition_succeeds(TicketStatus from, TicketStatus to)
    {
        var ticket = TicketInStatus(from, ActiveAgent());

        ticket.TransitionTo(to, Now.AddHours(1));

        Assert.Equal(to, ticket.Status);
    }

    [Fact]
    public void Reopen_resolved_to_in_progress_succeeds_and_clears_resolved_at()
    {
        var ticket = NewTicket();
        ticket.AssignAgent(ActiveAgent(), Now);
        ticket.TransitionTo(TicketStatus.InProgress, Now);
        ticket.TransitionTo(TicketStatus.Resolved, Now.AddHours(2));
        var resolvedAt = ticket.ResolvedAt;

        ticket.TransitionTo(TicketStatus.InProgress, Now.AddHours(3));

        Assert.Equal(TicketStatus.InProgress, ticket.Status);
        Assert.Null(ticket.ResolvedAt);      // reflects the latest resolution only
        Assert.NotNull(resolvedAt);          // the earlier resolution had a timestamp
    }

    [Theory]
    [InlineData(TicketStatus.Resolved)] // skip: New -> Resolved is illegal
    [InlineData(TicketStatus.Closed)]
    public void New_ticket_cannot_skip_to_terminal_statuses(TicketStatus target)
    {
        var ticket = NewTicket();

        var ex = Assert.Throws<InvalidTicketTransitionException>(() => ticket.TransitionTo(target, Now));

        Assert.Equal(TicketStatus.New, ex.From);
        Assert.Equal(target, ex.To);
        Assert.Equal([TicketStatus.InProgress], ex.AllowedTargets);
        Assert.Equal(TicketStatus.New, ticket.Status); // unchanged
    }

    [Fact]
    public void In_progress_cannot_jump_straight_to_closed()
    {
        var ticket = NewTicket();
        ticket.AssignAgent(ActiveAgent(), Now);
        ticket.TransitionTo(TicketStatus.InProgress, Now);

        Assert.Throws<InvalidTicketTransitionException>(
            () => ticket.TransitionTo(TicketStatus.Closed, Now));
    }

    [Fact]
    public void Closed_ticket_can_never_be_reopened_or_changed()
    {
        var ticket = NewTicket();
        ticket.AssignAgent(ActiveAgent(), Now);
        ticket.TransitionTo(TicketStatus.InProgress, Now);
        ticket.TransitionTo(TicketStatus.Resolved, Now);
        ticket.TransitionTo(TicketStatus.Closed, Now);

        Assert.Throws<TicketClosedException>(() => ticket.TransitionTo(TicketStatus.InProgress, Now));
        Assert.Throws<TicketClosedException>(() => ticket.TransitionTo(TicketStatus.Resolved, Now));
        Assert.Equal(TicketStatus.Closed, ticket.Status);
    }

    [Fact]
    public void Transition_to_in_progress_without_agent_is_rejected()
    {
        var ticket = NewTicket(); // never assigned

        var ex = Assert.Throws<AgentAssignmentRequiredException>(
            () => ticket.TransitionTo(TicketStatus.InProgress, Now));

        Assert.Equal("AGENT_ASSIGNMENT_REQUIRED", ex.ErrorCode);
        Assert.Equal(TicketStatus.New, ticket.Status);
    }

    [Fact]
    public void Transition_to_in_progress_is_rejected_when_the_assignee_was_deactivated_after_assignment()
    {
        var ticket = NewTicket();
        var agent = ActiveAgent();
        ticket.AssignAgent(agent, Now); // active at assignment time (rule 4 satisfied)
        agent.SetActive(false);         // then deactivated before work starts

        var ex = Assert.Throws<InactiveAgentException>(
            () => ticket.TransitionTo(TicketStatus.InProgress, Now));

        Assert.Equal("AGENT_INACTIVE", ex.ErrorCode);
        Assert.Equal(TicketStatus.New, ticket.Status);
    }

    [Fact]
    public void Reopen_requires_an_assigned_active_agent()
    {
        var ticket = NewTicket();
        var agent = ActiveAgent();
        ticket.AssignAgent(agent, Now);
        ticket.TransitionTo(TicketStatus.InProgress, Now);
        ticket.TransitionTo(TicketStatus.Resolved, Now);

        ticket.AssignAgent(null, Now); // unassign while Resolved (allowed)

        Assert.Throws<AgentAssignmentRequiredException>(
            () => ticket.TransitionTo(TicketStatus.InProgress, Now));
    }

    [Fact]
    public void Deactivating_the_agent_after_in_progress_does_not_kick_the_ticket_back()
    {
        var ticket = NewTicket();
        var agent = ActiveAgent();
        ticket.AssignAgent(agent, Now);
        ticket.TransitionTo(TicketStatus.InProgress, Now);

        agent.SetActive(false);

        Assert.Equal(TicketStatus.InProgress, ticket.Status); // only transitions INTO In Progress are gated
    }
}

/// <summary>Rules 4/5/6: assignment guard, closed-is-read-only, system-set timestamps.</summary>
public class TicketClosedAndAssignmentTests
{
    private static readonly DateTime Now = new(2026, 1, 10, 9, 0, 0, DateTimeKind.Utc);

    private static Ticket ClosedTicket()
    {
        var ticket = Ticket.Create("TCK-2026-0002", "Login broken", "500 on login",
            "Bob", "bob@example.com", TicketPriority.High, Now);
        ticket.AssignAgent(new Agent("Evan", "evan@example.com", AgentDepartment.Billing), Now);
        ticket.TransitionTo(TicketStatus.InProgress, Now);
        ticket.TransitionTo(TicketStatus.Resolved, Now.AddHours(1));
        ticket.TransitionTo(TicketStatus.Closed, Now.AddHours(2));
        return ticket;
    }

    [Fact]
    public void Closed_ticket_rejects_edits_comments_and_assignment()
    {
        var ticket = ClosedTicket();

        Assert.Throws<TicketClosedException>(() =>
            ticket.UpdateDetails("x", "y", "z", "z@e.com", TicketPriority.Low, Now));
        Assert.Throws<TicketClosedException>(() => ticket.AddComment("Agent", "text", Now));
        Assert.Throws<TicketClosedException>(() => ticket.AssignAgent(null, Now));
    }

    [Fact]
    public void Resolved_and_closed_dates_are_set_by_the_system_during_transitions()
    {
        var ticket = Ticket.Create("TCK-2026-0003", "S", "D", "C", "c@e.com", TicketPriority.Normal, Now);

        var resolvedAt = Now.AddHours(5);
        var closedAt = Now.AddHours(9);
        ticket.AssignAgent(new Agent("Evan", "evan@example.com", AgentDepartment.Billing), Now);
        ticket.TransitionTo(TicketStatus.InProgress, Now.AddHours(1));
        ticket.TransitionTo(TicketStatus.Resolved, resolvedAt);
        ticket.TransitionTo(TicketStatus.Closed, closedAt);

        Assert.Equal(resolvedAt, ticket.ResolvedAt);
        Assert.Equal(closedAt, ticket.ClosedAt);
    }

    [Fact]
    public void Assigning_an_inactive_agent_is_rejected()
    {
        var ticket = Ticket.Create("TCK-2026-0004", "S", "D", "C", "c@e.com", TicketPriority.Normal, Now);
        var agent = new Agent("Frank", "frank@example.com", AgentDepartment.General);
        agent.SetActive(false);

        var ex = Assert.Throws<InactiveAgentException>(() => ticket.AssignAgent(agent, Now));

        Assert.Equal("AGENT_INACTIVE", ex.ErrorCode);
        Assert.Null(ticket.AssignedAgentId);
    }

    [Fact]
    public void Unassign_and_reassign_are_allowed_while_not_closed()
    {
        var ticket = Ticket.Create("TCK-2026-0005", "S", "D", "C", "c@e.com", TicketPriority.Normal, Now);
        var first = new Agent("Evan", "evan@example.com", AgentDepartment.Billing);
        var second = new Agent("Gina", "gina@example.com", AgentDepartment.Technical);

        ticket.AssignAgent(first, Now);
        ticket.TransitionTo(TicketStatus.InProgress, Now);
        ticket.AssignAgent(second, Now); // reassign in progress

        Assert.Equal(second.Id, ticket.AssignedAgentId);

        ticket.AssignAgent(null, Now); // unassign in progress

        Assert.Null(ticket.AssignedAgentId);
    }
}
