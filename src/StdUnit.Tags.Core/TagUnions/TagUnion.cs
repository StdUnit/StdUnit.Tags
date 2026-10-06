namespace StdUnit.Tags;


/// <summary>
/// 节点联合： <see cref="ITag"/> | <see cref="ITagCbnt"/> | <see cref="ITagGrp"/> 
/// 封闭类型。
/// </summary>
public abstract record TagUnion
{
    /// <summary>
    /// 接收一个访问者
    /// </summary>
    /// <param name="visitor"></param>
    public abstract void Accept(ITagUnionVisitor visitor);

    /// <summary>
    /// TagUnit
    /// </summary>
    /// <param name="Value"></param>
    public record TagUnit(ITag Value) : TagUnion
    {
        /// <inheritdoc/>
        public override void Accept(ITagUnionVisitor visitor)
        {
            visitor.Visit(this);
        }
    };

    /// <summary>
    /// TagCbnt
    /// </summary>
    /// <param name="Value"></param>
    public record TagCbnt(ITagCbnt Value) : TagUnion
    {
        /// <inheritdoc/>
        public override void Accept(ITagUnionVisitor visitor)
        {
            visitor.Visit(this);
        }
    }

    /// <summary>
    /// TagGrp
    /// </summary>
    /// <param name="Value"></param>
    public record TagGrp(ITagGrp Value) : TagUnion
    {
        /// <inheritdoc/>
        public override void Accept(ITagUnionVisitor visitor)
        {
            visitor.Visit(this);
        }
    }

    /// <summary>
    /// 把自身映射到类型T
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="handleTagUnit"></param>
    /// <param name="handleTagCbnt"></param>
    /// <param name="handleTagGrp"></param>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    public T Map<T>(Func<ITag, T> handleTagUnit, Func<ITagCbnt, T> handleTagCbnt, Func<ITagGrp, T> handleTagGrp) => this switch
    {
        TagUnit(ITag Value) => handleTagUnit(Value),
        TagCbnt(ITagCbnt Value) => handleTagCbnt(Value),
        TagGrp(ITagGrp Value) => handleTagGrp(Value),
        _ => throw new NotImplementedException($"unknown types: {this.GetType()}")
    };


    #region Child
    /// <summary>
    /// 以指定的子节点名称，获取直接子节点。<br/>
    /// 如果自身是一个<see cref="ITag"/>节点，表明自身没有子节点，会直接抛出异常；<br/>
    /// 如果相应的子节点不存在，也会抛出异常<br/>
    /// </summary>
    /// <param name="tagName"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException">当前节点是叶测点，本身没有子节点</exception>
    /// <exception cref="KeyNotFoundException">指定的子测点名不存在</exception>
    public TagUnion this[string tagName]
    {
        get => this.Map(
            tagunit => throw new InvalidOperationException($"Tag(Name={tagunit.TagName()} has not child) "),
            tagcbnt => new TagUnit(tagcbnt[tagName]),
            taggrp => taggrp[tagName]
            );
    }
    #endregion
}
