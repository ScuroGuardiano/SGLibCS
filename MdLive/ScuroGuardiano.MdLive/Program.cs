using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.HostFiltering;
using Microsoft.Extensions.Options;
using ScuroGuardiano.MdLive.Commands;
using ScuroGuardiano.MdLive.ConfigBase;
using ScuroGuardiano.MdLive.Services;
using StarFederation.Datastar.DependencyInjection;

switch (args)
{
    case ["init", .. var initArgs]:
        return await InitCommand.RunAsync(initArgs);

    case ["serve", ..]:
        break;

    case ["help" or "--help" or "-h", ..]:
        PrintUsage(Console.Out);
        return 0;

    default:
        PrintUsage(Console.Error);
        return 1;
}

// Wszystko poniżej to "mdlive serve". Reszta argumentów idzie do konfiguracji, np. --AllowedHosts=...
var builder = WebApplication.CreateBuilder(args[1..]);

// Tylko loopback i losowy wolny port. Celowo nie do nadpisania z konfiguracji: wystawienie podglądu
// poza localhost wyłączyłoby całą ochronę opartą na tym, że serwer jest osiągalny tylko lokalnie.
builder.WebHost.UseUrls("http://127.0.0.1:0");

// stdout należy do protokołu z edytorem, więc wszystkie logi idą na stderr
builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);

builder.Services.AddDatastar();
builder.Services.AddSingleton<DocumentContentChannel>();

builder.Services.AddSingleton<PreviewHub>();
builder.Services.AddSingleton<MdLiveConfigurator>();
builder.Services.AddHostedService<StdinListener>();
builder.Services.AddHostedService<RenderService>();
builder.Services.AddSingleton<PreviewAccessToken>();

// Klucze szyfrujące ciasteczka tylko w pamięci. Domyślnie Data Protection zapisuje je na dysk
// (~/.aspnet/DataProtection-Keys) i sesja przeżyłaby restart serwera; tak każdy start unieważnia stare ciasteczka.
// UseEphemeralDataProtectionProvider nie wystarcza: podmienia tylko dostawcę, a menedżer kluczy
// i tak tworzy przy starcie nieużywany klucz na dysku.
builder.Services.AddDataProtection();
builder.Services.Configure<KeyManagementOptions>(options => options.XmlRepository = new InMemoryXmlRepository());

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        // Ciasteczka nie są rozdzielane po portach, więc dwa podglądy na 127.0.0.1 (np. z dwóch neovimów)
        // nadpisywałyby sobie wspólne ciasteczko. Port nie jest jeszcze znany, więc losowy sufiks na każdy start.
        options.Cookie.Name = $"mdlive_{Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(4))}";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        // Podgląd działa po HTTP na loopbacku
        options.Cookie.SecurePolicy = CookieSecurePolicy.None;

        // Podgląd to nie aplikacja z formularzem logowania: zamiast przekierowań zwykłe kody statusu
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    });

// Każdy endpoint wymaga zalogowania, wyjątki są oznaczone AllowAnonymous
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

builder.Services.Configure<HostOptions>(options =>
{
    options.ShutdownTimeout = TimeSpan.FromSeconds(0);
});

// Domyślnie tylko loopback, nadpisywalne przez AllowedHosts z konfiguracji (env, --AllowedHosts=..., appsettings).
// appsettings.json jest czytany z bieżącego katalogu, więc w dotnet toolu zwykle go nie ma albo jest cudzy.
// Frameworkowe PostConfigure (zarejestrowane w CreateBuilder, więc wykonuje się przed naszym) przy pustym
// AllowedHosts w konfiguracji wstawia "*". Po samych opcjach nie da się odróżnić tego od jawnego "*",
// dlatego sprawdzamy konfigurację, a nie listę.
string[] defaultAllowedHosts = ["localhost", "127.0.0.1", "[::1]"];

builder.Services.AddOptions<HostFilteringOptions>()
    .PostConfigure<IConfiguration>((options, configuration) =>
    {
        if (string.IsNullOrWhiteSpace(configuration["AllowedHosts"]))
        {
            options.AllowedHosts = defaultAllowedHosts;
        }
    });

