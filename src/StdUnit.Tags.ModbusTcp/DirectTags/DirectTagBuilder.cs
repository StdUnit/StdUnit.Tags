namespace StdUnit.Tags.ModbusTcp;

/// <summary>
/// 构建 ModbusTcp 直接测点的构建器。<br/>
/// 当通道的驱动为 <see cref="ModbusTcpNames.DriverName"/> 时，用于构建测点。
/// </summary>
public partial class ModbusTcpDirectTagBuilder : TagBuilderBase
{
    /// <summary>
    /// c'tor
    /// </summary>
    public ModbusTcpDirectTagBuilder()
    {
    }

    /// <inheritdoc/>
    protected override ITag Fallback(ITagChannel channel)
    {

        ModbusTcpChannel? mbch;
        if (this.Channel is null)
        {
            mbch = null;
        }
        else if (this.Channel is not ModbusTcpChannel)
        {
            throw new Exception($"测点({this.Name})配置了通道，但不是{nameof(ModbusTcpChannel)}");
        }
        else
        {
            mbch = this.Channel as ModbusTcpChannel;
        }

        var factory = new ModbusTcpDirectTagFactory(this.Parent.IntoTagContainer());
        var tag = factory.Create(this.TagDescriptor, mbch);
        return tag;
    }
}
