using Microsoft.Extensions.Logging;
using System.IO.Ports;
using System.Threading.Channels;


namespace StdUnit.Tags.ComScanner.Channels;

/// <summary>
/// 抽象基类，表示基于串口的Tag通道。<br/>
/// 每一次读取一个数据包（读取方法由子类定义）放入待处理的队列中，并触发DataReceived事件。
/// 外部可以通过<see cref="TryDequeueInput(out T?)"/> 方法从队列中读取数据包进行处理。
/// </summary>
/// <typeparam name="T"></typeparam>
public abstract class ComChannelBase<T> : ITagChannel
{
    /// <summary>
    /// 通道选项
    /// </summary>
    protected readonly ComChannelOption _opt;

    /// <summary>
    /// 日志记录器
    /// </summary>
    protected readonly ILogger<ComChannelBase<T>> _logger;

    /// <summary>
    /// 换行符
    /// </summary>
    public string? NewLine { get; }

    /// <summary>
    /// 串口工厂委托。若设置，<see cref="CreateSerialPort(ComChannelOption)"/> 会优先调用此委托创建串口句柄。
    /// </summary>
    public Func<ComChannelOption, ISerialPortHandle>? SerialPortFactory { get; set; }

    /// <summary>
    /// 消息通道的容量
    /// </summary>
    public int Capacity { get; }

    /// <summary>
    /// 内部的消息通道，用于存储从串口读取的数据包
    /// </summary>
    protected Channel<T> _channel;


    /// <inheritdoc/>
    public TagChannelDescriptor Descriptor { get; }


    /// <summary>
    /// 底层串口句柄。
    /// 如果未连接，则为null
    /// </summary>
    public ISerialPortHandle? SerialPort { get; private set; }

    /// <summary>
    /// 连接/断开互斥信号量。确保 <see cref="EnsureConnectedAsync"/> 和 <see cref="DisconnectAsync"/> 不会并发执行。
    /// </summary>
    private readonly SemaphoreSlim _connSema = new SemaphoreSlim(1, 1);

    /// <summary>
    /// 读取互斥信号量。轮询线程通过此信号量独占读取操作。
    /// </summary>
    private readonly SemaphoreSlim _readSema = new SemaphoreSlim(1, 1);

    /// <summary>
    /// 写入互斥信号量。所有写操作通过此信号量串行化。
    /// 与读取（<see cref="_readSema"/>）不互斥——SerialPort 支持全双工读写。
    /// </summary>
    private readonly SemaphoreSlim _writeSema = new SemaphoreSlim(1, 1);

    /// <summary>
    /// 后台轮询线程的生命周期 CancellationTokenSource。
    /// <see cref="Dispose()"/> 时先取消此源以停止轮询，再安全释放资源。
    /// </summary>
    private CancellationTokenSource? _pollCts;


    /// <summary>
    /// c'tor
    /// </summary>
    protected ComChannelBase(ComChannelDescriptor descriptor, ILogger<ComChannelBase<T>> logger)
    {
        this.Descriptor = descriptor;
        this._opt = descriptor.Option;
        this.Capacity = descriptor.Option.ChannelCapacity <= 0 ? 1 : descriptor.Option.ChannelCapacity;
        this.NewLine = descriptor.Option.NewLine;
        this._logger = logger;


        this._channel = Channel.CreateBounded<T>(this.Capacity);
    }

    /// <summary>
    /// 确保连接。注意：如果已经连接，则不执行任何操作；如果未连接，则打开串口并启动轮询线程
    /// </summary>
    /// <param name="force"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public async virtual Task EnsureConnectedAsync(bool force, CancellationToken ct)
    {
        await this.ExecuteOneByOneAsync(
            _connSema,
            async ct =>
            {
                if (this.SerialPort != null && !force)
                {
                    return;
                }

                // 打开串口
                this.SerialPort = CreateSerialPort(this._opt);
                // 不能写成 string.IsNullOrEmpty —— net472 引用程序集无可空标注，
                // 编译器看不到 [NotNullWhen(false)]，无法收缩类型（会报 CS8601）。
                var newLine = this.NewLine;
                if (newLine is not null && newLine.Length > 0)
                {
                    this.SerialPort.NewLine = newLine;
                }
                this.SerialPort.Open();

                // 清空缓存
                if (this._channel is not null)
                {
                    this._channel.Writer.TryComplete();
                }
                this._channel = Channel.CreateBounded<T>(Capacity);

                // 启动轮询（使用内部 _pollCts 管理生命周期）
                var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                this._pollCts = cts;
                var pollToken = cts.Token;
                var t = new Thread(async () => await PollDataAsync(pollToken));
                t.IsBackground = true;
                t.Start();
            },
            ct
        );
    }

    /// <summary>
    /// 断开连接
    /// </summary>
    /// <param name="ct"></param>
    /// <returns></returns>
    public async virtual Task DisconnectAsync(CancellationToken ct)
    {
        await this.ExecuteOneByOneAsync(
            _connSema,
            async ct =>
            {
                // 先取消轮询线程，防止 PollDataAsync 在通道关闭后
                // 因 ChannelClosedException 递归调用 DisconnectAsync 导致死锁
                this._pollCts?.Cancel();

                this.SerialPort?.Close();
                this.SerialPort?.Dispose();
                this._channel.Writer.TryComplete();
                this.SerialPort = null;
            },
            ct
        );
    }

