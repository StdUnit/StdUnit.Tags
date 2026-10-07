using System;
using System.Threading;
using System.Threading.Tasks;
using StdUnit.Tags;
using StdUnit.Tags.ModbusTcp;

namespace StdUnit.Tags.Tests.ModbusTags;

/// <summary>
/// 测试用 Modbus 通道：不连接任何设备，只记录最后一次读写时收到的地址字符串，
/// 用于验证组合子（Cbnt）实际把哪个地址交给通道。
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
        return Task.FromResult(new bool[bitCount]);
    }

    /// <inheritdoc/>
    public Task WriteBitsAsync(string address, bool[] bits, CancellationToken ct)
    {
        this.LastBitAddress = address;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<ushort[]> ReadRegistersAsync(string address, int registerCount, CancellationToken ct)
    {
        this.LastRegisterAddress = address;
        return Task.FromResult(new ushort[registerCount]);
    }

    /// <inheritdoc/>
    public Task WriteRegistersAsync(string address, ReadOnlyMemory<ushort> registers, CancellationToken ct)
    {
        this.LastRegisterAddress = address;
        return Task.CompletedTask;
    }
}
