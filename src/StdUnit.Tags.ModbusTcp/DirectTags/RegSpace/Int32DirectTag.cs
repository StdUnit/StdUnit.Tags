
using System.Buffers.Binary;

namespace StdUnit.Tags.ModbusTcp;

internal class UInt32DirectTag : MultipleBytesDirectTag<uint>
{
    public UInt32DirectTag(TagDescriptor descriptor, ModbusTcpChannel? thisChannel, TagContainer container)
        : base(descriptor, thisChannel, container)
    {
    }

    protected override int RegisterCount => 2;

    protected override void FillRegisters(uint value, Span<ushort> registers)
    {
        switch (this.TagDescriptor.EndianKind)
        {
            case EndianKinds.BigEndian:
                // 标准 Modbus：高寄存器在前（word order）
                registers[0] = (ushort)(value >> 16);
                registers[1] = (ushort)value;
                break;
            case EndianKinds.LittleEndian:
                // word order 反：低寄存器在前（寄存器内部由协议固定为大端，NModbus 已解析）
                registers[0] = (ushort)value;
                registers[1] = (ushort)(value >> 16);
                break;
            default:
                throw new InvalidOperationException($"不支持的字节序类型: {this.TagDescriptor.EndianKind}");
        }
    }

    protected override uint GetValueFromRegisters(ReadOnlySpan<ushort> registers)
    {
        return this.TagDescriptor.EndianKind switch
        {
            EndianKinds.BigEndian => (uint)((registers[0] << 16) | registers[1]),
            EndianKinds.LittleEndian => (uint)((registers[1] << 16) | registers[0]),
            _ => throw new InvalidOperationException($"不支持的字节序类型: {this.TagDescriptor.EndianKind}")
        };
    }
}


internal class Int32DirectTag : MultipleBytesDirectTag<int>
{
    public Int32DirectTag(TagDescriptor descriptor, ModbusTcpChannel? thisChannel, TagContainer container)
        : base(descriptor, thisChannel, container)
    {
    }

    protected override int RegisterCount => 2;

    protected override void FillRegisters(int value, Span<ushort> registers)
    {
        switch (this.TagDescriptor.EndianKind)
        {
            case EndianKinds.BigEndian:
                registers[0] = (ushort)((uint)value >> 16);
                registers[1] = (ushort)value;
                break;
            case EndianKinds.LittleEndian:
                registers[0] = (ushort)value;
                registers[1] = (ushort)((uint)value >> 16);
                break;
            default:
                throw new InvalidOperationException($"不支持的字节序类型: {this.TagDescriptor.EndianKind}");
        }
    }

    protected override int GetValueFromRegisters(ReadOnlySpan<ushort> registers)
    {
        return this.TagDescriptor.EndianKind switch
        {
            EndianKinds.BigEndian => (int)((registers[0] << 16) | registers[1]),
            EndianKinds.LittleEndian => (int)((registers[1] << 16) | registers[0]),
            _ => throw new InvalidOperationException($"不支持的字节序类型: {this.TagDescriptor.EndianKind}")
        };
    }
}
