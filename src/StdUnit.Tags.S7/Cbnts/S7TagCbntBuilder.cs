namespace StdUnit.Tags.S7;

/// <summary>
/// 针对S7的测点组合构建器
/// </summary>
public class S7TagCbntBuilder : TagCbntBuilderBase
{
    /// <summary>
    /// c'tor<br/>
    /// 需要额外使用 <c>WithCbntDescriptor()</c> 设置实际描述符。
    /// </summary>
    public S7TagCbntBuilder()
        : this(new S7TagCbnt(
            new TagCbntDescriptor
            {
                Name = "unkown_s7_cbnt_name",
                StartAddress = "unknown_s7_cbnt_start_address"
            }
        ))
    {
    }

    private readonly S7TagCbnt _cbnt;

    S7TagCbntBuilder(S7TagCbnt cbnt) : base(cbnt)
    {
        this._cbnt = cbnt;
    }

    /// <summary>
    /// 所属组合的强类型引用。
    /// </summary>
    internal S7TagCbnt TypedCbnt => this._cbnt;

    /// <inheritdoc/>
    protected override ITagCbntor Fallback(TagDescriptor descriptor, ITagChannel channel)
    {
        var tagFactory = this.MakeS7TagFactory();
        return tagFactory.CreateTag(descriptor);
    }

    /// <inheritdoc/>
    protected override TagCbntBuilderBase AutoLayout()
    {
        var cacheSize = 0;
        foreach (var kvp in this.TagCbnt.Children)
        {
            var tag = kvp.Value;
            var occupied = tag.TagOffset + tag.TagDescriptor.TagSize;
            if (tag is S7BitTagCbntor bitTag)
            {
                if (bitTag.CacheOffset != bitTag.TagOffset)
                {
                    occupied = bitTag.CacheOffset + 1;
                }
            }
            if (occupied > cacheSize)
            {
                cacheSize = occupied;
            }
        }

        this._cbnt.ResizeCache(cacheSize);

        // initialize str tag prefix
        foreach (var kvp in this.TagCbnt.Children)
        {
            var tag = kvp.Value;
            if (tag is S7StrTagCbntor strTag)
            {
                this.InitializeStrTag(strTag);
            }
        }

        NormalizeTagAddress();
        return this;
    }

    private void NormalizeTagAddress()
    {
        var groupAddr = S7AddressParser.Parse(this.TagCbnt.StartAddress);
        foreach (var kvp in this.TagCbnt.Children)
        {
            var tag = kvp.Value;
            var addr = S7AddressParser.Parse(tag.RawAddress());
            if (addr.BlockSpecified)
            {
                if (addr.Area != groupAddr.Area || addr.BlockNumber != groupAddr.BlockNumber)
                {
                    var tagname = tag.TagName();
                    throw new TagsProjectAddressException(
                        $"测点 '{tagname}' 的地址 '{tag.RawAddress()}' 与所属组合 '{this.Name}' 的起始地址 '{this.TagCbnt.StartAddress}' 不在同一区域/DB 块；若要沿用组合的区域与 DB 块，请写成相对地址（如 '$${addr.StartAddress}'）",
                        $"TagCbnt({this.Name})/Tag({tagname})");
                }
            }
            else
            {
                // let's keep it false to indicate it was a relative address
                addr.BlockSpecified = false;
                // fill in the area and block number from group address
                addr.Area = groupAddr.Area;
                addr.BlockNumber = groupAddr.BlockNumber;
                tag.TagDescriptor.NormalizedAddress = addr.ToString();
            }
        }
    }

    private void InitializeStrTag(S7StrTagCbntor tag)
    {
        var prefix = this._cbnt.Cache.Slice(tag.CacheOffset, 2).Span;
        prefix[0] = tag.Maxlen;
        prefix[1] = 0;
    }
}