var app = builder.Build();

if (app.Services.GetRequiredService<IOptions<HostFilteringOptions>>().Value.AllowedHosts.Contains("*"))
{
    app.Logger.LogWarning("AllowedHosts contains '*', Host header filtering is disabled. " +
                          "Any website can reach the preview server through DNS rebinding");
}

// Strona dla przeglądarki bez ciasteczka: zabiera token z #, usuwa go z paska adresu i historii,
// wymienia na ciasteczko i przeładowuje się już jako podgląd
// language=html
const string LoginPage =
    """
    <!DOCTYPE html>
    <html>
    <head>
      <meta charset="UTF-8">
      <title>MdLive</title>
    </head>
    <body>
      <p id="status">Connecting...</p>
      <script>
        (async () => {
          const token = location.hash.slice(1);
          history.replaceState(null, "", location.pathname + location.search);

          const status = document.getElementById("status");
          if (!token) {
            status.textContent = "Missing access token. Open the preview from the editor (:MdLiveOpen).";
            return;
          }

          const response = await fetch("/auth", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ token }),
          });

          if (response.ok) {
            location.reload();
          } else {
            status.textContent = "Invalid access token, the server was probably restarted. Open the preview from the editor again (:MdLiveOpen).";
          }
        })();
      </script>
    </body>
    </html>
    """;

// Wymiana tokenu dostępu na ciasteczko sesji
app.MapPost("/auth", async (AuthRequest request, PreviewAccessToken token, HttpContext context) =>
{
    if (!token.IsValid(request.Token))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, "editor")],
        CookieAuthenticationDefaults.AuthenticationScheme);

    // IsPersistent = false: ciasteczko bez Expires, czyli sesyjne, znika po zamknięciu przeglądarki
    await context.SignInAsync(new ClaimsPrincipal(identity), new AuthenticationProperties { IsPersistent = false });
    return Results.NoContent();
}).AllowAnonymous();

app.MapGet("/", (HttpContext context) =>
{
    if (context.User.Identity?.IsAuthenticated != true)
    {
        return Results.Content(LoginPage, "text/html");
    }

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
          <div id="__japierdole" data-ignore-morph style="display: none;" data-init="@get('/preview/sse')"></div>

          <main id="app">
            <h1>Waiting for document data...</h1>
          </main>

        </body>
        </html>
        """;

    return Results.Content(html, "text/html");
}).AllowAnonymous();

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
app.MapDocumentFiles();

await app.StartAsync();

// Port znany jest dopiero po starcie. Pierwsza (i jedyna) linia na stdout, edytor ją czyta;
// logi idą na stderr, więc nic innego tu nie trafi. Edytor otwiera http://127.0.0.1:<port>/#<token>.
var port = new Uri(app.Urls.First()).Port;
var token = app.Services.GetRequiredService<PreviewAccessToken>().Value;
await Console.Out.WriteLineAsync(JsonSerializer.Serialize(new { port, token }));
await Console.Out.FlushAsync();

await app.WaitForShutdownAsync();
return 0;

static void PrintUsage(TextWriter writer)
{
    writer.WriteLine("""
        Usage: mdlive <command>

        Commands:
          init     Create .mdlive in the current directory with default style.css and template.html.
                   Fails if .mdlive already exists and isn't empty.
                   --force, -f  Overwrite the default files, other files in .mdlive are kept.
          serve    Start the preview server on a random port on 127.0.0.1 and print
                   {"port":<port>,"token":"<access token>"} to stdout; open http://127.0.0.1:<port>/#<token>.
                   Reads editor messages from stdin, exits when stdin is closed.
          help     Show this message.
        """);
}

// Ciało POST /auth, wysyłane jako JSON przez stronę logowania. JSON wymaga preflightu CORS,
// więc inna strona nie wyśle go z przeglądarki użytkownika.
internal sealed record AuthRequest(string? Token);
