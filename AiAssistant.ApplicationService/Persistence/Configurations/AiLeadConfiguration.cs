using AiAssistant.ApplicationService.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiAssistant.ApplicationService.Persistence.Configurations;

public sealed class AiLeadConfiguration : IEntityTypeConfiguration<AiLead>
{
    public void Configure(EntityTypeBuilder<AiLead> builder)
    {
        builder.ToTable("AiLeads");
        builder.HasKey(lead => lead.Id);
        builder.Property(lead => lead.PhoneNumber).HasMaxLength(32).IsRequired();
        builder.Property(lead => lead.UserName).HasMaxLength(200);
        builder.Property(lead => lead.ReportDescription).HasMaxLength(2000);
        builder.HasIndex(lead => lead.SourceLeadAssignmentId).IsUnique();
    }
}
