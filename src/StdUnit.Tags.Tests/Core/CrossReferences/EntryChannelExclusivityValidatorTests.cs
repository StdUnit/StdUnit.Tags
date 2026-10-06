using StdUnit.Tags;
using StdUnit.Tags.S7;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
using System.Xml.Linq;
using Xunit;

namespace StdUnit.Tags.Tests.Core.CrossReferences;

/// <summary>
/// 测试 <see cref="EntryChannelExclusivityValidator"/>（随 <c>EnableCrossReferenceValidation()</c> 默认启用）：
/// 同一个通道不被多个入口共用（一个通道 = 一个轮询回路）。
/// </summary>
public class EntryChannelExclusivityValidatorTests
{
    private static void Validate(string xml) => new EntryChannelExclusivityValidator().Validate(XElement.Parse(xml));

    #region 通过

    /// <summary>单入口多通道（入口下的子组各自声明通道）不涉及共用，通过。</summary>
    [Fact]
    public void Validate_SingleEntryWithMultipleChannels_NoErrors()
    {
        Validate("""
            <Project>
                <Channel name="S7-1" driver="S7"><IpAddr>localhost</IpAddr></Channel>
                <Channel name="ch-2" driver="S7"><IpAddr>localhost</IpAddr></Channel>
                <TagGrp name="entry" isEntry="true" channel="S7-1">
                    <TagGrp name="通用状态">
                        <Tag name="心跳" address="DB200.100.0" type="BIT"/>
                    </TagGrp>
                    <TagGrp name="grp2" channel="ch-2">
                        <Tag name="t" address="DB1.0" type="INT16"/>
                    </TagGrp>
                </TagGrp>
            </Project>
            """);
    }

    /// <summary>多个入口各用各的通道，通过。</summary>
    [Fact]
    public void Validate_MultipleEntriesWithDistinctChannels_NoErrors()
    {
        Validate("""
            <Project>
                <Channel name="S7-1" driver="S7"><IpAddr>localhost</IpAddr></Channel>
                <Channel name="ch-2" driver="S7"><IpAddr>localhost</IpAddr></Channel>
                <TagGrp name="ioBox" isEntry="true" channel="S7-1">
                    <Tag name="t" address="DB1.0" type="INT16"/>
                </TagGrp>
                <TagGrp name="grp2" isEntry="true" channel="ch-2">
                    <Tag name="t" address="DB1.0" type="INT16"/>
                </TagGrp>
            </Project>
            """);
    }

    /// <summary>入口没有解析到任何通道（Tags 各自声明）时，不做任何判定，通过。</summary>
    [Fact]
    public void Validate_EntriesWithoutAnyChannel_NoErrors()
    {
        Validate("""
            <Project>
                <TagGrp name="entry" isEntry="true">
                    <Tag name="t" address="DB1.0" type="INT16"/>
                </TagGrp>
            </Project>
            """);
    }

    #endregion

    #region 拒绝：值传递共用

    /// <summary>两个兄弟入口显式声明同一个通道 → 拒绝。</summary>
    [Fact]
    public void Validate_SiblingEntriesShareChannel_Throws()
    {
        var ex = Assert.Throws<TagsProjectValidationException>(() => Validate("""
            <Project>
                <Channel name="S7-1" driver="S7"><IpAddr>localhost</IpAddr></Channel>
                <TagGrp name="entryA" isEntry="true" channel="S7-1">
                    <Tag name="t" address="DB1.0" type="INT16"/>
                </TagGrp>
                <TagGrp name="entryB" isEntry="true" channel="S7-1">
                    <Tag name="t" address="DB2.0" type="INT16"/>
                </TagGrp>
            </Project>
            """));

        Assert.Contains("S7-1", ex.Message);
        Assert.Contains("TagGrp(entryA)", ex.Message);
        Assert.Contains("TagGrp(entryB)", ex.Message);
    }

    /// <summary>多个入口从同一个非入口祖先继承同一个通道 → 拒绝。</summary>
    [Fact]
    public void Validate_EntriesInheritSameChannelFromCommonAncestor_Throws()
    {
        var ex = Assert.Throws<TagsProjectValidationException>(() => Validate("""
            <Project>
                <Channel name="S7-1" driver="S7"><IpAddr>localhost</IpAddr></Channel>
                <TagGrp name="root" channel="S7-1">
                    <TagGrp name="entryA" isEntry="true">
                        <Tag name="t" address="DB1.0" type="INT16"/>
                    </TagGrp>
                    <TagGrp name="entryB" isEntry="true">
                        <Tag name="t" address="DB2.0" type="INT16"/>
                    </TagGrp>
                </TagGrp>
            </Project>
            """));

        Assert.Contains("S7-1", ex.Message);
        Assert.Contains("TagGrp(root)/TagGrp(entryA)", ex.Message);
    }

    /// <summary>入口主通道与另一入口子树中 Tag 上声明的通道同名（同一实例）→ 拒绝。</summary>
    [Fact]
    public void Validate_TagChannelInOneEntryConflictsWithOtherEntryChannel_Throws()
    {
        var ex = Assert.Throws<TagsProjectValidationException>(() => Validate("""
            <Project>
                <Channel name="S7-1" driver="S7"><IpAddr>localhost</IpAddr></Channel>
                <TagGrp name="entryA" isEntry="true" channel="S7-1">
                    <Tag name="t" address="DB1.0" type="INT16"/>
                </TagGrp>
                <TagGrp name="entryB" isEntry="true">
                    <Tag name="t" address="DB2.0" type="INT16" channel="S7-1"/>
                </TagGrp>
            </Project>
            """));

        Assert.Contains("S7-1", ex.Message);
    }

