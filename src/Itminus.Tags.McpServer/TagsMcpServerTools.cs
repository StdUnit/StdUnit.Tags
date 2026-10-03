using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace Itminus.Tags.McpServer;

/// <summary>
/// 基于 Itminus.Tags 的 MCP 工具集，提供对已运行项目的测点读写操作。
/// </summary>
[McpServerToolType]
public class TagsMcpServerTools
{
    private readonly ITagsProjectCtrl _ctrl;
    private readonly ILogger<TagsMcpServerTools> _logger;

    /// <summary>创建 <see cref="TagsMcpServerTools"/> 实例。</summary>
    public TagsMcpServerTools(ITagsProjectCtrl ctrl, ILogger<TagsMcpServerTools> logger)
    {
        _ctrl = ctrl;
        _logger = logger;
    }

    #region
    /// <summary>
    /// 描述当前运行项目静态信息，返回一段 XML，其中描述了各个通道、层级式的测点点位。
    /// </summary>
    [McpServerTool]
    [Description(
        "列出当前运行的测点项目静态描述信息(XML)，包括各个通道、层级式的测点点位等。这个静态结构描述，为后续所有操作提供了必要上下文信息。" +
        "尤其是从顶级`<TagGrp>`开始，以 '/' 分隔各级元素的`name`，形成一个路径。这些Tag的路径是对相关Tag进行读、写点位时必须提供的的参数。"
    )]
    public string DescribeProject()
    {
        var root = this._ctrl.Project?.GetRootElement();
        return root?.ToString() ?? "there's no project yet. You should start it before you go on";
    }
    #endregion


    #region  读取测点
    /// <summary>
    /// 按完整路径读取单个测点的值，同时返回元数据（类型、访问模式、时间戳等）。
    /// 路径必须从项目描述(XML)根元素下的顶层`TagGrp`开始逐层往下，每层应该使用`/`而不是`.`来分隔。
    /// </summary>
    /// <param name="path">测点的完整路径，路径来自项目静态描述文件，用各级元素的"name"形成一个路径，以 "/" 分隔层级，如 "IoBox/通用状态/PLC/心跳请求"。</param>
    [McpServerTool]
    [Description("按完整路径读取测点的值。其中路径类似于'topGrpName/subGrpName/.../optionalCbntName/tagName'")]
    public TagValue ReadTagValue(string path)
    {
        var proj = EnsureProject();
        ITag tag;
        try { tag = proj.Tags.SelectTag(path); }
        catch (Exception ex) { throw new InvalidOperationException($"Failed to find tag at path '{path}': {ex.Message}"); }
        return BuildTagValue(tag);
    }

    /// <summary>
    /// 按完整路径批量读取多个测点的值。
    /// 路径必须从项目描述(XML)根元素下的顶层`TagGrp`开始逐层往下，每层应该使用`/`而不是`.`来分隔。
    /// </summary>
    /// <param name="paths">测点的完整路径数组，每个路径以 "/" 分隔层级。</param>
    [McpServerTool]
    [Description("按完整路径批量读取测点的值。其中路径类似于'topGrpName/subGrpName/.../optionalCbntName/tagName'")]
    public List<TagValue> ReadTagValues(string[] paths)
    {
        var proj = EnsureProject();
        var results = new List<TagValue>();
        foreach (var path in paths)
        {
            try
            {
                var tag = proj.Tags.SelectTag(path);
                results.Add(BuildTagValue(tag));
            }
            catch (Exception ex)
            {
                results.Add(new TagValue(null, null, DateTime.MinValue, ex.Message));
            }
        }
        return results;
    }

    #endregion

