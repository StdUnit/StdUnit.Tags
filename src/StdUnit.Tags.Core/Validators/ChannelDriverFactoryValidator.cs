using System.Xml.Linq;

namespace StdUnit.Tags;

/// <summary>
/// <see cref="ITagsProjectValidator"/> 的一个实现：校验 <c>&lt;Channel&gt;</c> 的 <c>driver</c> 属性
/// 必须对应已注册的通道工厂（<see cref="ITagChannelFactory"/>）。拼错的驱动名在加载期报错，
/// 而不是运行时（<see cref="CompositeTagChannelFactory.Create"/>）才暴露。
/// <para>
/// 通过构造器注入 <see cref="ITagChannelFactory"/>（DI 单例，即 <see cref="CompositeTagChannelFactory"/>），
/// 用其 <c>GetAvailableDrivers()</c> 判断 driver 是否已注册。第三方驱动注册自己的工厂后，
/// 本校验器自动识别其 driver。
/// </para>
/// </summary>
public class ChannelDriverFactoryValidator : ITagsProjectValidator
{
    private readonly ITagChannelFactory _channelFactory;

    /// <summary>
    /// c'tor
    /// </summary>
    /// <param name="channelFactory">通道工厂（DI 单例，即 <see cref="CompositeTagChannelFactory"/>）</param>
    public ChannelDriverFactoryValidator(ITagChannelFactory channelFactory)
    {
        this._channelFactory = channelFactory;
    }

    /// <inheritdoc/>
    public void Validate(XElement root)
    {
        // 已注册的驱动名集合（CompositeTagChannelFactory.GetAvailableDrivers 汇总全部工厂）
        var registeredDrivers = this._channelFactory.GetAvailableDrivers().ToHashSet(StringComparer.Ordinal);

        var errors = new List<string>();

        foreach (var channel in root.Elements("Channel"))
        {
            var driver = channel.Attribute("driver")?.Value;
            // 本来这个地方是 string.IsNullOrEmpty
            // 但是 net472 引用程序集无可空标注，编译器看不到 [NotNullWhen(false)]，无法收缩类型（会报 CS8603）
            if (driver is null || driver.Length == 0)
            {
                continue; // driver 缺失由 XSD 校验（ChannelType 的 driver 为必填）负责
            }
            if (!registeredDrivers.Contains(driver))
            {
                var name = channel.Attribute("name")?.Value ?? "?";
                errors.Add($"通道 '{name}' 的 driver='{driver}' 未注册对应的 ITagChannelFactory（已注册: {string.Join(", ", registeredDrivers.OrderBy(d => d, StringComparer.Ordinal))}）");
            }
        }

        if (errors.Count > 0)
        {
            throw new TagsProjectSchemaException(errors);
        }
    }
}
