# OpenAI Realtime Voice Assistant

ASP.NET Core bridges one browser WebSocket to one isolated OpenAI Realtime WebSocket. The backend forwards transcript events as JSON and decoded PCM16/24 kHz/mono audio as binary WebSocket frames.

## Configure and run

The API key is intentionally not stored in configuration. Set it with user-secrets:

```bash
dotnet user-secrets init --project AiCall
dotnet user-secrets set "OpenAi:ApiKey" "YOUR_KEY" --project AiCall
dotnet run --project AiCall
```

Open `https://localhost:7250`, click **اتصال**, and send the pre-filled `سلام از مرورگر` message. Configuration for the `gpt-realtime-2.1` model, endpoint, voice, instructions, and output sample rate is in `AiCall/appsettings.json`.

The page connects automatically when it loads and retries with exponential backoff while the server is unavailable. Once the OpenAI session update is acknowledged, the assistant automatically introduces itself as the office secretary. Browsers may require one click or tap on the page before allowing greeting audio to play because of autoplay policy; transcript streaming does not require that interaction.

To speak instead of typing, click **شروع صحبت**, allow microphone access, speak in Persian, and click **پایان صحبت و ارسال**. The browser resamples microphone input to mono PCM16 at 24 kHz and streams binary frames to the backend. The backend commits the audio to OpenAI and displays the resulting input transcription before the assistant response.

## Browser protocol

Browser to backend:

```json
{ "type": "message", "message": "سلام از مرورگر" }
```

Backend to browser text frames are `status`, `error`, or `transcript.delta` objects. Audio is sent only as binary PCM16 frames; it is never wrapped in JSON.

Microphone audio uses binary browser-to-backend frames followed by:

```json
{ "type": "audio.commit" }
```
