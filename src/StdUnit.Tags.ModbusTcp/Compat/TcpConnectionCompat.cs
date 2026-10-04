using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace StdUnit.Tags.ModbusTcp.Compat;

/// <summary>
/// <see cref="TcpClient"/> 建连（含取消支持）。<br/>
/// <br/>
/// 存在的理由：net472 的 <c>TcpClient.ConnectAsync(host, port)</c> 没有可取消重载
/// （带 <see cref="CancellationToken"/> 的重载要 .NET 5+）。<br/>
/// net8.0 直接用标准库的可取消重载（有标准库就用标准）；<br/>
/// net472 退化为「取消时关闭 socket 打断连接」，并把由此产生的异常归一为
/// <see cref="OperationCanceledException"/>，从而对上层的<b>取消语义保持一致</b>。<br/>
/// <br/>
/// 注意：net472 分支依赖「关闭 socket」来打断阻塞，这是该框架下唯一可用的取消手段；
/// 因此调用方不应在等待期间复用同一个 <see cref="TcpClient"/> 实例。
/// </summary>
internal static class TcpConnectionCompat
{
    /// <summary>
    /// 连接到指定主机与端口，并支持通过 <paramref name="ct"/> 取消。
    /// </summary>
    internal static async Task ConnectAsync(TcpClient client, string host, int port, CancellationToken ct)
    {
#if NETFRAMEWORK
        using (ct.Register(static state =>
        {
            try
            {
                ((TcpClient)state!).Close();
            }
            catch
            {
                /* 有意忽略：连接已失败/已释放时关闭会抛异常，不影响取消语义 */
            }
        }, client))
        {
            try
            {
                await client.ConnectAsync(host, port).ConfigureAwait(false);
            }
            catch (Exception) when (ct.IsCancellationRequested)
            {
                throw new OperationCanceledException(ct);
            }
        }
#else
        await client.ConnectAsync(host, port, ct).ConfigureAwait(false);
#endif
    }
}
