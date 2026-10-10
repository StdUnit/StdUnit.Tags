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
    public static async Task ReadAsync(this TagUnion tagunion, TraversalMode mode, CancellationToken ct)
    {
        await tagunion.Map(
            async tag =>
            {
                if (tag.SearchAccessMode() == TagAccessMode.R1W && tag.IsScanned)
                {
                    return;
                }
                if (tag.IsWriteOnly())
                {
                    return;
                }
                await tag.ReadAsync(ct);
                tag.IsScanned = true;
            },
            async cbnt =>
            {
                // 是否短路？
                if (mode == TraversalMode.RespectEnabled && !cbnt.IsEnabled)
                {
                    return;
                }
                if (cbnt.SearchAccessMode() == TagAccessMode.R1W && cbnt.IsScanned)
                {
                    return;
                }
                if (cbnt.IsWriteOnly())
                {
                    return;
                }
                await cbnt.ReadAsync(ct);
                cbnt.IsScanned = true;
            },
            async grp =>
            {
                await grp.ReadAsync(mode, ct);
            }
         );
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
    public static async Task WriteAsync(this TagUnion tagunion, TraversalMode mode, CancellationToken ct)
    {
        await tagunion.Map(
            async tag =>
            {
                if (tag.IsReadOnly())
                {
                    return;
                }
                if (tag.IsDirty)
                {
                    await tag.WriteAsync(ct);
                }
            },
            async cbnt =>
            {
                // 使能门控：未使能的组合整块跳过（含其下的所有测点），其脏标记会被保留到重新使能后再刷写
                if (mode == TraversalMode.RespectEnabled && !cbnt.IsEnabled)
                {
                    return;
                }
                if (cbnt.IsReadOnly())
                {
                    return;
                }

                if (cbnt.IsDirty)
                {
                    await cbnt.WriteAsync(ct);
                }
            },
            async grp =>
            {
                await grp.WriteAsync(mode, ct);
            }
         );
    }

    /// <summary>
    /// 是否已脏
    /// </summary>
    /// <param name="tagunion"></param>
    /// <returns></returns>
    public static bool IsDirty(this TagUnion tagunion)
    {
        return tagunion.Map(
            tag => tag.IsDirty,
            cbnt => cbnt.IsDirty,
            grp => grp.IsDirty()
         );
    }
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