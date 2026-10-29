using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace StdUnit.Tags.Tests.Fakes;

// 本文件是主测试项目 Fakes/FakedTagCbnt.cs 的副本：
// 本项目所需的夹具都在本项目下自带一份，不引用主测试项目的源码（见 csproj 顶部注释）。

/// <summary>
/// 纯内存测点组合，不关联任何物理设备，仅用于测试。<br/>
/// 没有缓存、没有批量 IO：读/写都只作用在子测点自己的值上，用来看"结构里出现 TagCbnt 时各处是否处理正确"。
/// </summary>
internal sealed class FakedTagCbnt : ITagCbnt
{
    public FakedTagCbnt(TagCbntDescriptor descriptor)
    {
        this.Descriptor = descriptor;
        this.StartAddress = descriptor.StartAddress;
        this.IsEnabled = descriptor.IsEnabled;
    }

    public TagCbntDescriptor Descriptor { get; set; }

    public ITagGrp? Parent { get; set; }

    public IDictionary<string, ITagCbntor> Children { get; } = new Dictionary<string, ITagCbntor>();

    public ITagCbntor this[string tagName] => this.Children[tagName];

    public bool IsEnabled { get; set; }

    public bool IsScanned { get; set; }

    public ITagChannel? Channel { get; set; }

    public string StartAddress { get; set; }

    public bool IsDirty { get; set; }

    public Task ReadAsync(CancellationToken ct) => Task.CompletedTask;

    public Task WriteAsync(CancellationToken ct)
    {
        this.IsDirty = false;
        return Task.CompletedTask;
    }
}

/// <summary>组合子：值是内存里的一份数据，读/写不碰硬件。</summary>
internal sealed class FakedTagCbntor : TagCbntor
{
    public FakedTagCbntor(TagDescriptor tagDescriptor, ITagCbnt tagCbnt)
        : base(tagDescriptor, tagCbnt, tagOffset: 0, cacheOffset: 0)
    {
    }

    public override object? Value { get; set; }

    public override Task ReadAsync(CancellationToken ct) => Task.CompletedTask;

    public override Task WriteAsync(CancellationToken ct)
    {
        this.IsDirty = false;
        return Task.CompletedTask;
    }
}

/// <summary>构建 <see cref="FakedTagCbnt"/>。</summary>
internal sealed class FakedTagCbntBuilder : TagCbntBuilderBase
{
    public FakedTagCbntBuilder()
        : base(new FakedTagCbnt(new TagCbntDescriptor { Name = "unknown", StartAddress = "0" }))
    {
    }

    protected override ITagCbntor Fallback(TagDescriptor descriptor, ITagChannel channel) =>
        new FakedTagCbntor(descriptor, this.TagCbnt);

    protected override TagCbntBuilderBase AutoLayout() => this;
}
