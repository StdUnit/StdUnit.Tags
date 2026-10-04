using System.Threading;
using System.Threading.Tasks;

namespace StdUnit.Tags.Tests.Fakes;

internal class FakedChannel : ITagChannel
{
    public FakedChannel(TagChannelDescriptor descriptor)
    {
        Descriptor = descriptor;
    }
    public TagChannelDescriptor Descriptor { get; }

    public Task DisconnectAsync(CancellationToken ct) => Task.CompletedTask;

    public void Dispose() { }

    public Task EnsureConnectedAsync(bool force, CancellationToken ct)
    {
        return Task.CompletedTask;
    }
}
