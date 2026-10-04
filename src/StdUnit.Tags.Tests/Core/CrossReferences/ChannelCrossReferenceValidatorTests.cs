using StdUnit.Tags;
using StdUnit.Tags.S7;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace StdUnit.Tags.Tests.Core.CrossReferences;

/// <summary>
/// 测试加载期项目校验（<see cref="ITagsProjectValidator"/> 的 <see cref="ChannelCrossReferenceValidator"/> 实现，
/// 由 <c>EnableCrossReferenceValidation()</c> 启用）：测点的 channel 属性必须指向已声明的 Channel。
/// </summary>
public class ChannelCrossReferenceValidatorTests
{
    /// <summary>合法的 channel 引用通过校验。</summary>
    [Fact]
    public void Validate_ValidChannelReference_NoErrors()
    {
        var xml = XElement.Parse("""
            <Project>
                <Channel name="S7-1" driver="S7">
                    <IpAddr>localhost</IpAddr>
                </Channel>
                <TagGrp name="g1" isEntry="true" channel="S7-1">
                    <Tag name="bit" address="DB200.0.0" type="BIT"/>
                </TagGrp>
            </Project>
            """);

        var validator = new ChannelCrossReferenceValidator();
        validator.Validate(xml); // 不抛即通过
    }

    /// <summary>拼错的通道名在加载期被拒绝，错误带路径上下文。</summary>
    [Fact]
    public void Validate_UnknownChannelReference_ThrowsWithPath()
    {
        var xml = XElement.Parse("""
            <Project>
                <Channel name="S7-1" driver="S7">
                    <IpAddr>localhost</IpAddr>
                </Channel>
                <TagGrp name="产线1" isEntry="true" channel="S7-2">
                    <TagCbnt name="输入">
                        <Tag name="bit" address="DB200.0.0" type="BIT" channel="S7-99"/>
                    </TagCbnt>
                </TagGrp>
            </Project>
            """);

        var validator = new ChannelCrossReferenceValidator();
        var ex = Assert.Throws<TagsProjectSchemaException>(() => validator.Validate(xml));

        Assert.Contains("S7-2", ex.Message);
        Assert.Contains("Tag(bit)", ex.Message);
    }

    /// <summary>省略 channel（向上冒泡继承）不报错。</summary>
    [Fact]
    public void Validate_NoChannelAttribute_Skip()
    {
        var xml = XElement.Parse("""
            <Project>
                <Channel name="S7-1" driver="S7">
                    <IpAddr>localhost</IpAddr>
                </Channel>
                <TagGrp name="g1" isEntry="true" channel="S7-1">
                    <TagCbnt name="输入">
                        <Tag name="bit" address="DB200.0.0" type="BIT"/>
                    </TagCbnt>
                </TagGrp>
            </Project>
            """);

        var validator = new ChannelCrossReferenceValidator();
        validator.Validate(xml); // 不抛即通过（子级省略 channel 继承父级）
    }

    /// <summary>端到端：EnableCrossReferenceValidation 后，拼错通道名在 MakeProject 时被拒绝。</summary>
    [Fact]
    public void MakeProject_WithCrossReferenceValidation_RejectsUnknownChannel()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTagsProjectServices(b =>
        {
            b.AddS7Support();
            b.EnableCrossReferenceValidation();
        });
        using var root = services.BuildServiceProvider();
        using var scope = root.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<ITagsProjectFactory>();

        var xml = XElement.Parse("""
            <Project>
                <Channel name="S7-1" driver="S7">
                    <IpAddr>localhost</IpAddr>
                </Channel>
                <TagGrp name="g1" isEntry="true" channel="S7-2">
                    <Tag name="bit" address="DB200.0.0" type="BIT"/>
                </TagGrp>
            </Project>
            """);

        Assert.Throws<TagsProjectSchemaException>(() => factory.Create(string.Empty, xml));
    }

    /// <summary>
    /// 端到端：不启用交叉引用校验时，拼错的通道名仍会被运行期拦截
    /// （SearchRequiredChannel 在加载测点阶段抛错），但错误时机晚、信息是裸 Exception——
    /// 交叉引用把错误提前到加载期、带完整路径上下文。
    /// </summary>
    [Fact]
    public void MakeProject_WithoutCrossReferenceValidation_StillFailsAtRuntime()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTagsProjectServices(b => b.AddS7Support());
        using var root = services.BuildServiceProvider();
        using var scope = root.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<ITagsProjectFactory>();

        // 通道名拼错 S7-2（实际声明 S7-1）——不启用校验时，运行期加载测点阶段抛错
        var xml = XElement.Parse("""
            <Project>
                <Channel name="S7-1" driver="S7">
                    <IpAddr>localhost</IpAddr>
                </Channel>
                <TagGrp name="g1" isEntry="true" channel="S7-2">
                    <Tag name="bit" address="DB200.0.0" type="BIT"/>
                </TagGrp>
            </Project>
            """);

        Assert.ThrowsAny<Exception>(() => factory.Create(string.Empty, xml));
    }

    /// <summary>
    /// 多实现注册：可注册多个 ITagsProjectValidator，全部按注册顺序执行
    /// （第三方也可用 AddValidation&lt;T&gt; 追加自己的校验器）。
    /// </summary>
    [Fact]
    public void MakeProject_MultipleValidators_AllExecuted()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTagsProjectServices(b =>
        {
            b.AddS7Support();
            b.EnableCrossReferenceValidation();                                  // 内置：channel 引用校验
            b.AddValidation<AlwaysFailValidator>();                             // 追加自定义校验器
        });
        using var root = services.BuildServiceProvider();
        using var scope = root.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<ITagsProjectFactory>();

        var xml = XElement.Parse("""
            <Project>
                <Channel name="S7-1" driver="S7">
                    <IpAddr>localhost</IpAddr>
                </Channel>
                <TagGrp name="g1" isEntry="true" channel="S7-1">
                    <Tag name="bit" address="DB200.0.0" type="BIT"/>
                </TagGrp>
            </Project>
            """);

        // 通道引用合法（channel 校验器通过），但追加的 AlwaysFailValidator 抛错
        var ex = Assert.Throws<TagsProjectSchemaException>(() => factory.Create(string.Empty, xml));
        Assert.Contains("AlwaysFail", ex.Message);
    }

    /// <summary>自定义校验器：无论什么 XML 都失败（用于验证多实现注册）。</summary>
    private sealed class AlwaysFailValidator : ITagsProjectValidator
    {
        public void Validate(XElement root) =>
            throw new TagsProjectSchemaException(new[] { "AlwaysFail validator triggered" });
    }
}
