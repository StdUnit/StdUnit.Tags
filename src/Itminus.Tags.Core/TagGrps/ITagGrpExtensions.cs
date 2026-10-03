

using System.Runtime.CompilerServices;

namespace Itminus.Tags;

/// <summary>
/// extensions for <see cref="ITagGrp"/>
/// </summary>
public static class ITagGrpExtensions
{
    /// <summary>
    /// 获取当前测点的名称
    /// </summary>
    /// <param name="tagGrp"></param>
    /// <returns></returns>
    public static string TagName(this ITagGrp tagGrp) => tagGrp.Descriptor.Name;


    #region 获取子孙节点
    /// <summary>
    /// 以路径获取子节点并作为<see cref="ITag"/>返回。<br/>
    /// 如果路径不存在，会抛出异常；如果对应节点不是 <see cref="ITag"/>，也会抛出异常<br/>
    /// </summary>
    /// <param name="grp"></param>
    /// <param name="path">以“/”分隔</param>
    /// <returns></returns>
    public static ITag SelectTag(this ITagGrp grp, string path)
    {
        var tagunion = grp.Descendant(path);
        return tagunion.AsTag();
    }

    /// <summary>
    /// 以路径获取子节点并作为<see cref="ITagCbnt"/>返回。<br/>
    /// 如果路径不存在，会抛出异常；如果对应节点不是 <see cref="ITagCbnt"/>，也会抛出异常<br/>
    /// </summary>
    /// <param name="grp"></param>
    /// <param name="path">以“/”分隔</param>
    /// <returns></returns>
    public static ITagCbnt SelectCbnt(this ITagGrp grp, string path)
    {
        var tagunion = grp.Descendant(path);
        return tagunion.AsTagCbnt();
    }

    /// <summary>
    /// 以路径获取子节点并作为<see cref="ITagGrp"/>返回。<br/>
    /// 如果路径不存在，会抛出异常；如果对应节点不是 <see cref="ITagGrp"/>，也会抛出异常<br/>
    /// </summary>
    /// <param name="grp"></param>
    /// <param name="path">以“/”分隔</param>
    /// <returns></returns>
    public static ITagGrp SelectGrp(this ITagGrp grp, string path)
    {
        var tagunion = grp.Descendant(path);
        return tagunion.AsTagGrp();
    }
    #endregion

    #region
    /// <summary>
    /// 当前节点是否是入口
    /// </summary>
    public static bool IsEntry(this ITagGrp grp) => grp.Descriptor.IsEntry;

    /// <summary>
    /// 扫描入口节点：如果当前节点是入口，则返回自身；否则检索所有子节点中入口节点。<br/>
    /// 返回入口节点列表，可能为空列表。
    /// </summary>
    /// <param name="grp"></param>
    /// <returns></returns>
    public static IList<ITagGrp> ScanEntries(this ITagGrp grp)
    {
        if (grp.IsEntry())
        {
            return new List<ITagGrp>() { grp };
        }

        var results = new List<ITagGrp>();
        foreach (var kvp in grp.Children)
        {
            var child = kvp.Value;
            if (child is TagUnion.TagGrp unionTagGroup)
            {
                var g = unionTagGroup.Value;
                if (g.IsEntry())
                {
                    results.Add(g);
                }
                else
                {
                    var list = g.ScanEntries();
                    results.AddRange(list);
                }
            }
        }
        return results;
    }
    #endregion

    #region 冒泡式获取通道
    /// <summary>
    /// 冒泡式获取通信通道
    /// </summary>
    /// <param name="tagGrp"></param>
    /// <returns></returns>
    public static ITagChannel? SearchChannel(this ITagGrp tagGrp)
    {
        if (tagGrp.Channel is not null)
        {
            return tagGrp.Channel;
        }

        if (tagGrp.Parent is not null)
        {
            return tagGrp.Parent.SearchChannel();
        }

        return null;
    }

    /// <summary>
    /// 冒泡式获取通信通道，如果为空则抛出异常
    /// </summary>
    /// <param name="tagGrp"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public static ITagChannel SearchRequiredChannel(this ITagGrp tagGrp) => tagGrp.SearchChannel() ?? throw new Exception($"Channel is not configured : TagGrp({tagGrp.TagName()})");

    #endregion


