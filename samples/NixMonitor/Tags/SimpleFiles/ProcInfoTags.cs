using System.Text.Json;
using StdUnit.Tags;
using StdUnit.Tags.SimpleFiles;

namespace NixMonitor.Tags.SimpleTags;

/// <summary>
/// 解析 Linux <c>/proc/meminfo</c> 的特化测点。<br/>
/// 测点值类型为 <see cref="MemInfo"/>；文件不存在（如非 Linux 环境）时保持空值，由基类静默跳过。
/// </summary>
public sealed class MemInfoTag : SimpleFilesDirectTagBase<MemInfo>
{
    /// <summary>
    /// c'tor
    /// </summary>
    public MemInfoTag(TagDescriptor descriptor, SimpleFilesTagChannel? thisChannel, TagContainer container)
        : base(descriptor, thisChannel, container)
    {
    }

    /// <inheritdoc/>
    protected override MemInfo? ParseValue(string text)
        => string.IsNullOrWhiteSpace(text) ? null : MemInfo.Parse(text);

    /// <inheritdoc/>
    protected override string FormatValue(MemInfo? value)
        => JsonSerializer.Serialize(value, ProcTagJson.Options);
}

/// <summary>
/// 解析 Linux <c>/proc/cpuinfo</c> 的特化测点。<br/>
/// 测点值类型为 <see cref="CpuInfo"/>；文件不存在（如非 Linux 环境）时保持空值，由基类静默跳过。
/// </summary>
public sealed class CpuInfoTag : SimpleFilesDirectTagBase<CpuInfo>
{
    /// <summary>
    /// c'tor
    /// </summary>
    public CpuInfoTag(TagDescriptor descriptor, SimpleFilesTagChannel? thisChannel, TagContainer container)
        : base(descriptor, thisChannel, container)
    {
    }

    /// <inheritdoc/>
    protected override CpuInfo? ParseValue(string text)
        => string.IsNullOrWhiteSpace(text) ? null : CpuInfo.Parse(text);

    /// <inheritdoc/>
    protected override string FormatValue(CpuInfo? value)
        => JsonSerializer.Serialize(value, ProcTagJson.Options);
}

/// <summary>
/// 解析 Linux <c>/proc/loadavg</c> 的特化测点。<br/>
/// 测点值类型为 <see cref="LoadAvg"/>；文件不存在（如非 Linux 环境）时保持空值，由基类静默跳过。
/// </summary>
public sealed class LoadAvgTag : SimpleFilesDirectTagBase<LoadAvg>
{
    /// <summary>
    /// c'tor
    /// </summary>
    public LoadAvgTag(TagDescriptor descriptor, SimpleFilesTagChannel? thisChannel, TagContainer container)
        : base(descriptor, thisChannel, container)
    {
    }

    /// <inheritdoc/>
    protected override LoadAvg? ParseValue(string text)
        => string.IsNullOrWhiteSpace(text) ? null : LoadAvg.Parse(text);

    /// <inheritdoc/>
    protected override string FormatValue(LoadAvg? value)
        => JsonSerializer.Serialize(value, ProcTagJson.Options);
}

/// <summary>
/// /proc 测点共用的 JSON 序列化选项（缩进输出，便于查看默认文件内容）
/// </summary>
internal static class ProcTagJson
{
    public static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
}
