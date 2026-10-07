namespace StdUnit.Tags.ModbusTcp;

internal class UInt64DirectTag : MultipleBytesDirectTag<ulong>
{
    private readonly ModbusValueInterpreter<ulong> _interpreter;

    public UInt64DirectTag(TagDescriptor descriptor, ModbusTcpChannel? thisChannel, TagContainer container)
        : base(descriptor, thisChannel, container)
    {
        this._interpreter = ModbusUInt64Interpreter.For(descriptor);
    }

    protected override int RegisterCount => this._interpreter.RegisterCount;

    protected override void FillRegisters(ulong value, Span<ushort> registers) => this._interpreter.Write(value, registers);

    protected override ulong GetValueFromRegisters(ReadOnlySpan<ushort> registers) => this._interpreter.Read(registers);
}


internal class Int64DirectTag : MultipleBytesDirectTag<long>
{
    private readonly ModbusValueInterpreter<long> _interpreter;

    public Int64DirectTag(TagDescriptor descriptor, ModbusTcpChannel? thisChannel, TagContainer container)
        : base(descriptor, thisChannel, container)
    {
        this._interpreter = ModbusInt64Interpreter.For(descriptor);
    }

    protected override int RegisterCount => this._interpreter.RegisterCount;

    protected override void FillRegisters(long value, Span<ushort> registers) => this._interpreter.Write(value, registers);

    protected override long GetValueFromRegisters(ReadOnlySpan<ushort> registers) => this._interpreter.Read(registers);
}
