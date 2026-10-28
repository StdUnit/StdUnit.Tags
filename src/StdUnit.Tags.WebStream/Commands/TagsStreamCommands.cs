using System.Globalization;
using Microsoft.AspNetCore.Http;

namespace StdUnit.Tags.WebStream.Internal;

/// <summary>
/// 写命令的结果：HTTP 状态码 + 错误文本。
/// </summary>
internal readonly record struct CommandOutcome(int StatusCode, string? Error)
{
    public static CommandOutcome Ok { get; } = new(StatusCodes.Status200OK, null);

    public static CommandOutcome BadRequest(string error) => new(StatusCodes.Status400BadRequest, error);

    public static CommandOutcome Conflict(string error) => new(StatusCodes.Status409Conflict, error);

    public static CommandOutcome Timeout(string error) => new(StatusCodes.Status504GatewayTimeout, error);

    public static CommandOutcome Failure(string error) => new(StatusCodes.Status500InternalServerError, error);
}

/// <summary>
/// 写命令：改测点值、标记未扫描。<br/>
/// 所有变更都通过 <see cref="ITagsProject.WriteIntent(string, TagGrpWriteIntent, out Task)"/> 投递到对应入口的轮询线程上执行——
/// <b>绝不在请求线程上直接改测点</b>（那会与轮询并发读写同一份状态）。
/// </summary>
internal static class TagsStreamCommands
{
    /// <summary>改测点值。</summary>
    public static async Task<CommandOutcome> WriteValueAsync(
        ITagsProjectCtrl ctrl,
        WriteTagValueRequest request,
        TimeSpan timeout
    )
    {
        if (!TryResolve(ctrl, request.Path, out var target, out var failure))
        {
            return failure;
        }

        if (target.Tag.IsReadOnly())
        {
            return CommandOutcome.Conflict($"测点只读（access=RO），不能写入：{target.Tag.TagName()}");
        }

        if (!TryParseValue(target.Tag, request.Raw, out var value, out var parseError))
        {
            return CommandOutcome.BadRequest(parseError!);
        }

        return await ExecuteAsync(target, () => target.Tag.Value = value, timeout).ConfigureAwait(false);
    }

    /// <summary>标记未扫描（R1W 测点会因此强制重读一次）。</summary>
    public static Task<CommandOutcome> RescanAsync(
        ITagsProjectCtrl ctrl,
        RescanTagRequest request,
        TimeSpan timeout)
    {
        if (!TryResolve(ctrl, request.Path, out var target, out var failure))
        {
            return Task.FromResult(failure);
        }

        return ExecuteAsync(target, () => ResetScanned(target.Tag), timeout);
    }

    /// <summary>
    /// 与 Blazor 组件里的语义一致：子测点属于组合测点时，标记<b>组合</b>未扫描（冒泡），否则标记测点本身。
    /// </summary>
    internal static void ResetScanned(ITag tag)
    {
        var container = tag.Parent;
        if (container is not null && container.IsTagCbnt)
        {
            container.Map(
                static cbnt =>
                {
                    cbnt.IsScanned = false;
                    return 0;
                },
                static _ => 0);
            return;
        }

        tag.IsScanned = false;
    }

    private static async Task<CommandOutcome> ExecuteAsync(Target target, Action mutate, TimeSpan timeout)
    {
        Task completion;
        try
        {
            var accepted = target.Project.WriteIntent(
                target.Entry.TagName(),
                (_, _) =>
                {
                    mutate();
                    return ValueTask.CompletedTask;
                },
                out completion);

            if (!accepted)
            {
                // 队列满：WriteIntent 已把 completion 置为异常，这里只取它的消息
                await ObserveAsync(completion).ConfigureAwait(false);
                return CommandOutcome.Conflict("该入口的写入意图队列已满，请稍后重试");
            }
        }
        catch (Exception ex)
        {
            return CommandOutcome.Failure(ex.Message);
        }

        var finished = await Task.WhenAny(completion, Task.Delay(timeout)).ConfigureAwait(false);
        if (!ReferenceEquals(finished, completion))
        {
            return CommandOutcome.Timeout(
                $"等待 {(int)timeout.TotalSeconds}s 仍未被执行：入口可能被禁用或不轮询（意图会在它恢复轮询后执行）");
        }

        try
        {
            await completion.ConfigureAwait(false);
            return CommandOutcome.Ok;
        }
        catch (Exception ex)
        {
            return CommandOutcome.Failure(ex.Message);
        }
    }

