namespace StdUnit.Tags.OpcUaClient.DirectTags;

/// <summary>
/// 构建 OpcUaClient 直接测点的构建器。<br/>
/// 当通道的驱动为 <see cref="OpcUaClientNames.DriverName"/> 时，用于构建测点。
/// </summary>
public class OpcUaClientDirectTagBuilder : TagBuilderBase
{
    /// <inheritdoc/>
    protected override ITag Fallback(ITagChannel channel)
    {
        var container = this.Parent.IntoTagContainer();

        OpcUaClientTagChannel? occh;
        if (this.Channel is null)
        {
            occh = null;
        }
        else if (this.Channel is not OpcUaClientTagChannel)
        {
            throw new TagsProjectConfigurationException(
                $"测点 '{this.Name}' 配置了通道，但不是 {nameof(OpcUaClientTagChannel)}",
                $"Tag({this.Name})");
        }
        else
        {
            occh = this.Channel as OpcUaClientTagChannel;
        }

        return new OpcUaClientDirectTag(this.TagDescriptor, occh, container);
    }
}