namespace AiAssistant.ApplicationService.Contract.IService
{
    public interface IOpernAiService
    {
        Task ConnectAsync(CancellationToken cancellationToken);
        Task SendEventAsync(string json, CancellationToken cancellationToken);
        Task UpdateSessionAsync(CancellationToken cancellationToken);
        event Func<string, Task>? OnMessageReceived;
        event Func<string, Task>? OnAudioReceived;
        Task SendTextMessageAsync(
    string text,
    CancellationToken cancellationToken);
    }

}
