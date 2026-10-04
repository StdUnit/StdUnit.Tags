using System;
namespace StdUnit.Tags.OpcUaClient.Cbnts;

/// <summary>
/// 构建 OpcUaClient 测点组合的构建器。<br/>
/// 当通道的驱动为 <see cref="OpcUaClientNames.DriverName"/> 时，用于构建测点组合。
/// </summary>
public class OpcUaClientTagCbntBuilder : TagCbntBuilderBase
{
    /// <summary>
    /// c'tor<br/>
    /// 需要额外使用 <c>WithCbntDescriptor()</c> 设置实际描述符。
    /// </summary>
    public OpcUaClientTagCbntBuilder()
        : base(new OpcUaClientTagCbnt(new TagCbntDescriptor { Name = "unkown_opcua_cbnt_name", StartAddress = "unknown_opcua_cbnt_start_address" }))
    {
    }

    /// <inheritdoc/>
    protected override ITagCbntor Fallback(TagDescriptor descriptor, ITagChannel channel)
    {
        var tagFactory = this.MakeOpcUaTagFactory();
        return tagFactory.CreateTag(descriptor);
    }

    /// <inheritdoc/>
    protected override TagCbntBuilderBase AutoLayout()
    {
        return this;
    }
}