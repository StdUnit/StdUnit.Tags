using Microsoft.Extensions.Logging;
using StdUnit.Sharp7;

namespace StdUnit.Tags.S7;

/// <summary>
/// S7通道实现
/// </summary>
public class S7TagChannel : ITagChannel
{
    /// <summary>
    /// c'tor
    /// </summary>
    public S7TagChannel(S7TagChannelDescriptor descriptor, ILogger<S7TagChannel> logger)
    {
        this.Descriptor = descriptor;
        this._logger = logger;
        this.PlcItem = new S7PlcItem()
        {
            IpAddr = descriptor.IpAddr,
            Rack = descriptor.Rack,
            Slot = descriptor.Slot,
            ConnectionType = descriptor.ConnectionType
        };

    }

    private readonly ILogger<S7TagChannel> _logger;
    private readonly SemaphoreSlim _rw = new SemaphoreSlim(1, 1);

    /// <summary>
    /// S7Client 工厂。
    /// 如果不为 null，则在 <see cref="CreateClientAndConnect"/> 里被调用；
    /// 如果返回null，则会直接使用默认的<see cref="S7Client"/>构造
    /// </summary>
    public Func<S7PlcItem, S7Client?>? ClientFactory { get; set; }


    internal S7Client? Client { get; set; }

    /// <inheritdoc/>
    public TagChannelDescriptor Descriptor { get; }

    /// <inheritdoc/>
    public S7PlcItem PlcItem { get; }




    /// <inheritdoc/>
    public virtual async Task DisconnectAsync(CancellationToken ct)
    {
        await this.RunExclusiveAsync(() =>
        {
            // 先摘引用再断开：断开失败也不留下状态未知的 Client；异常原样抛出，堆栈天然完整。
            var client = this.DetachClient();
            client?.Disconnect();
        }, ct);
    }

    /// <inheritdoc/>
    public virtual async Task EnsureConnectedAsync(bool force, CancellationToken ct)
    {
        await this.RunExclusiveAsync(() =>
        {
            //当前client存在并且连接有效
            if (!force && this.Client != null && !this.Client.IsDead())
            {
                return;
            }

            var channelName = this.ChannelName();

            // 如果要强制连接，先清理当前的连接
            if (force && this.Client != null)
            {
                this._logger.LogInformation("通道={ChannelName} 强制断开连接中...", channelName);
                try
                {
                    this.DetachClient()?.Disconnect();
                }
                catch (Exception ex)
                {
                    this._logger.LogError(ex, "通道={ChannelName} 强制断开异常", channelName);
                }
            }

            this.Client = this.CreateClientAndConnect(channelName);
        }, ct);
    }


    /// <summary>
    /// 创建客户端并连接PLC。失败时抛 <see cref="InvalidOperationException"/>（消息带 Sharp7 的 ApiError 文案与错误码）。
    /// </summary>
    /// <param name="channelName">通道名，仅用于错误消息</param>
    protected virtual S7Client CreateClientAndConnect(string channelName)
    {
        var client = this.ClientFactory?.Invoke(this.PlcItem) ?? new S7Client();
        client.SetConnectionType(this.PlcItem.ConnectionType);

        var code = client.ConnectTo(this.PlcItem.IpAddr, this.PlcItem.Rack, this.PlcItem.Slot);
        if (code != 0)
        {
            var err = S7ErrorCodeHelper.GenerateApiError(channelName, code);
            throw new InvalidOperationException($"通道({channelName}) 连接PLC失败：{this.PlcItem.IpAddr} Rack={this.PlcItem.Rack} Slot={this.PlcItem.Slot}；{err.Text}（{err.Error}）");
        }

        return client;
    }


    /// <summary>
    /// 读取
    /// </summary>
    /// <param name="address">测点地址</param>
    /// <param name="buffer">接收数据的缓冲区，其长度即本次读取的字节数</param>
    /// <param name="ct">
    /// 等待 I/O 互斥体时可取消（连接/断开可能长时间占用它）；<b>不</b>作用于底层 S7 调用本身——那是同步阻塞的。
    /// </param>
    /// <exception cref="InvalidOperationException">客户端未连接，或 PLC 返回错误码</exception>
    public virtual void Read(string address, Span<byte> buffer, CancellationToken ct)
    {
        var addr = S7AddressParser.Parse(address);
        this._rw.Wait(ct);
        try
        {
            var client = this.Client ?? throw new InvalidOperationException($"通道({this.ChannelName()}) 的客户端为null，无法读取：{addr}");
            int code;
            if (addr.Area == AreaKinds.DB)
            {
                code = client.DBRead(addr.BlockNumber, addr.StartAddress, buffer.Length, buffer);
            }
            else if (addr.Area == AreaKinds.MB)
            {
                code = client.MBRead(addr.StartAddress, buffer.Length, buffer);
            }
            else
            {
                throw new NotImplementedException($"不支持的地址区域类型={addr.Area}（地址={addr}）");
            }
            if (code != 0)
            {
                var err = S7ErrorCodeHelper.GenerateApiError(this.ChannelName(), code);
                throw new InvalidOperationException($"通道({this.ChannelName()}) 读取失败：{addr}，长度={buffer.Length}；{err.Text}（{err.Error}）");
            }
        }
        finally
        {
            this._rw.Release();
        }
    }

