using StdUnit.Tags.ModbusTcp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace StdUnit.Tags.ZLan;


/// <summary>
/// ZLan 位空间测点组合构建器的基类：组合的起始地址 = <c>从站号~区域起始地址</c>。
/// </summary>
public abstract class ZLanCbntBuilderBase : ModbusBitTagCbntBuilder
{
    /// <summary>
    /// 区域起始地址
    /// </summary>
    public abstract string AreaStartAddr { get; }


    /// <summary>
    /// 设置描述符，并把组合的起始地址设为 <c>从站号~区域起始地址</c>。
    /// </summary>
    /// <param name="descriptor">测点组合描述符</param>
    /// <returns>本构建器</returns>
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

