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
/// 寄存器空间组合子（<see cref="ModbusRegisterCbntorBase"/>）被<b>单独调用</b>时的读写：只读写自己那段寄存器、
/// 发的是自己地址上的一帧，不动组合内其他槽位（整块读写由 <see cref="ModbusRegisterTagCbnt"/> 承担）。<br/>
/// 另外钉住构造期与可写性校验：字节偏移必须为偶数、位号 0~15、输入寄存器（3x）只读。
/// </summary>
public class ModbusRegisterCbntorAccessTests : IDisposable
{
    private readonly ServiceProvider _root;
    private readonly IServiceScope _scope;

    /// <summary>
    /// c'tor
    /// </summary>
    public ModbusRegisterCbntorAccessTests()
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

    /// <summary>组合起始 40001，子测点 40003 ⇒ 组合内第 2 个寄存器（下标 2）</summary>
    private static string HoldingCbnt(string childAddr = "40003") => $@"
        <TagCbnt name='c' address='40001' access='RW'>
            <Tag name='v' address='{childAddr}' type='INT16' endian='BigEndian' />
        </TagCbnt>";

    #region 单独读写

    /// <summary>读只发自己那一个寄存器</summary>
    [Fact]
    public async Task Cbntor_ReadAsync_ReadsOwnRegister()
    {
        using var server = new FakeModbusTcpServer();
        server.SetRegisters(2, 0x1234);
        using var proj = await this.LoadAsync(server, HoldingCbnt());
        var tag = (ITagCbntor)proj.Tags.SelectGrp("g1")!.SelectTag("c/v");

        await tag.ReadAsync(CancellationToken.None);

        Assert.Equal((short)0x1234, (short)tag.Value!);
        Assert.Equal(1, server.RequestCount);
    }

    /// <summary>写只送自己那一个寄存器，组合内其他槽位原样不动</summary>
    [Fact]
    public async Task Cbntor_WriteAsync_WritesOwnRegisterOnly()
    {
        using var server = new FakeModbusTcpServer();
        server.SetRegisters(0, 0xAAAA);
        using var proj = await this.LoadAsync(server, HoldingCbnt());
        var tag = (ITagCbntor)proj.Tags.SelectGrp("g1")!.SelectTag("c/v");

        tag.Value = (short)0x1234;
        await tag.WriteAsync(CancellationToken.None);

        Assert.Equal(new ushort[] { 0x1234 }, server.GetRegisters(2, 1));
        Assert.Equal(new ushort[] { 0xAAAA }, server.GetRegisters(0, 1));
        Assert.Equal(1, server.RequestCount);
        Assert.False(tag.IsDirty);
    }

