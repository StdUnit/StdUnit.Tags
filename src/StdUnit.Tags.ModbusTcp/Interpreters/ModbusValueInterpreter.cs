using System.Buffers.Binary;

namespace StdUnit.Tags.ModbusTcp;

/// <summary>
/// 多寄存器数值的解读器：把寄存器数组（NModbus 已按协议解析成数值）与目标类型的值互转。<br/>
/// <br/>
/// 链路上有三个"字节坐标系"，本类型负责接上后两个：<br/>
/// · <b>寄存器数值</b>（<c>ushort[]</c>，NModbus 已按协议解析成数值）；<br/>
/// · <b>设备端字节</b>（<c>deviceBytes</c>）—— 从寄存器数值<b>重建</b>出的"设备里这 4/8 个字节怎么摆"，
///   与真实链路上同几个寄存器的载荷字节<b>逐字节相同</b>（重建恰好是 NModbus 网络序 → 数值转换的逆运算），
///   所以它就是 <c>interpret</c> 描述的坐标；<br/>
/// · <b>值里的字节</b>（<c>valueBytes</c>）—— 值的规范大端字节（<c>valueBytes[0]</c> = 最高字节，
///   即 <c>BinaryPrimitives</c> 的约定）。<br/>
/// <br/>
/// 读 = 寄存器数值 → 设备端字节 → 按 <c>endian</c> + <c>interpret</c> 重排成"值里的字节" → 目标类型；
/// 写 = 反方向。排布的记法与校验见 <see cref="ModbusInterpret"/>。<br/>
/// <br/>
/// <b>为什么是"基类 + 具体实现"</b>：32/64 位 × 有无符号的"字节 → 值"转换各不相同，做成一个个具体类型，
/// 调用点就能直接拿到目标类型（<see cref="Read"/> / <see cref="Write"/>），不必先算出 <c>ulong</c> 再强转。<br/>
/// <br/>
/// <b>实例是复用的</b>：本类型<b>不可变</b>，全部状态只有"字节数 + 一个置换表"，与具体测点无关；而置换表只能取
/// 有限几种（32 位 4 种、64 位 48 种）。所以每个具体类型在首次使用时把它们一次性造成静态实例池，之后按
/// 排布编号取（见 <see cref="BuildInstances"/> 与各具体类型的 <c>For</c>）——同一型号、同记法的测点共用同一个
/// 对象，"每个测点一份"的开销降成常量级，也没有任何锁或延迟初始化技巧。
/// </summary>
/// <typeparam name="T">数值类型（<c>float</c> 复用 32 位无符号那条路：字节数相同）</typeparam>
internal abstract class ModbusValueInterpreter<T>
    where T : unmanaged
{
    private readonly int _byteCount;

    /// <summary>
    /// 落位表。<br/>
    /// 第i个元素值，表示字节数组里的第 i 个字节应该处在设备端字节里的位置；
    /// 空表 = 恒等排布<br/>
    /// </summary>
    private readonly ReadOnlyMemory<byte> _deviceIndexOfValueByte;

    /// <summary>
    /// c'tor
    /// </summary>
    /// <param name="byteCount">该数值占用的字节数（4/8）</param>
    /// <param name="deviceIndexOfValueByte">落位表：第i个元素值，表示值字节数组里的第 i 个字节在设备端字节里的下标；空表 = 恒等排布（无需搬运）</param>
    private protected ModbusValueInterpreter(int byteCount, ReadOnlyMemory<byte> deviceIndexOfValueByte)
    {
        this._byteCount = byteCount;
        this._deviceIndexOfValueByte = deviceIndexOfValueByte;
    }

    /// <summary>
    /// 该数值占用的字节数（4/8）
    /// </summary>
    internal int ByteCount => this._byteCount;

    /// <summary>
    /// 该数值占用的寄存器数（= 字节数 / 2）
    /// </summary>
    internal int RegisterCount => this._byteCount / 2;

    /// <summary>
    /// 寄存器数组 → 值
    /// </summary>
    internal T Read(ReadOnlySpan<ushort> registers)
    {
        Span<byte> deviceBytes = stackalloc byte[8];
        Span<byte> valueBytes = stackalloc byte[8];
        RegistersToDeviceBytes(registers, deviceBytes);
        this.DeviceBytesToValueBytes(deviceBytes, valueBytes);
        return this.FromValueBytes(valueBytes);
    }

    /// <summary>
    /// 值 → 寄存器数组，写入 <paramref name="registers"/>
    /// </summary>
    internal void Write(T value, Span<ushort> registers)
    {
        Span<byte> valueBytes = stackalloc byte[8];
        Span<byte> deviceBytes = stackalloc byte[8];
        this.ToValueBytes(value, valueBytes);
        this.ValueBytesToDeviceBytes(valueBytes, deviceBytes);
        DeviceBytesToRegisters(deviceBytes, registers);
    }

    /// <summary>
    /// "值里的字节"（大端顺序：<c>valueBytes[0]</c> 是最高字节）→ 值
    /// </summary>
    protected abstract T FromValueBytes(ReadOnlySpan<byte> valueBytes);

    /// <summary>
    /// 值 → "值里的字节"（大端顺序，与 <see cref="FromValueBytes"/> 对称）
    /// </summary>
    protected abstract void ToValueBytes(T value, Span<byte> valueBytes);

    /// <summary>
    /// 一次造好全部合法排布的实例（下标与 <see cref="ModbusInterpret.VariantIndex"/> 同序）
    /// </summary>
    /// <param name="byteCount">该数值占用的字节数（4/8）</param>
    /// <param name="create">具体类型的工厂（只需把置换表交给它自己的 ctor）</param>
    /// <remarks>
    /// 每张置换表都由 <see cref="ModbusInterpret.EnumeratePackings"/> 里的同一个数组实例给出，
    /// 相邻实例共享同一张表（不复制）；恒等排布是 <c>null</c>，读路径直接整段拷贝。
    /// </remarks>
    private protected static ModbusValueInterpreter<T>[] BuildInstances(
        int byteCount,
        Func<ReadOnlyMemory<byte>, ModbusValueInterpreter<T>> create)
    {
        var packings = ModbusInterpret.EnumeratePackings(byteCount);
        var instances = new ModbusValueInterpreter<T>[packings.Length];
        for (var i = 0; i < packings.Length; i++)
        {
            instances[i] = create(packings[i]);
        }
        return instances;
    }

    /// <summary>
    /// 取某个测点该用的实例下标（<b>这里做加载期校验</b>：记法的长度/字符集/重复/与 <c>endian</c> 的一致性）
    /// </summary>
    /// <param name="descriptor">测点描述符</param>
    /// <param name="byteCount">该数值占用的字节数（4/8）</param>
    /// <exception cref="TagsProjectXmlException">记法本身不合法</exception>
    private protected static int VariantIndexOf(TagDescriptor descriptor, int byteCount) =>
        ModbusInterpret.VariantIndex(descriptor, byteCount, $"Tag({descriptor.TagName})");

    /// <summary>
    /// 把寄存器数值 变成设备端字节数组（协议规定每个寄存器内部大端）
    /// </summary>
    private static void RegistersToDeviceBytes(ReadOnlySpan<ushort> registers, Span<byte> deviceBytes)
    {
        for (var i = 0; i < registers.Length; i++)
        {
            deviceBytes[i * 2] = (byte)(registers[i] >> 8);
            deviceBytes[(i * 2) + 1] = (byte)registers[i];
        }
    }

    /// <summary>
    /// 设备端字节 → 寄存器数值（每个寄存器内部大端）
    /// </summary>
    private static void DeviceBytesToRegisters(ReadOnlySpan<byte> deviceBytes, Span<ushort> registers)
    {
        for (var i = 0; i < registers.Length; i++)
        {
            registers[i] = (ushort)((deviceBytes[i * 2] << 8) | deviceBytes[(i * 2) + 1]);
        }
    }

    /// <summary>
    /// 使用落位表，把设备端字节数组 解读成 值的字节数组
    /// </summary>
    /// <param name="deviceBytes"></param>
    /// <param name="valueBytes"></param>
    private void DeviceBytesToValueBytes(ReadOnlySpan<byte> deviceBytes, Span<byte> valueBytes)
    {
        if (this._deviceIndexOfValueByte.IsEmpty)
        {
            // 恒等排布（ABCD…）
            deviceBytes.Slice(0, this._byteCount).CopyTo(valueBytes);
            return;
        }

        for (var i = 0; i < this._byteCount; i++)
        {
            valueBytes[i] = deviceBytes[this._deviceIndexOfValueByte.Span[i]];
        }
    }

    /// <summary>
    /// 使用落位表，把值的字节数组 排布成 设备端字节数组
    /// </summary>
    /// <param name="valueBytes"></param>
    /// <param name="deviceBytes"></param>
    private void ValueBytesToDeviceBytes(ReadOnlySpan<byte> valueBytes, Span<byte> deviceBytes)
    {
        if (this._deviceIndexOfValueByte.IsEmpty)
        {
            valueBytes.Slice(0, this._byteCount).CopyTo(deviceBytes);
            return;
        }

        for (var i = 0; i < this._byteCount; i++)
        {
            deviceBytes[this._deviceIndexOfValueByte.Span[i]] = valueBytes[i];
        }
    }
}
