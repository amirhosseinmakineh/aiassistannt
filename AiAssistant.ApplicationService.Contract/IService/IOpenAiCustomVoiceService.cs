namespace AiAssistant.ApplicationService.Contract.IService;

public interface IOpenAiCustomVoiceService
{
    Task<string> CreateConsentAsync(
        string name,
        Stream audioStream,
        string fileName,
        CancellationToken cancellationToken = default);

    Task<string> CreateVoiceAsync(
        string name,
        string consentId,
        Stream audioStream,
        string fileName,
        CancellationToken cancellationToken = default);
}
