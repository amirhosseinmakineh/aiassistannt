using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using AiAssistant.ApplicationService.Contract.IService;
using AiAssistant.ApplicationService.Contract.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AiAssistant.ApplicationService.Services;

public sealed class OpenAiCustomVoiceService(
    HttpClient httpClient,
    IOptions<OpenAiRealtimeOptions> options,
    ILogger<OpenAiCustomVoiceService> logger) : IOpenAiCustomVoiceService
{
    public Task<string> CreateConsentAsync(
        string name, Stream audioStream, string fileName, CancellationToken cancellationToken = default) =>
        PostAudioAsync("audio/voice_consents", name, "recording", audioStream, fileName, null, cancellationToken);

    public Task<string> CreateVoiceAsync(
        string name, string consentId, Stream audioStream, string fileName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(consentId))
            throw new ArgumentException("Consent ID is required.", nameof(consentId));

        return PostAudioAsync(
            "audio/voices", name, "audio_sample", audioStream, fileName,
            new KeyValuePair<string, string>("consent", consentId.Trim()), cancellationToken);
    }

    private async Task<string> PostAudioAsync(
        string path, string name, string audioField, Stream audioStream, string fileName,
        KeyValuePair<string, string>? additionalField, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.Value.ApiKey))
            throw new InvalidOperationException("OpenAI:ApiKey is not configured.");

        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(name.Trim()), "name");
        if (additionalField is { } field)
            form.Add(new StringContent(field.Value), field.Key);

        var audio = new StreamContent(audioStream);
        audio.Headers.ContentType = new MediaTypeHeaderValue(GetMediaType(fileName));
        form.Add(audio, audioField, Path.GetFileName(fileName));

        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = form };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.Value.ApiKey);
        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var message = ReadErrorMessage(body) ?? "OpenAI rejected the custom voice request.";
            var accessDenied = response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized;
            logger.LogError(
                "OpenAI custom voice creation failed. StatusCode: {StatusCode}, Message: {Message}",
                (int)response.StatusCode, message);
            throw new OpenAiCustomVoiceException((int)response.StatusCode, accessDenied, message);
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.TryGetProperty("id", out var id) &&
                !string.IsNullOrWhiteSpace(id.GetString()))
                return id.GetString()!;
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "OpenAI custom voice response was not valid JSON");
        }

        throw new OpenAiCustomVoiceException((int)response.StatusCode, false, "OpenAI response did not contain an ID.");
    }

    private static string? ReadErrorMessage(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            return document.RootElement.TryGetProperty("error", out var error) &&
                   error.TryGetProperty("message", out var message)
                ? message.GetString()
                : null;
        }
        catch (JsonException) { return null; }
    }

    private static string GetMediaType(string fileName) => Path.GetExtension(fileName).ToLowerInvariant() switch
    {
        ".mp3" => "audio/mpeg",
        ".wav" => "audio/wav",
        ".m4a" => "audio/mp4",
        ".ogg" => "audio/ogg",
        ".webm" => "audio/webm",
        _ => "application/octet-stream"
    };
}
