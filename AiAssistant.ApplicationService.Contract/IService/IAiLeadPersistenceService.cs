using AiAssistant.ApplicationService.Contract.Leads;

namespace AiAssistant.ApplicationService.Contract.IService;

public interface IAiLeadPersistenceService
{
    Task<bool> SaveAsync(ReceiveAiLeadRequest request, CancellationToken cancellationToken = default);
}
