namespace StdUnit.Tags.ModbusTcp;

/// <summary>
/// Modbus 字空间"多寄存器数值"组合子基类（32/64 位）：把 <see cref="ModbusValueInterpreter{T}"/> 接到寄存器缓存上，
/// 并统一"可写性检查 + 时间戳 + 脏标记"。<br/>
/// 只占一个寄存器的组合子（<see cref="ModbusRegisterInt16Cbntor"/>、<see cref="ModbusRegisterByteCbntor"/>、
/// <see cref="ModbusRegisterBitCbntor"/>）不经过这里。<br/>
/// 语义与直接测点（<see cref="MultipleBytesDirectTag{T}"/>）一致，详见项目根目录的 Notes.md。
/// </summary>
/// <typeparam name="T">测点值类型</typeparam>
internal abstract class ModbusMultipleBytesCbntor<T> : ModbusRegisterCbntorBase
    where T : unmanaged
{
    private readonly ModbusValueInterpreter<T> _interpreter;

    /// <summary>
    /// c'tor
    /// </summary>
    /// <param name="tagDescriptor"></param>
    /// <param name="tagCbnt">Modbus 字空间组合（寄存器缓存）</param>
    /// <param name="tagOffset">字节偏移（必须为偶数）</param>
    /// <param name="isReadOnly">是否只读（输入寄存器）</param>
    /// <param name="interpreter">该类型的解读器（由子类按数值类型给出）</param>
    protected ModbusMultipleBytesCbntor(
        TagDescriptor tagDescriptor,
        TagCbnt<ushort> tagCbnt,
        int tagOffset,
        bool isReadOnly,
        ModbusValueInterpreter<T> interpreter)
        : base(tagDescriptor, tagCbnt, tagOffset, isReadOnly)
    {
        this._interpreter = interpreter;
    }

    /// <summary>
    /// 测点值
    /// </summary>
    public override object? Value
    {
        get
        {
            var span = this.RegCache.Span.Slice(this.RegOffset, this._interpreter.RegisterCount);
            return this._interpreter.Read(span);
        }
        set
        {
#pragma warning disable CS8605 // Unboxing a possibly null value.
            var data = (T)value;
#pragma warning restore CS8605 // Unboxing a possibly null value.
            this.EnsureWritable();
            var span = this.RegCache.Span.Slice(this.RegOffset, this._interpreter.RegisterCount);
            this._interpreter.Write(data, span);
            this.Timestamp = DateTime.UtcNow;
            this.MarkDirty();
        }
    }
}
