
namespace StdUnit.Tags.S7;


internal class FloatDirectTag : ContinuousBytesBasedDirectTag<float>
{
    public FloatDirectTag(TagDescriptor descriptor, S7TagChannel? thisChannel, TagContainer parent)
        : base(descriptor, thisChannel, parent)
    {
    }

    public override int BufferSize => 4;

    protected override float ConvertFromBytes(Span<byte> bytes)
    {
        return Compat.FloatBitsCompat.Read(bytes, this.TagEndian() == EndianKinds.BigEndian);
    }

    protected override void FillBytes(Span<byte> bytes, float value)
    {
        Compat.FloatBitsCompat.Write(bytes, value, this.TagEndian() == EndianKinds.BigEndian);
    }
}