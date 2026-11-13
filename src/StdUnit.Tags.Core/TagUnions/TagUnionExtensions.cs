namespace StdUnit.Tags;

/// <summary>
/// extensions for <see cref="TagUnion"/>
/// </summary>
public static class TagUnionExtensions
{
    #region R/W
    /// <summary>
    /// TagUnion 从底层读取。<br/>
    /// 不检查使能（等价于 <see cref="TraversalMode.IgnoreEnabled"/>），因此被禁用的分组/组合也会被读取。
    /// </summary>
    /// <param name="tagunion"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public static Task ReadAsync(this TagUnion tagunion, CancellationToken ct) => 
        tagunion.ReadAsync(TraversalMode.IgnoreEnabled, ct);

    /// <summary>
    /// TagUnion 从底层读取，并可指定遍历模式（是否应用使能门控）。<br/>
    /// 本重载是唯一的实现，公开重载只是以 <see cref="TraversalMode.IgnoreEnabled"/> 调用它。
    /// </summary>
    /// <param name="tagunion"></param>
    /// <param name="mode">遍历模式</param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public static Task ReadAsync(this TagUnion tagunion, TraversalMode mode, CancellationToken ct)
    {
        // 不要改回 TagUnion.Map：每个分支都会构造闭包 + 委托 + 异步状态机（分配）。
        // 另外这里**不是** async 方法：常见路径（读同步完成）直接返回已完成 Task；
        // 只有真正异步（设备 I/O）时才走下面的两个辅助方法。
        switch (tagunion)
        {
            case TagUnion.TagUnit unit:
            {
                var tag = unit.Value;
                var access = tagunion.CachedAccessMode ??= tag.SearchAccessMode();
                if (access == TagAccessMode.R1W && tag.IsScanned)
                {
                    return Task.CompletedTask;
                }
                if (access == TagAccessMode.WO)
                {
                    return Task.CompletedTask;
                }

                var task = tag.ReadAsync(ct);
                if (task.Status == TaskStatus.RanToCompletion)
                {
                    tag.IsScanned = true;
                    return Task.CompletedTask;
                }

                return MarkScannedAfterReadAsync(tag, task);
            }
            case TagUnion.TagCbnt cbntUnion:
            {
                var cbnt = cbntUnion.Value;
                // 是否短路？
                if (mode == TraversalMode.RespectEnabled && !cbnt.IsEnabled)
                {
                    return Task.CompletedTask;
                }

                var access = tagunion.CachedAccessMode ??= cbnt.SearchAccessMode();
                if (access == TagAccessMode.R1W && cbnt.IsScanned)
                {
                    return Task.CompletedTask;
                }
                if (access == TagAccessMode.WO)
                {
                    return Task.CompletedTask;
                }

                var task = cbnt.ReadAsync(ct);
                if (task.Status == TaskStatus.RanToCompletion)
                {
                    cbnt.IsScanned = true;
                    return Task.CompletedTask;
                }

                return MarkScannedAfterReadAsync(cbnt, task);
            }
            case TagUnion.TagGrp grpUnion:
                return grpUnion.Value.ReadAsync(mode, ct);
            default:
                return Task.FromException(new NotImplementedException($"unknown types: {tagunion.GetType()}"));
        }
    }

    private static async Task MarkScannedAfterReadAsync(ITag tag, Task readTask)
    {
        await readTask.ConfigureAwait(false);
        tag.IsScanned = true;
    }

    private static async Task MarkScannedAfterReadAsync(ITagCbnt cbnt, Task readTask)
    {
        await readTask.ConfigureAwait(false);
        cbnt.IsScanned = true;
    }

    /// <summary>
    /// TagUnion 写入底层。<br/>
    /// 不检查使能（等价于 <see cref="TraversalMode.IgnoreEnabled"/>），因此被禁用的分组/组合也会被写入。
    /// </summary>
    /// <param name="tagunion"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public static Task WriteAsync(this TagUnion tagunion, CancellationToken ct) => tagunion.WriteAsync(TraversalMode.IgnoreEnabled, ct);

