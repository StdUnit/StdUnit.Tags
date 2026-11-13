using System.Threading;
using System.Threading.Tasks;

namespace StdUnit.Tags.Benchmarks;

/// <summary>空实现通道：只为了让测点能挂上，不做任何 IO。</summary>
internal sealed class BenchChannel : ITagChannel
{
    public TagChannelDescriptor Descriptor { get; } = new() { Name = "bench", Driver = "Bench" };

    public Task EnsureConnectedAsync(bool force, CancellationToken ct) => Task.CompletedTask;

    public Task DisconnectAsync(CancellationToken ct) => Task.CompletedTask;

    public void Dispose()
    {
    }
}

/// <summary>纯内存测点：Read/Write 立即完成，只留下遍历与判定的成本。</summary>
internal sealed class BenchLeafTag : Tag<object, BenchChannel>
{
    public BenchLeafTag(TagDescriptor descriptor, BenchChannel channel, ITagGrp parent)
        : base(descriptor, channel, TagContainer.From(parent))
    {
    }

    public override Task ReadAsync(CancellationToken ct) => Task.CompletedTask;

    public override Task WriteAsync(CancellationToken ct)
    {
        IsDirty = false;
        return Task.CompletedTask;
    }
}
