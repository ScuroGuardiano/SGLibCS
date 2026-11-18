using ScuroGuardiano.MdLive.Model;

namespace ScuroGuardiano.MdLive.Services;

using System.Threading.Channels;

internal sealed class PreviewHub
{
    private readonly Lock _lock = new();
    private readonly List<Channel<bool>> _subscribers = [];
    private PreviewSnapshot _snapshot = PreviewSnapshot.Empty;

    public PreviewSnapshot Current => Volatile.Read(ref _snapshot);

    public void PublishDocument(RenderedDocument document)
    {
        lock (_lock)
        {
            var next = _snapshot with
            {
                Document = document,
                DocumentVersion = _snapshot.DocumentVersion + 1
            };
            Volatile.Write(ref _snapshot, next);
            NotifySubscribers();
        }
    }

    public void PublishCursor(int line)
    {
        lock (_lock)
        {
            if (_snapshot.CursorLine == line)
            {
                return;
            }

            var next = _snapshot with
            {
                CursorLine = line,
                CursorVersion = _snapshot.CursorVersion + 1
            };
            Volatile.Write(ref _snapshot, next);
            NotifySubscribers();
        }
    }

    public PreviewSubscription Subscribe()
    {
        var signal = Channel.CreateBounded<bool>(new BoundedChannelOptions(1)
        {
            // Jeśli sygnał już czeka, kolejny nic nie wnosi
            FullMode = BoundedChannelFullMode.DropWrite,
            SingleReader = true
        });

        // Nowy klient od razu dostaje aktualny stan
        signal.Writer.TryWrite(true);

        lock (_lock)
        {
            _subscribers.Add(signal);
        }

        return new PreviewSubscription(this, signal);
    }

    internal void Unsubscribe(Channel<bool> signal)
    {
        lock (_lock)
        {
            _subscribers.Remove(signal);
        }

        signal.Writer.TryComplete();
    }

    private void NotifySubscribers()
    {
        foreach (var subscriber in _subscribers)
        {
            subscriber.Writer.TryWrite(true);
        }
    }
}
