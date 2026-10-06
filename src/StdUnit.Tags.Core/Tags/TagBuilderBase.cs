using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StdUnit.Tags;


/// <summary>
/// TagBuilder基类，用于构建 <see cref="ITag"/> 实例
/// </summary>
public abstract class TagBuilderBase
{
    /// <summary>
    /// 测点描述。<br/>
    /// 会被自动设置，通常不需要手动调用
    /// </summary>
    public virtual TagDescriptor TagDescriptor { get; protected set; } = null!;

    /// <summary>
    /// 测点自身的通道。<br/>
    /// 会被自动设置，通常不需要手动调用
    /// </summary>
    public virtual ITagChannel? Channel { get; protected set; } = null;

    /// <summary>
    /// 测点所属的群组。
    /// 会被自动设置，通常不需要手动调用
    /// </summary>
    public virtual ITagGrp Parent { get; protected set; } = null!;

    /// <summary>
    /// Tag's Name
    /// </summary>
    public string Name => TagDescriptor.TagName;


    /// <summary>
    /// 会被自动调用以设置 <see cref="TagDescriptor"/> 属性，通常不需要手动调用
    /// </summary>
    /// <param name="descriptor"></param>
    /// <returns></returns>
    public TagBuilderBase WithTagDescriptor(TagDescriptor descriptor)
    {
        this.TagDescriptor = descriptor;
        return this;
    }

    /// <summary>
    /// 会被自动调用以设置 <see cref="ITagChannel"/> 属性，通常不需要手动调用
    /// </summary>
    /// <param name="channel">配置测点本身的通道</param>
    /// <returns></returns>
    public virtual TagBuilderBase WithChannel(ITagChannel? channel)
    {
        this.Channel = channel;
        return this;
    }

    /// <summary>
    /// 会被自动调用以设置 <see cref="Parent"/> 属性，通常不需要手动调用
    /// </summary>
    /// <param name="grp"></param>
    /// <returns></returns>
    public virtual TagBuilderBase WithParent(ITagGrp grp)
    {
        this.Parent = grp;
        return this;
    }

    #region CreateTag 委托
    /// <summary>
    /// 用于自定义测点创建逻辑的委托。<br/>
    /// 如果设置了该委托，则在构建测点时会优先调用；如果委托返回 null，则回退到构建器内部默认逻辑。
    /// </summary>
    protected CreateTag? _createTag;

    /// <summary>
    /// 设置创建测点的委托。<br/>
    /// </summary>
    /// <param name="createTag"></param>
    /// <returns></returns>
    public TagBuilderBase WithFactory(CreateTag createTag)
    {
        this._createTag = createTag;
        return this;
    }

    /// <summary>
    /// 委托：创建测点。<br/>
    /// 委托会被优先调用；如果返回 null，则回退到构建器内部默认逻辑。
    /// </summary>
    /// <param name="descriptor"></param>
    /// <param name="thisChannel"></param>
    /// <param name="container"></param>
    /// <returns></returns>
    public delegate ITag? CreateTag(
        TagDescriptor descriptor,
        ITagChannel? thisChannel,
        TagContainer container
        );
    #endregion


    /// <summary>
    /// 兜底的配置方法，用于配置当前构建器。<br/>
    /// </summary>
    /// <param name="action"></param>
    /// <returns></returns>
    public virtual TagBuilderBase Configure(Action<TagBuilderBase> action)
    {
        action.Invoke(this);
        return this;
    }

    /// <summary>
    /// 构建 <see cref="ITag"/> 实例
    /// </summary>
    /// <param name="channel">(冒泡式得到的)通道</param>
    /// <returns></returns>
    public virtual ITag Build(ITagChannel channel)
    {
        if (channel is null)
        {
            throw new InvalidOperationException($"测点({this.Name})未配置通道");
        }

        if (this._createTag is not null)
        {
            var tag = this._createTag(this.TagDescriptor, this.Channel, TagContainer.From(this.Parent));
            if (tag is not null)
            {
                return tag;
            }
        }

        return Fallback(channel);
    }

    /// <summary>
    /// 兜底的构建逻辑。<br/>
    /// 如果没有设置 <see cref="_createTag"/>，或者 <see cref="_createTag"/> 返回 null，则会调用本方法。<br/>
    /// </summary>
    /// <param name="channel"></param>
    /// <returns></returns>
    protected abstract ITag Fallback(ITagChannel channel);
}
