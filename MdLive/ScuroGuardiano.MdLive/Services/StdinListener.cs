using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using ScuroGuardiano.MdLive.Model;

namespace ScuroGuardiano.MdLive.Services;

internal sealed class StdinListener : BackgroundService
{
    private readonly Channel<DocumentContent> _contentChannel;
    private readonly PreviewHub _hub;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<StdinListener> _logger;

    public StdinListener(DocumentContentChannel contentChannel,
        PreviewHub hub,
        IHostApplicationLifetime lifetime,
        ILogger<StdinListener> logger)
    {
        _contentChannel = contentChannel.Channel;
        _hub = hub;
        _lifetime = lifetime;
        _logger = logger;
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        // vim.json.encode nie gwarantuje kolejności kluczy, a dyskryminator "type"
        // domyślnie musi być pierwszą właściwością obiektu
        AllowOutOfOrderMetadataProperties = true
    };

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var reader = new StreamReader(Console.OpenStandardInput(), new UTF8Encoding(false));

        while (!stoppingToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(stoppingToken);

            if (line is null)
            {
                _logger.LogInformation("Stdin closed, shutting down");
                _lifetime.StopApplication();
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
                _logger.LogWarning(ex, "Malformed message from editor");
                continue;
            }

            switch (message)
            {
                case EditorMessage.Content content:
                    // DropOldest, więc TryWrite zawsze się powiedzie
                    _contentChannel.Writer.TryWrite(new DocumentContent(content.Path, content.Text));
                    break;

                case EditorMessage.Cursor cursor:
                    _hub.PublishCursor(cursor.Line);
                    break;
            }
        }
    }

}
