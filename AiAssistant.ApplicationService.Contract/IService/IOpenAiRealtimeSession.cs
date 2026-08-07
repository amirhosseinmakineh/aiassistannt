namespace AiAssistant.ApplicationService.Contract.IService;

public interface IOpenAiRealtimeSession : IAsyncDisposable
{
    event Func<string, Task>? TranscriptDeltaReceived;
    event Func<string, Task>? InputTranscriptDeltaReceived;
    event Func<string, Task>? InputTranscriptCompleted;
    event Func<ReadOnlyMemory<byte>, Task>? AudioReceived;
    event Func<string, Task>? StatusReceived;
    event Func<string, Task>? ErrorReceived;

    Task ConnectAsync(CancellationToken cancellationToken);
    Task<bool> StartGreetingAsync(CancellationToken cancellationToken);
    Task<bool> SendTextMessageAsync(string text, CancellationToken cancellationToken);
    Task SendAudioAsync(ReadOnlyMemory<byte> pcm16Audio, CancellationToken cancellationToken);
    Task<bool> CommitAudioAsync(CancellationToken cancellationToken);
}

public interface IOpenAiRealtimeSessionFactory
{
    IOpenAiRealtimeSession CreateSession();
}
