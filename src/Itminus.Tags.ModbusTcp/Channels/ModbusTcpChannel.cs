using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using NModbus;
using System.Net.Sockets;

namespace Itminus.Tags.ModbusTcp;

/// <summary>
/// ModbusTcp通道
/// </summary>
public class ModbusTcpChannel : IModbusRegisterChannel, IModbusBitsChannel
{
    private readonly ILogger<ModbusTcpChannel> _logger;

    private SemaphoreSlim _connSignal = new SemaphoreSlim(1, 1);

    /// <summary>
    /// Modbus 读保持/输入寄存器(FC03/FC04)单帧最大寄存器数：125。<br/>
    /// 见 MODBUS Application Protocol V1.1b3 6.3/6.4 节。<br/>
    /// 可通过 <see cref="ModbusTcpItem.MaxReadRegisters"/> 配置更小的值（设备上限可能小于协议值）。<br/>
    /// </summary>
    public const ushort MaxReadRegistersPerPdu = 125;

    /// <summary>
    /// Modbus 读线圈/离散输入(FC01/FC02)单帧最大点数：2000。<br/>
    /// 见 MODBUS Application Protocol V1.1b3 6.1/6.2 节。<br/>
    /// 可通过 <see cref="ModbusTcpItem.MaxReadBits"/> 配置更小的值（设备上限可能小于协议值）。<br/>
    /// </summary>
    public const ushort MaxReadBitsPerPdu = 2000;

    /// <summary>
    /// Modbus 写保持寄存器(FC16)单帧最大寄存器数：123。<br/>
    /// 见 MODBUS Application Protocol V1.1b3 6.11 节。<br/>
    /// 可通过 <see cref="ModbusTcpItem.MaxWriteRegisters"/> 配置更小的值（设备上限可能小于协议值）。<br/>
    /// </summary>
    public const ushort MaxWriteRegistersPerPdu = 123;

    #region 配置
    private readonly ModbusTcpItem _modbusItem;
    /// <summary>
    /// 单帧最多写入的寄存器数量(FC16)。null 表示使用协议默认值(123)。
    /// </summary>
    public ushort? MaxWriteRegisters => _modbusItem.MaxWriteRegisters;
    /// <summary>
    /// 单帧最多读取的寄存器数量(FC03/FC04)。null 表示使用协议默认值(125)。
    /// </summary>
    public ushort? MaxReadRegisters => _modbusItem.MaxReadRegisters;
    /// <summary>
    /// 单帧最多读取的位数/点数(FC01/FC02)。null 表示使用协议默认值(2000)。
    /// </summary>
    public ushort? MaxReadBits => _modbusItem.MaxReadBits;
    /// <summary>
    /// IP 地址
    /// </summary>
    public string IpAddr => _modbusItem.IpAddr;

    /// <summary>
    /// 端口号
    /// </summary>
    public int Port => _modbusItem.Port;

    /// <summary>
    /// 连接超时
    /// </summary>
    public int ConnTimeout => _modbusItem.ConnTimeout;

    /// <summary>
    /// 读超时
    /// </summary>
    public int ReadTimeout => _modbusItem.ReadTimeout;

    /// <summary>
    /// 写超时
    /// </summary>
    public int WriteTimeout => _modbusItem.WriteTimeout;
    #endregion


    /// <inheritdoc/>
    public TagChannelDescriptor Descriptor { get; }

    /// <summary>
    /// c'tor
    /// </summary>
    public ModbusTcpChannel(ModbusTcpTagChannelDescriptor descriptor, ILogger<ModbusTcpChannel> logger)
    {
        this.Descriptor = descriptor;
        this._modbusItem = new ModbusTcpItem()
        {
            IpAddr = descriptor.IpAddr,
            Port = descriptor.Port,
            MaxWriteRegisters = descriptor.MaxWriteRegisters,
            MaxReadRegisters = descriptor.MaxReadRegisters,
            MaxReadBits = descriptor.MaxReadBits,
        }; ;
        _logger = logger;
    }

    #region 连接
    /// <summary>
    /// 底层 TCP 客户端
    /// </summary>
    protected TcpClient? _tcpClient;

    /// <summary>
    /// 底层 Modbus 主站对象
    /// </summary>
    internal IModbusMaster? ModbusMaster { get; private set; }

