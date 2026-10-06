
namespace StdUnit.Tags.SimpleFiles;

/// <summary>
/// SimpleFiles 直接测点基类
/// </summary>
/// <typeparam name="T"></typeparam>
public abstract class SimpleFilesDirectTagBase<T> : Tag<T, SimpleFilesTagChannel>
{


    /// <summary>
    /// c'tor
    /// </summary>
    /// <param name="descriptor"></param>
    /// <param name="thisChannel"></param>
    /// <param name="container"></param>
    protected SimpleFilesDirectTagBase(TagDescriptor descriptor, SimpleFilesTagChannel? thisChannel, TagContainer container)
        : base(descriptor, thisChannel, container)
    {
        // 旧 key 自 1.0 起不再支持，但也不静默忽略：升级后文件不再自动创建却不报错，会变成难以定位的运行期怪象。
        if (descriptor.Extras.ContainsKey(LegacyAutoCreateFileAttrName))
        {
            throw new TagsProjectConfigurationException(
                $"SimpleFiles 测点使用了已移除的 XML 属性 {LegacyAutoCreateFileAttrName}，请改为 {AutoCreateFileAttrName}",
                $"Tag({descriptor.TagName})");
        }
    }

    private bool? _autoCreateFile;

    /// <summary>
    /// XML 属性 key：是否自动创建文件（camelCase）。
    /// </summary>
    private const string AutoCreateFileAttrName = "autoCreateFile";

    /// <summary>
    /// 旧 XML 属性 key（已移除，仅用于给出迁移提示）。
    /// </summary>
    private const string LegacyAutoCreateFileAttrName = "AutoCreateFile";

    /// <summary>
    /// 是否自动创建文件
    /// </summary>
    public virtual bool AutoCreateFile
    {
        get
        {
            if (this._autoCreateFile.HasValue)
            {
                return this._autoCreateFile.Value;
            }

            if (!this.TagDescriptor.Extras.TryGetValue(AutoCreateFileAttrName, out var autoCreateFileValue))
            {
                this._autoCreateFile = false;
                return false;
            }

            if (!bool.TryParse(autoCreateFileValue.Value, out var autoCreateFile))
            {
                throw new TagsProjectXmlException(
                    $"测点配置了 {AutoCreateFileAttrName}，但无法解析为布尔值：{autoCreateFileValue.Value}",
                    $"Tag({this.TagName()})");
            }

            this._autoCreateFile = autoCreateFile;
            return autoCreateFile;
        }
    }

    /// <inheritdoc/>
    public override async Task ReadAsync(CancellationToken ct)
    {
        var path = this.NormalizedAddress();
        if (!File.Exists(path))
        {
            if (this.AutoCreateFile)
            {
                await CreateAndWriteDefaultAsync(path, ct);
            }
            return;
        }
        var text = await Compat.FileAsyncCompat.ReadAllTextAsync(path, ct);
        T? value;
        try
        {
            value = this.ParseValue(text);
        }
        catch (InvalidDataException ex)
        {
            // ParseValue 看不到文件路径（子类只拿到文本），这里补上测点与文件，否则系统里几十个文件时无法定位是哪一个
            throw new InvalidDataException($"Tag({this.TagName()}) 解析文件内容失败：文件={path}；{ex.Message}", ex);
        }
        this._value = value;
        this.Timestamp = DateTime.UtcNow;
        this.NotifyTagRead(value);
    }

    /// <summary>
    /// 解析文件内容为测点值。<br/>
    /// 返回 <c>null</c> 是合法的：对于引用类型的 <typeparamref name="T"/>（如 JSON POCO），
    /// 文件内容可能表示 null（如 JSON 字面量 <c>null</c>），此时返回 null 即可，<see cref="FormatValue"/> 会对称地序列化回去。<br/>
    /// 对于值类型的 <typeparamref name="T"/>，返回值即为 <c>T</c> 本身（无约束泛型的 <c>T?</c> 不会变成 <c>Nullable&lt;T&gt;</c>）。
    /// </summary>
    /// <param name="text"></param>
    /// <returns></returns>
    protected abstract T? ParseValue(string text);

    /// <summary>
    /// 将测点值格式化为文件内容。<br/>
    /// 与 <see cref="ParseValue"/> 对称：<see cref="ParseValue"/> 负责 文本→值，本方法负责 值→文本。<br/>
    /// 默认实现使用 <see cref="object.ToString()"/>，基元类型（int/float 等）无需重写；
    /// 自定义类型（如 JSON POCO）应重写本方法，并处理 <paramref name="value"/> 为 null 的情况（自动创建默认文件时会以 <c>default(T)</c> 调用）。
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    protected virtual string FormatValue(T? value)
    {
        return value?.ToString() ?? string.Empty;
    }

    /// <summary>
    /// 创建文件并写入默认值。<br/>
    /// 默认内容由 <see cref="FormatValue"/> 生成（以 <c>default(T)</c> 调用）。
    /// </summary>
    /// <param name="path"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    protected virtual async Task CreateAndWriteDefaultAsync(string path, CancellationToken ct)
    {
        var data = this.FormatValue(default);
        await Compat.FileAsyncCompat.WriteAllTextAsync(path, data, ct);
        return;
    }

    /// <inheritdoc/>
    public override async Task WriteAsync(CancellationToken ct)
    {
        var path = this.NormalizedAddress();
        if (!File.Exists(path))
        {
            if (!this.AutoCreateFile)
            {
                return;
            }
            // 自动创建文件后继续往下执行写入
            await this.CreateAndWriteDefaultAsync(path, ct);
        }
        var value = this.FormatValue(this._value);
        await Compat.FileAsyncCompat.WriteAllTextAsync(path, value, ct);
        this.NotifyTagWritten(value);
        this.IsDirty = false;
    }
}



