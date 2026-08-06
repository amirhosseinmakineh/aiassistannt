using System.Buffers;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using AiAssistant.ApplicationService.Contract.IService;
using AiAssistant.ApplicationService.Contract.Options;
using Microsoft.Extensions.Logging;

namespace AiAssistant.ApplicationService.Services;

/// <summary>A single, isolated OpenAI Realtime connection. Instances must never be shared by browser clients.</summary>
public sealed class OpenAiRealtimeSession : IOpenAiRealtimeSession
{
    private readonly OpenAiRealtimeOptions _options;
    private readonly ILogger<OpenAiRealtimeSession> _logger;
    private readonly ClientWebSocket _socket = new();
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private readonly CancellationTokenSource _lifetime = new();
    private Task? _receiveTask;
    private int _responseInProgress;
    private int _disposed;

    public event Func<string, Task>? TranscriptDeltaReceived;
    public event Func<ReadOnlyMemory<byte>, Task>? AudioReceived;
    public event Func<string, Task>? StatusReceived;
    public event Func<string, Task>? ErrorReceived;

    public OpenAiRealtimeSession(OpenAiRealtimeOptions options, ILogger<OpenAiRealtimeSession> logger)
    {
        _options = options;
        _logger = logger;
    }

    public async Task ConnectAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
            throw new InvalidOperationException("OpenAI:ApiKey is not configured. Use user-secrets or an environment variable.");
        if (!Uri.TryCreate(_options.RealtimeUrl, UriKind.Absolute, out var endpoint))
            throw new InvalidOperationException("OpenAI:RealtimeUrl is not a valid absolute URL.");

        var uriBuilder = new UriBuilder(endpoint);
        var model = Uri.EscapeDataString(_options.Model);
        uriBuilder.Query = string.IsNullOrEmpty(uriBuilder.Query)
            ? $"model={model}"
            : $"{uriBuilder.Query.TrimStart('?')}&model={model}";

        _socket.Options.SetRequestHeader("Authorization", $"Bearer {_options.ApiKey}");
        _logger.LogInformation("Connecting to OpenAI Realtime");
        await _socket.ConnectAsync(uriBuilder.Uri, cancellationToken);
        _logger.LogInformation("Connected to OpenAI Realtime");

