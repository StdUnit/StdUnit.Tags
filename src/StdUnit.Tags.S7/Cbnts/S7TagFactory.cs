namespace StdUnit.Tags.S7;


/// <summary>
/// S7 测点工厂
/// </summary>
internal class S7TagFactory : TagCbntorFactoryBase
{


    /// <summary>
    /// c'tor（强类型绑定）
    /// </summary>
    internal S7TagFactory(TagCbntBuilderBase builder, S7TagCbnt cbnt) : base(builder)
    {
        this._cbnt = cbnt;
    }

    private readonly S7TagCbnt _cbnt;

    /// <summary>
    /// 所属组合的强类型引用（byte 缓存）。
    /// </summary>
    internal S7TagCbnt TypedCbnt => this._cbnt;

    /// <summary>
    /// 获取测点偏移
    /// </summary>
    protected int GetTagOffset(TagDescriptor tagDescriptor, out S7Address tagAddr)
    {
        tagAddr = S7AddressParser.Parse(tagDescriptor.RawAddress);
        var groupAddr = S7AddressParser.Parse(this.TagCbnt.StartAddress);

        var offset = tagAddr.StartAddress - groupAddr.StartAddress;
        return offset;
    }


    #region
    /// <summary>
    /// 创建 Bit型测点
    /// </summary>
    /// <param name="tagDescriptor"></param>
    /// <returns></returns>
    protected virtual S7BitTagCbntor CreateBitTag(TagDescriptor tagDescriptor)
    {
        // normalize the tagsize
        if (tagDescriptor.TagSize == 0)
        {
            tagDescriptor.TagSize = 1;
        }

        var tagAddr = S7AddressParser.Parse(tagDescriptor.RawAddress);
        var groupAddr = S7AddressParser.Parse(this.TagCbnt.StartAddress);

        var offset = tagAddr.StartAddress - groupAddr.StartAddress;
        if (tagAddr.NthBit < 8)
        {
            return new S7BitTagCbntor(tagDescriptor, TypedCbnt, offset, offset, tagAddr.NthBit);
        }
        else
        {
            var nth = tagAddr.NthBit % 8;
            return new S7BitTagCbntor(tagDescriptor, TypedCbnt, offset, offset + 1, (byte)nth);
        }
    }

    /// <summary>
    /// 创建 Byte型测点
    /// </summary>
    /// <param name="tagDescriptor"></param>
    /// <returns></returns>
    protected virtual S7ByteTagCbntor CreateByteTag(TagDescriptor tagDescriptor)
    {
        // normalize the tagsize
        if (tagDescriptor.TagSize == 0)
        {
            tagDescriptor.TagSize = 1;
        }

        int offset = GetTagOffset(tagDescriptor, out var tagAddr);
        return new S7ByteTagCbntor(tagDescriptor, TypedCbnt, offset);
    }

    /// <summary>
    /// 创建 Int16型测点
    /// </summary>
    /// <param name="tagDescriptor"></param>
    /// <returns></returns>
    protected virtual S7Int16TagCbntor CreateInt16Tag(TagDescriptor tagDescriptor)
    {
        // normalize the tagsize
        if (tagDescriptor.TagSize == 0)
        {
            tagDescriptor.TagSize = 2;
        }

        int offset = GetTagOffset(tagDescriptor, out var tagAddr);
        return new S7Int16TagCbntor(tagDescriptor, TypedCbnt, offset);
    }

    /// <summary>
    ///  创建 UInt16型测点
    /// </summary>
    /// <param name="tagDescriptor"></param>
    /// <returns></returns>
    protected virtual S7UInt16TagCbntor CreateUInt16Tag(TagDescriptor tagDescriptor)
    {
        // normalize the tagsize
        if (tagDescriptor.TagSize == 0)
        {
            tagDescriptor.TagSize = 2;
        }

        int offset = GetTagOffset(tagDescriptor, out var tagAddr);
        return new S7UInt16TagCbntor(tagDescriptor, TypedCbnt, offset);
    }

    /// <summary>
    /// 创建 Int32 型测点
    /// </summary>
    /// <param name="tagDescriptor"></param>
    /// <returns></returns>
    protected virtual S7Int32TagCbntor CreateInt32Tag(TagDescriptor tagDescriptor)
    {
        // normalize the tagsize
        if (tagDescriptor.TagSize == 0)
        {
            tagDescriptor.TagSize = 4;
        }
        int offset = GetTagOffset(tagDescriptor, out var tagAddr);
        return new S7Int32TagCbntor(tagDescriptor, TypedCbnt, offset);
    }

