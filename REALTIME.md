# OpenAI Realtime Voice Assistant

ASP.NET Core bridges one browser WebSocket to one isolated OpenAI Realtime WebSocket. The backend forwards transcript events as JSON and decoded PCM16/24 kHz/mono audio as binary WebSocket frames.

## Configure and run

The API key is intentionally not stored in configuration. Set it with user-secrets:

```bash
dotnet user-secrets init --project AiCall
dotnet user-secrets set "OpenAi:ApiKey" "YOUR_KEY" --project AiCall
dotnet run --project AiCall
```

Open the printed HTTP or HTTPS URL, click **اتصال**, and send the pre-filled `سلام از مرورگر` message. Configuration for the model, endpoint, voice, instructions, and output sample rate is in `AiCall/appsettings.json`.

## Browser protocol

Browser to backend:

```json
{ "type": "message", "message": "سلام از مرورگر" }
```

Backend to browser text frames are `status`, `error`, or `transcript.delta` objects. Audio is sent only as binary PCM16 frames; it is never wrapped in JSON.
