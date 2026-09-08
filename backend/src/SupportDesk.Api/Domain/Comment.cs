namespace SupportDesk.Api.Domain;

/// <summary>A note attached to a ticket by anyone (agent or customer-facing staff).</summary>
public class Comment
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    public Guid TicketId { get; private set; }
    public Ticket Ticket { get; private set; } = null!;

    public string AuthorName { get; private set; } = null!;
    public string Body { get; private set; } = null!;

    public DateTime CreatedAt { get; private set; }

    private Comment() { } // EF

    internal Comment(Guid ticketId, string authorName, string body, DateTime createdAt)
    {
        TicketId = ticketId;
        AuthorName = authorName;
        Body = body;
        CreatedAt = createdAt;
    }
}
