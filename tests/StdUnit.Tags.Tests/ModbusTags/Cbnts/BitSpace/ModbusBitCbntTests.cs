using StdUnit.Tags;
using StdUnit.Tags.ModbusTcp;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Xunit;

namespace StdUnit.Tags.Tests.ModbusTags;

/// <summary>
/// 位空间（线圈 0x / 离散输入 1x）组合的读写模型：缓存 = bool 数组（每元素 = 一个地址位），
/// <see cref="ModbusBitTagCbnt"/> <b>一次读写覆盖整个组合缓存</b>（一帧 FC01/FC02/FC15），子测点不各自访问通道。<br/>
/// <br/>
/// 子测点自己（<see cref="ITagCbntor"/>）的 <c>ReadAsync</c> / <c>WriteAsync</c> 只在被单独调用时走：
/// 那时按自己的地址直读直写，不碰组合缓存以外的内容。
/// </summary>
public class ModbusBitCbntTests : IDisposable
{
    private readonly ServiceProvider _root;
    private readonly IServiceScope _scope;

    /// <summary>
    /// c'tor
    /// </summary>
    public ModbusBitCbntTests()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTagsProjectServices(b => b.AddModbusTcpSupport());
        this._root = services.BuildServiceProvider();
        this._scope = this._root.CreateScope();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        this._scope.Dispose();
        this._root.Dispose();
    }

    /// <summary>起一个连到假服务端的真实项目（真通道、真 socket）</summary>
    private async Task<ITagsProject> LoadAsync(FakeModbusTcpServer server, string grpInnerXml)
    {
        var xml = XElement.Parse($@"
<root>
    <Channel name='ModbusTcp-2' driver='ModbusTcp'>
        <IpAddr>127.0.0.1</IpAddr>
        <Port>{server.Port}</Port>
    </Channel>
    <TagGrp name='g1' isEntry='true' isEnabled='true' channel='ModbusTcp-2' scanInterval='10'>
{grpInnerXml}
    </TagGrp>
</root>");
        var proj = this._scope.ServiceProvider.GetRequiredService<ITagsProjectFactory>().Create(string.Empty, xml);
        await proj.Channels[0].EnsureConnectedAsync(false, CancellationToken.None);
        return proj;
    }

    /// <summary>组合起始 00001，子测点占第 1 / 3 位 ⇒ 缓存 3 个元素</summary>
    private const string DoCbnt = @"
        <TagCbnt name='c' address='00001' access='RW'>
            <Tag name='a' address='00001' type='DO' endian='BigEndian' />
            <Tag name='b' address='00003' type='DO' endian='BigEndian' />
        </TagCbnt>";

    #region 组合：整块读写

    /// <summary>整块读：一帧 FC01 覆盖整个缓存，所有子测点被一次性刷新</summary>
    [Fact]
    public async Task Cbnt_ReadAsync_ReadsWholeCacheInOneRequest()
    {
        using var server = new FakeModbusTcpServer();
        // 缓存 = [a 位, 中间空位, b 位]
        server.SetBits(0, false, false, true);
        using var proj = await this.LoadAsync(server, DoCbnt);
        var grp = proj.Tags.SelectGrp("g1")!;
        var cbnt = grp.SelectCbnt("c")!;

        await cbnt.ReadAsync(CancellationToken.None);

        Assert.False((bool)grp.SelectTag("c/a").Value!);
        Assert.True((bool)grp.SelectTag("c/b").Value!);
        Assert.Equal(1, server.RequestCount);
    }

    /// <summary>整块写：一帧 FC15 送出整个缓存（含未配置的中间位），并清掉组合与子测点的脏标记</summary>
    [Fact]
    public async Task Cbnt_WriteAsync_SendsWholeCacheAndClearsDirty()
    {
        using var server = new FakeModbusTcpServer();
        using var proj = await this.LoadAsync(server, DoCbnt);
        var grp = proj.Tags.SelectGrp("g1")!;
        var cbnt = grp.SelectCbnt("c")!;

        grp.SelectTag("c/a").Value = true;
        grp.SelectTag("c/b").Value = true;
        Assert.True(cbnt.IsDirty);

        await cbnt.WriteAsync(CancellationToken.None);

        Assert.Equal(new bool[] { true, false, true }, server.LastBitsWritten!);
        Assert.Equal((ushort)0, server.LastBitsWriteStart!);
        Assert.Equal(1, server.RequestCount);
        Assert.False(cbnt.IsDirty);
        Assert.False(grp.SelectTag("c/a").IsDirty);
        Assert.False(grp.SelectTag("c/b").IsDirty);
    }

    /// <summary>离散输入（1x）组合同样整块读，走 FC02</summary>
    [Fact]
    public async Task DiCbnt_ReadAsync_ReadsWholeCache()
    {
        using var server = new FakeModbusTcpServer();
        server.SetBits(19, true, false);
        using var proj = await this.LoadAsync(server, @"
        <TagCbnt name='c' address='10001' access='RW'>
            <Tag name='a' address='10020' type='DI' endian='BigEndian' />
            <Tag name='b' address='10021' type='DI' endian='BigEndian' />
        </TagCbnt>");
        var grp = proj.Tags.SelectGrp("g1")!;

        await grp.SelectCbnt("c")!.ReadAsync(CancellationToken.None);

        Assert.True((bool)grp.SelectTag("c/a").Value!);
        Assert.False((bool)grp.SelectTag("c/b").Value!);
        Assert.Equal(1, server.RequestCount);
    }

    #endregion

    #region 子测点：被单独调用时按自己的地址直读直写

    /// <summary>子测点的读只取自己那一位，并写回组合缓存的对应槽位</summary>
    [Fact]
    public async Task Cbntor_ReadAsync_ReadsOwnBitOnly()
    {
        using var server = new FakeModbusTcpServer();
        server.SetBits(19, true);
        using var proj = await this.LoadAsync(server, @"
        <TagCbnt name='c' address='00001' access='RW'>
            <Tag name='v' address='00020' type='DO' endian='BigEndian' />
        </TagCbnt>");
        var grp = proj.Tags.SelectGrp("g1")!;
        var cbnt = (TagCbnt<bool>)grp.SelectCbnt("c")!;
        var tag = (ITagCbntor)grp.SelectTag("c/v");

        await tag.ReadAsync(CancellationToken.None);

        Assert.True((bool)tag.Value!);
        Assert.True(cbnt.Cache.Span[19]);
        Assert.Equal(1, server.RequestCount);
    }

    /// <summary>子测点的写只送自己那一位</summary>
    [Fact]
    public async Task Cbntor_WriteAsync_WritesOwnBitOnly()
    {
        using var server = new FakeModbusTcpServer();
        using var proj = await this.LoadAsync(server, @"
        <TagCbnt name='c' address='00001' access='RW'>
            <Tag name='v' address='00020' type='DO' endian='BigEndian' />
        </TagCbnt>");
        var grp = proj.Tags.SelectGrp("g1")!;
        var tag = (ITagCbntor)grp.SelectTag("c/v");
        tag.Value = true;

        await tag.WriteAsync(CancellationToken.None);

        Assert.Equal(new bool[] { true }, server.LastBitsWritten!);
        Assert.Equal((ushort)19, server.LastBitsWriteStart!);
        Assert.False(tag.IsDirty);
    }

    #endregion

    #region 通道形态不符

    /// <summary>组合子的默认实现依赖 <see cref="IModbusBitsChannel"/>；通道是别的东西时给出可定位的报错</summary>
    [Fact]
    public async Task Cbntor_ReadAsync_WhenChannelIsNotBitsChannel_Throws()
    {
        var cbnt = new ModbusBitTagCbnt(new TagCbntDescriptor { Name = "c", StartAddress = "00001" });
        cbnt.ResizeCache(1);
        cbnt.Channel = new RegistersOnlyChannel();
        var tag = new DOTagCbntor(
            new TagDescriptor { TagName = "v", RawAddress = "00001", TagKind = BuiltinTagKinds.DO, TagSize = 1 },
            cbnt,
            0);

        var ex = await Assert.ThrowsAsync<NotImplementedException>(() => tag.ReadAsync(CancellationToken.None));

        Assert.Contains(nameof(IModbusBitsChannel), ex.Message);
        Assert.Contains(nameof(RegistersOnlyChannel), ex.Message);
    }

    /// <summary>整块读同理：通道不是位通道时，点位名与通道类型都在报错里</summary>
    [Fact]
    public async Task Cbnt_ReadAsync_WhenChannelIsNotBitsChannel_Throws()
    {
        var cbnt = new ModbusBitTagCbnt(new TagCbntDescriptor { Name = "c", StartAddress = "00001" });
        cbnt.ResizeCache(1);
        cbnt.Channel = new RegistersOnlyChannel();

        var ex = await Assert.ThrowsAsync<NotImplementedException>(() => cbnt.ReadAsync(CancellationToken.None));

        Assert.Contains(nameof(ModbusBitTagCbnt), ex.Message);
        Assert.Contains(nameof(RegistersOnlyChannel), ex.Message);
    }

    #endregion

    /// <summary>只实现寄存器接口的假通道，用于验证"通道形态不符"的报错路径</summary>
    private sealed class RegistersOnlyChannel : IModbusRegisterChannel
    {
        public TagChannelDescriptor Descriptor { get; } = new TagChannelDescriptor
        {
            Name = "registers-only",
            Driver = ModbusTcpNames.DriverName,
        };

        public Task EnsureConnectedAsync(bool force, CancellationToken ct) => Task.CompletedTask;

        public Task DisconnectAsync(CancellationToken ct) => Task.CompletedTask;

        public void Dispose()
        {
        }

        public Task<ushort[]> ReadRegistersAsync(string address, int registerCount, CancellationToken ct) =>
            Task.FromResult(new ushort[registerCount]);

        public Task WriteRegistersAsync(string address, ReadOnlyMemory<ushort> registers, CancellationToken ct) =>
            Task.CompletedTask;
    }
}
