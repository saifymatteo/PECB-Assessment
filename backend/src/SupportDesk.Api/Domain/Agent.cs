namespace SupportDesk.Api.Domain;

/// <summary>A support team member who can be assigned tickets.</summary>
public class Agent
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    public string FullName { get; private set; } = null!;

    /// <summary>Unique. Used for sign-in contexts later; today it is contact + identity data.</summary>
    public string Email { get; private set; } = null!;

    public AgentDepartment Department { get; private set; }

    /// <summary>Inactive agents cannot be assigned to tickets (rule 4) but keep their history.</summary>
    public bool Active { get; private set; } = true;

    private readonly List<Ticket> _tickets = [];
    public IReadOnlyCollection<Ticket> Tickets => _tickets.AsReadOnly();

    private Agent() { } // EF

    public Agent(string fullName, string email, AgentDepartment department, bool active = true)
    {
        FullName = fullName;
        Email = email;
        Department = department;
        Active = active;
    }

    public void SetActive(bool active) => Active = active;
}
