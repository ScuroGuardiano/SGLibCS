using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection.Repositories;

namespace ScuroGuardiano.MdLive.Services;

/// <summary>
/// Data Protection key repository kept only in memory. Keys (and so session cookies) die with the server,
/// and nothing is written to <c>~/.aspnet/DataProtection-Keys</c>, which is where the default repository
/// stores them, even with an ephemeral provider configured.
/// </summary>
internal sealed class InMemoryXmlRepository : IXmlRepository
{
    private readonly Lock _lock = new();
    private readonly List<XElement> _elements = [];

    public IReadOnlyCollection<XElement> GetAllElements()
    {
        lock (_lock)
        {
            return _elements.Select(element => new XElement(element)).ToList();
        }
    }

    public void StoreElement(XElement element, string friendlyName)
    {
        lock (_lock)
        {
            _elements.Add(new XElement(element));
        }
    }
}
