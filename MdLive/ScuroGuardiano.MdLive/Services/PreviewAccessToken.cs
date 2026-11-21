using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace ScuroGuardiano.MdLive.Services;

/// <summary>
/// <para>
/// Access token generated on every server run and handed to the editor on stdout. The editor opens
/// <c>http://127.0.0.1:port/#token</c> and the login page exchanges it at <c>/auth</c> for a session
/// cookie issued by the cookie authentication scheme.
/// </para>
/// <para>
/// This is only the credential. Sessions, cookies and access checks are ASP.NET Core authentication
/// and authorization, see <c>Program.cs</c>.
/// </para>
/// </summary>
internal sealed class PreviewAccessToken
{
    private readonly byte[] _bytes;

    public PreviewAccessToken()
    {
        Value = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));
        _bytes = Encoding.ASCII.GetBytes(Value);
    }

    /// <summary>Valid for the whole server run, so the editor can open the preview again.</summary>
    public string Value { get; }

    public bool IsValid(string? candidate)
    {
        if (candidate is null)
        {
            return false;
        }

        // Porównanie w stałym czasie, żeby czas odpowiedzi nie zdradzał, ile znaków się zgadza.
        // Przy różnej długości FixedTimeEquals zwraca false od razu, ale długość tokenu jest stała i jawna.
        return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(candidate), _bytes);
    }
}
