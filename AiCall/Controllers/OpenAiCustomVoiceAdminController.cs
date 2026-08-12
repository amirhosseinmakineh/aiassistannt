using AiAssistant.ApplicationService.Contract.IService;
using AiAssistant.ApplicationService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AiCall.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/openai/custom-voice")]
public sealed class OpenAiCustomVoiceAdminController(
    IOpenAiCustomVoiceService customVoiceService,
    ILogger<OpenAiCustomVoiceAdminController> logger) : ControllerBase
{
    // The current OpenAI Custom Voices API limit is 10 MiB per uploaded recording.
    private const long MaximumAudioBytes = 10 * 1024 * 1024;
    private static readonly HashSet<string> AllowedExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".mp3", ".wav", ".m4a", ".ogg", ".webm" };
    private static readonly HashSet<string> AllowedContentTypes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "audio/mpeg", "audio/mp3", "audio/wav", "audio/x-wav", "audio/mp4",
            "audio/x-m4a", "audio/ogg", "application/ogg", "audio/webm"
        };

    [HttpPost("consent")]
    [RequestSizeLimit(MaximumAudioBytes + 1024 * 1024)]
    public async Task<IActionResult> CreateConsent(
        [FromForm] CreateConsentRequest request, CancellationToken cancellationToken)
    {
        var validation = Validate(request.Name, request.Audio);
        if (validation is not null) return BadRequest(new { message = validation });

        try
        {
            await using var stream = request.Audio.OpenReadStream();
            var consentId = await customVoiceService.CreateConsentAsync(
                request.Name.Trim(), stream, request.Audio.FileName, cancellationToken);
            logger.LogInformation("Custom voice consent uploaded. ConsentId: {ConsentId}", consentId);
            return Ok(new { consentId });
        }
        catch (OpenAiCustomVoiceException exception) { return MapOpenAiError(exception); }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Uploading the OpenAI custom voice consent failed");
            return OpenAiUnavailable();
        }
    }

    [HttpPost]
    [RequestSizeLimit(MaximumAudioBytes + 1024 * 1024)]
    public async Task<IActionResult> CreateVoice(
        [FromForm] CreateCustomVoiceRequest request, CancellationToken cancellationToken)
    {
        var validation = Validate(request.Name, request.Audio);
        if (validation is not null) return BadRequest(new { message = validation });
        if (string.IsNullOrWhiteSpace(request.ConsentId))
            return BadRequest(new { message = "شناسه رضایت برای ساخت صدای شخصی الزامی است." });

        try
        {
            await using var stream = request.Audio.OpenReadStream();
            var voiceId = await customVoiceService.CreateVoiceAsync(
                request.Name.Trim(), request.ConsentId.Trim(), stream, request.Audio.FileName, cancellationToken);
            logger.LogInformation("Custom voice created. VoiceId: {VoiceId}", voiceId);
            return Ok(new { voiceId });
        }
        catch (OpenAiCustomVoiceException exception) { return MapOpenAiError(exception); }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Creating the OpenAI custom voice failed");
            return OpenAiUnavailable();
        }
    }

    private IActionResult MapOpenAiError(OpenAiCustomVoiceException exception)
    {
        if (exception.AccessDenied)
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "قابلیت Custom Voice برای این حساب OpenAI فعال نیست." });

        return OpenAiUnavailable();
    }

    private ObjectResult OpenAiUnavailable() => StatusCode(StatusCodes.Status502BadGateway,
        new { message = "در ارتباط با سرویس OpenAI برای ساخت صدای شخصی خطایی رخ داد." });

    private static string? Validate(string? name, IFormFile? audio)
    {
        if (string.IsNullOrWhiteSpace(name)) return "نام صدا الزامی است.";
        if (audio is null) return "فایل صوتی الزامی است.";
        if (audio.Length == 0) return "فایل صوتی خالی است.";
        if (audio.Length > MaximumAudioBytes) return "حجم فایل صوتی نباید بیشتر از ۱۰ مگابایت باشد.";
        if (!AllowedExtensions.Contains(Path.GetExtension(audio.FileName)) ||
            !AllowedContentTypes.Contains(audio.ContentType))
            return "فرمت فایل صوتی معتبر نیست. فرمت‌های مجاز: MP3، WAV، M4A، OGG و WEBM.";
        return null;
    }
}

public sealed class CreateConsentRequest
{
    public string Name { get; set; } = string.Empty;
    public IFormFile Audio { get; set; } = default!;
}

public sealed class CreateCustomVoiceRequest
{
    public string Name { get; set; } = string.Empty;
    public string ConsentId { get; set; } = string.Empty;
    public IFormFile Audio { get; set; } = default!;
}
