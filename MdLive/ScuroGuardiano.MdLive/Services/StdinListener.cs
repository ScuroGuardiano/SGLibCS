using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using ScuroGuardiano.MdLive.Model;

namespace ScuroGuardiano.MdLive.Services;

internal sealed class StdinListener(
    Channel<DocumentContent> contentChannel,
    PreviewHub hub,
    IHostApplicationLifetime lifetime,
    ILogger<StdinListener> logger) : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var reader = new StreamReader(Console.OpenStandardInput(), new UTF8Encoding(false));

        while (!stoppingToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(stoppingToken);

            if (line is null)
            {
                logger.LogInformation("Stdin closed, shutting down");
                lifetime.StopApplication();
                return;
            }

            if (line.Length == 0)
            {
                continue;
            }

            EditorMessage? message;
            try
            {
                message = JsonSerializer.Deserialize<EditorMessage>(line, JsonOptions);
            }
            catch (JsonException ex)
            {
                logger.LogWarning(ex, "Malformed message from editor");
                continue;
            }

            switch (message)
            {
                case { Type: "content", Path: { } path, Text: { } text }:
                    // DropOldest, więc TryWrite zawsze się powiedzie
                    contentChannel.Writer.TryWrite(new DocumentContent(path, text));
                    break;

                case { Type: "cursor", Line: { } cursorLine }:
                    hub.PublishCursor(cursorLine);
                    break;

                default:
                    logger.LogWarning("Unknown message type: {Type}", message?.Type);
                    break;
            }
        }
    }
}
