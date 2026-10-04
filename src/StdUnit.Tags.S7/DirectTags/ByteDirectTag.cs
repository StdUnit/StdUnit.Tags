using System;

namespace StdUnit.Tags.S7;


internal class ByteDirectTag : ContinuousBytesBasedDirectTag<byte>
{
    public ByteDirectTag(TagDescriptor descriptor, S7TagChannel? thisChannel, TagContainer parent)
        : base(descriptor, thisChannel, parent)
    {
    }

    public override int BufferSize => 1;

    protected override byte ConvertFromBytes(Span<byte> bytes) => bytes[0];

    protected override void FillBytes(Span<byte> bytes, byte value)
    {
        bytes[0] = value;
    }
}



