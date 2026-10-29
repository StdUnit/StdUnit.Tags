using System.Collections.Generic;

namespace StdUnit.Tags.Tests.Fakes;

// 本文件是主测试项目 Fakes/FakedChannelFactory.cs 的副本：
// 本项目所需的夹具都在本项目下自带一份，不引用主测试项目的源码（见 csproj 顶部注释）。

internal class FakedChannelFactory : ITagChannelFactory
{
    public FakedChannelFactory()
    {
    }


    public ITagChannel Create(TagChannelDescriptor chDescriptor)
    {
        return new FakedChannel(chDescriptor);
    }


    private static IReadOnlyList<string> _drivers = new List<string>() { "fake" };


    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    public IReadOnlyList<string> GetAvailableDrivers() => _drivers;
}
