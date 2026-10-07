
using System.Buffers.Binary;

namespace StdUnit.Tags.ModbusTcp;

internal class UInt16DirectTag : MultipleBytesDirectTag<ushort>
{
    public UInt16DirectTag(TagDescriptor descriptor, ModbusTcpChannel? thisChannel, TagContainer container)
        : base(descriptor, thisChannel, container)
    {
    }

    protected override int RegisterCount => 1;

    protected override void FillRegisters(ushort value, Span<ushort> registers)
    {
        // 16 位：单寄存器。EndianKind 描述寄存器内部字节序（BigEndian=标准大端直写；LittleEndian=内部颠倒）
        registers[0] = this.TagDescriptor.EndianKind switch
        {
            EndianKinds.BigEndian => value,
            EndianKinds.LittleEndian => SwapBytes(value),
            _ => throw new InvalidOperationException($"不支持的字节序类型: {this.TagDescriptor.EndianKind}")
        };
    }

    protected override ushort GetValueFromRegisters(ReadOnlySpan<ushort> registers)
    {
        return this.TagDescriptor.EndianKind switch
        {
            EndianKinds.BigEndian => registers[0],
            EndianKinds.LittleEndian => SwapBytes(registers[0]),
            _ => throw new InvalidOperationException($"不支持的字节序类型: {this.TagDescriptor.EndianKind}")
        };
    }
}


internal class Int16DirectTag : MultipleBytesDirectTag<short>
{
    public Int16DirectTag(TagDescriptor descriptor, ModbusTcpChannel? thisChannel, TagContainer container)
        : base(descriptor, thisChannel, container)
    {
    }

    protected override int RegisterCount => 1;

    protected override void FillRegisters(short value, Span<ushort> registers)
    {
        registers[0] = this.TagDescriptor.EndianKind switch
        {
            EndianKinds.BigEndian => (ushort)value,
            EndianKinds.LittleEndian => SwapBytes((ushort)value),
            _ => throw new InvalidOperationException($"不支持的字节序类型: {this.TagDescriptor.EndianKind}")
        };
    }

    protected override short GetValueFromRegisters(ReadOnlySpan<ushort> registers)
    {
        return this.TagDescriptor.EndianKind switch
        {
            EndianKinds.BigEndian => (short)registers[0],
            EndianKinds.LittleEndian => (short)SwapBytes(registers[0]),
            _ => throw new InvalidOperationException($"不支持的字节序类型: {this.TagDescriptor.EndianKind}")
        };
    }
}