    /// <summary>
    /// 写入底层
    /// </summary>
    /// <param name="address">测点地址</param>
    /// <param name="buffer">待写入的字节</param>
    /// <param name="ct">
    /// 等待 I/O 互斥体时可取消（连接/断开可能长时间占用它）；<b>不</b>作用于底层 S7 调用本身——那是同步阻塞的。
    /// </param>
    /// <exception cref="InvalidOperationException">客户端未连接，或 PLC 返回错误码</exception>
    public virtual void Write(string address, Span<byte> buffer, CancellationToken ct)
    {
        var addr = S7AddressParser.Parse(address);
        this._rw.Wait(ct);
        try
        {
            var client = this.Client ?? throw new InvalidOperationException($"通道({this.ChannelName()}) 的客户端为null，无法写入：{addr}");
            int code;
            if (addr.Area == AreaKinds.DB)
            {
                code = client.DBWrite(addr.BlockNumber, addr.StartAddress, buffer.Length, buffer);
            }
            else if (addr.Area == AreaKinds.MB)
            {
                code = client.MBWrite(addr.StartAddress, buffer.Length, buffer);
            }
            else
            {
                throw new NotImplementedException($"不支持的地址区域类型={addr.Area}（地址={addr}）");
            }
            if (code != 0)
            {
                var err = S7ErrorCodeHelper.GenerateApiError(this.ChannelName(), code);
                throw new InvalidOperationException($"通道({this.ChannelName()}) 写入失败：{addr}，长度={buffer.Length}；{err.Text}（{err.Error}）");
            }
        }
        finally
        {
            this._rw.Release();
        }
    }

    /// <inheritdoc/>
    public virtual void Dispose()
    {
        try
        {
            this.RunExclusive(() =>
            {
                var client = this.DetachClient();
                if (client is null)
                {
                    return;
                }

                var channelName = this.ChannelName();
                try
                {
                    client.Disconnect();
                    this._logger.LogInformation("通道={ChannelName} 释放完成（已断开连接）", channelName);
                }
                catch (Exception e)
                {
                    // Dispose 只留痕、不外抛（.NET 的释放惯例）。
                    this._logger.LogWarning(e, "通道={ChannelName} 释放异常", channelName);
                }
            });
        }
        finally
        {
            this._rw.Dispose();
        }
    }

    /// <summary>
    /// 把当前连接摘下来并清空 <see cref="Client"/>。<b>不做</b>断开动作——由调用方决定怎么处理：
    /// <see cref="DisconnectAsync"/> 让它抛（显式方法报告失败），<see cref="Dispose"/> 与强制重连只记日志。<br/>
    /// 有意<b>不</b>判 <see cref="S7Client.Connected"/>：它是底层 Socket 的快照，半死连接上返回 <c>false</c>，
    /// 恰好会把最该关的 socket 留下来。
    /// </summary>
    private S7Client? DetachClient()
    {
        var client = this.Client;
        this.Client = null;
        return client;
    }

    /// <summary>
    /// 独占 I/O 互斥体执行一段<b>同步</b>工作（读/写直接用 <see cref="_rw"/>，因为 <see cref="Span{T}"/> 不能进闭包）。
    /// </summary>
    private void RunExclusive(Action action, CancellationToken ct = default)
    {
        this._rw.Wait(ct);
        try
        {
            action();
        }
        finally
        {
            this._rw.Release();
        }
    }

    /// <summary>
    /// 同 <see cref="RunExclusive"/>，差别只在"等待互斥体时可取消"——连接/断开这类可能长时间持锁的操作走这个。
    /// </summary>
    private async Task RunExclusiveAsync(Action action, CancellationToken ct)
    {
        await this._rw.WaitAsync(ct);
        try
        {
            action();
        }
        finally
        {
            this._rw.Release();
        }
    }
}