    /// <summary>
    /// 连接动作为空实现的通道（如 SimpleFiles 的 EnsureConnectedAsync/DisconnectAsync 都是空动作）
    /// 同样要遵守：共享不只中"断开互相踩"，还会让两个入口的扫描周期交错读写同一底层资源，
    /// 而要求分离的代价几乎为零（再声明一个 Channel 即可）。
    /// </summary>
    [Fact]
    public void Validate_NoOpConnectionChannelSharedAcrossEntries_StillThrows()
    {
        var ex = Assert.Throws<TagsProjectValidationException>(() => Validate("""
            <Project>
                <Channel name="file" driver="SimpleFiles"><BaseDir>D:/temp</BaseDir></Channel>
                <TagGrp name="entryA" isEntry="true" channel="file">
                    <Tag name="aa" address="D:/temp/aa" type="BIT"/>
                </TagGrp>
                <TagGrp name="entryB" isEntry="true" channel="file">
                    <Tag name="bb" address="D:/temp/bb" type="BIT"/>
                </TagGrp>
            </Project>
            """));

        Assert.Contains("file", ex.Message);
    }

    #endregion

    #region 嵌套 isEntry 不生效：不构成额外入口

    /// <summary>
    /// 嵌套的 isEntry="true" 不是独立入口（入口识别遇到最外层入口即停止下探），
    /// 它的子树归外层入口：即使它声明了自己的通道，也不构成共用。
    /// </summary>
    [Fact]
    public void Validate_NestedEntryWithOwnChannel_NoErrors()
    {
        Validate("""
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
    }

    /// <summary>
    /// 嵌套入口重申外层入口的通道：两者同属一个入口单元（嵌套的 isEntry 不生效），
    /// 因此不是共用，不报错（这类写法由 <see cref="NestedEntryValidator"/> 负责拒绝）。
    /// </summary>
    [Fact]
    public void Validate_NestedEntryRedeclaresOuterEntryChannel_NoErrors()
    {
        Validate("""
            <Project>
                <Channel name="S7-1" driver="S7"><IpAddr>localhost</IpAddr></Channel>
                <TagGrp name="entry" isEntry="true" channel="S7-1">
                    <Tag name="t" address="DB1.0" type="INT16"/>
                    <TagGrp name="subEntry" isEntry="true" channel="S7-1">
                        <Tag name="t2" address="DB2.0" type="INT16"/>
                    </TagGrp>
                </TagGrp>
            </Project>
            """);
    }

    /// <summary>
    /// 外层入口的通道与它嵌套子组的通道相同时不报错——两者是同一个入口单元。
    /// </summary>
    [Fact]
    public void Validate_NestedGroupReusesEntryChannel_NoErrors()
    {
        Validate("""
            <Project>
                <Channel name="S7-1" driver="S7"><IpAddr>localhost</IpAddr></Channel>
                <TagGrp name="entry" isEntry="true" channel="S7-1">
                    <Tag name="t" address="DB1.0" type="INT16"/>
                    <TagGrp name="sub" channel="S7-1">
                        <Tag name="t2" address="DB2.0" type="INT16"/>
                    </TagGrp>
                </TagGrp>
            </Project>
            """);
    }

    #endregion

    #region 端到端

    /// <summary>
    /// 一致性：仓库内示例项目 XML 均不违返通道独占（否则用户一旦启用严格校验，示例先被拒）。
    /// 同时也反向验证本校验器对真实配置不会误报。
    /// </summary>
    [Fact]
    public void Validate_RepoSampleXmls_NoErrors()
    {
        var samples = new[]
        {
            @"Samples\Web\index.xml",
            @"Samples\Web\index2.xml",
            @"Samples\Web\index-plugin.xml",
            @"Samples\WpfDemo\index.xml",
            @"Samples\WpfDemo\index-2.xml",
            @"Samples\NixMonitor\index.xml",
        };

        foreach (var rel in samples)
        {
            var path = Path.Combine(AppContext.BaseDirectory, rel);
            Assert.True(File.Exists(path), $"示例文件未复制到输出目录: {path}");
            new EntryChannelExclusivityValidator().Validate(XDocument.Load(path).Root!);
        }
    }

    /// <summary>端到端：默认校验（含本校验器）在 MakeProject 时拒绝跨入口共用通道。</summary>
    [Fact]
    public void MakeProject_WithDefaultValidation_RejectsSharedChannel()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTagsProjectServices(b => b.AddS7Support());
        using var root = services.BuildServiceProvider();
        using var scope = root.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<ITagsProjectFactory>();

        var xml = XElement.Parse("""
            <Project>
                <Channel name="S7-1" driver="S7">
                    <IpAddr>localhost</IpAddr>
                </Channel>
                <TagGrp name="entryA" isEntry="true" channel="S7-1">
                    <Tag name="t" address="DB1.0" type="INT16"/>
                </TagGrp>
                <TagGrp name="entryB" isEntry="true" channel="S7-1">
                    <Tag name="t" address="DB2.0" type="INT16"/>
                </TagGrp>
            </Project>
            """);

        Assert.Throws<TagsProjectValidationException>(() => factory.Create(string.Empty, xml));
    }

    #endregion
}
