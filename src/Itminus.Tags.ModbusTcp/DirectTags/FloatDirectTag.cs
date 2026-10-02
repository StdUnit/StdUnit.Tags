
using System.Buffers.Binary;

namespace Itminus.Tags.ModbusTcp;

internal class FloatDirectTag : MultipleBytesDirectTag<float>
{
    public FloatDirectTag(TagDescriptor descriptor, ModbusTcpChannel? thisChannel, TagContainer container)
        : base(descriptor, thisChannel, container)
    {
    }

    protected override int RegisterCount => 2;

    protected override void FillRegisters(float value, Span<ushort> registers)
    {
        var bits = Compat.FloatBitsCompat.ToBits(value);
        switch (this.TagDescriptor.EndianKind)
        {
            case EndianKinds.BigEndian:
                registers[0] = (ushort)(bits >> 16);
                registers[1] = (ushort)bits;
                break;
            case EndianKinds.LittleEndian:
                registers[0] = (ushort)bits;
                registers[1] = (ushort)(bits >> 16);
                break;
            default:
                throw new InvalidOperationException($"不支持的字节序类型: {this.TagDescriptor.EndianKind}");
        }
    }

    protected override float GetValueFromRegisters(ReadOnlySpan<ushort> registers)
    {
        var bits = this.TagDescriptor.EndianKind switch
        {
            EndianKinds.BigEndian => (uint)((registers[0] << 16) | registers[1]),
            EndianKinds.LittleEndian => (uint)((registers[1] << 16) | registers[0]),
            _ => throw new InvalidOperationException($"不支持的字节序类型: {this.TagDescriptor.EndianKind}")
        };
        return Compat.FloatBitsCompat.ToSingle(bits);
    }
}
