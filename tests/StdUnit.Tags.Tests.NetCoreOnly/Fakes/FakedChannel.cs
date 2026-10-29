using System.Threading;
using System.Threading.Tasks;

namespace StdUnit.Tags.Tests.Fakes;

// 本文件是主测试项目 Fakes/FakedChannel.cs 的副本：
// 本项目所需的夹具都在本项目下自带一份，不引用主测试项目的源码（见 csproj 顶部注释）。

internal class FakedChannel : ITagChannel
{
    public FakedChannel(TagChannelDescriptor descriptor)
    {
        Descriptor = descriptor;
    }
    public TagChannelDescriptor Descriptor { get; }

    public Task DisconnectAsync(CancellationToken ct) => Task.CompletedTask;

    public void Dispose() { }

    public Task EnsureConnectedAsync(bool force, CancellationToken ct)
    {
        return Task.CompletedTask;
    }
}
