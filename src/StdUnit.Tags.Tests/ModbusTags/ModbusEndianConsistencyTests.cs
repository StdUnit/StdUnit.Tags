using StdUnit.Tags;
using StdUnit.Tags.ModbusTcp;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NModbus;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Xunit;

namespace StdUnit.Tags.Tests.ModbusTags;

/// <summary>
/// 端序不变式：<b>同一个测点、同一份 `endian` 配置，写成直接测点与写进 `TagCbnt`，必须得到同一个物理值</b>
/// （读回的值相同、写出的寄存器内容相同）。<br/>
/// <br/>
/// 这条不变式被真实破坏过一次：16 位组合子曾完全忽略 `endian`（直接取值），而 16 位直接测点会按
/// `LittleEndian` 交换寄存器内两个字节，于是同一份 XML 放在 `<TagGrp>` 下与放在 `<TagCbnt>` 下
/// 缺省解读恰好相反，且不报错。现在两条路径都按"`endian` = 每个 16 位单元内部两个字节、
/// 32/64 位的单元之间由 `interpret` 决定"解读，与 S7 驱动的 16 位测点一致。<br/>
/// <br/>
/// 直接测点走 <see cref="ModbusTcpDirectTagFactory"/>（生产同一条路），组合成员走真实 XML 加载 +
/// <see cref="FakeModbusChannel"/> 喂寄存器数据。
/// </summary>
public class ModbusEndianConsistencyTests
{
    /// <summary>1~40070 ⇒ 内部 StartPoint = 69；组合起始 40001，子测点 40070 ⇒ 缓存下标 69</summary>
    private const int ChildOffset = 69;

