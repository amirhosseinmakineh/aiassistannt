using System.Buffers;
using System.Net.WebSockets;
using System.Text.Json;
using AiAssistant.ApplicationService.Contract.IService;
using Microsoft.AspNetCore.Mvc;

namespace AiCall.Controllers;

[ApiController]
[Route("api/dental-realtime")]
public sealed class DentalRealtimeController(
    IOpenAiRealtimeSessionFactory sessionFactory,
    ILogger<DentalRealtimeController> logger) : ControllerBase
{
    [HttpGet("connect")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public async Task Connect(CancellationToken requestAborted)
    {
        if (!HttpContext.WebSockets.IsWebSocketRequest)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await HttpContext.Response.WriteAsync("A WebSocket upgrade request is required.", requestAborted);
            return;
        }

        logger.LogInformation("Browser websocket request received");
        using var browser = await HttpContext.WebSockets.AcceptWebSocketAsync();
        logger.LogInformation("Browser websocket accepted");
        await using var openAi = sessionFactory.CreateSession();
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(requestAborted);
        using var browserSendLock = new SemaphoreSlim(1, 1);

        Task SendJsonAsync(object value) => SendBrowserAsync(
            browser, JsonSerializer.SerializeToUtf8Bytes(value), WebSocketMessageType.Text,
            browserSendLock, lifetime.Token);

        openAi.TranscriptDeltaReceived += delta => SendJsonAsync(new { type = "transcript.delta", delta });
        openAi.AudioReceived += audio => SendBrowserAsync(
            browser, audio, WebSocketMessageType.Binary, browserSendLock, lifetime.Token);
        openAi.StatusReceived += message => SendJsonAsync(new { type = "status", message });
        openAi.ErrorReceived += message => SendJsonAsync(new { type = "error", message });

        try
        {
            await SendJsonAsync(new { type = "status", message = "WebSocket connected; creating OpenAI session." });
            await openAi.ConnectAsync(lifetime.Token);
            logger.LogInformation("OpenAI session connected");
            await ReceiveBrowserMessagesAsync(browser, openAi, SendJsonAsync, logger, lifetime.Token);
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        catch (Exception ex)
        {
            logger.LogError(ex, "Realtime browser session failed");
            if (browser.State == WebSocketState.Open)
            {
                try { await SendJsonAsync(new { type = "error", message = ex.Message }); }
                catch (Exception sendException) when (sendException is WebSocketException or OperationCanceledException) { }
            }
        }
        finally
        {
            lifetime.Cancel();
            if (browser.State is WebSocketState.Open or WebSocketState.CloseReceived)
            {
                try { await browser.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Session closed", CancellationToken.None); }
                catch (WebSocketException) { }
            }
        }
    }

    private static async Task ReceiveBrowserMessagesAsync(
        WebSocket browser,
        IOpenAiRealtimeSession openAi,
        Func<object, Task> sendJsonAsync,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(8 * 1024);
        try
        {
            while (browser.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                using var message = new MemoryStream();
                WebSocketReceiveResult result;
                do
                {
                    result = await browser.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                    if (result.MessageType == WebSocketMessageType.Close) return;
                    message.Write(buffer, 0, result.Count);
                    if (message.Length > 64 * 1024)
                    {
                        await sendJsonAsync(new { type = "error", message = "Browser message exceeds 64 KB." });
                        return;
                    }
                } while (!result.EndOfMessage);

                if (result.MessageType != WebSocketMessageType.Text)
                {
                    await sendJsonAsync(new { type = "error", message = "Only text request frames are supported." });
                    continue;
                }

                BrowserMessage? request;
                try { request = JsonSerializer.Deserialize<BrowserMessage>(message.ToArray(), JsonOptions); }
                catch (JsonException)
                {
                    await sendJsonAsync(new { type = "error", message = "Invalid JSON message." });
                    continue;
                }

                if (request is null || request.Type != "message" || string.IsNullOrWhiteSpace(request.Message))
                {
                    await sendJsonAsync(new { type = "error", message = "Send { type: 'message', message: '...' }." });
                    continue;
                }

                var browserMessage = request.Message.Trim();
                logger.LogInformation("Browser message received: {Message}", browserMessage);
                await openAi.SendTextMessageAsync(browserMessage, cancellationToken);
            }
        }
        finally { ArrayPool<byte>.Shared.Return(buffer); }
    }

    private static async Task SendBrowserAsync(
        WebSocket socket,
        ReadOnlyMemory<byte> payload,
        WebSocketMessageType messageType,
        SemaphoreSlim sendLock,
        CancellationToken cancellationToken)
    {
        await sendLock.WaitAsync(cancellationToken);
        try
        {
            if (socket.State == WebSocketState.Open)
                await socket.SendAsync(payload, messageType, true, cancellationToken);
        }
        finally { sendLock.Release(); }
    }

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private sealed record BrowserMessage(string Type, string Message);
}