    /// <summary>
    /// 创建连接并初始化
    /// </summary>
    /// <returns></returns>
    protected virtual async Task<IModbusMaster> CreateConnectionAsync(int timeout, CancellationToken ct)
    {
        var channelName = this.ChannelName();
        var entered = await _connSignal.WaitAsync(timeout, ct);
        if (!entered)
        {
            throw new TimeoutException($"ModbusTcp 通道={channelName} 在创建连接前，获取锁超时！");
        }
        try
        {
            _tcpClient = new TcpClient();
            await Compat.TcpConnectionCompat.ConnectAsync(_tcpClient, IpAddr, Port, ct);
            var factory = new ModbusFactory();
            var mb = factory.CreateMaster(_tcpClient);
            mb.Transport.ReadTimeout = ReadTimeout;
            mb.Transport.WriteTimeout = WriteTimeout;
            _logger.LogInformation("ModbusMaster 初始化完成: 设备名={channelName}; addr={IpAddr}; port={Port}", channelName, IpAddr, Port);
            return mb;
        }
        finally
        {
            _connSignal.Release();
        }
    }

    /// <summary>
    /// 当前TCP是否已经连接
    /// </summary>
    protected virtual bool Connected
    {
        get
        {
            var s = _tcpClient?.Client;
            if (s == null)
            {
                return false;
            }
            if (!s.Connected)
            {
                return false;
            }
            // see https://stackoverflow.com/a/2661876/10091607
            bool part1 = s.Poll(1000, SelectMode.SelectRead);
            bool part2 = s.Available == 0;
            if (part1 && part2)
                return false;
            else
                return true;
        }
    }

    /// <summary>
    /// 确保已经建立连接
    /// </summary>
    /// <returns></returns>
    public async Task EnsureConnectedAsync(bool force, CancellationToken ct)
    {
        //当前client存在并且连接有效
        if (Connected)
        {
            return;
        }

        this.ModbusMaster = await CreateConnectionAsync(ConnTimeout, ct);
        return;
    }

    /// <summary>
    /// 断开连接
    /// </summary>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    public Task DisconnectAsync(CancellationToken ct)
    {
        if (_tcpClient == null)
            return Task.CompletedTask;

        var tcs = new TaskCompletionSource<object?>();
        var th = new Thread(() =>
        {
            try
            {
                _tcpClient.Close();
                _tcpClient = null;
                tcs.SetResult(null);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("通道{ChannelName}断开连接失败：{message}\r\n{stackTrace}", this.ChannelName(), ex.Message, ex.StackTrace);
                _tcpClient = null;
                tcs.SetException(ex);
            }
        });
        th.IsBackground = true;
        th.Start();
        return tcs.Task;
    }
    #endregion



    #region 读写

    /// <inheritdoc/>
    public virtual async Task<bool[]> ReadBitsAsync(string address, int bitCount, CancellationToken ct)
    {
        var addr = ModBusTcpAddressParser.Parse(address);
        if (addr.Area == RegisterKinds.InputContacts)
        {
            var pointsCount = (ushort)bitCount;

            // 单帧最多读取的点数(FC02)，默认协议上限 2000；若设备上限更小，可通过 MaxReadBits 配置
            var batchLimit = _modbusItem.MaxReadBits ?? MaxReadBitsPerPdu;
            var flags = new List<bool>(pointsCount);
            ushort offset = 0;
            while (offset < pointsCount)
            {
                var batchSize = (ushort)Math.Min(pointsCount - offset, batchLimit);
                var part = await ModbusMaster!.ReadInputsAsync(addr.SlaveAddress, (ushort)(addr.StartPoint + offset), batchSize);
                flags.AddRange(part);
                offset += batchSize;
            }
            return flags.ToArray();
        }

        if (addr.Area == RegisterKinds.OutputCoils)
        {
            var pointsCount = (ushort)bitCount;

            // 单帧最多读取的点数(FC01)，默认协议上限 2000；若设备上限更小，可通过 MaxReadBits 配置
            var batchLimit = _modbusItem.MaxReadBits ?? MaxReadBitsPerPdu;
            var flags = new List<bool>(pointsCount);
            ushort offset = 0;
            while (offset < pointsCount)
            {
                var batchSize = (ushort)Math.Min(pointsCount - offset, batchLimit);
                var part = await ModbusMaster!.ReadCoilsAsync(addr.SlaveAddress, (ushort)(addr.StartPoint + offset), batchSize);
                flags.AddRange(part);
                offset += batchSize;
            }
            return flags.ToArray();
        }

        throw new Exception($"ReadBitsAsync 仅支持位空间(InputContacts/OutputCoils)，当前地址区域={addr.Area}");
    }

    /// <inheritdoc/>
    public virtual async Task WriteBitsAsync(string address, bool[] bits, CancellationToken ct)
    {
        var addr = ModBusTcpAddressParser.Parse(address);
        if (addr.Area != RegisterKinds.OutputCoils)
        {
            throw new Exception($"WriteBitsAsync 仅支持线圈(OutputCoils)，当前地址区域={addr.Area}");
        }
        await ModbusMaster!.WriteMultipleCoilsAsync(addr.SlaveAddress, addr.StartPoint, bits);
    }

