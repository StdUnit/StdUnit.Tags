namespace StdUnit.Tags.ModbusTcp;

internal class UInt32DirectTag : MultipleBytesDirectTag<uint>
{
    private readonly ModbusValueInterpreter<uint> _interpreter;

    public UInt32DirectTag(TagDescriptor descriptor, ModbusTcpChannel? thisChannel, TagContainer container)
        : base(descriptor, thisChannel, container)
    {
        this._interpreter = ModbusUInt32Interpreter.For(descriptor);
    }

    protected override int RegisterCount => this._interpreter.RegisterCount;

    protected override void FillRegisters(uint value, Span<ushort> registers) => this._interpreter.Write(value, registers);

    protected override uint GetValueFromRegisters(ReadOnlySpan<ushort> registers) => this._interpreter.Read(registers);
}


internal class Int32DirectTag : MultipleBytesDirectTag<int>
{
    private readonly ModbusValueInterpreter<int> _interpreter;

    public Int32DirectTag(TagDescriptor descriptor, ModbusTcpChannel? thisChannel, TagContainer container)
        : base(descriptor, thisChannel, container)
    {
        this._interpreter = ModbusInt32Interpreter.For(descriptor);
    }

    protected override int RegisterCount => this._interpreter.RegisterCount;

    protected override void FillRegisters(int value, Span<ushort> registers) => this._interpreter.Write(value, registers);

    protected override int GetValueFromRegisters(ReadOnlySpan<ushort> registers) => this._interpreter.Read(registers);
}
