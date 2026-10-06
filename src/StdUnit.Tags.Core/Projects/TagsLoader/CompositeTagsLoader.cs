using System.Xml.Linq;

namespace StdUnit.Tags;

/// <summary>
/// 根据channel和element，给出 <see cref="TagCbntBuilderBase"/> <br/>。
/// 如果当前参数不合适，给出null。
/// </summary>
/// <param name="channel"></param>
/// <param name="element"></param>
/// <returns></returns>
public delegate TagCbntBuilderBase? MakeTagCbntBuilder(ITagChannel channel, TagCbntDescriptor element);

/// <summary>
/// 根据channel、tagDescriptor 和element，给出<see cref="TagBuilderBase"/>  <br/>
/// 如果当前参数不合适，给出null。
/// </summary>
/// <param name="channel"></param>
/// <param name="descriptor"></param>
/// <returns></returns>
public delegate TagBuilderBase? MakeTagBuilder(ITagChannel channel, TagDescriptor descriptor);

/// <summary>
/// 复合测点集加载器。<br/>
/// </summary>
public class CompositeTagsLoader : ITagsLoader
{
    #region TagsBuilder Choose
    /// <summary>
    /// 支持的直接测点构建器集合
    /// </summary>
    protected List<MakeTagBuilder> _directTagFactories = new();

    /// <summary>
    /// 注册 <see cref="TagBuilderBase"/> 的构建器
    /// </summary>
    /// <param name="factory"></param>
    /// <returns></returns>
    public virtual CompositeTagsLoader AddDirectTagBuilder(MakeTagBuilder factory)
    {
        this._directTagFactories.Add(factory);
        return this;
    }

    /// <summary>
    /// 根据给定的channel和element, 生成合适的 <see cref="TagBuilderBase"/> <br/>
    /// 返回null表示未找到结果
    /// </summary>
    /// <param name="channel"></param>
    /// <param name="tagDescriptor"></param>
    /// <returns></returns>
    protected virtual TagBuilderBase? ChooseDirectTagBuilder(ITagChannel channel, TagDescriptor tagDescriptor)
    {
        foreach (var f in this._directTagFactories)
        {
            var x = f(channel, tagDescriptor);
            if (x != null)
            {
                return x;
            }
        }
        return null;
    }
    #endregion


    #region TagsCbntBulder Choice
    /// <summary>
    /// 支持的测点构建器集合
    /// </summary>
    protected List<MakeTagCbntBuilder> _tagCbntBuilders = new();

    /// <summary>
    /// 添加测点组合构建器
    /// </summary>
    /// <param name="func"></param>
    /// <returns></returns>
    public virtual CompositeTagsLoader AddTagsCbntBuilder(MakeTagCbntBuilder func)
    {
        this._tagCbntBuilders.Add(func);
        return this;
    }

    /// <summary>
    /// 按顺序，逐一调用测点组合构建器，如果返回为null，表示当前构建器不适用于对应的节点，需要继续尝试其它构建器
    /// </summary>
    /// <param name="channel"></param>
    /// <param name="thisElement"></param>
    /// <returns></returns>
    protected virtual TagCbntBuilderBase? ChooseTagCbntBuilder(ITagChannel channel, TagCbntDescriptor thisElement)
    {
        foreach (var b in this._tagCbntBuilders)
        {
            var x = b(channel, thisElement);
            if (x != null)
            {
                return x;
            }
        }
        return null;
    }
    #endregion


    #region 从 XElement 中加载 Tag|TagCbnt|TagGrp，并作为子节点追加到指定的父节点中
    /// <inheritdoc/>
    public virtual void LoadTagGroup(ITagGrp parent, ITagsDescriptor descriptor, IReadOnlyList<ITagChannel> availableChannels)
    {
        if (descriptor is TagGrpDescriptor grpDescriptor)
        {
            LoadTagGroup(parent, grpDescriptor, availableChannels);
        }
        else if (descriptor is TagCbntDescriptor cbntDescriptor)
        {
            LoadTagCbnt(parent, cbntDescriptor, availableChannels);
        }
        else if (descriptor is TagDescriptor tagDescriptor)
        {
            LoadDirectTag(parent, tagDescriptor, availableChannels);
        }
        else
        {
            throw new TagsProjectConfigurationException($"不支持的 {nameof(ITagsDescriptor)} 类型: {descriptor.GetType().FullName}");
        }
    }

    /// <summary>
    /// 确保同一个父节点下的测点名唯一。<br/>
    /// 底层 <see cref="ITagGrp.Children"/> 是字典，重名时字典自身抛出的
    /// "An item with the same key has already been added" 既没有路径上下文、也难以定位，
    /// 因此在这里提前拦截，错误消息带上完整路径。
    /// </summary>
    /// <param name="parent">父级测点组</param>
    /// <param name="childKind">子节点类型名（Tag / TagCbnt / TagGrp）</param>
    /// <param name="childName">子节点名称</param>
    /// <exception cref="TagsProjectConfigurationException">同名子节点已存在</exception>
    protected static void EnsureChildNameAvailable(ITagGrp parent, string childKind, string childName)
    {
        if (parent.Children.ContainsKey(childName))
        {
            throw new TagsProjectConfigurationException(
                $"测点名重复：{childKind}({childName})。同一个父节点下的测点名必须唯一，请修改其中一个的名称",
                parent.GetLocationPath($"{childKind}({childName})"));
        }
    }

