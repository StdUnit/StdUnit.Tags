namespace StdUnit.Tags.ModbusTcp;

/// <summary>
/// Modbus 字空间的 Int64 组合子：占用 4 个寄存器。<br/>
/// 字节排布由 <c>endian</c>（每个 16 位单元内部两个字节的顺序）与可选的 <c>interpret</c>（寄存器之间的顺序）决定，
/// 缺省时 <c>BigEndian</c> = 完全大端、<c>LittleEndian</c> = 完全小端。换算统一走
/// <see cref="ModbusValueInterpreter{T}"/>，与直接测点一致；详见项目根目录的 Notes.md。
/// </summary>
internal class ModbusRegisterInt64Cbntor : ModbusMultipleBytesCbntor<long>
{
    /// <summary>
    /// c'tor
    /// </summary>
    /// <param name="tagDescriptor"></param>
    /// <param name="tagCbnt">Modbus 字空间组合（寄存器缓存）</param>
    /// <param name="tagOffset">字节偏移（必须为偶数）</param>
    /// <param name="isReadOnly">是否只读（输入寄存器）</param>
    internal ModbusRegisterInt64Cbntor(TagDescriptor tagDescriptor, TagCbnt<ushort> tagCbnt, int tagOffset, bool isReadOnly)
        : base(tagDescriptor, tagCbnt, tagOffset, isReadOnly, ModbusInt64Interpreter.For(tagDescriptor))
    {
    }
}

/// <summary>
/// Modbus 字空间的 UInt64 组合子：占用 4 个寄存器。
/// </summary>
internal class ModbusRegisterUInt64Cbntor : ModbusMultipleBytesCbntor<ulong>
{
    /// <summary>
    /// c'tor
    /// </summary>
    /// <param name="tagDescriptor"></param>
    /// <param name="tagCbnt">Modbus 字空间组合（寄存器缓存）</param>
    /// <param name="tagOffset">字节偏移（必须为偶数）</param>
    /// <param name="isReadOnly">是否只读（输入寄存器）</param>
    internal ModbusRegisterUInt64Cbntor(TagDescriptor tagDescriptor, TagCbnt<ushort> tagCbnt, int tagOffset, bool isReadOnly)
        : base(tagDescriptor, tagCbnt, tagOffset, isReadOnly, ModbusUInt64Interpreter.For(tagDescriptor))
    {
    }
}
