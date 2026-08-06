namespace AiAssistant.ApplicationService.Contract.Options
{
    public sealed class OpenAiRealtimeOptions
    {
        public string ApiKey { get; set; } = string.Empty;

        public string RealtimeUrl { get; set; } = string.Empty;

        public string Model { get; set; } = string.Empty;

        public string Voice { get; set; } = string.Empty;
        public string Instructions { get; set; } = string.Empty;
        public int OutputSampleRate { get; set; } = 24000;
    }
}
