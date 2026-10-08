using System;
using System.Threading;
using System.Threading.Tasks;
using StdUnit.Tags;
using StdUnit.Tags.ModbusTcp;

namespace StdUnit.Tags.Tests.ModbusTags;

/// <summary>
/// 测试用 Modbus 通道：不连接任何设备，只记录最后一次读写时收到的地址字符串，
/// 用于验证组合子（Cbnt）实际把哪个地址交给通道。<br/>
/// <see cref="RegisterPayload"/> / <see cref="BitPayload"/> 用来模拟设备读回的内容，
/// 让"同一份寄存器数据走不同路径"的对比成为可能。
/// </summary>
internal class FakeModbusChannel : IModbusBitsChannel, IModbusRegisterChannel
{
    /// <summary>
    /// 最后一次位读写收到的地址
    /// </summary>
    public string? LastBitAddress { get; private set; }

    /// <summary>
    /// 最后一次寄存器读写收到的地址
    /// </summary>
    public string? LastRegisterAddress { get; private set; }

    /// <summary>
    /// 读寄存器时返回的内容（按索引取，不足部分补 0）
    /// </summary>
    public ushort[] RegisterPayload { get; set; } = Array.Empty<ushort>();

    /// <summary>
    /// 读位时返回的内容（按索引取，不足部分补 false）
    /// </summary>
    public bool[] BitPayload { get; set; } = Array.Empty<bool>();

    /// <summary>
    /// 最后一次写寄存器真正发出的内容
    /// </summary>
    public ushort[]? LastWrittenRegisters { get; private set; }

    /// <summary>
    /// 最后一次写位真正发出的内容
    /// </summary>
    public bool[]? LastWrittenBits { get; private set; }

    /// <inheritdoc/>
    public TagChannelDescriptor Descriptor { get; } = new TagChannelDescriptor
    {
        Name = "fake-modbus",
        Driver = ModbusTcpNames.DriverName,
    };

    /// <inheritdoc/>
    public Task EnsureConnectedAsync(bool force, CancellationToken ct) => Task.CompletedTask;

    /// <inheritdoc/>
    public Task DisconnectAsync(CancellationToken ct) => Task.CompletedTask;

    /// <inheritdoc/>
    public void Dispose()
    {
    }

    /// <inheritdoc/>
    public Task<bool[]> ReadBitsAsync(string address, int bitCount, CancellationToken ct)
    {
        this.LastBitAddress = address;
        var result = new bool[bitCount];
        for (var i = 0; i < bitCount && i < this.BitPayload.Length; i++)
        {
            result[i] = this.BitPayload[i];
        }
        return Task.FromResult(result);
    }

    /// <inheritdoc/>
    public Task WriteBitsAsync(string address, bool[] bits, CancellationToken ct)
    {
        this.LastBitAddress = address;
        this.LastWrittenBits = bits;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<ushort[]> ReadRegistersAsync(string address, int registerCount, CancellationToken ct)
    {
        this.LastRegisterAddress = address;
        var result = new ushort[registerCount];
        for (var i = 0; i < registerCount && i < this.RegisterPayload.Length; i++)
        {
            result[i] = this.RegisterPayload[i];
        }
        return Task.FromResult(result);
    }

    /// <inheritdoc/>
    public Task WriteRegistersAsync(string address, ReadOnlyMemory<ushort> registers, CancellationToken ct)
    {
        this.LastRegisterAddress = address;
        this.LastWrittenRegisters = registers.ToArray();
        return Task.CompletedTask;
    }
}
