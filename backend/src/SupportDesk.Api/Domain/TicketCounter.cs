namespace SupportDesk.Api.Domain;

/// <summary>Tracks the last used reference number per calendar year, for TCK-YYYY-NNNN references.</summary>
public class TicketCounter
{
    public int Year { get; private set; }

    public int LastNumber { get; private set; }

    private TicketCounter() { } // EF

    public TicketCounter(int year) => Year = year;

    /// <summary>Caller must hold a lock (serializable transaction) on this row.</summary>
    public int Next() => ++LastNumber;
}
