namespace AiAssistant.ApplicationService.Services;

public sealed class OpenAiCustomVoiceException : Exception
{
    public OpenAiCustomVoiceException(int statusCode, bool accessDenied, string message)
        : base(message)
    {
        StatusCode = statusCode;
        AccessDenied = accessDenied;
    }

    public int StatusCode { get; }
    public bool AccessDenied { get; }
}
