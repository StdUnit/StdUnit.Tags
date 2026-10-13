using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace StdUnit.Tags.Tests.ModbusTags;

/// <summary>
/// 最小可用的假 Modbus TCP 服务端。<br/>
/// <br/>
/// 它的存在价值是让测试用<b>真 socket + 真 NModbus</b>走一遍协议，从而能断言"线上字节 ↔ 数值/测点值"
/// 的对应关系，而不是靠读源码推测端序语义。支持：<br/>
/// FC01/FC02（读线圈/离散输入）、FC03/FC04（读保持/输入寄存器）、FC15（写多个线圈）、
/// FC16（写多个寄存器）；其它功能码回异常帧（0x01 非法功能）。<br/>
/// <br/>
/// 数据按"绝对参考号 1 起算后的内部下标"存取（与 <c>ModbusTcpAddress.StartPoint</c> 同一套下标），
/// 未设置的下标一律返回 0。<b>线上字节与数值的换算严格按 Modbus 规范</b>（寄存器大端、位按 LSB 先填）。
/// </summary>
internal sealed class FakeModbusTcpServer : IDisposable
{
    private readonly TcpListener _listener;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _loop;
    private readonly object _gate = new();
    private readonly Dictionary<ushort, ushort> _registers = new();
    private readonly Dictionary<ushort, bool> _bits = new();

    /// <summary>
    /// c'tor：监听环回地址上的随机空闲端口
    /// </summary>
    public FakeModbusTcpServer()
    {
        this._listener = new TcpListener(IPAddress.Loopback, 0);
        this._listener.Start();
        this.Port = ((IPEndPoint)this._listener.LocalEndpoint).Port;
        this._loop = Task.Run(this.AcceptLoopAsync);
    }

    /// <summary>
    /// 监听端口
    /// </summary>
    public int Port { get; }

    /// <summary>
    /// 已处理过的请求数
    /// </summary>
    public int RequestCount { get; private set; }

    /// <summary>
    /// 最后一次 FC16 写入的数据（已按规范从大端字节还原成寄存器数值）
    /// </summary>
    public ushort[]? LastRegistersWritten { get; private set; }

    /// <summary>
    /// 最后一次 FC16 写入的起始下标
    /// </summary>
    public ushort? LastRegistersWriteStart { get; private set; }

    /// <summary>
    /// 最后一次 FC15 写入的数据
    /// </summary>
    public bool[]? LastBitsWritten { get; private set; }

    /// <summary>
    /// 最后一次 FC15 写入的起始下标
    /// </summary>
    public ushort? LastBitsWriteStart { get; private set; }

    /// <summary>
    /// 设置寄存器内容（<paramref name="start"/> 为内部下标：参考号 40001 → 0）
    /// </summary>
    public void SetRegisters(ushort start, params ushort[] values)
    {
        lock (this._gate)
        {
            for (var i = 0; i < values.Length; i++)
            {
                this._registers[(ushort)(start + i)] = values[i];
            }
        }
    }

    /// <summary>
    /// 设置位内容（<paramref name="start"/> 为内部下标：参考号 00001 → 0）
    /// </summary>
    public void SetBits(ushort start, params bool[] values)
    {
        lock (this._gate)
        {
            for (var i = 0; i < values.Length; i++)
            {
                this._bits[(ushort)(start + i)] = values[i];
            }
        }
    }

    /// <summary>
    /// 读取寄存器内容（用于断言"写出去的东西真的落到了设备上"）
    /// </summary>
    public ushort[] GetRegisters(ushort start, int count)
    {
        var result = new ushort[count];
        lock (this._gate)
        {
            for (var i = 0; i < count; i++)
            {
                result[i] = this._registers.TryGetValue((ushort)(start + i), out var v) ? v : (ushort)0;
            }
        }
        return result;
    }

    private ushort[] ReadRegisters(ushort start, ushort count) => this.GetRegisters(start, count);

    private bool[] ReadBits(ushort start, ushort count)
    {
        var result = new bool[count];
        lock (this._gate)
        {
            for (var i = 0; i < count; i++)
            {
                result[i] = this._bits.TryGetValue((ushort)(start + i), out var v) && v;
            }
        }
        return result;
    }

