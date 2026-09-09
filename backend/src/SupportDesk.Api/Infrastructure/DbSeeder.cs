using Microsoft.EntityFrameworkCore;
using SupportDesk.Api.Domain;

namespace SupportDesk.Api.Infrastructure;

/// <summary>
/// Seeds a demo dataset on first run only (no-op when data exists). Tickets are driven through
/// the same domain methods the API uses, so seeded timestamps and statuses always satisfy the
/// business rules. Dates are relative to seeding time, which keeps several tickets overdue.
/// </summary>
public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        if (await db.Tickets.AnyAsync(cancellationToken))
            return;

        var now = DateTime.UtcNow;

        // ---- Agents (5, one later deactivated, every department covered) ----------------
        var dana = new Agent("Dana Whitfield", "dana.whitfield@supportdesk.test", AgentDepartment.Technical);
        var evan = new Agent("Evan Ortiz", "evan.ortiz@supportdesk.test", AgentDepartment.Billing);
        var gina = new Agent("Gina Kowalski", "gina.kowalski@supportdesk.test", AgentDepartment.General);
        var marcus = new Agent("Marcus Lee", "marcus.lee@supportdesk.test", AgentDepartment.Technical);
        var priya = new Agent("Priya Nair", "priya.nair@supportdesk.test", AgentDepartment.General);

        db.Agents.AddRange(dana, evan, gina, marcus, priya);
        await db.SaveChangesAsync();

        var referenceGenerator = new TicketReferenceGenerator(db);

        // ---- Tickets --------------------------------------------------------------------
        // (title, description, customer, email, priority, target status, age, agent, hours-after-creation for each transition)
        var specs = new List<TicketSpec>
        {
            new("Payment gateway timeouts", "Checkout fails intermittently with 504 from the payment provider.", "Nora Blake", "nora.blake@acmecorp.com", TicketPriority.Critical, TicketStatus.New, 6.0, dana),
            new("Cannot reset password", "Reset link expires immediately for all users.", "Tom Ives", "tom.ivies@brightpath.io", TicketPriority.Critical, TicketStatus.New, 1.0, agent: null),
            new("Data export stuck", "CSV export has been pending for an hour.", "Lena Fischer", "lena.fischer@nordwind.de", TicketPriority.High, TicketStatus.New, 12.0, agent: null),
            new("Invoice VAT amount wrong", "December invoice shows 25% VAT instead of 20%.", "Omar Haddad", "omar.haddad@globex.com", TicketPriority.Normal, TicketStatus.New, 5.0, evan),
            new("Mobile app crashes on login", "App force-closes after tapping 'Sign in' on Android 15.", "Sara Kim", "sara.kim@zenko.jp", TicketPriority.Low, TicketStatus.New, 8.0, priya),
            new("Feature request: dark mode", "Team asks for a dark theme in the dashboard.", "Pete Larsen", "pete.larsen@lumenlabs.dev", TicketPriority.Low, TicketStatus.New, 24.0, agent: null),

            new("API returns 401 after token refresh", "All API calls fail once the access token is refreshed.", "Maya Chen", "maya.chen@fableworks.com", TicketPriority.High, TicketStatus.InProgress, 96.0, dana),
            new("Database replication lag", "Read replica is hours behind the primary.", "Igor Petrov", "igor.petrov@datalane.eu", TicketPriority.High, TicketStatus.InProgress, 4.5, marcus),
            new("Webhook retries too aggressively", "Endpoint receives retries every 10 seconds.", "Chris Doyle", "chris.doyle@spiralworks.com", TicketPriority.Normal, TicketStatus.InProgress, 1.0, priya),
            new("SSO login loop", "Users bounce between identity provider and app.", "Ahmed Salah", "ahmed.salah@meridian.org", TicketPriority.Critical, TicketStatus.InProgress, 6.0, marcus, reopened: true),
            new("Email notifications duplicated", "Every notification arrives twice since Monday.", "Julia Moreau", "julia.moreau@vertpart.fr", TicketPriority.Normal, TicketStatus.InProgress, 1.5, evan),

            new("Refund not received", "Customer refunded 10 days ago, nothing on the statement.", "Emma Wilson", "emma.wilson@shopfast.co", TicketPriority.Normal, TicketStatus.Resolved, 120.0, priya),
            new("Production outage", "Complete outage of the order service for 12 minutes.", "Tom Novak", "tom.novak@cadencehq.com", TicketPriority.Critical, TicketStatus.Resolved, 0.5, dana),
            new("Billing address won't save", "Form rejects valid postal codes.", "Rita Gomez", "rita.gomez@casaverde.es", TicketPriority.High, TicketStatus.Resolved, 3.0, evan),
            new("Update docs for API v2", "Documentation still references v1 endpoints.", "Leo Martins", "leo.martins@draftly.app", TicketPriority.Low, TicketStatus.Resolved, 2.0, gina),

            new("Corrupted attachment uploads", "PDFs above 10 MB upload corrupted.", "Nina Berg", "nina.berg@stackline.no", TicketPriority.Critical, TicketStatus.Closed, 1.0, marcus),
            new("Duplicate charges on card", "Customer charged twice for one subscription.", "Samir Patel", "samir.patel@quickcart.in", TicketPriority.Normal, TicketStatus.Closed, 6.0, evan),
            new("CSV import drops rows", "Importer silently skips rows with special characters.", "Kate Miles", "kate.miles@gridbase.ai", TicketPriority.High, TicketStatus.Closed, 3.0, dana),
            new("Password reset email in spam", "Deliverability of reset emails degraded.", "Yuki Tanaka", "yuki.tanaka@hanabi.jp", TicketPriority.Low, TicketStatus.Closed, 10.0, gina),
            new("Account locked after migration", "Legacy users cannot sign in post-migration.", "Femi Adeyemi", "femi.adeyemi@orbita.ng", TicketPriority.Normal, TicketStatus.Closed, 3.0, marcus),
        };

        foreach (var spec in specs)
        {
            var createdAt = spec.AgeHours.HasValue ? now.AddHours(-spec.AgeHours.Value) : now;
            var reference = await referenceGenerator.NextReferenceAsync(createdAt);

            var ticket = Ticket.Create(reference, spec.Title, spec.Description,
                spec.CustomerName, spec.CustomerEmail, spec.Priority, createdAt);

            if (spec.Agent is not null)
                ticket.AssignAgent(spec.Agent, createdAt);

            // Comment arrives during triage - while the ticket is still open (rule 5).
            ticket.AddComment(spec.Agent?.FullName ?? "Intake Bot",
                spec.Reopened
                    ? "Customer reported the issue again after the first fix."
                    : "Initial triage notes added.",
                createdAt.AddHours(spec.AgeHours!.Value * 0.1));

            // Drive the lifecycle through the domain so all timestamps follow the rules.
            if (spec.TargetStatus >= TicketStatus.InProgress)
            {
                ticket.TransitionTo(TicketStatus.InProgress, createdAt.AddHours(spec.AgeHours.Value * 0.2));
                if (spec.TargetStatus >= TicketStatus.Resolved)
                {
                    ticket.TransitionTo(TicketStatus.Resolved, createdAt.AddHours(spec.AgeHours.Value * 0.6));
                    if (spec.TargetStatus == TicketStatus.Closed)
                        ticket.TransitionTo(TicketStatus.Closed, createdAt.AddHours(spec.AgeHours.Value * 0.9));
                }
                else if (spec.Reopened)
                {
                    ticket.TransitionTo(TicketStatus.Resolved, createdAt.AddHours(spec.AgeHours.Value * 0.5));
                    ticket.TransitionTo(TicketStatus.InProgress, createdAt.AddHours(spec.AgeHours.Value * 0.75)); // reopening clears ResolvedAt
                }
            }

            db.Tickets.Add(ticket);
        }

        // Priya "left the team" after working the tickets above. Deactivation does not
        // retroactively unassign her open tickets, so the demo data includes an inactive
        // assignee on New / In Progress / Resolved tickets (rule 3 blocks transitions into
        // In Progress with her as assignee, rule 4 blocks new assignments). The UI marks
        // her as "(inactive)" in the list filter, the Agent column and the detail picker.
        priya.SetActive(false);

        await db.SaveChangesAsync();
    }

    private sealed class TicketSpec(
        string title, string description, string customerName, string customerEmail,
        TicketPriority priority, TicketStatus targetStatus, double? ageHours, Agent? agent,
        bool reopened = false)
    {
        public string Title { get; } = title;
        public string Description { get; } = description;
        public string CustomerName { get; } = customerName;
        public string CustomerEmail { get; } = customerEmail;
        public TicketPriority Priority { get; } = priority;
        public TicketStatus TargetStatus { get; } = targetStatus;
        public double? AgeHours { get; } = ageHours;
        public Agent? Agent { get; } = agent;
        public bool Reopened { get; } = reopened;
    }
}