    /// <summary>输入寄存器（3x）走 FC04，读得到值</summary>
    [Fact]
    public async Task Cbntor_ReadAsync_InputRegisters_ReadsOwnRegister()
    {
        using var server = new FakeModbusTcpServer();
        server.SetRegisters(2, 0x00FF);
        using var proj = await this.LoadAsync(server, $@"
        <TagCbnt name='c' address='30001' access='RO'>
            <Tag name='v' address='30003' type='UINT16' endian='BigEndian' />
        </TagCbnt>");
        var tag = (ITagCbntor)proj.Tags.SelectGrp("g1")!.SelectTag("c/v");

        await tag.ReadAsync(CancellationToken.None);

        Assert.Equal((ushort)0x00FF, (ushort)tag.Value!);
    }

    /// <summary>输入寄存器只读：赋值与写入都抛 <see cref="NotSupportedException"/></summary>
    [Fact]
    public async Task Cbntor_InputRegisters_AreReadOnly()
    {
        using var server = new FakeModbusTcpServer();
        using var proj = await this.LoadAsync(server, $@"
        <TagCbnt name='c' address='30001' access='RO'>
            <Tag name='v' address='30003' type='INT16' endian='BigEndian' />
        </TagCbnt>");
        var tag = (ITagCbntor)proj.Tags.SelectGrp("g1")!.SelectTag("c/v");

        var ex = Assert.Throws<NotSupportedException>(() => tag.Value = (short)1);
        Assert.Contains("输入寄存器", ex.Message);
        await Assert.ThrowsAsync<NotSupportedException>(() => tag.WriteAsync(CancellationToken.None));
    }

    #endregion

    #region 构造期与通道形态校验

    private static ModbusRegisterTagCbnt NewCbnt(int cacheSizeBytes)
    {
        var cbnt = new ModbusRegisterTagCbnt(new TagCbntDescriptor { Name = "c", StartAddress = "40001" });
        cbnt.ResizeCache(cacheSizeBytes);
        return cbnt;
    }

    private static TagDescriptor NewDescriptor(string kind = BuiltinTagKinds.INT16) => new()
    {
        TagName = "v",
        RawAddress = "40001",
        TagKind = kind,
        TagSize = 2,
    };

    /// <summary>字节偏移必须为偶数（半个寄存器没有意义）</summary>
    [Fact]
    public void Ctor_OddByteOffset_Throws()
    {
        var ex = Assert.Throws<TagsProjectConfigurationException>(
            () => new ModbusRegisterInt16Cbntor(NewDescriptor(), NewCbnt(4), 1, false));

        Assert.Contains("偶数", ex.Message);
    }

    /// <summary>位号超出 0~15</summary>
    [Fact]
    public void BitCbntor_NthBitAbove15_Throws()
    {
        var ex = Assert.Throws<TagsProjectAddressException>(
            () => new ModbusRegisterBitCbntor(NewDescriptor(BuiltinTagKinds.BIT), NewCbnt(2), 0, false, 16));

        Assert.Contains("0~15", ex.Message);
    }

    /// <summary>寄存器里的位测点只接受 bool 值（给别的类型要立刻报错，而不是当 0/1 用）</summary>
    [Fact]
    public void BitCbntor_WithNonBoolValue_Throws()
    {
        var tag = new ModbusRegisterBitCbntor(NewDescriptor(BuiltinTagKinds.BIT), NewCbnt(2), 0, false, 3);

        var ex = Assert.Throws<ArgumentException>(() => tag.Value = 1);

        Assert.Contains("Int32", ex.Message);
    }

    /// <summary>组合子的默认实现依赖 <see cref="IModbusRegisterChannel"/>；通道是别的东西时给出可定位的报错</summary>
    [Fact]
    public async Task Cbntor_ReadAsync_WhenChannelIsNotRegisterChannel_Throws()
    {
        var cbnt = NewCbnt(2);
        cbnt.Channel = new BitsOnlyChannel();
        var tag = new ModbusRegisterInt16Cbntor(NewDescriptor(), cbnt, 0, false);

        var ex = await Assert.ThrowsAsync<NotImplementedException>(() => tag.ReadAsync(CancellationToken.None));

        Assert.Contains(nameof(IModbusRegisterChannel), ex.Message);
        Assert.Contains(nameof(BitsOnlyChannel), ex.Message);
    }

    /// <summary>整块读同理：通道不是寄存器通道时，组合名与通道类型都在报错里</summary>
    [Fact]
    public async Task Cbnt_ReadAsync_WhenChannelIsNotRegisterChannel_Throws()
    {
        var cbnt = NewCbnt(2);
        cbnt.Channel = new BitsOnlyChannel();

        var ex = await Assert.ThrowsAsync<NotImplementedException>(() => cbnt.ReadAsync(CancellationToken.None));

        Assert.Contains(nameof(ModbusRegisterTagCbnt), ex.Message);
        Assert.Contains(nameof(BitsOnlyChannel), ex.Message);
    }

    #endregion

    /// <summary>只实现位接口的假通道，用于验证"通道形态不符"的报错路径</summary>
    private sealed class BitsOnlyChannel : IModbusBitsChannel
    {
        public TagChannelDescriptor Descriptor { get; } = new TagChannelDescriptor
        {
            Name = "bits-only",
            Driver = ModbusTcpNames.DriverName,
        };

        public Task EnsureConnectedAsync(bool force, CancellationToken ct) => Task.CompletedTask;

        public Task DisconnectAsync(CancellationToken ct) => Task.CompletedTask;

        public void Dispose()
        {
        }

        public Task<bool[]> ReadBitsAsync(string address, int bitCount, CancellationToken ct) =>
            Task.FromResult(new bool[bitCount]);

        public Task WriteBitsAsync(string address, bool[] bits, CancellationToken ct) => Task.CompletedTask;
    }
}
