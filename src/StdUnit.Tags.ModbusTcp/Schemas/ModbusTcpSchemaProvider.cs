using StdUnit.Tags;
using StdUnit.Tags.Generated;

namespace StdUnit.Tags.ModbusTcp;

/// <summary>
/// ModbusTcp 驱动 schema 提供者：把源生成器编译的 modbustcp.xsd 常量注册进运行期 schema 校验
/// （<see cref="ITagsProjectValidator"/>，由 <c>EnableXmlSchemaValidation()</c> 启用时生效）。
/// </summary>
public sealed class ModbusTcpSchemaProvider : ITagsProjectSchemaProvider
{
    /// <inheritdoc/>
    public IEnumerable<(string LogicalName, string Content)> GetSchemaContents() =>
        SchemaContent_modbustcp.GetSchemaContents();
}