    /// <summary>
    /// 创建 UInt32 型测点
    /// </summary>
    /// <param name="tagDescriptor"></param>
    /// <returns></returns>
    protected virtual S7UInt32TagCbntor CreateUInt32Tag(TagDescriptor tagDescriptor)
    {
        // normalize the tagsize
        if (tagDescriptor.TagSize == 0)
        {
            tagDescriptor.TagSize = 4;
        }

        int offset = GetTagOffset(tagDescriptor, out var tagAddr);
        return new S7UInt32TagCbntor(tagDescriptor, TypedCbnt, offset);
    }

    /// <summary>
    /// 创建 Int64 型测点
    /// </summary>
    /// <param name="tagDescriptor"></param>
    /// <returns></returns>
    protected virtual S7Int64TagCbntor CreateInt64Tag(TagDescriptor tagDescriptor)
    {
        // normalize the tagsize
        if (tagDescriptor.TagSize == 0)
        {
            tagDescriptor.TagSize = 8;
        }

        int offset = GetTagOffset(tagDescriptor, out var tagAddr);
        return new S7Int64TagCbntor(tagDescriptor, TypedCbnt, offset);
    }

    /// <summary>
    /// 创建 UInt64 型测点
    /// </summary>
    /// <param name="tagDescriptor"></param>
    /// <returns></returns>
    protected virtual S7UInt64TagCbntor CreateUInt64Tag(TagDescriptor tagDescriptor)
    {
        // normalize the tagsize
        if (tagDescriptor.TagSize == 0)
        {
            tagDescriptor.TagSize = 8;
        }

        int offset = GetTagOffset(tagDescriptor, out var tagAddr);
        return new S7UInt64TagCbntor(tagDescriptor, TypedCbnt, offset);
    }

    /// <summary>
    /// 创建 float 型测点
    /// </summary>
    /// <param name="tagDescriptor"></param>
    /// <returns></returns>
    protected virtual S7FloatTagCbntor CreateFloatTag(TagDescriptor tagDescriptor)
    {
        // normalize the tagsize
        if (tagDescriptor.TagSize == 0)
        {
            tagDescriptor.TagSize = 4;
        }

        int offset = GetTagOffset(tagDescriptor, out var tagAddr);
        return new S7FloatTagCbntor(tagDescriptor, TypedCbnt, offset);
    }


    /// <summary>
    /// 创建 STR 型测点
    /// </summary>
    /// <param name="tagDescriptor"></param>
    /// <returns></returns>
    protected virtual S7StrTagCbntor CreateStrTag(TagDescriptor tagDescriptor)
    {
        S7Utils.NormalizeS7StrTagSize(tagDescriptor, out var maxlen);
        int offset = GetTagOffset(tagDescriptor, out var tagAddr);
        return new S7StrTagCbntor(tagDescriptor, TypedCbnt, offset, maxlen);
    }
    #endregion

    /// <inheritdoc/>
    public override ITagCbntor CreateTag(TagDescriptor descriptor)
    {
        ITagCbntor tag = descriptor.TagKind switch
        {
            BuiltinTagKinds.BIT => CreateBitTag(descriptor),
            BuiltinTagKinds.BYTE => CreateByteTag(descriptor),
            BuiltinTagKinds.INT16 => CreateInt16Tag(descriptor),
            BuiltinTagKinds.UINT16 => CreateUInt16Tag(descriptor),
            BuiltinTagKinds.INT32 => CreateInt32Tag(descriptor),
            BuiltinTagKinds.UINT32 => CreateUInt32Tag(descriptor),
            BuiltinTagKinds.INT64 => CreateInt64Tag(descriptor),
            BuiltinTagKinds.UINT64 => CreateUInt64Tag(descriptor),
            BuiltinTagKinds.FLOAT => CreateFloatTag(descriptor),
            BuiltinTagKinds.STR => CreateStrTag(descriptor),
            _ => throw new TagsProjectConfigurationException(
                $"S7 驱动未预料到的测点种类={descriptor.TagKind}。type 必须是 BIT/BYTE/INT16/UINT16/INT32/UINT32/INT64/UINT64/FLOAT/STR 之一",
                $"Tag({descriptor.TagName})")
        };
        return tag;
    }

}
