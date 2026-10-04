
using System.Buffers.Binary;

namespace StdUnit.Tags.ModbusTcp;

internal class UInt64DirectTag : MultipleBytesDirectTag<ulong>
{
    public UInt64DirectTag(TagDescriptor descriptor, ModbusTcpChannel? thisChannel, TagContainer container)
        : base(descriptor, thisChannel, container)
    {
    }

    protected override int RegisterCount => 4;

    protected override void FillRegisters(ulong value, Span<ushort> registers)
    {
        switch (this.TagDescriptor.EndianKind)
        {
            case EndianKinds.BigEndian:
                registers[0] = (ushort)(value >> 48);
                registers[1] = (ushort)(value >> 32);
                registers[2] = (ushort)(value >> 16);
                registers[3] = (ushort)value;
                break;
            case EndianKinds.LittleEndian:
                registers[0] = (ushort)value;
                registers[1] = (ushort)(value >> 16);
                registers[2] = (ushort)(value >> 32);
                registers[3] = (ushort)(value >> 48);
                break;
            default:
                throw new InvalidOperationException($"不支持的字节序类型: {this.TagDescriptor.EndianKind}");
        }
    }

    protected override ulong GetValueFromRegisters(ReadOnlySpan<ushort> registers)
    {
        return this.TagDescriptor.EndianKind switch
        {
            EndianKinds.BigEndian => ((ulong)registers[0] << 48) | ((ulong)registers[1] << 32) | ((ulong)registers[2] << 16) | registers[3],
            EndianKinds.LittleEndian => ((ulong)registers[3] << 48) | ((ulong)registers[2] << 32) | ((ulong)registers[1] << 16) | registers[0],
            _ => throw new InvalidOperationException($"不支持的字节序类型: {this.TagDescriptor.EndianKind}")
        };
    }
}


internal class Int64DirectTag : MultipleBytesDirectTag<long>
{
    public Int64DirectTag(TagDescriptor descriptor, ModbusTcpChannel? thisChannel, TagContainer container)
        : base(descriptor, thisChannel, container)
    {
    }

    protected override int RegisterCount => 4;

    protected override void FillRegisters(long value, Span<ushort> registers)
    {
        var bits = (ulong)value;
        switch (this.TagDescriptor.EndianKind)
        {
            case EndianKinds.BigEndian:
                registers[0] = (ushort)(bits >> 48);
                registers[1] = (ushort)(bits >> 32);
                registers[2] = (ushort)(bits >> 16);
                registers[3] = (ushort)bits;
                break;
            case EndianKinds.LittleEndian:
                registers[0] = (ushort)bits;
                registers[1] = (ushort)(bits >> 16);
                registers[2] = (ushort)(bits >> 32);
                registers[3] = (ushort)(bits >> 48);
                break;
            default:
                throw new InvalidOperationException($"不支持的字节序类型: {this.TagDescriptor.EndianKind}");
        }
    }

    protected override long GetValueFromRegisters(ReadOnlySpan<ushort> registers)
    {
        var bits = this.TagDescriptor.EndianKind switch
        {
            EndianKinds.BigEndian => ((ulong)registers[0] << 48) | ((ulong)registers[1] << 32) | ((ulong)registers[2] << 16) | registers[3],
            EndianKinds.LittleEndian => ((ulong)registers[3] << 48) | ((ulong)registers[2] << 32) | ((ulong)registers[1] << 16) | registers[0],
            _ => throw new InvalidOperationException($"不支持的字节序类型: {this.TagDescriptor.EndianKind}")
        };
        return (long)bits;
    }
}