    /// <inheritdoc/>
    public virtual void Dispose()
    {
        // 1. 先取消轮询线程
        var cts = Interlocked.Exchange(ref _pollCts, null);
        cts?.Cancel();
        cts?.Dispose();

        // 2. 排干读取（等当前读操作完成）、写入（等当前写操作完成）、连接操作
        this._readSema.Wait();
        this._readSema.Release();
        this._readSema.Dispose();

        this._writeSema.Wait();
        this._writeSema.Release();
        this._writeSema.Dispose();

        this._connSema.Wait();
        try
        {
            this.SerialPort?.Dispose();
            this.SerialPort = null;
            this._channel.Writer.TryComplete();
        }
        finally
        {
            this._connSema.Release();
            this._connSema.Dispose();
        }
    }

    /// <summary>
    /// 创建串口句柄的工厂方法。
    /// 若设置了 <see cref="SerialPortFactory"/> 则优先使用委托；
    /// 否则默认创建 <see cref="SerialPortAdapter"/> 包装真实的 <see cref="System.IO.Ports.SerialPort"/>。
    /// 子类可重写此方法以自定义创建逻辑。
    /// </summary>
    protected virtual ISerialPortHandle CreateSerialPort(ComChannelOption opt)
    {
        return SerialPortFactory?.Invoke(opt)
            ?? new SerialPortAdapter(
                new SerialPort(opt.Port, opt.BaudRate, opt.Parity, opt.DataBits, opt.StopBits));
    }

    /// <summary>
    /// 从串口读取并解析一个数据包。如果未读到有效数据，返回 default(T?)。<br/>
    /// 子类在此方法中实现具体的读取+解析逻辑（如 ReadLine、ReadExisting 或脚本执行），
    /// </summary>
    /// <param name="sport">串口句柄</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>解析后的数据包，未读到有效数据时返回 default(T?)</returns>
    protected abstract Task<T?> ParseDataAsync(ISerialPortHandle sport, CancellationToken ct);

    private async Task PollDataAsync(CancellationToken ct)
    {
        try
        {
            if (this.SerialPort is null)
            {
                throw new InvalidOperationException($"通道({this.ChannelName()})的串口为null, 无法Poll");
            }
            while (!ct.IsCancellationRequested)
            {
                T? data;
                await this._readSema.WaitAsync(ct);
                try
                {
                    data = await ParseDataAsync(this.SerialPort, ct);
                }
                finally
                {
                    this._readSema.Release();
                }

                // 未读到有效数据 → 跳过本轮，不写入 Channel 也不触发事件
                if (data is null)
                    continue;

                await _channel.Writer.WriteAsync(data, ct);
                DataReceived?.Invoke(this, data);
            }
        }
        catch (Exception ex) when (!ct.IsCancellationRequested && ex is not OperationCanceledException)
        {
            // 只有非关闭引起的异常才尝试重连
            this._logger.LogError("通道({channel})读取失败：{ex}", this.ChannelName(), ex.Message);
            await this.DisconnectAsync(CancellationToken.None);
        }
        catch
        {
            // 关闭过程中的异常（取消/释放）静默吞掉
        }
    }

    /// <summary>
    /// 收到消息事件
    /// </summary>
    public event EventHandler<T>? DataReceived;

    /// <summary>
    /// 尝试从队列中读取输入，成功返回true，失败返回false
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public virtual bool TryDequeueInput(out T? input)
    {
        if (this.SerialPort is null)
        {
            throw new InvalidOperationException($"通道({this.ChannelName()})的串口为空");
        }
        if (!this._channel.Reader.TryRead(out input))
        {
            return false;
        }
        this._logger.LogInformation("通道({ChannelName})收到扫码枪输入：{input}", this.ChannelName(), input);
        return true;
    }


    /// <summary>
    /// 写入串口数据。注意：如果串口未连接，则会抛出异常
    /// </summary>
    /// <param name="response"></param>
    /// <exception cref="InvalidOperationException"></exception>
    public async virtual Task WriteAsync(string response)
    {
        var serial = this.SerialPort;
        if (serial is null)
        {
            throw new InvalidOperationException($"通道({this.ChannelName()})的串口为空");
        }
        await this.ExecuteOneByOneAsync(
            _writeSema,
            async ct =>
            {
                serial.Write(response);
            },
            CancellationToken.None
        );
    }

    /// <summary>
    /// 写入串口数据。注意：如果串口未连接，则会抛出异常
    /// </summary>
    /// <param name="response"></param>
    /// <param name="offset"></param>
    /// <param name="count"></param>
    /// <exception cref="InvalidOperationException"></exception>
    public async virtual Task WriteAsync(byte[] response, int offset, int count)
    {
        var serial = this.SerialPort;
        if (serial is null)
        {
            throw new InvalidOperationException($"通道({this.ChannelName()})的串口为空");
        }
        await this.ExecuteOneByOneAsync(
            _writeSema,
            async ct =>
            {
                serial.Write(response, offset, count);
            },
            CancellationToken.None
        );
    }


    /// <summary>
    /// 串行化执行异步操作的辅助方法。
    /// 通过指定的信号量确保操作互斥执行。
    /// </summary>
    private async Task ExecuteOneByOneAsync(SemaphoreSlim sema, Func<CancellationToken, Task> action, CancellationToken ct)
    {
        await sema.WaitAsync(ct);
        try
        {
            await action(ct);
        }
        finally
        {
            sema.Release();
        }
    }


}
