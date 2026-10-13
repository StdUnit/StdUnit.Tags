using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using StdUnit.Tags.SimpleFiles.Compat;

namespace StdUnit.Tags.SimpleFiles;

/// <summary>
/// 构建 SimpleFiles 直接测点的构建器。<br/>
/// 当通道的驱动为 <see cref="SimpleFilesNames.DriverName"/> 时，用于构建测点。
/// </summary>
public partial class SimpleFilesDirectTagBuilder : TagBuilderBase
{
    /// <summary>
    /// c'tor
    /// </summary>
    public SimpleFilesDirectTagBuilder()
    {
    }


    #region WithFactory
    /// <summary>
    /// 使用自定义工厂创建测点。<br/>
    /// 与基类 <see cref="TagBuilderBase.WithFactory(StdUnit.Tags.TagBuilderBase.CreateTag)"/> 的区别：<br/>
    /// 1. 调用工厂前会先把通道解析为 <see cref="SimpleFilesTagChannel"/>（自身通道为空时冒泡获取），类型不匹配抛出 <see cref="TagsProjectConfigurationException"/>；<br/>
    /// 2. <see cref="TagDescriptor.NormalizedAddress"/> 已按通道的 BaseDir 完成归一化，工厂内无需重复处理；<br/>
    /// </summary>
    /// <typeparam name="TVal"></typeparam>
    /// <param name="factory"></param>
    /// <returns></returns>
    /// <exception cref="TagsProjectConfigurationException">通道类型不是 <see cref="SimpleFilesTagChannel"/> 时抛出。</exception>
    public SimpleFilesDirectTagBuilder WithFactory<TVal>(CreateSimpleFilesDirectTag<TVal> factory)
    {
        base.WithFactory(
            (descriptor, thisChannel, container) =>
            {
                var channel = thisChannel ?? container.SearchRequiredChannel();
                var sfsChannel = channel as SimpleFilesTagChannel ?? throw new TagsProjectConfigurationException(
                    $"测点 '{descriptor.TagName}' 的通道类型不匹配：期望 {nameof(SimpleFilesTagChannel)}，实际 {channel.GetType().FullName}",
                    $"Tag({descriptor.TagName})");
                descriptor.NormalizedAddress = sfsChannel.MakePath(descriptor.RawAddress);
                return factory(descriptor, thisChannel as SimpleFilesTagChannel, container);
            }
        );
        return this;
    }

    /// <summary>
    /// 创建 SimpleFile 测点的委托。<br/>
    /// </summary>
    /// <param name="descriptor"></param>
    /// <param name="thisChannel"></param>
    /// <param name="container"></param>
    /// <returns></returns>
    public delegate SimpleFilesDirectTagBase<TVal> CreateSimpleFilesDirectTag<TVal>(
        TagDescriptor descriptor,
        SimpleFilesTagChannel? thisChannel,
        TagContainer container
        );


    /// <summary>
    /// 使用 JSON 测点工厂创建<see cref="JsonDirectTag{TVal}"/>型测点。<br/>
    /// <typeparamref name="TVal"/> 的序列化元数据来自注册时登记的 <c>JsonSerializerContext</c>
    /// （见 <c>TagsProject_Extensions.AddJsonSerializerContext</c>）：
    /// <list type="bullet">
    /// <item>登记过 → 用其中的源生成元数据（<b>AOT / 裁剪安全</b>）；</item>
    /// <item>没登记且宿主是普通 JIT → 退化为反射序列化；</item>
    /// <item>没登记且宿主禁用了反射（NativeAOT / 裁剪）→ <b>构建测点时即抛</b>
    /// <see cref="TagsProjectNotSupportedException"/>（加载期快速失败，不必等第一次读写）。</item>
    /// </list>
    /// </summary>
    /// <typeparam name="TVal"></typeparam>
    /// <returns></returns>
    /// <exception cref="TagsProjectConfigurationException">通道类型不匹配，或登记了上下文但没有该类型的元数据</exception>
    /// <exception cref="TagsProjectNotSupportedException">没登记元数据，而宿主又禁用了反射序列化（NativeAOT / 裁剪）</exception>
    public SimpleFilesDirectTagBuilder WithJsonTagFactory<TVal>()
    {
        this.WithFactory(
            (descriptor, thisChannel, container) =>
            {
                var jsonTypeInfo = this.ResolveJsonTypeInfo(typeof(TVal), descriptor);
                return new JsonDirectTag<TVal>(descriptor, thisChannel, container, jsonTypeInfo);
            }
        );
        return this;
    }
    #endregion

    /// <summary>
    /// 已登记的 <c>JsonSerializerContext</c>（由 <c>AddSimpleFilesDirectTagBuilder</c> 从 DI 注入）。
    /// </summary>
    private IReadOnlyList<JsonSerializerContext>? _jsonContexts;

    /// <summary>
    /// 注入已登记的 <c>JsonSerializerContext</c>。由 <c>TagsProject_Extensions.AddSimpleFilesDirectTagBuilder</c>
    /// 在运行使用者的 configure 之前调用。
    /// </summary>
    /// <param name="contexts"></param>
    internal void UseJsonSerializerContexts(IReadOnlyList<JsonSerializerContext>? contexts)
    {
        this._jsonContexts = contexts;
    }

    /// <summary>
    /// 为 <paramref name="type"/> 解析源生成元数据；拿不到时按宿主能力决定是"退化到反射"还是"加载期报错"。
    /// </summary>
    /// <param name="type">测点值类型</param>
    /// <param name="descriptor">用于报错时的定位上下文</param>
    /// <returns>元数据；null 表示调用方应走反射路径</returns>
    /// <exception cref="TagsProjectConfigurationException">登记了上下文但没有该类型的元数据</exception>
    /// <exception cref="TagsProjectNotSupportedException">没登记元数据，而宿主又禁用了反射序列化（NativeAOT / 裁剪）</exception>
    private JsonTypeInfo? ResolveJsonTypeInfo(Type type, TagDescriptor descriptor)
    {
        if (this._jsonContexts is { Count: > 0 })
        {
            foreach (var context in this._jsonContexts)
            {
                if (context.GetTypeInfo(type) is { } typeInfo)
                {
                    return typeInfo;
                }
            }

            throw new TagsProjectConfigurationException(
                $"已用 AddJsonSerializerContext 登记了 JsonSerializerContext，但其中没有 {type.FullName} 的元数据；"
                + "请把它加进某个上下文的 [JsonSerializable]（或再登记一个覆盖它的上下文）。",
                $"Tag({descriptor.TagName})");
        }

        // 运行至此，说明没有登记任何 JsonSerializerContext，或者登记了但都没有该类型的元数据。
        // 宿主禁用了反射序列化(e.g. AOT场景)时无法退化，需要提前丢出异常
        JsonReflectionCompat.EnsureReflectionSupported(type, $"Tag({descriptor.TagName})", JsonReflectionCompat.IsSupported);

        return null;
    }

    /// <inheritdoc/>
    protected override ITag Fallback(ITagChannel ch)
    {
        var factory = new SimpleFilesDirectTagFactory(this.Parent.IntoTagContainer());
        var tag = factory.Create(this.TagDescriptor, this.Channel as SimpleFilesTagChannel);

        return tag;
    }
}
