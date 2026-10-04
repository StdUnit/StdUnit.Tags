namespace StdUnit.Tags.OpcUaClient.Cbnts;

internal static class TagCbntBuilderExtensions
{
    internal static OpcUaClientTagFactory MakeOpcUaTagFactory(this TagCbntBuilderBase tagGroupBuilder)
    {
        var tagFactory = new OpcUaClientTagFactory(tagGroupBuilder);
        return tagFactory;
    }
}