using StdUnit.Tags.ModbusTcp;
namespace StdUnit.Tags.Hjzk;

/// <summary>
/// Hjzk CbntBuiilder 基类，将来会被扩展成 DI/DO CbntBuilder
/// </summary>
public abstract class HjzkCbntBuilderBase : ModbusBitTagCbntBuilder
{


    /// <summary>
    /// 区域起始地址
    /// </summary>
    public abstract string AreaStartAddr { get; }


    /// <inheritdoc/>
    public override TagCbntBuilderBase WithCbntDescriptor(TagCbntDescriptor descriptor)
    {
        base.WithCbntDescriptor(descriptor);
        this.TagCbnt.StartAddress = $"{this.Slave}~{AreaStartAddr}";
        return this;
    }

    /// <inheritdoc/>
    protected override ITagCbntor Fallback(TagDescriptor descriptor, ITagChannel channel)
    {
        var tagFactory = this.MakeHjzkTagFactory();
        return tagFactory.CreateTag(descriptor);
    }
}