    #region  写入测点
    /// <summary>
    /// 按完整路径写入单个测点的值，会根据 TagKind 自动转换类型。
    /// 路径必须从项目描述(XML)根元素下的顶层`TagGrp`开始逐层往下，每层应该使用`/`而不是`.`来分隔。
    /// </summary>
    /// <param name="path">测点的完整路径。</param>
    /// <param name="value">要写入的值，需要与测点类型兼容（BIT→bool，INT16→short，STR→string 等）。</param>
    /// <param name="waitForCompletion">是否等待写入完成，默认为 true。</param>
    [McpServerTool]
    [Description("按完整路径写入测点的值。")]
    public async Task<WriteResult> WriteTagValue(
        [Description("从顶层TagGrp导航到子元素的路径，用`/`分隔元素名，类似于'topGrpName/subGrpName/.../optionalCbntName/tagName'")] string path,
        [Description("要写入的目标值")] string value,
        [Description("是否要等待写入完成")] bool waitForCompletion = true)
    {
        var proj = EnsureProject();

        ITag tag;
        try { tag = proj.Tags.SelectTag(path); }
        catch (Exception ex) { return WriteResult.Fail($"Failed to find tag at path '{path}': {ex.Message}"); }

        if (tag.SearchAccessMode() == TagAccessMode.RO)
            return WriteResult.Fail($"Tag '{path}' is read-only (access mode: RO). Cannot write.");

        var entry = tag.SearchEntry();
        if (entry is null)
            return WriteResult.Fail($"Could not find an entry group containing tag '{path}'.");

        if (!TryParseValue(value, tag.TagKind(), out var convertedValue))
            return WriteResult.Fail($"Failed to convert value for tag '{path}'.");

        TagGrpWriteIntent intent = (_, _) =>
        {
            tag.Value = convertedValue;
            // net472 的 ValueTask 没有 CompletedTask（该属性在 .NET Core 2.1+），用 default 等价。
            return default;
        };

        if (!proj.WriteIntent(entry.TagName(), intent, out var task))
            return WriteResult.Fail($"Failed to write intent for tag '{path}' in entry '{entry.TagName()}'. Intent queue may be full.");

        if (waitForCompletion)
        {
            try
            {
                await task;
                return WriteResult.Ok($"Successfully wrote tag '{path}' with value: {convertedValue}");
            }
            catch (Exception ex)
            {
                return WriteResult.Fail($"Write intent for tag '{path}' failed: {ex.Message}");
            }
        }

        return WriteResult.Ok($"Write intent for tag '{path}' queued successfully (not waiting for completion).");
    }

    /// <summary>
    /// 按完整路径批量写入多个测点的值。
    /// 内部按入口组（entry）分组，每个入口组合并为一个 WriteIntent，以提高效率。
    /// 路径必须从项目描述(XML)根元素下的顶层`TagGrp`开始逐层往下，使用 `name` attribute 作为路径，路径应该使用`/`而不是`.`来分隔。
    /// </summary>
    /// <param name="tagValues">字典，key 为测点完整路径，value 为要写入的值。</param>
    /// <param name="waitForCompletion">是否等待所有写入意图完成，默认为 true。</param>
    [McpServerTool]
    [Description("按完整路径批量写入测点的值。其中路径类似于'topGrpName/subGrpName/.../optionalCbntName/tagName'")]
    public async Task<WriteResult> WriteTagValues(
        [Description("键值对，键名代表从顶层TagGrp导航到子元素的路径，用`/`分隔元素名，类似于'topGrpName/subGrpName/.../optionalCbntName/tagName'；键值是用字符串表示的目标值")] Dictionary<string, string> tagValues,
        [Description("是否要等待写入完成")] bool waitForCompletion = true)
    {
        var proj = EnsureProject();

        // Phase 1: resolve all tags and group by entry
        var perEntry = new Dictionary<string, List<(string TagPath, ITag Tag, object? ConvertedValue)>>();
        foreach (var kvp in tagValues)
        {
            var path = kvp.Key;
            ITag tag;
            try { tag = proj.Tags.SelectTag(path); }
            catch (Exception ex) { return WriteResult.Fail($"Failed to find tag at '{path}': {ex.Message}"); }

            if (tag.SearchAccessMode() == TagAccessMode.RO)
                return WriteResult.Fail($"Tag '{path}' is read-only. Aborting batch write.");

            var entry = tag.SearchEntry();
            if (entry is null)
                return WriteResult.Fail($"Could not find an entry group containing tag '{path}'.");

            if (!TryParseValue(kvp.Value, tag.TagKind(), out var convertedValue))
                return WriteResult.Fail($"Failed to convert value for tag '{path}'.");

            if (!perEntry.TryGetValue(entry.TagName(), out var list))
            {
                list = new List<(string, ITag, object?)>();
                perEntry[entry.TagName()] = list;
            }
            list.Add((path, tag, convertedValue));
        }

        // Phase 2: issue one intent per entry, collect tasks
        var tasks = new List<Task>();
        var results = new List<string>();
        // net472 的 KeyValuePair<,> 没有 Deconstruct（该方法是 .NET Core 2.0+），故显式取 Key/Value。
        foreach (var kvp in perEntry)
        {
            var entryName = kvp.Key;
            var writes = kvp.Value;
            var tagsToWrite = writes.Select(w => (w.Tag, w.ConvertedValue)).ToList();
            TagGrpWriteIntent intent = (_, _) =>
            {
                foreach (var (t, cv) in tagsToWrite)
                    t.Value = cv;
                return default;
            };

            if (!proj.WriteIntent(entryName, intent, out var task))
                return WriteResult.Fail($"Failed to write batch intent for entry '{entryName}'. Intent queue may be full.");

            tasks.Add(task);
            foreach (var (tagPath, _, cv) in writes)
                results.Add($"{tagPath}={cv}");
        }

        if (waitForCompletion)
        {
            try
            {
                await Task.WhenAll(tasks);
                return WriteResult.Ok($"Successfully wrote {results.Count} tag(s) across {perEntry.Count} entry group(s): {string.Join(", ", results)}");
            }
            catch (Exception ex)
            {
                return WriteResult.Fail($"Batch write failed: {ex.Message}");
            }
        }

        return WriteResult.Ok($"Write intents for {results.Count} tag(s) across {perEntry.Count} entry group(s) queued successfully.");
    }
    #endregion



