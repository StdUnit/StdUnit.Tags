using StdUnit.Tags.ModbusTcp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace StdUnit.Tags.ZLan;


public abstract class ZLanCbntBuilderBase : ModbusBitTagCbntBuilder
{
    /// <summary>
    /// 区域起始地址
    /// </summary>
    public abstract string AreaStartAddr { get; }


    public override TagCbntBuilderBase WithCbntDescriptor(TagCbntDescriptor descriptor)
    {
        base.WithCbntDescriptor(descriptor);
        this.TagCbnt.StartAddress = $"{this.Slave}~{AreaStartAddr}";
        return this;
    }


    /// <inheritdoc/>
    protected override ITagCbntor Fallback(TagDescriptor descriptor, ITagChannel channel)
    {
        var tagFactory = this.MakeZLanTagFactory();
        return tagFactory.CreateTag(descriptor);
    }
}