    /// <inheritdoc/>
    protected virtual void LoadTagGroup(ITagGrp parent, TagGrpDescriptor grpDescriptor, IReadOnlyList<ITagChannel> availableChannels)
    {
        var thisTagName = grpDescriptor.Name;
        var thisIsEntry = grpDescriptor.IsEntry;
        var thisChannel = string.IsNullOrEmpty(grpDescriptor.ChannelName) ?
            null :
            availableChannels.FirstOrDefault(c => c.ChannelName() == grpDescriptor.ChannelName);

        var thisGrp = new TagGrp(grpDescriptor, thisChannel);
        EnsureChildNameAvailable(parent, "TagGrp", thisTagName);
        parent.AddTag(thisGrp);
        foreach (var child in grpDescriptor.Children)
        {
            if (child is TagGrpDescriptor childGrpDescriptor)
            {
                LoadTagGroup(thisGrp, childGrpDescriptor, availableChannels);
            }
            else if (child is TagCbntDescriptor childCbntDescriptor)
            {
                LoadTagCbnt(thisGrp, childCbntDescriptor, availableChannels);
            }
            else if (child is TagDescriptor childTagDescriptor)
            {
                LoadDirectTag(thisGrp, childTagDescriptor, availableChannels);
            }
        }
        return;
    }

    /// <summary>
    /// 加载 TagCbnt
    /// </summary>
    /// <param name="parent"></param>
    /// <param name="cbntDescriptor"></param>
    /// <param name="availableChannels"></param>
    /// <exception cref="TagsProjectConfigurationException">通道未声明 / 未注册对应的构建器 / 测点重名</exception>
    protected virtual void LoadTagCbnt(ITagGrp parent, TagCbntDescriptor cbntDescriptor, IReadOnlyList<ITagChannel> availableChannels)
    {
        var thisChannel = string.IsNullOrEmpty(cbntDescriptor.ChannelName) ?
            null :
            availableChannels.FirstOrDefault(c => c.ChannelName() == cbntDescriptor.ChannelName);
        if (!string.IsNullOrEmpty(cbntDescriptor.ChannelName) && thisChannel is null)
        {
            throw new TagsProjectConfigurationException(
                $"测点组合 '{cbntDescriptor.Name}' 引用了未声明的通道 '{cbntDescriptor.ChannelName}'（已声明的通道: {DescribeChannels(availableChannels)}）",
                parent.GetLocationPath($"TagCbnt({cbntDescriptor.Name})"));
        }
        var channel = thisChannel ?? parent.SearchRequiredChannel();

        var builder = this.ChooseTagCbntBuilder(channel, cbntDescriptor) ??
            throw new TagsProjectConfigurationException(
                $"未注册能处理测点组合 '{cbntDescriptor.Name}' 的 TagCbntBuilder：通道（Name={channel.ChannelName()}, Driver={channel.Driver()}）。请确认已通过 AddXxxSupport() 注册了对应驱动的支持",
                parent.GetLocationPath($"TagCbnt({cbntDescriptor.Name})"));
        var cbntors = cbntDescriptor.Children.ToList();
        var cbntBuilder = builder
            .WithParent(parent)
            .WithChannel(thisChannel)
            .AddTags(cbntors, channel);
        var cbnt = cbntBuilder.Build(channel);
        EnsureChildNameAvailable(parent, "TagCbnt", cbntDescriptor.Name);
        parent.AddTag(cbnt);
        return;
    }

    /// <summary>
    /// 加载直接测点
    /// </summary>
    /// <param name="parent"></param>
    /// <param name="tagDescriptor"></param>
    /// <param name="availableChannels"></param>
    /// <exception cref="TagsProjectConfigurationException">通道未声明 / 未注册对应的构建器 / 测点重名</exception>
    protected virtual void LoadDirectTag(ITagGrp parent, TagDescriptor tagDescriptor, IReadOnlyList<ITagChannel> availableChannels)
    {
        var thisChannel = string.IsNullOrEmpty(tagDescriptor.ChannelName) ?
            null :
            availableChannels.FirstOrDefault(c => c.ChannelName() == tagDescriptor.ChannelName);
        if (!string.IsNullOrEmpty(tagDescriptor.ChannelName) && thisChannel is null)
        {
            throw new TagsProjectConfigurationException(
                $"测点 '{tagDescriptor.TagName}' 引用了未声明的通道 '{tagDescriptor.ChannelName}'（已声明的通道: {DescribeChannels(availableChannels)}）",
                parent.GetLocationPath($"Tag({tagDescriptor.TagName})"));
        }
        var channel = thisChannel ?? parent.SearchRequiredChannel();

        var builder = this.ChooseDirectTagBuilder(channel, tagDescriptor) ??
            throw new TagsProjectConfigurationException(
                $"未注册能处理测点 '{tagDescriptor.TagName}' 的 DirectTagBuilder：通道（Name={channel.ChannelName()}, Driver={channel.Driver()}）。请确认已通过 AddXxxSupport() 注册了对应驱动的支持",
                parent.GetLocationPath($"Tag({tagDescriptor.TagName})"));
        var tag = builder
            .WithParent(parent)
            .WithChannel(thisChannel)
            .Build(channel);
        EnsureChildNameAvailable(parent, "Tag", tagDescriptor.TagName);
        parent.AddTag(tag);
    }

    /// <summary>
    /// 把已声明的通道名拼成一行，用于"引用了未声明的通道"这类错误消息。
    /// </summary>
    /// <param name="channels"></param>
    private static string DescribeChannels(IReadOnlyList<ITagChannel> channels)
    {
        if (channels.Count == 0)
        {
            return "（无）";
        }
        return string.Join(", ", channels.Select(c => c.ChannelName()).OrderBy(n => n, StringComparer.Ordinal));
    }

    /// <summary>
    /// 加载 XElement 为 TagDescriptor
    /// </summary>
    /// <param name="e"></param>
    /// <returns></returns>
    protected virtual TagDescriptor LoadTagDescriptor(XElement e) => e.ToTagDescriptor();
    #endregion

}