    #region 收集子树通道集
    /// <summary>
    /// <b>向下递归</b>收集该入口子树中真正会被读写的通道集（按首次出现顺序去重）。<br/>
    /// <br/>
    /// 用途：一个入口下可以挂多个通道。<see cref="SearchChannel(ITagGrp)"/> 只能解析出入口的<b>主通道</b>，
    /// 而子树中其它子节点各自显式声明的通道（<see cref="ITag.Channel"/>、<see cref="ITagCbnt.Channel"/>、
    /// <see cref="ITagGrp.Channel"/>）若不建立连接，读写对应测点时就会失败。<br/>
    /// 本方法把"入口本轮实际会用到的全部通道"一次性算出来，供 <see cref="ITagGrpRunner"/> 逐一
    /// <see cref="ITagChannel.EnsureConnectedAsync"/>。<br/>
    /// <br/>
    /// 语义（重要）：<br/>
    /// 1. <b>只向下递归</b>入口自身的子树，不向上冒泡；<br/>
    /// 2. 入口主通道（<see cref="SearchChannel(ITagGrp)"/>，可能来自入口本身，也可能是入口向上继承而来）
    ///    若存在则<b>排在最前</b>且必定包含；<br/>
    /// 3. 嵌套的 <c>isEntry="true"</c> 子组被当作<b>普通子组</b>处理（连带其子树一起收集）——
    ///    只有最外层入口才会被 <see cref="ScanEntries"/> 识别为入口、拥有独立的 <see cref="ITagGrpRunner"/>，
    ///    嵌套的 isEntry 不生效（见 <see cref="NestedEntryValidator"/>）；<br/>
    /// 4. 各节点的通道按<b>运行期实际读写时的解析规则</b>解析（<see cref="ITagExtensions.SearchChannel"/> /
    ///    <see cref="ITagCbntExtensions.SearchChannel"/> 为冒泡解析；<see cref="ITagGrp"/> 取其自身声明），
    ///    因此不会出现"收集到的通道"与"实际使用的通道"不一致；<br/>
    /// 5. 返回顺序即文档顺序，也就是 <c>EnsureConnectedAsync</c> 的建议调用顺序，
    ///    方便连接数受限的设备（串口独占、PLC 连接数上限）按序建连。<br/>
    /// </summary>
    /// <param name="entry">入口测点组；非入口也可以调用（此时按该节点的子树收集）</param>
    /// <returns>按首次出现顺序去重的通道列表，可能为空列表</returns>
    public static IReadOnlyList<ITagChannel> CollectChannels(this ITagGrp entry)
    {
        var results = new List<ITagChannel>();

        // 通道代表一条物理连接，按引用去重（避免某个驱动重写 Equals 后导致两条连接被误判为同一条）
        var seen = new HashSet<ITagChannel>(Compat.EqualityComparersCompat.ByReference<ITagChannel>());

        void Add(ITagChannel? channel)
        {
            if (channel is not null && seen.Add(channel))
            {
                results.Add(channel);
            }
        }

        void Walk(ITagGrp grp)
        {
            foreach (var kvp in grp.Children)
            {
                switch (kvp.Value)
                {
                    case TagUnion.TagUnit tag:
                        Add(tag.Value.SearchChannel());
                        break;

                    case TagUnion.TagCbnt cbnt:
                        Add(cbnt.Value.SearchChannel());
                        break;

                    case TagUnion.TagGrp child:
                        // 只取该子组自身声明的通道：祖先声明的通道会在访问祖先时被收集；
                        // 若该子组自身没有声明，则其成员冒泡到祖先通道，同样已被收集。
                        Add(child.Value.Channel);
                        Walk(child.Value);
                        break;
                }
            }
        }

        Add(entry.SearchChannel());
        Walk(entry);
        return results;
    }
#endregion


    #region
    /// <summary>
    /// 冒泡式获取访问模式。<br/>
    /// 先查自身，再冒泡查父级。<br/>
    /// 如果所有层级均为 null，默认返回 <see cref="TagAccessMode.RW"/>。
    /// </summary>
    /// <param name="tagGrp"></param>
    /// <returns></returns>
    public static TagAccessMode SearchAccessMode(this ITagGrp tagGrp)
    {
        if (tagGrp.Descriptor.AccessMode.HasValue)
            return tagGrp.Descriptor.AccessMode.Value;

        if (tagGrp.Parent is not null)
            return tagGrp.Parent.SearchAccessMode();

        return TagAccessMode.RW;
    }

    /// <summary>
    /// 冒泡式获取扫描间隔
    /// </summary>
    /// <param name="tagGrp"></param>
    /// <returns></returns>
    public static int? SearchScanInterval(this ITagGrp tagGrp)
    {
        // Descriptor 中存在 ScanInterval 配置时直接返回（即使为 0 也是有效配置）
        if (tagGrp.Descriptor.ScanInterval is not null)
        {
            return tagGrp.Descriptor.ScanInterval;
        }
        if (tagGrp.Parent is not null)
        {
            return tagGrp.Parent.SearchScanInterval();
        }
        return null;
    }

    #endregion
}