    private static ServiceProvider BuildSp()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTagsProjectServices(b => b.AddModbusTcpSupport());
        return services.BuildServiceProvider();
    }

    #region 直接测点路径

    private static async Task<object?> ReadDirectAsync(string tagXml, int registerCount, ushort[] payload)
    {
        var descriptor = XElement.Parse(tagXml).ToTagDescriptor();
        var mock = new Mock<IModbusMaster>(MockBehavior.Strict);
        mock
            .Setup(x => x.ReadHoldingRegistersAsync(1, (ushort)ChildOffset, (ushort)registerCount))
            .ReturnsAsync(payload);
        var channel = new TestModbusTcpChannel(new ModbusTcpTagChannelDescriptor { Name = "mb1" }, mock);
        var grp = new TagGrp(new TagGrpDescriptor { Name = "grp", IsEntry = true }, channel);
        var container = TagContainer.From(grp);
        await channel.EnsureConnectedAsync(false, CancellationToken.None);

        var tag = new ModbusTcpDirectTagFactory(container).Create(descriptor, channel);
        await tag.ReadAsync(CancellationToken.None);
        return tag.Value;
    }

    private static async Task<ushort[]> WriteDirectAsync(string tagXml, object value)
    {
        var descriptor = XElement.Parse(tagXml).ToTagDescriptor();
        var mock = new Mock<IModbusMaster>(MockBehavior.Strict);
        ushort[]? sent = null;
        mock
            .Setup(x => x.WriteMultipleRegistersAsync(1, (ushort)ChildOffset, It.IsAny<ushort[]>()))
            .Returns(Task.CompletedTask)
            .Callback<byte, ushort, ushort[]>((_, _, data) => sent = data);
        var channel = new TestModbusTcpChannel(new ModbusTcpTagChannelDescriptor { Name = "mb1" }, mock);
        var grp = new TagGrp(new TagGrpDescriptor { Name = "grp", IsEntry = true }, channel);
        var container = TagContainer.From(grp);
        await channel.EnsureConnectedAsync(false, CancellationToken.None);

        var tag = new ModbusTcpDirectTagFactory(container).Create(descriptor, channel);
        tag.Value = value;
        await tag.WriteAsync(CancellationToken.None);
        return sent!;
    }

    #endregion

    #region 组合成员路径（真实 XML 加载 + 假通道）

    private static ITagsProject CreateCbntProject(IServiceProvider sp, string childXml) => sp
        .GetRequiredService<ITagsProjectFactory>()
        .Create(string.Empty, XElement.Parse($@"
<root>
    <Channel name='ModbusTcp-2' driver='ModbusTcp'>
        <IpAddr>localhost</IpAddr>
        <Port>502</Port>
    </Channel>
    <TagGrp name='g1' isEntry='true' isEnabled='true' channel='ModbusTcp-2' scanInterval='10'>
        <TagCbnt name='c' address='40001' access='RW'>
            {childXml}
        </TagCbnt>
    </TagGrp>
</root>"));

    private static async Task<object?> ReadViaCbntAsync(IServiceProvider sp, string childXml, ushort[] payload)
    {
        using var proj = CreateCbntProject(sp, childXml);
        var grp = proj.Tags.SelectGrp("g1")!;
        var cbnt = grp.SelectCbnt("c")!;
        cbnt.Channel = new FakeModbusChannel { RegisterPayload = payload };
        await cbnt.ReadAsync(CancellationToken.None);
        return grp.SelectTag("c/v").Value;
    }

    private static async Task<ushort[]> WriteViaCbntAsync(IServiceProvider sp, string childXml, object value)
    {
        using var proj = CreateCbntProject(sp, childXml);
        var grp = proj.Tags.SelectGrp("g1")!;
        var cbnt = grp.SelectCbnt("c")!;
        var fake = new FakeModbusChannel();
        cbnt.Channel = fake;
        grp.SelectTag("c/v").Value = value;
        await cbnt.WriteAsync(CancellationToken.None);
        return fake.LastWrittenRegisters!;
    }

    #endregion

    /// <summary>把子测点要读的那段寄存器放到组合缓存的对应下标上</summary>
    private static ushort[] Payload(params ushort[] childRegisters)
    {
        var payload = new ushort[ChildOffset + childRegisters.Length];
        Array.Copy(childRegisters, 0, payload, ChildOffset, childRegisters.Length);
        return payload;
    }

    [Theory]
    [InlineData("LittleEndian")]
    [InlineData("BigEndian")]
    public async Task Int16_Read_SameValueInBothPaths(string endian)
    {
        var direct = await ReadDirectAsync($@"<Tag name='v' address='1~40070' type='INT16' endian='{endian}'/>", 1, new ushort[] { 0x3412 });

        using var sp = BuildSp();
        var viaCbnt = await ReadViaCbntAsync(sp, $@"<Tag name='v' address='40070' type='INT16' endian='{endian}'/>", Payload(0x3412));

        Assert.Equal(direct, viaCbnt);
    }

    [Theory]
    [InlineData("LittleEndian")]
    [InlineData("BigEndian")]
    public async Task UInt16_Read_SameValueInBothPaths(string endian)
    {
        var direct = await ReadDirectAsync($@"<Tag name='v' address='1~40070' type='UINT16' endian='{endian}'/>", 1, new ushort[] { 0x3412 });

        using var sp = BuildSp();
        var viaCbnt = await ReadViaCbntAsync(sp, $@"<Tag name='v' address='40070' type='UINT16' endian='{endian}'/>", Payload(0x3412));

        Assert.Equal(direct, viaCbnt);
    }

    [Theory]
    [InlineData("LittleEndian")]
    [InlineData("BigEndian")]
    public async Task Int16_Write_SameRegistersInBothPaths(string endian)
    {
        var direct = await WriteDirectAsync($@"<Tag name='v' address='1~40070' type='INT16' endian='{endian}'/>", (short)0x1234);

        using var sp = BuildSp();
        var viaCbnt = await WriteViaCbntAsync(sp, $@"<Tag name='v' address='40070' type='INT16' endian='{endian}'/>", (short)0x1234);

        Assert.Equal(direct[0], viaCbnt[ChildOffset]);
    }

    [Theory]
    [InlineData("LittleEndian")]
    [InlineData("BigEndian")]
    public async Task UInt16_Write_SameRegistersInBothPaths(string endian)
    {
        var direct = await WriteDirectAsync($@"<Tag name='v' address='1~40070' type='UINT16' endian='{endian}'/>", (ushort)0x1234);

        using var sp = BuildSp();
        var viaCbnt = await WriteViaCbntAsync(sp, $@"<Tag name='v' address='40070' type='UINT16' endian='{endian}'/>", (ushort)0x1234);

        Assert.Equal(direct[0], viaCbnt[ChildOffset]);
    }

    [Theory]
    [InlineData("LittleEndian")]
    [InlineData("BigEndian")]
    public async Task Int32_Read_SameValueInBothPaths(string endian)
    {
        var direct = await ReadDirectAsync($@"<Tag name='v' address='1~40070' type='INT32' endian='{endian}'/>", 2, new ushort[] { 0x1234, 0x5678 });

        using var sp = BuildSp();
        var viaCbnt = await ReadViaCbntAsync(sp, $@"<Tag name='v' address='40070' type='INT32' endian='{endian}'/>", Payload(0x1234, 0x5678));

        Assert.Equal(direct, viaCbnt);
    }

    [Theory]
    [InlineData("LittleEndian")]
    [InlineData("BigEndian")]
    public async Task UInt32_Read_SameValueInBothPaths(string endian)
    {
        var direct = await ReadDirectAsync($@"<Tag name='v' address='1~40070' type='UINT32' endian='{endian}'/>", 2, new ushort[] { 0x1234, 0x5678 });

        using var sp = BuildSp();
        var viaCbnt = await ReadViaCbntAsync(sp, $@"<Tag name='v' address='40070' type='UINT32' endian='{endian}'/>", Payload(0x1234, 0x5678));

        Assert.Equal(direct, viaCbnt);
    }

    [Theory]
    [InlineData("LittleEndian")]
    [InlineData("BigEndian")]
    public async Task Int64_Read_SameValueInBothPaths(string endian)
    {
        var regs = new ushort[] { 0x1111, 0x2222, 0x3333, 0x4444 };
        var direct = await ReadDirectAsync($@"<Tag name='v' address='1~40070' type='INT64' endian='{endian}'/>", 4, regs);

        using var sp = BuildSp();
        var viaCbnt = await ReadViaCbntAsync(sp, $@"<Tag name='v' address='40070' type='INT64' endian='{endian}'/>", Payload(regs));

        Assert.Equal(direct, viaCbnt);
    }

    [Theory]
    [InlineData("LittleEndian")]
    [InlineData("BigEndian")]
    public async Task Float_Read_SameValueInBothPaths(string endian)
    {
        var direct = await ReadDirectAsync($@"<Tag name='v' address='1~40070' type='FLOAT' endian='{endian}'/>", 2, new ushort[] { 0x4122, 0x0000 });

        using var sp = BuildSp();
        var viaCbnt = await ReadViaCbntAsync(sp, $@"<Tag name='v' address='40070' type='FLOAT' endian='{endian}'/>", Payload(0x4122, 0x0000));

        Assert.Equal(direct, viaCbnt);
    }

    [Theory]
    [InlineData("LittleEndian")]
    [InlineData("BigEndian")]
    public async Task Byte_Read_SameValueInBothPaths(string endian)
    {
        var direct = await ReadDirectAsync($@"<Tag name='v' address='1~40070' type='BYTE' endian='{endian}'/>", 1, new ushort[] { 0x1234 });

        using var sp = BuildSp();
        var viaCbnt = await ReadViaCbntAsync(sp, $@"<Tag name='v' address='40070' type='BYTE' endian='{endian}'/>", Payload(0x1234));

        Assert.Equal(direct, viaCbnt);
    }

    [Theory]
    [InlineData("LittleEndian")]
    [InlineData("BigEndian")]
    public async Task Bit_Read_SameValueInBothPaths(string endian)
    {
        var direct = await ReadDirectAsync($@"<Tag name='v' address='1~40070.9' type='BIT' endian='{endian}'/>", 1, new ushort[] { 0x0200 });

        using var sp = BuildSp();
        var viaCbnt = await ReadViaCbntAsync(sp, $@"<Tag name='v' address='40070.9' type='BIT' endian='{endian}'/>", Payload(0x0200));

        Assert.Equal(direct, viaCbnt);
    }
}
