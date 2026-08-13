using AiAssistant.ApplicationService.Contract.IService;
using AiAssistant.ApplicationService.Contract.Leads;
using AiAssistant.ApplicationService.Entities;
using AiAssistant.ApplicationService.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AiAssistant.ApplicationService.Services;

public sealed class AiLeadPersistenceService(AiAssistantDbContext dbContext) : IAiLeadPersistenceService
{
    public async Task<bool> SaveAsync(
        ReceiveAiLeadRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.SourceLeadAssignmentId <= 0)
            throw new ArgumentOutOfRangeException(nameof(request), "Source lead assignment ID must be positive.");
        if (string.IsNullOrWhiteSpace(request.PhoneNumber))
            throw new ArgumentException("Phone number is required.", nameof(request));

        if (await dbContext.AiLeads.AnyAsync(
                lead => lead.SourceLeadAssignmentId == request.SourceLeadAssignmentId,
                cancellationToken))
        {
            return false;
        }

        dbContext.AiLeads.Add(new AiLead
        {
            SourceLeadAssignmentId = request.SourceLeadAssignmentId,
            PhoneNumber = request.PhoneNumber.Trim(),
            UserName = string.IsNullOrWhiteSpace(request.UserName) ? null : request.UserName.Trim(),
            ReceivedAt = DateTime.UtcNow
        });

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException)
        {
            dbContext.ChangeTracker.Clear();
            if (await dbContext.AiLeads.AnyAsync(
                    lead => lead.SourceLeadAssignmentId == request.SourceLeadAssignmentId,
                    cancellationToken))
            {
                return false;
            }

            throw;
        }
    }
}
