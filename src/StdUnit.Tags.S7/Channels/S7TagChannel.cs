using Microsoft.Extensions.Logging;
using StdUnit.Sharp7;
using Microsoft.FSharp.Core;


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
    /// 如果不为 null，则在 <see cref="CreateClientAndConnectAsync"/> 被调用；
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
        await this.ExecuteOneByOneAsync(
            async ct =>
            {
                if (this.Client == null)
                    return;

                var tcs = new TaskCompletionSource<Object?>();
                var th = new Thread(() =>
                {
                    try
                    {
                        this.Client?.Disconnect();
                        this.Client = null;
                        tcs.SetResult(null);
                    }
                    catch (Exception ex)
                    {
                        this._logger.LogWarning(ex, "通道{ChannelName}断开连接失败", this.ChannelName());
                        this.Client = null;
                        tcs.SetException(ex);
                    }
                });
                th.IsBackground = true;
                th.Start();
                await tcs.Task;
            },
            ct
        );
    }

    /// <inheritdoc/>
    public virtual async Task EnsureConnectedAsync(bool force, CancellationToken ct)
    {
        await this.ExecuteOneByOneAsync(
            async ct =>
            {
                //当前client存在并且连接有效
                if (!force && Client != null && !Client.IsDead())
                {
                    return;
                }
                var channelName = this.ChannelName();

                // 如果要强制连接，先清理当前的连接
                if (force && Client != null)
                {
                    try
                    {
                        this._logger.LogInformation("通道={ChannelName} 强制断开连接中...", channelName);
                        this.Client.Disconnect();
                    }
                    catch (Exception ex)
                    {
                        this._logger.LogError(ex, "通道={ChannelName} 强制断开异常", channelName);
                    }
                    finally
                    {
                        this.Client = null;
                    }
                }

                var result = await this.CreateClientAndConnectAsync(ct);
                if (result.IsError)
                {
                    var err = result.ErrorValue;
                    throw new InvalidOperationException($"通道({channelName}) 连接PLC失败：{this.PlcItem.IpAddr} Rack={this.PlcItem.Rack} Slot={this.PlcItem.Slot}；{err.Text}（{err.Error}）");
                }
                this.Client = result.ResultValue;
            },
            ct
        );
    }


    /// <summary>
    /// 创建客户端并连接PLC
    /// </summary>
    /// <param name="ct"></param>
    /// <returns></returns>
    protected virtual Task<FSharpResult<S7Client, ApiError>> CreateClientAndConnectAsync(CancellationToken ct)
    {
        var tcs = new TaskCompletionSource<FSharpResult<S7Client, ApiError>>();
        var th = new Thread(() =>
        {
            var client = ClientFactory?.Invoke(this.PlcItem) ?? new S7Client();
            client.SetConnectionType(this.PlcItem.ConnectionType);
            try
            {
                var code = client.ConnectTo(this.PlcItem.IpAddr, this.PlcItem.Rack, this.PlcItem.Slot);
                if (code == 0)
                {
                    tcs.SetResult(FSharpResult<S7Client, ApiError>.NewOk(client));
                }
                else
                {
                    tcs.SetResult(FSharpResult<S7Client, ApiError>.NewError(S7ErrorCodeHelper.GenerateApiError(this.ChannelName(), code)));
                }
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });
        th.IsBackground = true;
        th.Start();
        return tcs.Task;
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
        var client = this.Client;
        if (client != null && client.Connected)
        {
            var channelName = this.ChannelName();
            try
            {
                this._logger.LogInformation("通道={ChannelName} 正在释放: 断开连接中...", channelName);

                this.ExecuteOneByOne(() =>
                {
                    client.Disconnect();
                    this._logger.LogInformation("通道={ChannelName} 正在释放: 断开连接完成!", channelName);
                });
            }
            catch (Exception e)
            {
                this._logger.LogWarning("通道={ChannelName} 释放异常:{exception}", channelName, e.Message);
            }
        }
        this.Client = null;
        this._rw.Dispose();
    }


    private async Task ExecuteOneByOneAsync(Func<CancellationToken, Task> action, CancellationToken ct)
    {
        await this._rw.WaitAsync(ct);
        try
        {
            await action(ct);
        }
        finally
        {
            this._rw.Release();
        }
    }

    private void ExecuteOneByOne(Action action)
    {
        this._rw.Wait();
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