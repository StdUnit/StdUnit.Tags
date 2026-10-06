namespace StdUnit.Tags;

/// <summary>
/// extensions for <see cref="TagUnion"/>
/// </summary>
public static class TagUnionExtensions
{
    #region R/W
    /// <summary>
    /// TagUnion 从底层读取
    /// </summary>
    /// <param name="tagunion"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public static async Task ReadAsync(this TagUnion tagunion, CancellationToken ct)
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
                await grp.ReadAsync(ct);
            }
         );
    }

    /// <summary>
    /// TagUnion 写入底层
    /// </summary>
    /// <param name="tagunion"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public static async Task WriteAsync(this TagUnion tagunion, CancellationToken ct)
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
                await grp.WriteAsync(ct);
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
    /// <exception cref="Exception"></exception>
    public static ITag AsTag(this TagUnion tagunion) => tagunion.Map(
        tagunit => tagunit,
        tagcbnt => throw new Exception($"{tagcbnt.TagName()} is a {nameof(ITagCbnt)} intead of a {nameof(ITag)}"),
        taggrp => throw new Exception($"{taggrp.TagName()} is a {nameof(ITagGrp)} intead of a {nameof(ITag)}")
        );

    /// <summary>
    /// 转成 <see cref="ITagCbnt"/>，如果类型不对则抛出异常
    /// </summary>
    /// <param name="tagunion"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public static ITagCbnt AsTagCbnt(this TagUnion tagunion) => tagunion.Map(
        tagunit => throw new Exception($"{tagunit.TagName()} is a {nameof(ITag)} intead of a {nameof(ITagCbnt)}"),
        tagcbnt => tagcbnt,
        taggrp => throw new Exception($"{taggrp.TagName()} is a {nameof(ITagGrp)} intead of a {nameof(ITagCbnt)}")
    );

    /// <summary>
    /// 转成 <see cref="ITagGrp"/>，如果类型不对则抛出异常
    /// </summary>
    /// <param name="tagunion"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public static ITagGrp AsTagGrp(this TagUnion tagunion) => tagunion.Map(
        tagunit => throw new Exception($"{tagunit.TagName()} is a {nameof(ITag)} intead of a {nameof(ITagGrp)}"),
        tagcbnt => throw new Exception($"{tagcbnt.TagName()} is a {nameof(ITagCbnt)} intead of a {nameof(ITagGrp)}"),
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