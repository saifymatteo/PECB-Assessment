using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SupportDesk.Api.Domain;

namespace SupportDesk.Api.Infrastructure;

public class TicketCounterConfiguration : IEntityTypeConfiguration<TicketCounter>
{
    public void Configure(EntityTypeBuilder<TicketCounter> builder)
    {
        builder.ToTable("ticket_counters");

        builder.HasKey(c => c.Year);

        builder.Property(c => c.LastNumber).IsRequired();
    }
}
