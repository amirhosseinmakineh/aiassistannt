namespace AiAssistant.ApplicationService.Contract.IService;

public interface IOpenAiRealtimeSession : IAsyncDisposable
{
    event Func<string, Task>? TranscriptDeltaReceived;
    event Func<ReadOnlyMemory<byte>, Task>? AudioReceived;
    event Func<string, Task>? StatusReceived;
    event Func<string, Task>? ErrorReceived;

    Task ConnectAsync(CancellationToken cancellationToken);
    Task<bool> SendTextMessageAsync(string text, CancellationToken cancellationToken);
}

public interface IOpenAiRealtimeSessionFactory
{
    IOpenAiRealtimeSession CreateSession();
}