        var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
        _receiveTask = ReceiveLoopAsync(linked);
        await SendSessionUpdateAsync(cancellationToken);
        _logger.LogInformation("Session update sent");
    }

    public async Task<bool> SendTextMessageAsync(string text, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;

        // Reserve the response before either send so two browser messages cannot race.
        if (Interlocked.CompareExchange(ref _responseInProgress, 1, 0) != 0)
        {
            await InvokeAsync(StatusReceived, "A response is already in progress. Please wait.");
            return false;
        }

        try
        {
            _logger.LogInformation("Sending message to OpenAI");
            await SendJsonAsync(new
            {
                type = "conversation.item.create",
                item = new
                {
                    type = "message",
                    role = "user",
                    content = new[] { new { type = "input_text", text } }
                }
            }, cancellationToken);
            await SendJsonAsync(new
            {
                type = "response.create",
                response = new { output_modalities = new[] { "audio" } }
            }, cancellationToken);
            await InvokeAsync(StatusReceived, "Message sent; response requested.");
            return true;
        }
        catch
        {
            Interlocked.Exchange(ref _responseInProgress, 0);
            throw;
        }
    }

    private Task SendSessionUpdateAsync(CancellationToken cancellationToken) => SendJsonAsync(new
    {
        type = "session.update",
        session = new
        {
            type = "realtime",
            instructions = _options.Instructions,
            audio = new
            {
                output = new
                {
                    format = new { type = "audio/pcm", rate = _options.OutputSampleRate },
                    voice = _options.Voice
                }
            }
        }
    }, cancellationToken);

    private async Task SendJsonAsync(object payload, CancellationToken cancellationToken)
    {
        if (_socket.State != WebSocketState.Open)
            throw new InvalidOperationException("OpenAI Realtime connection is not open.");

        var bytes = JsonSerializer.SerializeToUtf8Bytes(payload);
        await _sendLock.WaitAsync(cancellationToken);
        try
        {
            await _socket.SendAsync(bytes, WebSocketMessageType.Text, true, cancellationToken);
        }
        finally { _sendLock.Release(); }
    }

    private async Task ReceiveLoopAsync(CancellationTokenSource linked)
    {
        using (linked)
        {
            var token = linked.Token;
            var rented = ArrayPool<byte>.Shared.Rent(16 * 1024);
            try
            {
                while (!token.IsCancellationRequested && _socket.State == WebSocketState.Open)
                {
                    using var message = new MemoryStream();
                    WebSocketReceiveResult result;
                    do
                    {
                        result = await _socket.ReceiveAsync(new ArraySegment<byte>(rented), token);
                        if (result.MessageType == WebSocketMessageType.Close) return;
                        message.Write(rented, 0, result.Count);
                    } while (!result.EndOfMessage);

                    if (result.MessageType == WebSocketMessageType.Text)
                        await HandleEventAsync(message.ToArray());
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { }
            catch (WebSocketException ex) when (_lifetime.IsCancellationRequested)
            {
                _logger.LogDebug(ex, "OpenAI Realtime socket closed during disposal");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OpenAI Realtime receive loop failed");
                Interlocked.Exchange(ref _responseInProgress, 0);
                await InvokeAsync(ErrorReceived, "The OpenAI Realtime connection ended unexpectedly.");
            }
            finally { ArrayPool<byte>.Shared.Return(rented); }
        }
    }

    private async Task HandleEventAsync(byte[] utf8Json)
    {
        using var document = JsonDocument.Parse(utf8Json);
        var root = document.RootElement;
        if (!root.TryGetProperty("type", out var typeElement)) return;
        var type = typeElement.GetString() ?? string.Empty;
        _logger.LogInformation("OpenAI event: {EventType}", type);

        switch (type)
        {
            case "session.created":
                await InvokeAsync(StatusReceived, "OpenAI session created.");
                break;
            case "session.updated":
                await InvokeAsync(StatusReceived, "OpenAI session updated; ready.");
                break;
            case "conversation.item.added":
                await InvokeAsync(StatusReceived, "Conversation item accepted.");
                break;
            case "response.created":
                Interlocked.Exchange(ref _responseInProgress, 1);
                await InvokeAsync(StatusReceived, "AI response started.");
                break;
            case "response.output_item.added":
            case "response.content_part.added":
                break;
            case "response.output_audio_transcript.delta":
                if (root.TryGetProperty("delta", out var transcriptDelta))
                    await InvokeAsync(TranscriptDeltaReceived, transcriptDelta.GetString() ?? string.Empty);
                break;
            case "response.output_audio_transcript.done":
                await InvokeAsync(StatusReceived, "Transcript completed.");
                break;
            case "response.output_audio.delta":
                if (root.TryGetProperty("delta", out var audioDelta) && audioDelta.GetString() is { Length: > 0 } base64)
                {
                    try { await InvokeAsync(AudioReceived, Convert.FromBase64String(base64)); }
                    catch (FormatException) { await InvokeAsync(ErrorReceived, "OpenAI returned an invalid audio chunk."); }
                }
                break;
            case "response.output_audio.done":
                await InvokeAsync(StatusReceived, "Audio completed.");
                break;
            case "response.done":
                Interlocked.Exchange(ref _responseInProgress, 0);
                await InvokeAsync(StatusReceived, "AI response completed.");
                break;
            case "error":
                Interlocked.Exchange(ref _responseInProgress, 0);
                var error = root.TryGetProperty("error", out var errorElement) && errorElement.TryGetProperty("message", out var message)
                    ? message.GetString() : "OpenAI returned an error.";
                _logger.LogWarning("OpenAI Realtime error event received: {Message}", error);
                await InvokeAsync(ErrorReceived, error ?? "OpenAI returned an error.");
                break;
        }
    }

    private static async Task InvokeAsync<T>(Func<T, Task>? handler, T value)
    {
        if (handler is null) return;
        foreach (Func<T, Task> subscriber in handler.GetInvocationList()) await subscriber(value);
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _lifetime.Cancel();
        if (_socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
        {
            try { await _socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Session ended", CancellationToken.None); }
            catch (WebSocketException) { }
        }
        if (_receiveTask is not null)
        {
            try { await _receiveTask; }
            catch (OperationCanceledException) { }
        }
        _socket.Dispose();
        _sendLock.Dispose();
        _lifetime.Dispose();
    }
}
