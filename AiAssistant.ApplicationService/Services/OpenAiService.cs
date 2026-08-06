using AiAssistant.ApplicationService.Contract.IService;
using AiAssistant.ApplicationService.Contract.Options;
using Microsoft.Extensions.Options;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace AiAssistant.ApplicationService.Services;

public class OpenAiService : IOpernAiService
{
    private readonly OpenAiRealtimeOptions _options;

    private ClientWebSocket? _webSocket;
    private bool _responseInProgress;

    public event Func<string, Task>? OnMessageReceived;
    public event Func<string, Task>? OnAudioReceived;

    public OpenAiService(
        IOptions<OpenAiRealtimeOptions> options)
    {
        _options = options.Value;
    }


    public async Task ConnectAsync(
        CancellationToken cancellationToken)
    {
        _webSocket = new ClientWebSocket();


        _webSocket.Options.SetRequestHeader(
            "Authorization",
            $"Bearer {_options.ApiKey}");


        var url =
            $"{_options.RealtimeUrl}?model={_options.Model}";


        await _webSocket.ConnectAsync(
            new Uri(url),
            cancellationToken);


        Console.WriteLine(
            "Connected to OpenAI Realtime");


        _= ReceiveLoop(
            cancellationToken);
    }


    private async Task ReceiveLoop(
        CancellationToken cancellationToken)
    {
        var buffer = new byte[8192];


        while (
            _webSocket != null &&
            _webSocket.State == WebSocketState.Open &&
            !cancellationToken.IsCancellationRequested)
        {
            var result =
                await _webSocket.ReceiveAsync(
                    new ArraySegment<byte>(buffer),
                    cancellationToken);


            if (result.MessageType ==
                WebSocketMessageType.Close)
            {
                await CloseSocket(
                    cancellationToken);

                break;
            }


            if (result.MessageType ==
                WebSocketMessageType.Text)
            {
                var json =
                    Encoding.UTF8.GetString(
                        buffer,
                        0,
                        result.Count);


                Console.WriteLine(
                    "OpenAI Event:");

                Console.WriteLine(json);


                // پردازش داخلی Eventهای OpenAI
                await HandleOpenAiEvent(
                    json,
                    cancellationToken);


                // ارسال Event به مصرف‌کننده (Controller)
                if (OnMessageReceived != null)
                {
                    await OnMessageReceived(json);
                }
            }
        }
    }
    private async Task HandleOpenAiEvent(
        string json,
        CancellationToken cancellationToken)
    {
        using JsonDocument document =
            JsonDocument.Parse(json);


        if (!document.RootElement.TryGetProperty(
                "type",
                out JsonElement typeElement))
        {
            return;
        }


        string? type =
            typeElement.GetString();

        Console.WriteLine($"OPENAI EVENT TYPE: {type}");
        switch (type)
        {
            case "session.created":

                Console.WriteLine(
                    "Session created");


                await UpdateSessionAsync(
                    cancellationToken);

                break;



            case "session.updated":

                Console.WriteLine(
                    "Session updated");

                break;



            case "response.created":

                _responseInProgress = true;


                Console.WriteLine(
                    "Response started");

                break;



            case "response.output_item.added":

                Console.WriteLine(
                    "Assistant output item added");

                break;



            case "response.output_audio_transcript.delta":

                if (document.RootElement.TryGetProperty(
                        "delta",
                        out JsonElement transcriptElement))
                {
                    string? transcript =
                        transcriptElement.GetString();


                    if (!string.IsNullOrWhiteSpace(transcript))
                    {
                        Console.WriteLine(
                            $"AI Transcript: {transcript}");
                    }
                }

                break;



            case "response.output_audio.delta":

                if (document.RootElement.TryGetProperty(
                        "delta",
                        out JsonElement audioElement))
                {
                    string? audio =
                        audioElement.GetString();


                    if (!string.IsNullOrWhiteSpace(audio))
                    {
                        Console.WriteLine(
                            $"Audio chunk received: {audio.Length}");


                        if (OnAudioReceived != null)
                        {
                            await OnAudioReceived(audio);
                        }
                    }
                }

                break;



            case "response.done":

                _responseInProgress = false;


                Console.WriteLine(
                    "Response completed");

                break;



            case "error":

                Console.WriteLine(
                    "OpenAI Error:");

                Console.WriteLine(json);

                break;
        }
    }
    public async Task UpdateSessionAsync(
        CancellationToken cancellationToken)
    {
        var sessionUpdate = new
        {
            type = "session.update",

            session = new
            {
                type = "realtime",

                instructions =
                    _options.Instructions,

                audio = new
                {
                    input = new
                    {
                        format = new
                        {
                            type = "audio/pcm",
                            rate = 24000
                        },

                        turn_detection = new
                        {
                            type = "semantic_vad",
                            create_response = true,
                            interrupt_response = true
                        }
                    },

                    output = new
                    {
                        format = new
                        {
                            type = "audio/pcm",
                            rate = 24000
                        },

                        voice = _options.Voice
                    }
                }
            }
        };


        string json =
            JsonSerializer.Serialize(sessionUpdate);


        await SendEventAsync(
            json,
            cancellationToken);
    }

    public async Task SendEventAsync(
        string json,
        CancellationToken cancellationToken)
    {
        if (_webSocket == null ||
            _webSocket.State != WebSocketState.Open)
        {
            throw new InvalidOperationException(
                "OpenAI WebSocket is not connected.");
        }


        byte[] bytes =
            Encoding.UTF8.GetBytes(json);


        await _webSocket.SendAsync(
            new ArraySegment<byte>(bytes),
            WebSocketMessageType.Text,
            true,
            cancellationToken);
    }


    private async Task CloseSocket(
        CancellationToken cancellationToken)
    {
        if (_webSocket == null)
            return;


        await _webSocket.CloseAsync(
            WebSocketCloseStatus.NormalClosure,
            "Closed",
            cancellationToken);
    }
    public async Task SendTextMessageAsync(
        string text,
        CancellationToken cancellationToken)
    {
        Console.WriteLine(
            "SendTextMessageAsync called");

        Console.WriteLine(
            $"User text: {text}");


        if (_responseInProgress)
        {
            Console.WriteLine(
                "Response already running");

            return;
        }


        var userMessage = new
        {
            type = "conversation.item.create",

            item = new
            {
                type = "message",

                role = "user",

                content = new[]
                {
                new
                {
                    type = "input_text",
                    text = text
                }
            }
            }
        };


        string messageJson =
            JsonSerializer.Serialize(userMessage);


        Console.WriteLine(
            "Sending conversation.item.create");


        await SendEventAsync(
            messageJson,
            cancellationToken);



        var responseCreate = new
        {
            type = "response.create",

            response = new
            {
                output_modalities = new[]
                {
                "audio"
            }
            }
        };


        string responseJson =
            JsonSerializer.Serialize(responseCreate);


        Console.WriteLine(
            "Sending response.create");


        await SendEventAsync(
            responseJson,
            cancellationToken);
    }
}