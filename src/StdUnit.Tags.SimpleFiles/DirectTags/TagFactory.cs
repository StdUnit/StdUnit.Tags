
namespace StdUnit.Tags.SimpleFiles;

internal class SimpleFilesDirectTagFactory
{
    private readonly TagContainer _parent;

    public SimpleFilesDirectTagFactory(TagContainer parent)
    {
        _parent = parent;
    }

    private SimpleFilesTagChannel SearchRequiredChannel()
    {
        var channel = _parent.SearchRequiredChannel();
        if (channel is not SimpleFilesTagChannel simpleFilesChannel)
        {
            throw new Exception($"通道类型不匹配，期望={nameof(SimpleFilesTagChannel)}，实际={channel.GetType().Name}");
        }
        return simpleFilesChannel;
    }

    private BitDirectTag CreateBitTag(TagDescriptor descriptor, SimpleFilesTagChannel? thisChannel)
    {
        if (descriptor.TagSize == 0)
        {
            descriptor.TagSize = sizeof(byte);
        }

        return new BitDirectTag(descriptor, thisChannel, _parent);
    }

    private ByteDirectTag CreateByteTag(TagDescriptor descriptor, SimpleFilesTagChannel? thisChannel)
    {
        if (descriptor.TagSize == 0)
        {
            descriptor.TagSize = sizeof(byte);
        }

        return new ByteDirectTag(descriptor, thisChannel, _parent);
    }

    private ShortDirectTag CreateShortTag(TagDescriptor descriptor, SimpleFilesTagChannel? thisChannel)
    {
        if (descriptor.TagSize == 0)
        {
            descriptor.TagSize = sizeof(short);
        }

        return new ShortDirectTag(descriptor, thisChannel, _parent);
    }

    private UShortDirectTag CreateUShortTag(TagDescriptor descriptor, SimpleFilesTagChannel? thisChannel)
    {
        if (descriptor.TagSize == 0)
        {
            descriptor.TagSize = sizeof(ushort);
        }

        return new UShortDirectTag(descriptor, thisChannel, _parent);
    }

    private IntDirectTag CreateIntTag(TagDescriptor descriptor, SimpleFilesTagChannel? thisChannel)
    {
        if (descriptor.TagSize == 0)
        {
            descriptor.TagSize = sizeof(int);
        }

        return new IntDirectTag(descriptor, thisChannel, _parent);
    }

    private UIntDirectTag CreateUIntTag(TagDescriptor descriptor, SimpleFilesTagChannel? thisChannel)
    {
        if (descriptor.TagSize == 0)
        {
            descriptor.TagSize = sizeof(uint);
        }

        return new UIntDirectTag(descriptor, thisChannel, _parent);
    }

    private FloatDirectTag CreateFloatTag(TagDescriptor descriptor, SimpleFilesTagChannel? thisChannel)
    {
        if (descriptor.TagSize == 0)
        {
            descriptor.TagSize = sizeof(float);
        }

        return new FloatDirectTag(descriptor, thisChannel, _parent);
    }

    private DoubleDirectTag CreateDoubleTag(TagDescriptor descriptor, SimpleFilesTagChannel? thisChannel)
    {
        if (descriptor.TagSize == 0)
        {
            descriptor.TagSize = sizeof(double);
        }

        return new DoubleDirectTag(descriptor, thisChannel, _parent);
    }

    private StringDirectTag CreateStringTag(TagDescriptor descriptor, SimpleFilesTagChannel? thisChannel)
    {
        return new StringDirectTag(descriptor, thisChannel, _parent);
    }


    public ITag Create(TagDescriptor descriptor, SimpleFilesTagChannel? thisChannel)
    {
        // 归一化地址
        var channel = thisChannel ?? this.SearchRequiredChannel();
        descriptor.NormalizedAddress = channel.MakePath(descriptor.RawAddress);

        ITag tag = descriptor.TagKind switch
        {
            BuiltinTagKinds.BIT => CreateBitTag(descriptor, thisChannel),
            BuiltinTagKinds.BYTE => CreateByteTag(descriptor, thisChannel),
            BuiltinTagKinds.INT16 => CreateShortTag(descriptor, thisChannel),
            BuiltinTagKinds.UINT16 => CreateUShortTag(descriptor, thisChannel),
            BuiltinTagKinds.INT32 => CreateIntTag(descriptor, thisChannel),
            BuiltinTagKinds.UINT32 => CreateUIntTag(descriptor, thisChannel),
            BuiltinTagKinds.FLOAT => CreateFloatTag(descriptor, thisChannel),
            BuiltinTagKinds.DOUBLE => CreateDoubleTag(descriptor, thisChannel),
            BuiltinTagKinds.STR => CreateStringTag(descriptor, thisChannel),
            _ => throw new Exception($"未预料到的测点种类={descriptor.TagKind}")
        };
        return tag;

    }

}
