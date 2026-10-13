using StdUnit.Tags;
using StdUnit.Tags.S7;
using Microsoft.Extensions.DependencyInjection;
using System.Xml.Linq;
using Xunit;

namespace StdUnit.Tags.Tests.Core.CrossReferences;

/// <summary>
/// 测试 <see cref="NestedEntryValidator"/>（随 <c>EnableCrossReferenceValidation()</c> 默认启用）：
/// 嵌套在另一个入口内部的 <c>isEntry="true"</c> 不生效（<see cref="ITagGrpExtensions.ScanEntries"/>
/// 遇到最外层入口即停止下探），属于静默失效的错误配置。
/// </summary>
public class NestedEntryValidatorTests
{
    private static void Validate(string xml) => new NestedEntryValidator().Validate(XElement.Parse(xml));

    #region 通过

    /// <summary>多个同等层级的入口（互不嵌套）通过。</summary>
    [Fact]
    public void Validate_SiblingEntries_NoErrors()
    {
        Validate("""
            <Project>
                <Channel name="S7-1" driver="S7"/>
                <Channel name="ch-2" driver="S7"/>
                <TagGrp name="entryA" isEntry="true" channel="S7-1">
                    <Tag name="t" address="DB1.0" type="INT16"/>
                </TagGrp>
                <TagGrp name="entryB" isEntry="true" channel="ch-2">
                    <Tag name="t" address="DB2.0" type="INT16"/>
                </TagGrp>
            </Project>
            """);
    }

    /// <summary>入口内的普通子组（未标 isEntry）通过——这是表达"子单元"的正确方式。</summary>
    [Fact]
    public void Validate_PlainSubGroupInsideEntry_NoErrors()
    {
        Validate("""
            <Project>
                <Channel name="S7-1" driver="S7"/>
                <TagGrp name="entry" isEntry="true" channel="S7-1">
                    <TagGrp name="1#">
                        <Tag name="t" address="DB1.0" type="INT16"/>
                    </TagGrp>
                </TagGrp>
            </Project>
            """);
    }

    /// <summary>非入口祖先下的深层入口（未被任何入口包裹）通过。</summary>
    [Fact]
    public void Validate_DeepEntryWithoutEntryAncestor_NoErrors()
    {
        Validate("""
            <Project>
                <TagGrp name="root">
                    <TagGrp name="mid">
                        <TagGrp name="entry" isEntry="true">
                            <Tag name="t" address="DB1.0" type="INT16"/>
                        </TagGrp>
                    </TagGrp>
                </TagGrp>
            </Project>
            """);
    }

    #endregion

    #region 拒绝

    /// <summary>直接嵌在入口内的入口 → 拒绝。</summary>
    [Fact]
    public void Validate_EntryDirectlyInsideEntry_Throws()
    {
        var ex = Assert.Throws<TagsProjectValidationException>(() => Validate("""
            <Project>
                <TagGrp name="entry" isEntry="true" channel="S7-1">
                    <TagGrp name="subEntry" isEntry="true" channel="ch-2">
                        <Tag name="t" address="DB1.0" type="INT16"/>
                    </TagGrp>
                </TagGrp>
            </Project>
            """));

        Assert.Contains("TagGrp(entry)/TagGrp(subEntry)", ex.Message);
        Assert.Contains("不生效", ex.Message);
    }

    /// <summary>隔着若干普通子组嵌套的入口同样被拒绝（入口边界只认最外层）。</summary>
    [Fact]
    public void Validate_EntryDeepInsideEntry_Throws()
    {
        var ex = Assert.Throws<TagsProjectValidationException>(() => Validate("""
            <Project>
                <TagGrp name="entry" isEntry="true" channel="S7-1">
                    <TagGrp name="1#">
                        <TagGrp name="subEntry" isEntry="true">
                            <Tag name="t" address="DB1.0" type="INT16"/>
                        </TagGrp>
                    </TagGrp>
                </TagGrp>
            </Project>
            """));

        Assert.Contains("TagGrp(entry)/TagGrp(1#)/TagGrp(subEntry)", ex.Message);
    }

    /// <summary>isEntry 大小写不敏感（与运行期解析一致）。</summary>
    [Fact]
    public void Validate_IsEntryAttributeIsCaseInsensitive_Throws()
    {
        Assert.Throws<TagsProjectValidationException>(() => Validate("""
            <Project>
                <TagGrp name="entry" isEntry="true" channel="S7-1">
                    <TagGrp name="subEntry" isEntry="TRUE">
                        <Tag name="t" address="DB1.0" type="INT16"/>
                    </TagGrp>
                </TagGrp>
            </Project>
            """));
    }

    #endregion

    #region 端到端

    /// <summary>端到端：默认的交叉引用校验（含本校验器）在 MakeProject 时拒绝嵌套入口。</summary>
    [Fact]
    public void MakeProject_WithDefaultValidation_RejectsNestedEntry()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTagsProjectServices(b => b.AddS7Support());
        using var root = services.BuildServiceProvider();
        using var scope = root.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<ITagsProjectFactory>();

        var xml = XElement.Parse("""
            <Project>
                <Channel name="S7-1" driver="S7"><IpAddr>localhost</IpAddr></Channel>
                <Channel name="ch-2" driver="S7"><IpAddr>localhost</IpAddr></Channel>
                <TagGrp name="entry" isEntry="true" channel="S7-1">
                    <Tag name="t" address="DB1.0" type="INT16"/>
                    <TagGrp name="subEntry" isEntry="true" channel="ch-2">
                        <Tag name="t2" address="DB2.0" type="INT16"/>
                    </TagGrp>
                </TagGrp>
            </Project>
            """);

        var ex = Assert.Throws<TagsProjectValidationException>(() => factory.Create(string.Empty, xml));
        Assert.Contains("不生效", ex.Message);
    }

    #endregion
}
