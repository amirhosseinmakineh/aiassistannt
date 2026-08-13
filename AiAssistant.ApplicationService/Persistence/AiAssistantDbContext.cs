using AiAssistant.ApplicationService.Entities;
using Microsoft.EntityFrameworkCore;

namespace AiAssistant.ApplicationService.Persistence;

public sealed class AiAssistantDbContext(DbContextOptions<AiAssistantDbContext> options) : DbContext(options)
{
    public DbSet<AiLead> AiLeads => Set<AiLead>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AiAssistantDbContext).Assembly);
    }
}
