using System.Threading.Channels;
using ScuroGuardiano.MdLive.Model;

namespace ScuroGuardiano.MdLive.Services;

public class DocumentContentChannel
{
    public Channel<DocumentContent> Channel { get; } = System.Threading.Channels.Channel.CreateBounded<DocumentContent>(
        new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = true
        });
}
