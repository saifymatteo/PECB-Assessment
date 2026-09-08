using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SupportDesk.Api.Domain;

namespace SupportDesk.Api.Infrastructure;

public class CommentConfiguration : IEntityTypeConfiguration<Comment>
{
    public void Configure(EntityTypeBuilder<Comment> builder)
    {
        builder.ToTable("comments");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.AuthorName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(c => c.Body)
            .HasMaxLength(4000)
            .IsRequired();

        builder.Property(c => c.CreatedAt).IsRequired();

        builder.HasIndex(c => new { c.TicketId, c.CreatedAt });
    }
}
