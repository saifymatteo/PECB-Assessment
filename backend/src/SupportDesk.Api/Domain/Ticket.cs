namespace SupportDesk.Api.Domain;

/// <summary>
/// A customer issue tracked from report to closure.
/// <para>
/// This entity is the single home of the ticket business rules:
/// status transition graph, due-date derivation, closed-is-read-only, and
/// the active-agent requirement for entering In Progress. Application
/// services orchestrate persistence around these guards; controllers never
/// contain rule logic.
/// </para>
/// </summary>
public class Ticket
{
    /// <summary>Legal status transitions. Closed is terminal; New cannot skip to Resolved.</summary>
    public static readonly IReadOnlyDictionary<TicketStatus, IReadOnlyList<TicketStatus>> AllowedTransitions =
        new Dictionary<TicketStatus, IReadOnlyList<TicketStatus>>
        {
            [TicketStatus.New] = [TicketStatus.InProgress],
            [TicketStatus.InProgress] = [TicketStatus.Resolved],
            [TicketStatus.Resolved] = [TicketStatus.Closed, TicketStatus.InProgress], // In Progress = reopening
            [TicketStatus.Closed] = [],
        };

    /// <summary>Due-date offsets from the creation date, per priority (rule 1).</summary>
    private static readonly IReadOnlyDictionary<TicketPriority, TimeSpan> DueDateOffsets =
        new Dictionary<TicketPriority, TimeSpan>
        {
            [TicketPriority.Critical] = TimeSpan.FromHours(4),
            [TicketPriority.High] = TimeSpan.FromDays(1),
            [TicketPriority.Normal] = TimeSpan.FromDays(3),
            [TicketPriority.Low] = TimeSpan.FromDays(7),
        };

    public Guid Id { get; private set; } = Guid.NewGuid();

    /// <summary>Human-readable unique reference (e.g. TCK-2026-0001). System-generated.</summary>
    public string Reference { get; private set; } = null!;

    public string Title { get; private set; } = null!;
    public string Description { get; private set; } = null!;

    public string CustomerName { get; private set; } = null!;
    public string CustomerEmail { get; private set; } = null!;

    public TicketPriority Priority { get; private set; }
    public TicketStatus Status { get; private set; } = TicketStatus.New;

    public Guid? AssignedAgentId { get; private set; }
    public Agent? AssignedAgent { get; private set; }

    public DateTime CreatedAt { get; private set; }
    public DateTime LastModifiedAt { get; private set; }
    public DateTime? ResolvedAt { get; private set; }
    public DateTime? ClosedAt { get; private set; }

    /// <summary>System-derived from the priority, anchored to the original creation date. Never client-supplied.</summary>
    public DateTime DueDate { get; private set; }

    private readonly List<Comment> _comments = [];
    public IReadOnlyCollection<Comment> Comments => _comments.AsReadOnly();

    private Ticket() { } // EF

    private Ticket(string reference, string title, string description, string customerName,
        string customerEmail, TicketPriority priority, DateTime now)
    {
        Reference = reference;
        Title = title;
        Description = description;
        CustomerName = customerName;
        CustomerEmail = customerEmail;
        Priority = priority;
        Status = TicketStatus.New;
        CreatedAt = now;
        LastModifiedAt = now;
        DueDate = now + DueDateOffsets[priority];
    }

    /// <summary>Factory used by the application layer, which supplies the system-generated reference.</summary>
    public static Ticket Create(string reference, string title, string description,
        string customerName, string customerEmail, TicketPriority priority, DateTime now)
        => new(reference, title, description, customerName, customerEmail, priority, now);

    public bool IsClosed => Status == TicketStatus.Closed;

    /// <summary>Rule 7: overdue when the due date has passed and the ticket is neither Resolved nor Closed.</summary>
    public bool IsOverdue(DateTime now) => DueDate < now && Status is not (TicketStatus.Resolved or TicketStatus.Closed);

    /// <summary>
    /// Rule 2 (transition graph) and rule 3 (an assigned, active agent is required to enter
    /// In Progress - including when reopening). Rule 6: Resolved/Closed timestamps are set here,
    /// by the system. Reopening clears <see cref="ResolvedAt"/> so it always reflects the latest resolution.
    /// </summary>
    public void TransitionTo(TicketStatus target, DateTime now)
    {
        if (IsClosed)
            throw new TicketClosedException();

        if (!AllowedTransitions[Status].Contains(target))
            throw new InvalidTicketTransitionException(Status, target);

        if (target == TicketStatus.InProgress)
            RequireActiveAgentForInProgress();

        Status = target;
        LastModifiedAt = now;

        switch (target)
        {
            case TicketStatus.Resolved:
                ResolvedAt = now;
                break;
            case TicketStatus.Closed:
                ResolvedAt ??= now; // defensive; a legal path always resolved first
                ClosedAt = now;
                break;
            case TicketStatus.InProgress:
                ResolvedAt = null; // reopened
                break;
        }
    }

    /// <summary>Rule 4: only active agents can be assigned; unassignment is allowed in any non-Closed status.</summary>
    public void AssignAgent(Agent? agent, DateTime now)
    {
        if (IsClosed)
            throw new TicketClosedException();

        if (agent is not null && !agent.Active)
            throw new InactiveAgentException(agent.FullName);

        AssignedAgent = agent;
        AssignedAgentId = agent?.Id;
        LastModifiedAt = now;
    }

    /// <summary>
    /// Rules 1 and 5: editable fields on a non-Closed ticket. A priority change recomputes the
    /// due date from the ORIGINAL creation date.
    /// </summary>
    public void UpdateDetails(string title, string description, string customerName,
        string customerEmail, TicketPriority priority, DateTime now)
    {
        if (IsClosed)
            throw new TicketClosedException();

        Title = title;
        Description = description;
        CustomerName = customerName;
        CustomerEmail = customerEmail;

        if (priority != Priority)
        {
            Priority = priority;
            DueDate = CreatedAt + DueDateOffsets[priority];
        }

        LastModifiedAt = now;
    }

    /// <summary>Rule 5: no new comments on a Closed ticket.</summary>
    public Comment AddComment(string authorName, string body, DateTime now)
    {
        if (IsClosed)
            throw new TicketClosedException();

        var comment = new Comment(Id, authorName, body, now);
        _comments.Add(comment);
        LastModifiedAt = now;
        return comment;
    }

    /// <summary>The current assignee must be supplied by the caller (loaded from persistence).</summary>
    private void RequireActiveAgentForInProgress()
    {
        if (AssignedAgent is null)
            throw new AgentAssignmentRequiredException();

        if (!AssignedAgent.Active)
            throw new InactiveAgentException(AssignedAgent.FullName);
    }
}
