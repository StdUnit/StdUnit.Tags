using System.Buffers.Binary;

namespace StdUnit.Tags.S7;

internal class Int16DirectTag : ContinuousBytesBasedDirectTag<Int16>
{
    public Int16DirectTag(TagDescriptor descriptor, S7TagChannel? thisChannel, TagContainer parent)
        : base(descriptor, thisChannel, parent)
    {
    }

    public override int BufferSize => 2;


    protected override short ConvertFromBytes(Span<byte> bytes)
    {
        return this.TagEndian() == EndianKinds.BigEndian ?
            BinaryPrimitives.ReadInt16BigEndian(bytes) :
            BinaryPrimitives.ReadInt16LittleEndian(bytes);
    }

    protected override void FillBytes(Span<byte> bytes, short value)
    {
        if (this.TagEndian() == EndianKinds.BigEndian)
        {
            BinaryPrimitives.WriteInt16BigEndian(bytes, value);
        }
        else
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes, value);
        }
    }
}