    /// <summary>
    /// TagUnion 写入底层，并可指定遍历模式（是否应用使能门控）。<br/>
    /// 本重载是唯一的实现，公开重载只是以 <see cref="TraversalMode.IgnoreEnabled"/> 调用它。
    /// </summary>
    /// <param name="tagunion"></param>
    /// <param name="mode">遍历模式</param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public static Task WriteAsync(this TagUnion tagunion, TraversalMode mode, CancellationToken ct)
    {
        // 同 ReadAsync：用 switch 而非 Map，避免逐节点构造闭包/委托/状态机；
        // 且本方法无需"读后再标记"，所以完全不需要 async——直接把子任务的 Task 返回即可。
        switch (tagunion)
        {
            case TagUnion.TagUnit unit:
            {
                var tag = unit.Value;
                var access = tagunion.CachedAccessMode ??= tag.SearchAccessMode();
                if (access == TagAccessMode.RO || !tag.IsDirty)
                {
                    return Task.CompletedTask;
                }

                return tag.WriteAsync(ct);
            }
            case TagUnion.TagCbnt cbntUnion:
            {
                var cbnt = cbntUnion.Value;
                // 使能门控：未使能的组合整块跳过（含其下的所有测点），其脏标记会被保留到重新使能后再刷写
                if (mode == TraversalMode.RespectEnabled && !cbnt.IsEnabled)
                {
                    return Task.CompletedTask;
                }

                var access = tagunion.CachedAccessMode ??= cbnt.SearchAccessMode();
                if (access == TagAccessMode.RO || !cbnt.IsDirty)
                {
                    return Task.CompletedTask;
                }

                return cbnt.WriteAsync(ct);
            }
            case TagUnion.TagGrp grpUnion:
                return grpUnion.Value.WriteAsync(mode, ct);
            default:
                return Task.FromException(new NotImplementedException($"unknown types: {tagunion.GetType()}"));
        }
    }

    /// <summary>
    /// 是否已脏
    /// </summary>
    /// <param name="tagunion"></param>
    /// <returns></returns>
    public static bool IsDirty(this TagUnion tagunion) => tagunion switch
    {
        TagUnion.TagUnit unit => unit.Value.IsDirty,
        TagUnion.TagCbnt cbnt => cbnt.Value.IsDirty,
        TagUnion.TagGrp grp => grp.Value.IsDirty(),
        _ => throw new NotImplementedException($"unknown types: {tagunion.GetType()}")
    };
    #endregion



    #region AsXyz()
    /// <summary>
    /// 转成 <see cref="ITag"/>，如果类型不对则抛出异常
    /// </summary>
    /// <param name="tagunion"></param>
    /// <returns></returns>
    /// <exception cref="InvalidCastException">当前节点不是直接测点</exception>
    public static ITag AsTag(this TagUnion tagunion) => tagunion.Map(
        tagunit => tagunit,
        tagcbnt => throw new InvalidCastException($"{tagcbnt.TagName()} is a {nameof(ITagCbnt)} intead of a {nameof(ITag)}"),
        taggrp => throw new InvalidCastException($"{taggrp.TagName()} is a {nameof(ITagGrp)} intead of a {nameof(ITag)}")
        );

    /// <summary>
    /// 转成 <see cref="ITagCbnt"/>，如果类型不对则抛出异常
    /// </summary>
    /// <param name="tagunion"></param>
    /// <returns></returns>
    /// <exception cref="InvalidCastException">当前节点不是测点组合</exception>
    public static ITagCbnt AsTagCbnt(this TagUnion tagunion) => tagunion.Map(
        tagunit => throw new InvalidCastException($"{tagunit.TagName()} is a {nameof(ITag)} intead of a {nameof(ITagCbnt)}"),
        tagcbnt => tagcbnt,
        taggrp => throw new InvalidCastException($"{taggrp.TagName()} is a {nameof(ITagGrp)} intead of a {nameof(ITagCbnt)}")
    );

    /// <summary>
    /// 转成 <see cref="ITagGrp"/>，如果类型不对则抛出异常
    /// </summary>
    /// <param name="tagunion"></param>
    /// <returns></returns>
    /// <exception cref="InvalidCastException">当前节点不是测点组</exception>
    public static ITagGrp AsTagGrp(this TagUnion tagunion) => tagunion.Map(
        tagunit => throw new InvalidCastException($"{tagunit.TagName()} is a {nameof(ITag)} intead of a {nameof(ITagGrp)}"),
        tagcbnt => throw new InvalidCastException($"{tagcbnt.TagName()} is a {nameof(ITagCbnt)} intead of a {nameof(ITagGrp)}"),
        taggrp => taggrp
    );
    #endregion

    #region IsXyzFlag()
    /// <summary>
    /// 是否是 TagUnit
    /// </summary>
    /// <param name="tagunion"></param>
    /// <returns></returns>
    public static bool IsTagUnit(this TagUnion tagunion) => tagunion.Map(
        tag => true,
        tagcbnt => false,
        taggrp => false
        );

    /// <summary>
    /// 是否是 TagCbnt
    /// </summary>
    /// <param name="tagunion"></param>
    /// <returns></returns>
    public static bool IsTagCbnt(this TagUnion tagunion) => tagunion.Map(
        tag => false,
        tagcbnt => true,
        taggrp => false
    );

    /// <summary>
    /// 是否是 TagGrp
    /// </summary>
    /// <param name="tagunion"></param>
    /// <returns></returns>
    public static bool IsTagGrp(this TagUnion tagunion) => tagunion.Map(
        tag => false,
        tagcbnt => false,
        taggrp => true
    );
    #endregion
}