    /// <inheritdoc/>
    public virtual async Task<ushort[]> ReadRegistersAsync(string address, int registerCount, CancellationToken ct)
    {
        var addr = ModBusTcpAddressParser.Parse(address);
        if (addr.Area == RegisterKinds.HoldingRegisters)
        {
            var pointsCount = (ushort)registerCount;

            // 单帧最多读取的寄存器数(FC03)，默认协议上限 125；若设备上限更小，可通过 MaxReadRegisters 配置
            var batchLimit = _modbusItem.MaxReadRegisters ?? MaxReadRegistersPerPdu;
            ushort[] points = new ushort[pointsCount];
            ushort offset = 0;
            while (offset < pointsCount)
            {
                var batchSize = (ushort)Math.Min(pointsCount - offset, batchLimit);
                var part = await ModbusMaster!.ReadHoldingRegistersAsync(addr.SlaveAddress, (ushort)(addr.StartPoint + offset), batchSize);
                Array.Copy(part, 0, points, offset, batchSize);
                offset += batchSize;
            }
            return points;
        }

        if (addr.Area == RegisterKinds.InputRegisters)
        {
            var pointsCount = (ushort)registerCount;

            // 单帧最多读取的寄存器数(FC04)，默认协议上限 125；若设备上限更小，可通过 MaxReadRegisters 配置
            var batchLimit = _modbusItem.MaxReadRegisters ?? MaxReadRegistersPerPdu;
            var points = new ushort[pointsCount];
            ushort offset = 0;
            while (offset < pointsCount)
            {
                var batchSize = (ushort)Math.Min(pointsCount - offset, batchLimit);
                var part = await ModbusMaster!.ReadInputRegistersAsync(addr.SlaveAddress, (ushort)(addr.StartPoint + offset), batchSize);
                Array.Copy(part, 0, points, offset, batchSize);
                offset += batchSize;
            }
            return points;
        }

        throw new Exception($"ReadRegistersAsync 仅支持寄存器区域(Holding/Input)，当前地址区域={addr.Area}");
    }


    /// <inheritdoc/>
    public virtual async Task WriteRegistersAsync(string address, ReadOnlyMemory<ushort> registers, CancellationToken ct)
    {
        var addr = ModBusTcpAddressParser.Parse(address);
        if (addr.Area != RegisterKinds.HoldingRegisters)
        {
            throw new Exception($"WriteRegistersAsync 仅支持保持寄存器(Holding)，当前地址区域={addr.Area}");
        }

        var payload = GetArray(registers);
        if (payload.Length == 0)
        {
            return;
        }
        var maxBatch = _modbusItem.MaxWriteRegisters.HasValue ?
             _modbusItem.MaxWriteRegisters.Value :
            MaxWriteRegistersPerPdu;

        // 快路径：单批即可容纳全部寄存器，一次写入，避免分批与切片分配
        if (payload.Length <= maxBatch)
        {
            await ModbusMaster!.WriteMultipleRegistersAsync(addr.SlaveAddress, addr.StartPoint, payload);
            return;
        }

        ushort offset = 0;
        while (offset < payload.Length)
        {
            var currlen = (ushort)Math.Min(payload.Length - offset, maxBatch);
            var subbytes = payload.AsSpan().Slice(offset, currlen).ToArray();

            ushort effectiveOffset = (ushort)(addr.StartPoint + offset);
            await ModbusMaster!.WriteMultipleRegistersAsync(addr.SlaveAddress, effectiveOffset, subbytes);
            offset += currlen;
        }
    }

    /// <summary>
    /// 从 <see cref="ReadOnlyMemory{T}"/> 零拷贝取出底层数组（当内存是数组-backed 时）；否则拷贝。
    /// </summary>
    private static ushort[] GetArray(ReadOnlyMemory<ushort> memory)
    {
        if (MemoryMarshal.TryGetArray(memory, out var segment) && segment.Offset == 0 && segment.Count == memory.Length)
        {
            return segment.Array!;
        }
        return memory.ToArray();
    }
    #endregion

    /// <inheritdoc/>
    public void Dispose()
    {
        var channelName = this.ChannelName();
        if (_tcpClient != null && _tcpClient.Connected)
        {
            try
            {
                _logger.LogInformation("通道={ChannelName} 正在断开连接...", channelName);
                _tcpClient.Close();
                _logger.LogInformation("通道={ChannelName}  断开连接完成!", channelName);
            }
            catch (Exception e)
            {
                _logger.LogWarning("通道={ChannelName} 释放异常:{exception}", channelName, e.Message);
            }
            finally
            {

            }
        }
    }
}