    #region 内部辅助函数
    private ITagsProject EnsureProject() =>
        _ctrl.Project ??
        throw new InvalidOperationException("No project is running.");

    private static TagValue BuildTagValue(ITag tag)
    {
        try
        {
            return new TagValue(tag.TagDescriptor, tag.Value, tag.Timestamp);
        }
        catch (Exception ex)
        {
            return new TagValue(tag.TagDescriptor, null, tag.Timestamp, ex.Message);
        }
    }

    private static bool TryParseValue(string value, TagKinds tagKind, out object? result)
    {
        if (value is null)
        {
            result = null;
            return false;
        }

        switch (tagKind)
        {
            case BuiltinTagKinds.BIT:
                var parsedBit = Boolean.TryParse(value, out var bitResult);
                result = bitResult;
                return parsedBit;
            case BuiltinTagKinds.BYTE:
                var parsedByte = Byte.TryParse(value, out var byteResult);
                result = byteResult;
                return parsedByte;
            case BuiltinTagKinds.INT16:
                var parsedInt16 = Int16.TryParse(value, out var int16Result);
                result = int16Result;
                return parsedInt16;
            case BuiltinTagKinds.UINT16:
                var parsedUInt16 = UInt16.TryParse(value, out var uint16Result);
                result = uint16Result;
                return parsedUInt16;
            case BuiltinTagKinds.INT32:
                var parsedInt32 = Int32.TryParse(value, out var int32Result);
                result = int32Result;
                return parsedInt32;
            case BuiltinTagKinds.UINT32:
                var parsedUInt32 = UInt32.TryParse(value, out var uint32Result);
                result = uint32Result;
                return parsedUInt32;
            case BuiltinTagKinds.INT64:
                var parsedInt64 = Int64.TryParse(value, out var int64Result);
                result = int64Result;
                return parsedInt64;
            case BuiltinTagKinds.UINT64:
                var parsedUInt64 = UInt64.TryParse(value, out var uint64Result);
                result = uint64Result;
                return parsedUInt64;
            case BuiltinTagKinds.FLOAT:
                var parsedFloat = Single.TryParse(value, out var floatResult);
                result = floatResult;
                return parsedFloat;
            case BuiltinTagKinds.DOUBLE:
                var parsedDouble = Double.TryParse(value, out var doubleResult);
                result = doubleResult;
                return parsedDouble;
            case BuiltinTagKinds.STR:
                // 上面的 value is null 守卫已排除 null，此处直接透传
                result = value;
                return true;
            case BuiltinTagKinds.DI:
                var parsedDI = Boolean.TryParse(value, out var diResult);
                result = diResult;
                return parsedDI;
            case BuiltinTagKinds.DO:
                var parsedDO = Boolean.TryParse(value, out var doResult);
                result = doResult;
                return parsedDO;
            default:
                result = value;
                return true;
        };
    }

    #endregion
}


#region 结果
/// <summary>测点读取结果。</summary>
/// <param name="Descriptor">测点元数据（名称、类型、地址、大小、访问模式等）。测点未找到时为 null。</param>
/// <param name="Value">当前测点值。</param>
/// <param name="Timestamp">最后一次读取的时间戳。</param>
/// <param name="Error">读取失败时的错误信息。</param>
public record TagValue(TagDescriptor? Descriptor, object? Value, DateTime Timestamp, string? Error = null);

/// <summary>写入操作结果。</summary>
/// <param name="Success">是否写入成功。</param>
/// <param name="Message">描述信息。</param>
public record WriteResult(bool Success, string Message)
{
    /// <summary>创建成功结果。</summary>
    public static WriteResult Ok(string message) => new(true, message);

    /// <summary>创建失败结果。</summary>
    public static WriteResult Fail(string message) => new(false, message);
}
#endregion

