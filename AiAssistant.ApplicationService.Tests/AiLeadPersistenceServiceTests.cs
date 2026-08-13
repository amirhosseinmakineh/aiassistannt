using AiAssistant.ApplicationService.Contract.Leads;
using AiAssistant.ApplicationService.Persistence;
using AiAssistant.ApplicationService.Services;
using Microsoft.EntityFrameworkCore;

namespace AiAssistant.ApplicationService.Tests;

public sealed class AiLeadPersistenceServiceTests
{
    [Fact]
    public async Task SaveAsync_DoesNotInsertDuplicateSourceAssignment()
    {
        await using var dbContext = CreateDbContext();
        var service = new AiLeadPersistenceService(dbContext);
        var request = new ReceiveAiLeadRequest(42, "+989121234567", "کاربر تست");

        var firstSave = await service.SaveAsync(request);
        var duplicateSave = await service.SaveAsync(request);

        Assert.True(firstSave);
        Assert.False(duplicateSave);
        var lead = Assert.Single(await dbContext.AiLeads.ToListAsync());
        Assert.Equal(42, lead.SourceLeadAssignmentId);
        Assert.Equal(request.PhoneNumber, lead.PhoneNumber);
        Assert.Equal(request.UserName, lead.UserName);
        Assert.True(lead.ReceivedAt <= DateTime.UtcNow);
        Assert.True(lead.ReceivedAt > DateTime.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public void Model_HasUniqueSourceAssignmentIndexAndPhoneLength()
    {
        using var dbContext = CreateDbContext();
        var entity = dbContext.Model.FindEntityType("AiAssistant.ApplicationService.Entities.AiLead")!;

        Assert.True(entity.GetIndexes().Single(index =>
            index.Properties.Single().Name == "SourceLeadAssignmentId").IsUnique);
        Assert.Equal(32, entity.FindProperty("PhoneNumber")!.GetMaxLength());
    }

    private static AiAssistantDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AiAssistantDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AiAssistantDbContext(options);
    }
}
