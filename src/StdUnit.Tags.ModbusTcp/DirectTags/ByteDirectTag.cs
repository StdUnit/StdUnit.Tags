namespace StdUnit.Tags.ModbusTcp;

/// <summary>
/// Byte DirectTag：占 1 个寄存器的半字（高字节或低字节）。<br/>
/// 读复用 <see cref="MultipleBytesDirectTag{T}"/> 模板；写必须 read-modify-write（只改目标字节、保留另一字节）。
/// </summary>
internal class ByteDirectTag : MultipleBytesDirectTag<byte>
{
    public ByteDirectTag(TagDescriptor descriptor, ModbusTcpChannel? thisChannel, TagContainer container)
        : base(descriptor, thisChannel, container)
    {
    }

    protected override int RegisterCount => 1;

    /// <summary>
    /// 取寄存器的高字节还是低字节。EndianKind=BigEndian（标准 Modbus，寄存器高字节在前）→ 高字节；否则低字节。
    /// </summary>
    private byte PickByte(ushort reg) =>
        this.TagDescriptor.EndianKind is EndianKinds.BigEndian ? (byte)(reg >> 8) : (byte)reg;

    protected override byte GetValueFromRegisters(ReadOnlySpan<ushort> registers)
        => this.PickByte(registers[0]);

    /// <summary>
    /// 仅占寄存器一半：保留另一半，写入目标字节（read-modify-write）。
    /// </summary>
    protected override void FillRegisters(byte value, Span<ushort> registers)
    {
        // 基类 WriteAsync 传入新分配的寄存器数组（全零）；RMW 需要读回旧值，因此本类重写 WriteAsync，此处保持语义一致但实际不用。
        throw new NotSupportedException($"{nameof(ByteDirectTag)} 使用 read-modify-write 写路径，不会调用 {nameof(FillRegisters)}");
    }

    public override async Task WriteAsync(CancellationToken ct)
    {
        // read-modify-write：读出当前寄存器，替换目标字节后写回
        var regs = await this._bubbleChannel.ReadRegistersAsync(this.NormalizedAddress(), 1, ct);
        var reg = regs[0];
        var newReg = this.TagDescriptor.EndianKind is EndianKinds.BigEndian
            ? (ushort)((this._value << 8) | (reg & 0x00FF))
            : (ushort)((reg & 0xFF00) | this._value);

        await this._bubbleChannel.WriteRegistersAsync(this.NormalizedAddress(), new ushort[] { newReg }, ct);
        this.IsDirty = false;
        this.NotifyTagWritten(this._value);
    }
}
