namespace StdUnit.Tags.S7;

/// <summary>
/// 构建 S7 直接测点的构建器。<br/>
/// 当通道的驱动为 <see cref="S7Names.DriverName"/> 时，用于构建测点。
/// </summary>
public partial class S7DirectTagBuilder : TagBuilderBase
{
    /// <summary>
    /// c'tor
    /// </summary>
    public S7DirectTagBuilder()
    {
    }

    /// <inheritdoc/>
    protected override ITag Fallback(ITagChannel channel)
    {
        S7TagChannel? s7ch;
        if (this.Channel is null)
        {
            s7ch = null;
        }
        else if (this.Channel is not S7TagChannel)
        {
            throw new Exception($"测点({this.Name})配置了通道({this.Channel.ChannelName()})，但不是{nameof(S7TagChannel)}");
        }
        else
        {
            s7ch = this.Channel as S7TagChannel;
        }

        var factory = new S7DirectTagFactory(this.Parent.IntoTagContainer());
        var tag = factory.Create(this.TagDescriptor, s7ch);

        return tag;
    }
}
