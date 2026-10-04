using StdUnit.Tags.ModbusTcp;

namespace StdUnit.Tags.ZLan;

public class ZLanTagFactory : TagCbntorFactoryBase
{
    private readonly ZLanCbntBuilderBase _cbntBuilder;

    public ZLanTagFactory(ZLanCbntBuilderBase builder) : base(builder)
    {
        this._cbntBuilder = builder;
    }

    public virtual DITagCbntor CreateDITag(TagDescriptor tagDescriptor)
    {
        // normalize the tagsize
        if (tagDescriptor.TagSize == 0)
        {
            tagDescriptor.TagSize = 1;
        }
        var tagAddr = PinAddrUtils.ParseDI(tagDescriptor.RawAddress);
        tagDescriptor.NormalizedAddress = tagAddr.ToModbusTcpAddr(this._cbntBuilder.Slave);
        var startAddr = DIPinAddr.DI1;
        var offset = (int)tagAddr - (int)startAddr;
        return new DITagCbntor(tagDescriptor, this._cbntBuilder.TypedCbnt, offset);
    }


    public virtual DOTagCbntor CreateDOTag(TagDescriptor tagDescriptor)
    {
        // normalize the tagsize
        if (tagDescriptor.TagSize == 0)
        {
            tagDescriptor.TagSize = 1;
        }
        var tagAddr = PinAddrUtils.ParseDO(tagDescriptor.RawAddress);
        tagDescriptor.NormalizedAddress = tagAddr.ToModbusTcpAddr(this._cbntBuilder.Slave);
        var startAddr = DOPinAddr.DO1;
        var offset = (int)tagAddr - (int)startAddr;
        return new DOTagCbntor(tagDescriptor, this._cbntBuilder.TypedCbnt, offset);
    }

    public override ITagCbntor CreateTag(TagDescriptor descriptor)
    {
        var tag = descriptor.TagKind switch
        {
            BuiltinTagKinds.DI => this.CreateDITag(descriptor) as ITagCbntor,
            BuiltinTagKinds.DO => this.CreateDOTag(descriptor) as ITagCbntor,
            _ => throw new Exception($"未预料到的测点种类={descriptor.TagKind}")
        };
        return tag;
    }
}
