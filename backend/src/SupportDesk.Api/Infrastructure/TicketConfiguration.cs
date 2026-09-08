using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SupportDesk.Api.Domain;

namespace SupportDesk.Api.Infrastructure;

public class TicketConfiguration : IEntityTypeConfiguration<Ticket>
{
    public void Configure(EntityTypeBuilder<Ticket> builder)
    {
        builder.ToTable("tickets");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Reference)
            .HasMaxLength(20)
            .IsRequired();

        builder.HasIndex(t => t.Reference).IsUnique();

        builder.Property(t => t.Title)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(t => t.Description)
            .HasMaxLength(4000)
            .IsRequired();

        builder.Property(t => t.CustomerName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(t => t.CustomerEmail)
            .HasMaxLength(320)
            .IsRequired();

        // Stored as readable strings; ordering by enum value is not a business need.
        builder.Property(t => t.Priority)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(t => t.Status)
            .HasConversion<string>()
            .HasMaxLength(20);

        // Npgsql maps UTC DateTime to timestamptz.
        builder.Property(t => t.CreatedAt).IsRequired();
        builder.Property(t => t.LastModifiedAt).IsRequired();

        builder.HasIndex(t => t.Status);
        builder.HasIndex(t => t.Priority);
        builder.HasIndex(t => t.DueDate);

        // Comments die with their ticket; deleting a ticket removes its thread.
        builder.HasMany(t => t.Comments)
            .WithOne(c => c.Ticket)
            .HasForeignKey(c => c.TicketId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
