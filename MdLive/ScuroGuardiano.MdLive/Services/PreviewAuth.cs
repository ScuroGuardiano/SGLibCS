using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace ScuroGuardiano.MdLive.Services;

/// <summary>
/// <para>
/// Access control for the preview server. Every server run generates two random secrets:
/// an access token handed to the editor on stdout, and a session id stored in a cookie.
/// </para>
/// <para>
/// The editor opens <c>http://127.0.0.1:port/#token</c>. The fragment never reaches the server or logs,
/// the login page posts it to <c>/auth</c> and gets the session cookie. Without the cookie only the login page
/// and <c>/auth</c> are reachable, so other local users and other websites can't read the preview.
/// </para>
/// </summary>
internal sealed class PreviewAuth
{
    private readonly byte[] _token;
    private readonly string _sessionId;
    private readonly byte[] _sessionIdBytes;

    public PreviewAuth()
    {
        Token = NewSecret();
        _token = Encoding.ASCII.GetBytes(Token);
        _sessionId = NewSecret();
        _sessionIdBytes = Encoding.ASCII.GetBytes(_sessionId);
    }

    /// <summary>Access token for the editor, valid for the whole server run.</summary>
    public string Token { get; }

    public bool IsValidToken(string? candidate) => SecretEquals(candidate, _token);

    public bool IsAuthenticated(HttpContext context) =>
        SecretEquals(context.Request.Cookies[CookieName(context)], _sessionIdBytes);

    public void SignIn(HttpContext context)
    {
        context.Response.Cookies.Append(CookieName(context), _sessionId, new CookieOptions
        {
            // Bez Expires / Max-Age: ciasteczko sesyjne, znika po zamknięciu przeglądarki
            HttpOnly = true,
            SameSite = SameSiteMode.Strict,
            Path = "/",
        });
    }

    // Ciasteczka nie są rozdzielane po portach, więc dwa podglądy na 127.0.0.1 (np. z dwóch neovimów)
    // nadpisywałyby sobie wspólne ciasteczko. Port w nazwie trzyma je osobno.
    private static string CookieName(HttpContext context) => $"mdlive_{context.Connection.LocalPort}";

    private static string NewSecret() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));

    private static bool SecretEquals(string? candidate, byte[] expected)
    {
        if (candidate is null)
        {
            return false;
        }

        // Porównanie w stałym czasie, żeby czas odpowiedzi nie zdradzał, ile znaków się zgadza.
        // Przy różnej długości FixedTimeEquals zwraca false od razu, ale długość sekretu jest stała i jawna.
        return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(candidate), expected);
    }
}