    private static async Task ObserveAsync(Task task)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch
        {
            // 只是为了观察异常，具体消息在调用点统一给出
        }
    }

    private static bool TryResolve( ITagsProjectCtrl ctrl, string? path, out Target target, out CommandOutcome failure)
    {
        target = default;
        failure = default;

        if (string.IsNullOrWhiteSpace(path))
        {
            failure = CommandOutcome.BadRequest("path 不能为空");
            return false;
        }

        var project = ctrl.Project;
        if (project is null)
        {
            failure = CommandOutcome.Conflict("项目未运行");
            return false;
        }

        ITag tag;
        try
        {
            if (project.Tags.Descendant(path) is not TagUnion.TagUnit unit)
            {
                failure = CommandOutcome.BadRequest($"不是叶测点，无法对其操作：{path}");
                return false;
            }

            tag = unit.Value;
        }
        catch (Exception ex) when (ex is KeyNotFoundException or InvalidOperationException or ArgumentException)
        {
            failure = CommandOutcome.BadRequest($"测点不存在：{path}");
            return false;
        }

        var entry = tag.SearchEntry();
        if (entry is null)
        {
            failure = CommandOutcome.Conflict($"测点不在任何入口内（不会被轮询）：{path}");
            return false;
        }

        target = new Target(project, tag, entry);
        return true;
    }

    private static bool TryParseValue(ITag tag, string? raw, out object value, out string? error)
    {
        value = null!;
        error = null;

        if (raw is null)
        {
            error = "raw 不能为空";
            return false;
        }

        var kind = tag.TagKind();
        try
        {
            switch (kind)
            {
                case BuiltinTagKinds.BYTE:
                    value = byte.Parse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture);
                    return true;
                case BuiltinTagKinds.INT16:
                    value = short.Parse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture);
                    return true;
                case BuiltinTagKinds.UINT16:
                    value = ushort.Parse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture);
                    return true;
                case BuiltinTagKinds.INT32:
                    value = int.Parse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture);
                    return true;
                case BuiltinTagKinds.UINT32:
                    value = uint.Parse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture);
                    return true;
                case BuiltinTagKinds.INT64:
                    value = long.Parse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture);
                    return true;
                case BuiltinTagKinds.UINT64:
                    value = ulong.Parse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture);
                    return true;
                case BuiltinTagKinds.FLOAT:
                    value = float.Parse(raw, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture);
                    return true;
                case BuiltinTagKinds.DOUBLE:
                    value = double.Parse(raw, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture);
                    return true;
                case BuiltinTagKinds.BIT:
                case BuiltinTagKinds.DI:
                case BuiltinTagKinds.DO:
                    if (!TryParseBool(raw, out var flag))
                    {
                        error = $"无法解析为布尔值（接受 true/false/1/0）：{raw}";
                        return false;
                    }

                    value = flag;
                    return true;
                case BuiltinTagKinds.STR:
                    value = raw;
                    return true;
                default:
                    // 未登记的类型（如驱动自定义的 JSON 测点）不接受文本写入
                    error = $"不支持用文本写入的类型：{kind}（当前值类型：{tag.Value?.GetType().Name ?? "null"}）";
                    return false;
            }
        }
        catch (Exception ex) when (ex is FormatException or OverflowException)
        {
            error = $"无法解析为 {kind}：{raw}";
            return false;
        }
    }

    private static bool TryParseBool(string raw, out bool value)
    {
        if (bool.TryParse(raw, out value))
        {
            return true;
        }

        if (raw == "1")
        {
            value = true;
            return true;
        }

        if (raw == "0")
        {
            value = false;
            return true;
        }

        return false;
    }

    private readonly struct Target
    {
        public Target(ITagsProject project, ITag tag, ITagGrp entry)
        {
            this.Project = project;
            this.Tag = tag;
            this.Entry = entry;
        }

        public ITagsProject Project { get; }

        public ITag Tag { get; }

        public ITagGrp Entry { get; }
    }
}
