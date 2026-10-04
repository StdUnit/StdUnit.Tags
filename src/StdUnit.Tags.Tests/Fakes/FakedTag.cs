using System.Threading;
using System.Threading.Tasks;

namespace StdUnit.Tags.Tests.Fakes;

/// <summary>
/// 纯内存测点，不关联任何物理设备，仅用于测试。
/// </summary>
internal class FakedTag : Tag<object, FakedChannel>
{
    public FakedTag(TagDescriptor descriptor, FakedChannel? channel, ITagGrp parent)
        : base(descriptor, channel, TagContainer.From(parent))
    {
    }

    public override Task ReadAsync(CancellationToken ct) => Task.CompletedTask;
    public override Task WriteAsync(CancellationToken ct) { IsDirty = false; return Task.CompletedTask; }
}

/// <summary>
/// 构建 <see cref="FakedTag"/> 实例。
/// </summary>
internal class FakedTagBuilder : TagBuilderBase
{
    public FakedTagBuilder(TagDescriptor descriptor) => TagDescriptor = descriptor;

    protected override ITag Fallback(ITagChannel channel)
    {
        var fakeCh = channel as FakedChannel;
        return new FakedTag(TagDescriptor, fakeCh, Parent);
    }
}
