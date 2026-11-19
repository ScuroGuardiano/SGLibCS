using Microsoft.AspNetCore.HostFiltering;
using ScuroGuardiano.MdLive.ConfigBase;
using ScuroGuardiano.MdLive.Services;
using StarFederation.Datastar.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDatastar();
builder.Services.AddSingleton<DocumentContentChannel>();

builder.Services.AddSingleton<PreviewHub>();
builder.Services.AddSingleton<MdLiveConfigurator>();
builder.Services.AddHostedService<StdinListener>();
builder.Services.AddHostedService<RenderService>();

builder.Services.Configure<HostOptions>(options =>
{
    options.ShutdownTimeout = TimeSpan.FromSeconds(0);
});

// Domyślnie tylko loopback, nadpisywalne przez AllowedHosts z konfiguracji (env, --AllowedHosts=..., appsettings).
// Bez tego framework przy braku wartości w konfiguracji wstawia "*", a appsettings.json jest czytany
// z bieżącego katalogu, więc w dotnet toolu zwykle go nie ma albo jest cudzy.
// Configure wykonuje się przed frameworkowym PostConfigure, który niepustej listy już nie rusza.
string[] defaultAllowedHosts = ["localhost", "127.0.0.1", "[::1]"];
var configuredAllowedHosts = builder.Configuration["AllowedHosts"]
    ?.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
var allowedHosts = configuredAllowedHosts is { Length: > 0 } ? configuredAllowedHosts : defaultAllowedHosts;

builder.Services.Configure<HostFilteringOptions>(options =>
{
    options.AllowedHosts = allowedHosts;
});

var app = builder.Build();

if (allowedHosts.Contains("*"))
{
    app.Logger.LogWarning("AllowedHosts contains '*', Host header filtering is disabled. " +
                          "Any website can reach the preview server through DNS rebinding");
}

app.MapGet("/", () =>
{
    // language=html
    const string html =
        """
        <!DOCTYPE html>
        <html>
        <head>
          <meta charset="UTF-8">
          <title>MdLive waiting...</title>
          <script id="__jajebie" data-ignore-morph type="module" src="/scripts/datastar.js"></script>
          <script id="__noszkurwa" data-ignore-morph src="/scripts/mdlive.js"></script>
        </head>
        <body>
          <div id="__japierdole" data-ignore-morph style="display: none;" data-on:load__window="@get('/preview/sse')"></div>

          <main id="app">
            <h1>Waiting for document data...</h1>
          </main>

        </body>
        </html>
        """;

    return Results.Content(html, "text/html");
});

app.MapGet("/preview/sse", async (PreviewHub hub, IDatastarService datastarService, CancellationToken cancellationToken) =>
{
    using var subscription = hub.Subscribe();
    long sentDocument = 0;
    long sentCursor = 0;

    await foreach (var snapshot in subscription.ReadAllAsync(cancellationToken))
    {
        if (snapshot.DocumentVersion != sentDocument && snapshot.Document is { } document)
        {
            await datastarService.PatchElementsAsync(snapshot.Document.Html, cancellationToken);
            sentDocument = snapshot.DocumentVersion;
        }

        if (snapshot.CursorVersion != sentCursor)
        {
            // language=javascript
            await datastarService.ExecuteScriptAsync($"window.mdlive.scrollTo({snapshot.CursorLine})",
                cancellationToken);

            sentCursor = snapshot.CursorVersion;
        }
    }
});

app.MapGet("/scripts/datastar.js", () => Results.Content(MdLiveScripts.DatastarJs, "text/javascript"));
app.MapGet("/scripts/mdlive.js", () => Results.Content(MdLiveScripts.MdLiveJs, "text/javascript"));

app.Run();