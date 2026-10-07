namespace StdUnit.Tags.ModbusTcp;

internal class FloatDirectTag : MultipleBytesDirectTag<float>
{
    private readonly ModbusValueInterpreter<float> _interpreter;

    public FloatDirectTag(TagDescriptor descriptor, ModbusTcpChannel? thisChannel, TagContainer container)
        : base(descriptor, thisChannel, container)
    {
        this._interpreter = ModbusFloatInterpreter.For(descriptor);
    }

    protected override int RegisterCount => this._interpreter.RegisterCount;

    protected override void FillRegisters(float value, Span<ushort> registers) => this._interpreter.Write(value, registers);

    protected override float GetValueFromRegisters(ReadOnlySpan<ushort> registers) => this._interpreter.Read(registers);
}
