using System.Runtime.CompilerServices;
using System.Threading.Channels;
using ScuroGuardiano.MdLive.Model;

namespace ScuroGuardiano.MdLive.Services;

internal sealed class PreviewSubscription : IDisposable
{
    private readonly PreviewHub _hub;
    private readonly Channel<bool> _signal;

    internal PreviewSubscription(PreviewHub hub, Channel<bool> signal)
    {
        _hub = hub;
        _signal = signal;
    }

    public async IAsyncEnumerable<PreviewSnapshot> ReadAllAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var _ in _signal.Reader.ReadAllAsync(cancellationToken))
        {
            yield return _hub.Current;
        }
    }

    public void Dispose() => _hub.Unsubscribe(_signal);
}
