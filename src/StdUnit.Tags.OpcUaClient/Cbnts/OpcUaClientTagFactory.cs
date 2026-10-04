namespace StdUnit.Tags.OpcUaClient.Cbnts;



internal class OpcUaClientTagFactory : TagCbntorFactoryBase
{
    /// <summary>
    /// c'tor
    /// </summary>
    public OpcUaClientTagFactory(TagCbntBuilderBase builder)
        : base(builder)
    {
    }

    /// <inheritdoc/>
    public override ITagCbntor CreateTag(TagDescriptor descriptor)
    {
        var tag = new OpcUaClientTagCbntor(descriptor, CbntBuilder.TagCbnt, 0, 0);
        return tag;
    }

}