    private async Task AcceptLoopAsync()
    {
        while (!this._cts.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await this._listener.AcceptTcpClientAsync();
            }
            catch (Exception)
            {
                return;
            }
            _ = Task.Run(() => this.ServeAsync(client));
        }
    }

    private async Task ServeAsync(TcpClient client)
    {
        try
        {
            using (client)
            using (var stream = client.GetStream())
            {
                while (!this._cts.IsCancellationRequested)
                {
                    var header = await ReadExactAsync(stream, 7);
                    if (header is null)
                    {
                        return;
                    }
                    var length = (header[4] << 8) | header[5];
                    var pdu = await ReadExactAsync(stream, length - 1);
                    if (pdu is null)
                    {
                        return;
                    }

                    var response = this.Handle(header, pdu);
                    await stream.WriteAsync(response, 0, response.Length);
                    await stream.FlushAsync();
                }
            }
        }
        catch (Exception)
        {
            // 测试主动关闭连接时的正常异常，忽略
        }
    }

    private static async Task<byte[]?> ReadExactAsync(NetworkStream stream, int count)
    {
        var buffer = new byte[count];
        var read = 0;
        while (read < count)
        {
            var n = await stream.ReadAsync(buffer, read, count - read);
            if (n == 0)
            {
                return null;
            }
            read += n;
        }
        return buffer;
    }

    private byte[] Handle(byte[] header, byte[] pdu)
    {
        this.RequestCount++;
        var functionCode = pdu[0];
        var unitId = header[6];

        switch (functionCode)
        {
            // 读线圈 / 读离散输入
            case 1:
            case 2:
            {
                var start = ReadUInt16(pdu, 1);
                var count = ReadUInt16(pdu, 3);
                var bits = this.ReadBits(start, count);
                var packed = PackBits(bits);
                return this.BuildDataResponse(header, unitId, functionCode, packed);
            }

            // 读保持寄存器 / 读输入寄存器
            case 3:
            case 4:
            {
                var start = ReadUInt16(pdu, 1);
                var count = ReadUInt16(pdu, 3);
                var registers = this.ReadRegisters(start, count);
                var bytes = new byte[registers.Length * 2];
                for (var i = 0; i < registers.Length; i++)
                {
                    bytes[i * 2] = (byte)(registers[i] >> 8);
                    bytes[(i * 2) + 1] = (byte)registers[i];
                }
                return this.BuildDataResponse(header, unitId, functionCode, bytes);
            }

            // 写多个线圈
            case 15:
            {
                var start = ReadUInt16(pdu, 1);
                var count = ReadUInt16(pdu, 3);
                var bits = UnpackBits(pdu, 6, count);
                this.LastBitsWriteStart = start;
                this.LastBitsWritten = bits;
                lock (this._gate)
                {
                    for (var i = 0; i < bits.Length; i++)
                    {
                        this._bits[(ushort)(start + i)] = bits[i];
                    }
                }
                return this.BuildEchoResponse(header, unitId, functionCode, pdu);
            }

            // 写多个寄存器
            case 16:
            {
                var start = ReadUInt16(pdu, 1);
                var count = ReadUInt16(pdu, 3);
                var registers = new ushort[count];
                for (var i = 0; i < count; i++)
                {
                    registers[i] = (ushort)((pdu[6 + (i * 2)] << 8) | pdu[7 + (i * 2)]);
                }
                this.LastRegistersWriteStart = start;
                this.LastRegistersWritten = registers;
                lock (this._gate)
                {
                    for (var i = 0; i < registers.Length; i++)
                    {
                        this._registers[(ushort)(start + i)] = registers[i];
                    }
                }
                return this.BuildEchoResponse(header, unitId, functionCode, pdu);
            }

            default:
                return this.BuildExceptionResponse(header, unitId, functionCode, 0x01);
        }
    }

    private byte[] BuildDataResponse(byte[] header, byte unitId, byte functionCode, byte[] data)
    {
        var pduLength = 3 + data.Length;   // unitId + FC + byteCount + data
        var response = new byte[6 + pduLength];
        Array.Copy(header, 0, response, 0, 4);          // 事务号 + 协议号
        response[4] = (byte)(pduLength >> 8);
        response[5] = (byte)pduLength;
        response[6] = unitId;
        response[7] = functionCode;
        response[8] = (byte)data.Length;
        Array.Copy(data, 0, response, 9, data.Length);
        return response;
    }

    private byte[] BuildEchoResponse(byte[] header, byte unitId, byte functionCode, byte[] pdu)
    {
        var response = new byte[12];
        Array.Copy(header, 0, response, 0, 4);
        response[4] = 0;
        response[5] = 6;                                 // unitId + FC + start(2) + count(2)
        response[6] = unitId;
        response[7] = functionCode;
        Array.Copy(pdu, 1, response, 8, 4);              // 回显起始下标与数量
        return response;
    }

    private byte[] BuildExceptionResponse(byte[] header, byte unitId, byte functionCode, byte exceptionCode)
    {
        var response = new byte[9];
        Array.Copy(header, 0, response, 0, 4);
        response[4] = 0;
        response[5] = 3;
        response[6] = unitId;
        response[7] = (byte)(functionCode | 0x80);
        response[8] = exceptionCode;
        return response;
    }

    private static ushort ReadUInt16(byte[] bytes, int offset) => (ushort)((bytes[offset] << 8) | bytes[offset + 1]);

    private static byte[] PackBits(bool[] bits)
    {
        var bytes = new byte[(bits.Length + 7) / 8];
        for (var i = 0; i < bits.Length; i++)
        {
            if (bits[i])
            {
                bytes[i / 8] = (byte)(bytes[i / 8] | (1 << (i % 8)));
            }
        }
        return bytes;
    }

    private static bool[] UnpackBits(byte[] pdu, int offset, int count)
    {
        var bits = new bool[count];
        for (var i = 0; i < count; i++)
        {
            bits[i] = ((pdu[offset + (i / 8)] >> (i % 8)) & 1) != 0;
        }
        return bits;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        this._cts.Cancel();
        this._listener.Stop();
    }
}
