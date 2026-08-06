using AiAssistant.ApplicationService.Contract.IService;
using Microsoft.AspNetCore.Mvc;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;


namespace AiCall.Controllers;


[ApiController]
[Route("api/dental-realtime")]
public sealed class DentalRealtimeController : ControllerBase
{

    private readonly IOpernAiService _openAiService;

    private readonly ILogger<DentalRealtimeController> _logger;


    public DentalRealtimeController(
        ILogger<DentalRealtimeController> logger,
        IOpernAiService openAiService)
    {
        _logger = logger;
        _openAiService = openAiService;
    }




    [HttpGet("connect")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public async Task Connect(
        CancellationToken cancellationToken)
    {

        WebSocket socket =
            await HttpContext.WebSockets.AcceptWebSocketAsync();



        Func<string, Task> messageHandler =
            async message =>
            {

                await SendToBrowser(
                    socket,
                    new
                    {
                        type = "event",
                        data = message
                    },
                    cancellationToken);

            };




        Func<string, Task> audioHandler = async audio =>
        {
            await SendToBrowser(
                socket,
                new
                {
                    type = "audio",
                    data = audio,
                    format = "pcm16",
                    rate = 24000
                },
                cancellationToken);
        }; ;




        _openAiService.OnMessageReceived += messageHandler;

        _openAiService.OnAudioReceived += audioHandler;




        try
        {

            await _openAiService.ConnectAsync(
                cancellationToken);



            var buffer =
                new byte[8192];



            while (
                socket.State == WebSocketState.Open &&
                !cancellationToken.IsCancellationRequested)
            {


                var result =
                    await socket.ReceiveAsync(
                        new ArraySegment<byte>(buffer),
                        cancellationToken);



                if (result.MessageType ==
                    WebSocketMessageType.Close)
                {
                    break;
                }




                if (result.MessageType ==
                    WebSocketMessageType.Text)
                {

                    string json =
                        Encoding.UTF8.GetString(
                            buffer,
                            0,
                            result.Count);



                    _logger.LogInformation(
                        "Browser => Backend : {Json}",
                        json);



                    var request =
                        JsonSerializer.Deserialize<TestMessage>(
                            json,
                            new JsonSerializerOptions
                            {
                                PropertyNameCaseInsensitive = true
                            });



                    if (request == null ||
                       string.IsNullOrWhiteSpace(request.Message))
                    {
                        continue;
                    }




                    await _openAiService
                        .SendTextMessageAsync(
                            request.Message,
                            cancellationToken);

                }

            }

        }
        finally
        {

            _openAiService.OnMessageReceived -= messageHandler;

            _openAiService.OnAudioReceived -= audioHandler;



            if (socket.State == WebSocketState.Open)
            {

                await socket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "Closed",
                    cancellationToken);

            }

        }

    }





    private async Task SendToBrowser(
        WebSocket socket,
        object data,
        CancellationToken cancellationToken)
    {


        if (socket.State != WebSocketState.Open)
            return;



        string json =
            JsonSerializer.Serialize(data);



        byte[] bytes =
            Encoding.UTF8.GetBytes(json);



        await socket.SendAsync(
            new ArraySegment<byte>(bytes),
            WebSocketMessageType.Text,
            true,
            cancellationToken);

    }




    public sealed class TestMessage
    {
        public string Type { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;

        public DateTime SentAt { get; set; }
    }

}