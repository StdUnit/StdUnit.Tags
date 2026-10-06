namespace StdUnit.Tags;

/// <summary>
/// 测点项目<b>加载期</b>错误的基类：解析 XML、解析地址、构建测点树、执行加载期校验时抛出。<br/>
/// <br/>
/// 这类错误的共同点是：<b>都源于项目配置（XML），且都应该在启动阶段快速失败</b>，
/// 而不是等到轮询时才暴露。把所有加载期错误收口到一个异常族，调用方一个 catch 就能兜住：
/// <code>
/// try
/// {
///     var proj = factory.Create(projRoot);
/// }
/// catch (TagsProjectLoadException ex)
/// {
///     logger.LogError(ex, "项目加载失败：{Location}", ex.Location);
/// }
/// </code>
/// 具体的子类见 <see cref="TagsProjectXmlException"/>（XML 值不合法）、
/// <see cref="TagsProjectAddressException"/>（地址无法解析）、
/// <see cref="TagsProjectConfigurationException"/>（配置语义不自洽）、
/// <see cref="TagsProjectValidationException"/>（校验器聚合错误）。
/// <para>
/// <b>注意范围</b>：本类型只覆盖<b>加载期</b>。运行期（读写 / 连接 / 清理）错误不使用自定义类型，
/// 一律是语义最贴近的 BCL 类型（<see cref="ArgumentException"/>、<see cref="InvalidOperationException"/>、
/// <see cref="InvalidCastException"/>、<see cref="KeyNotFoundException"/> 等），
/// 一个核心的决策依据是，不管异常来自哪里，以及是什么原因导致，runner 都应该会统一 <c>catch (Exception)</c> 交给重试策略与 <c>RunnerCrashed</c>——
/// 所以<b>不存在</b>一个"能兜住本库全部异常"的根类型，请按加载期/运行期分别处理。
/// </para>
/// </summary>
public abstract class TagsProjectLoadException : Exception
{
    /// <summary>
    /// c'tor
    /// </summary>
    /// <param name="message">错误消息</param>
    /// <param name="location">
    /// 出错位置描述（XML 元素路径 / 测点路径 / 通道名等），可为 null。<br/>
    /// 例如 <c>TagGrp(产线1)/TagCbnt(输入)/Tag(bit)</c>、<c>Channel(S7-3)</c>。
    /// </param>
    /// <param name="innerException">内部异常，可为 null</param>
    protected TagsProjectLoadException(string message, string? location = null, Exception? innerException = null)
        : base(message, innerException)
    {
        this.Location = location;
    }

    /// <summary>
    /// 出错位置描述（XML 元素路径 / 测点路径 / 通道名等），可能为 null。
    /// </summary>
    public string? Location { get; }
}
