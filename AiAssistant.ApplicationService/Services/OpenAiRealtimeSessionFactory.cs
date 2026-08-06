using AiAssistant.ApplicationService.Contract.IService;
using AiAssistant.ApplicationService.Contract.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AiAssistant.ApplicationService.Services;

public sealed class OpenAiRealtimeSessionFactory(
    IOptions<OpenAiRealtimeOptions> options,
    ILoggerFactory loggerFactory) : IOpenAiRealtimeSessionFactory
{
    public IOpenAiRealtimeSession CreateSession() =>
        new OpenAiRealtimeSession(options.Value, loggerFactory.CreateLogger<OpenAiRealtimeSession>());
}
