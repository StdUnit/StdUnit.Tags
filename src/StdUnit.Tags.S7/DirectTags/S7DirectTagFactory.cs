
namespace StdUnit.Tags.S7;

internal class S7DirectTagFactory
{
    private readonly TagContainer _parent;

    public S7DirectTagFactory(TagContainer parent)
    {
        _parent = parent;
    }

    private BitDirectTag CreateBitTag(TagDescriptor descriptor, S7TagChannel? thisChannel)
    {
        var tagAddr = S7AddressParser.Parse(descriptor.RawAddress);
        if (descriptor.TagSize == 0)
        {
            descriptor.TagSize = tagAddr.NthBit / 8 + 1;
        }
        return new BitDirectTag(descriptor, thisChannel, _parent, nthBit: tagAddr.NthBit);
    }

    private ByteDirectTag CreateByteTag(TagDescriptor descriptor, S7TagChannel? thisChannel)
    {
        if (descriptor.TagSize == 0)
        {
            descriptor.TagSize = 1;
        }
        return new ByteDirectTag(descriptor, thisChannel, _parent);
    }

    private Int16DirectTag CreateInt16Tag(TagDescriptor descriptor, S7TagChannel? thisChannel)
    {
        if (descriptor.TagSize == 0)
        {
            descriptor.TagSize = 2;
        }
        return new Int16DirectTag(descriptor, thisChannel, _parent);
    }

    private UInt16DirectTag CreateUInt16Tag(TagDescriptor descriptor, S7TagChannel? thisChannel)
    {
        if (descriptor.TagSize == 0)
        {
            descriptor.TagSize = 2;
        }
        return new UInt16DirectTag(descriptor, thisChannel, _parent);
    }


    private Int32DirectTag CreateInt32Tag(TagDescriptor descriptor, S7TagChannel? thisChannel)
    {
        if (descriptor.TagSize == 0)
        {
            descriptor.TagSize = 4;
        }
        return new Int32DirectTag(descriptor, thisChannel, _parent);
    }

    private UInt32DirectTag CreateUInt32Tag(TagDescriptor descriptor, S7TagChannel? thisChannel)
    {
        if (descriptor.TagSize == 0)
        {
            descriptor.TagSize = 4;
        }
        return new UInt32DirectTag(descriptor, thisChannel, _parent);
    }


    private Int64DirectTag CreateInt64Tag(TagDescriptor descriptor, S7TagChannel? thisChannel)
    {
        if (descriptor.TagSize == 0)
        {
            descriptor.TagSize = 8;
        }
        return new Int64DirectTag(descriptor, thisChannel, _parent);
    }


    private UInt64DirectTag CreateUInt64Tag(TagDescriptor descriptor, S7TagChannel? thisChannel)
    {
        if (descriptor.TagSize == 0)
        {
            descriptor.TagSize = 8;
        }
        return new UInt64DirectTag(descriptor, thisChannel, _parent);
    }


    private FloatDirectTag CreateFloatTag(TagDescriptor descriptor, S7TagChannel? thisChannel)
    {
        if (descriptor.TagSize == 0)
        {
            descriptor.TagSize = 4;
        }
        return new FloatDirectTag(descriptor, thisChannel, _parent);
    }


    private StrDirectTag CreateStrTag(TagDescriptor descriptor, S7TagChannel? thisChannel)
    {
        S7Utils.NormalizeS7StrTagSize(descriptor, out byte maxlen);
        return new StrDirectTag(descriptor, thisChannel, _parent, maxLen: maxlen);
    }

    public ITag Create(TagDescriptor descriptor, S7TagChannel? thisChannel)
    {
        ITag tag = descriptor.TagKind switch
        {
            BuiltinTagKinds.BIT => CreateBitTag(descriptor, thisChannel),
            BuiltinTagKinds.BYTE => CreateByteTag(descriptor, thisChannel),
            BuiltinTagKinds.INT16 => CreateInt16Tag(descriptor, thisChannel),
            BuiltinTagKinds.UINT16 => CreateUInt16Tag(descriptor, thisChannel),
            BuiltinTagKinds.INT32 => CreateInt32Tag(descriptor, thisChannel),
            BuiltinTagKinds.UINT32 => CreateUInt32Tag(descriptor, thisChannel),
            BuiltinTagKinds.INT64 => CreateInt64Tag(descriptor, thisChannel),
            BuiltinTagKinds.UINT64 => CreateUInt64Tag(descriptor, thisChannel),
            BuiltinTagKinds.FLOAT => CreateFloatTag(descriptor, thisChannel),
            BuiltinTagKinds.STR => CreateStrTag(descriptor, thisChannel),
            _ => throw new Exception($"未预料到的测点种类={descriptor.TagKind}")
        };
        return tag;

    }

}
