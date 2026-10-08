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
/// 端序语义的<b>可观测字节</b>测试：手工构造线上字节，让两种承载方式——组合成员
/// （<c>TagCbnt</c> 下的子测点，由 Cbntor 解读）与直接测点（<c>TagGrp</c> 下的 <c>Tag</c>，
/// 由 DirectTag 解读）——各自独立跑一遍，断言两者解读出同一个物理值。<br/>
/// <br/>
/// 依据（由 <see cref="ModbusTcpWireRoundTripTests"/> 单独钉住）：NModbus 返回的 <c>ushort[]</c>
/// 是<b>数值</b>数组，主机端序与此无关。<br/>
/// 于是端序由两个属性分工表达：<c>endian</c> = <b>每个 16 位单元内部两个字节</b>的顺序
/// （与 S7 同名同义），<c>interpret</c> = <b>寄存器之间</b>的顺序（不写时：BigEndian = 完全大端、
/// LittleEndian = 完全小端）；<c>BYTE</c> → 取寄存器的高/低字节；<c>BIT</c>/<c>DI</c>/<c>DO</c> → 与端序无关。<br/>
/// <br/>
/// 用例写法是"设备里存的是同一个物理值，两种设备习惯各自的线上字节"，然后要求两种配置都能还原出那个值——
/// 这样"哪个配置对应哪种设备"是显式的，而不是靠读源码猜。
/// </summary>
public class ModbusEndianWireTests : IDisposable
{
    private readonly ServiceProvider _root;
    private readonly IServiceScope _scope;

    /// <summary>
    /// c'tor
    /// </summary>
    public ModbusEndianWireTests()
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

    /// <summary>组合起始 40001、子测点 40070 ⇒ 子测点占缓存下标 69</summary>
    private const int ChildOffset = 69;

    #region 装配

    /// <summary>
    /// 起一个连到假服务端的真实项目（真通道、真 socket）
    /// </summary>
    private async Task<ITagsProject> LoadProjectAsync(FakeModbusTcpServer server, string grpInnerXml)
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

    /// <summary>组合成员：组合起始 40001，子测点 v 默认在 40070</summary>
    private static string Cbnt(string type, string endian, string address = "40070", string? interpret = null) => $@"
        <TagCbnt name='c' address='40001' access='RW'>
            <Tag name='v' address='{address}' type='{type}' endian='{endian}'{Interpret(interpret)} />
        </TagCbnt>";

    /// <summary>直接测点，默认地址 40070</summary>
    private static string Direct(string type, string endian, string address = "40070", string? interpret = null) =>
        $@"<Tag name='v' address='{address}' type='{type}' endian='{endian}'{Interpret(interpret)} />";

    private static string Interpret(string? interpret) =>
        interpret is null ? string.Empty : $" interpret='{interpret}'";

    private async Task<object?> ReadViaCbntAsync(FakeModbusTcpServer server, string grpInnerXml)
    {
        using var proj = await this.LoadProjectAsync(server, grpInnerXml);
        var grp = proj.Tags.SelectGrp("g1")!;
        var cbnt = grp.SelectCbnt("c")!;

        await cbnt.ReadAsync(CancellationToken.None);

        return grp.SelectTag("c/v").Value;
    }

    private async Task<object?> ReadViaDirectAsync(FakeModbusTcpServer server, string tagXml)
    {
        using var proj = await this.LoadProjectAsync(server, tagXml);
        var tag = proj.Tags.SelectGrp("g1")!.SelectTag("v");

        await tag.ReadAsync(CancellationToken.None);

        return tag.Value;
    }

    private async Task<ushort[]> WriteViaCbntAsync(FakeModbusTcpServer server, string grpInnerXml, object value)
    {
        using var proj = await this.LoadProjectAsync(server, grpInnerXml);
        var grp = proj.Tags.SelectGrp("g1")!;
        var cbnt = grp.SelectCbnt("c")!;

        grp.SelectTag("c/v").Value = value;
        await cbnt.WriteAsync(CancellationToken.None);

        return server.GetRegisters(ChildOffset, 1);
    }

    private async Task<ushort[]> WriteViaDirectAsync(FakeModbusTcpServer server, string tagXml, object value)
    {
        using var proj = await this.LoadProjectAsync(server, tagXml);
        var tag = proj.Tags.SelectGrp("g1")!.SelectTag("v");

        tag.Value = value;
        await tag.WriteAsync(CancellationToken.None);

        return server.GetRegisters(ChildOffset, 1);
    }

    /// <summary>线上字节：从子测点所在下标开始的寄存器值（服务端按规范序列化成线上大端字节）</summary>
    private static FakeModbusTcpServer ServerWithRegisters(params ushort[] registers)
    {
        var server = new FakeModbusTcpServer();
        server.SetRegisters(ChildOffset, registers);
        return server;
    }

    #endregion

    #region 16 位：endian 管"寄存器内两个字节的顺序"

    /// <summary>
    /// 设备里的物理值都是 0x1234，区别只在线上字节：
    /// 大端存放 → <c>12 34</c>（读回数值 0x1234）；小端存放 → <c>34 12</c>（读回数值 0x3412）。
    /// 两种设备习惯各配一个 endian 值，都应该还原成 0x1234。
    /// </summary>
    [Theory]
    [InlineData("BigEndian", (ushort)0x1234)]     // 线上 12 34
    [InlineData("LittleEndian", (ushort)0x3412)]  // 线上 34 12
    public async Task Int16_Read_DirectTag_RecoversDeviceValue(string endian, ushort wireRegister)
    {
        using var server = ServerWithRegisters(wireRegister);

        var value = await ReadViaDirectAsync(server, Direct("INT16", endian));

        Assert.Equal((short)0x1234, (short)value!);
    }

    [Theory]
    [InlineData("BigEndian", (ushort)0x1234)]
    [InlineData("LittleEndian", (ushort)0x3412)]
    public async Task Int16_Read_CbntMember_RecoversDeviceValue(string endian, ushort wireRegister)
    {
        using var server = ServerWithRegisters(wireRegister);

        var value = await ReadViaCbntAsync(server, Cbnt("INT16", endian));

        Assert.Equal((short)0x1234, (short)value!);
    }

    [Theory]
    [InlineData("BigEndian", (ushort)0xCDEF)]     // 线上 CD EF
    [InlineData("LittleEndian", (ushort)0xEFCD)]  // 线上 EF CD
    public async Task UInt16_Read_DirectTag_RecoversDeviceValue(string endian, ushort wireRegister)
    {
        using var server = ServerWithRegisters(wireRegister);

        var value = await ReadViaDirectAsync(server, Direct("UINT16", endian));

        Assert.Equal((ushort)0xCDEF, (ushort)value!);
    }

    [Theory]
    [InlineData("BigEndian", (ushort)0xCDEF)]
    [InlineData("LittleEndian", (ushort)0xEFCD)]
    public async Task UInt16_Read_CbntMember_RecoversDeviceValue(string endian, ushort wireRegister)
    {
        using var server = ServerWithRegisters(wireRegister);

        var value = await ReadViaCbntAsync(server, Cbnt("UINT16", endian));

        Assert.Equal((ushort)0xCDEF, (ushort)value!);
    }

    /// <summary>配置与设备习惯不匹配时：拿到"字节颠倒"的值，但不报错（这正是它难查的原因）。</summary>
    [Theory]
    [InlineData("BigEndian", (ushort)0x3412)]     // 小端设备，却按大端直取
    [InlineData("LittleEndian", (ushort)0x1234)]  // 大端设备，却按小端交换
    public async Task Int16_Read_EndianMismatch_GivesByteSwappedValue(string endian, ushort wireRegister)
    {
        using var server = ServerWithRegisters(wireRegister);

        var direct = await ReadViaDirectAsync(server, Direct("INT16", endian));
        var viaCbnt = await ReadViaCbntAsync(server, Cbnt("INT16", endian));

        Assert.Equal((short)0x3412, (short)direct!);
        Assert.Equal((short)0x3412, (short)viaCbnt!);
    }

    [Theory]
    [InlineData("BigEndian", (ushort)0x1234)]     // 写出去线上是 12 34
    [InlineData("LittleEndian", (ushort)0x3412)]  // 写出去线上是 34 12
    public async Task Int16_Write_DirectTag_UsesConfiguredEndian(string endian, ushort expectedRegister)
    {
        using var server = new FakeModbusTcpServer();

        var written = await WriteViaDirectAsync(server, Direct("INT16", endian), (short)0x1234);

        Assert.Equal(expectedRegister, written[0]);
    }

    [Theory]
    [InlineData("BigEndian", (ushort)0x1234)]
    [InlineData("LittleEndian", (ushort)0x3412)]
    public async Task Int16_Write_CbntMember_UsesConfiguredEndian(string endian, ushort expectedRegister)
    {
        using var server = new FakeModbusTcpServer();

        var written = await WriteViaCbntAsync(server, Cbnt("INT16", endian), (short)0x1234);

        Assert.Equal(expectedRegister, written[0]);
    }

    [Theory]
    [InlineData("BigEndian", (ushort)0x1234)]
    [InlineData("LittleEndian", (ushort)0x3412)]
    public async Task UInt16_Write_DirectTag_UsesConfiguredEndian(string endian, ushort expectedRegister)
    {
        using var server = new FakeModbusTcpServer();

        var written = await WriteViaDirectAsync(server, Direct("UINT16", endian), (ushort)0x1234);

        Assert.Equal(expectedRegister, written[0]);
    }

    [Theory]
    [InlineData("BigEndian", (ushort)0x1234)]
    [InlineData("LittleEndian", (ushort)0x3412)]
    public async Task UInt16_Write_CbntMember_UsesConfiguredEndian(string endian, ushort expectedRegister)
    {
        using var server = new FakeModbusTcpServer();

        var written = await WriteViaCbntAsync(server, Cbnt("UINT16", endian), (ushort)0x1234);

        Assert.Equal(expectedRegister, written[0]);
    }

    #endregion

    #region 32/64 位：endian 管"寄存器内部的字节"，interpret 管"寄存器之间的顺序"

    /// <summary>
    /// 设备里存的物理值都是 0x12345678（<c>A</c>=0x12、<c>B</c>=0x34、<c>C</c>=0x56、<c>D</c>=0x78），
    /// 区别只在这 4 个字节的顺序；四种排布各配一组 (endian, interpret)，都应该还原成同一个值。
    /// </summary>
    [Theory]
    [InlineData("BigEndian", null, (ushort)0x1234, (ushort)0x5678)]       // ABCD
    [InlineData("BigEndian", "CDAB", (ushort)0x5678, (ushort)0x1234)]     // CDAB（字交换）
    [InlineData("LittleEndian", "BADC", (ushort)0x3412, (ushort)0x7856)]  // BADC（字节交换）
    [InlineData("LittleEndian", null, (ushort)0x7856, (ushort)0x3412)]    // DCBA（完全小端）
    public async Task Int32_Read_DirectTag_RecoversDeviceValue(
        string endian,
        string? interpret,
        ushort first,
        ushort second)
    {
        using var server = new FakeModbusTcpServer();
        server.SetRegisters(ChildOffset, first, second);

        var value = await ReadViaDirectAsync(server, Direct("INT32", endian, interpret: interpret));

        Assert.Equal(0x12345678, (int)value!);
    }

    [Theory]
    [InlineData("BigEndian", null, (ushort)0x1234, (ushort)0x5678)]
    [InlineData("BigEndian", "CDAB", (ushort)0x5678, (ushort)0x1234)]
    [InlineData("LittleEndian", "BADC", (ushort)0x3412, (ushort)0x7856)]
    [InlineData("LittleEndian", null, (ushort)0x7856, (ushort)0x3412)]
    public async Task Int32_Read_CbntMember_RecoversDeviceValue(
        string endian,
        string? interpret,
        ushort first,
        ushort second)
    {
        using var server = new FakeModbusTcpServer();
        server.SetRegisters(ChildOffset, first, second);

        var value = await ReadViaCbntAsync(server, Cbnt("INT32", endian, interpret: interpret));

        Assert.Equal(0x12345678, (int)value!);
    }

    /// <summary>
    /// 32 位的缺省排布：<c>BigEndian</c> = 完全大端（ABCD），<c>LittleEndian</c> = 完全小端（DCBA）。<br/>
    /// 排布矩阵由上面的 <c>INT32</c> 用例穷举，这里只额外钉住无符号类型的解读不失真。
    /// </summary>
    [Theory]
    [InlineData("BigEndian", (ushort)0x1234, (ushort)0x5678)]     // ABCD
    [InlineData("LittleEndian", (ushort)0x7856, (ushort)0x3412)]  // DCBA
    public async Task UInt32_Read_DirectTag_RecoversDeviceValue(
        string endian,
        ushort first,
        ushort second)
    {
        using var server = new FakeModbusTcpServer();
        server.SetRegisters(ChildOffset, first, second);

        var value = await ReadViaDirectAsync(server, Direct("UINT32", endian));

        Assert.Equal(0x12345678u, (uint)value!);
    }

    [Theory]
    [InlineData("BigEndian", (ushort)0x1234, (ushort)0x5678)]
    [InlineData("LittleEndian", (ushort)0x7856, (ushort)0x3412)]
    public async Task UInt32_Read_CbntMember_RecoversDeviceValue(
        string endian,
        ushort first,
        ushort second)
    {
        using var server = new FakeModbusTcpServer();
        server.SetRegisters(ChildOffset, first, second);

        var value = await ReadViaCbntAsync(server, Cbnt("UINT32", endian));

        Assert.Equal(0x12345678u, (uint)value!);
    }

    /// <summary>
    /// 配置与设备不符时的结果——都是为了排错时一眼认出来：
    /// 设备是标准 ABCD（寄存器 <c>[12 34][56 78]</c>），却按别的排布解读。
    /// </summary>
    [Theory]
    [InlineData("BigEndian", "CDAB", 0x56781234)]     // 多做了字交换
    [InlineData("LittleEndian", "BADC", 0x34127856)]  // 多做了字节交换
    [InlineData("LittleEndian", null, 0x78563412)]    // 多做了完全小端
    public async Task Int32_Read_MismatchedLayout_GivesPredictableWrongValue(
        string endian,
        string? interpret,
        uint expected)
    {
        using var server = new FakeModbusTcpServer();
        server.SetRegisters(ChildOffset, 0x1234, 0x5678);

        var direct = await ReadViaDirectAsync(server, Direct("INT32", endian, interpret: interpret));
        var viaCbnt = await ReadViaCbntAsync(server, Cbnt("INT32", endian, interpret: interpret));

        Assert.Equal(unchecked((int)expected), (int)direct!);
        Assert.Equal(unchecked((int)expected), (int)viaCbnt!);
    }

    /// <summary>
    /// 32 位里只有 <c>CDAB</c> 会换寄存器顺序、不换寄存器内部字节；<c>DCBA</c> 才是"完全小端"。
    /// 这条差别在排错时最容易搞混：两种配置给出的值不同。
    /// </summary>
    [Fact]
    public async Task Int32_Cdab_SwapsRegistersWhileDcba_ReversesEverything()
    {
        using var server = new FakeModbusTcpServer();
        server.SetRegisters(ChildOffset, 0x1234, 0x5678);

        var wordSwap = await ReadViaDirectAsync(server, Direct("INT32", "BigEndian", interpret: "CDAB"));
        var fullyLittle = await ReadViaDirectAsync(server, Direct("INT32", "LittleEndian"));

        Assert.Equal(0x56781234, (int)wordSwap!);
        Assert.Equal(0x78563412, (int)fullyLittle!);
    }

    [Theory]
    [InlineData("BigEndian", null, (ushort)0x1234, (ushort)0x5678)]
    [InlineData("BigEndian", "CDAB", (ushort)0x5678, (ushort)0x1234)]
    [InlineData("LittleEndian", "BADC", (ushort)0x3412, (ushort)0x7856)]
    [InlineData("LittleEndian", null, (ushort)0x7856, (ushort)0x3412)]
    public async Task Int32_Write_CbntMember_UsesConfiguredLayout(
        string endian,
        string? interpret,
        ushort expectedFirst,
        ushort expectedSecond)
    {
        using var server = new FakeModbusTcpServer();
        using var proj = await this.LoadProjectAsync(server, Cbnt("INT32", endian, interpret: interpret));
        var grp = proj.Tags.SelectGrp("g1")!;
        var cbnt = grp.SelectCbnt("c")!;

        grp.SelectTag("c/v").Value = 0x12345678;
        await cbnt.WriteAsync(CancellationToken.None);

        Assert.Equal(new ushort[] { expectedFirst, expectedSecond }, server.GetRegisters(ChildOffset, 2));
    }

    [Theory]
    [InlineData("BigEndian", null, (ushort)0x1234, (ushort)0x5678)]
    [InlineData("BigEndian", "CDAB", (ushort)0x5678, (ushort)0x1234)]
    [InlineData("LittleEndian", "BADC", (ushort)0x3412, (ushort)0x7856)]
    [InlineData("LittleEndian", null, (ushort)0x7856, (ushort)0x3412)]
    public async Task Int32_Write_DirectTag_UsesConfiguredLayout(
        string endian,
        string? interpret,
        ushort expectedFirst,
        ushort expectedSecond)
    {
        using var server = new FakeModbusTcpServer();

        await WriteViaDirectAsync(server, Direct("INT32", endian, interpret: interpret), 0x12345678);

        Assert.Equal(new ushort[] { expectedFirst, expectedSecond }, server.GetRegisters(ChildOffset, 2));
    }

    /// <summary>
    /// 设备里的浮点物理值都是 1.5f = <c>3F C0 00 00</c>，只有线上字节顺序不同，两种配置都该还原成 1.5f。
    /// </summary>
    [Theory]
    [InlineData("BigEndian", (ushort)0x3FC0, (ushort)0x0000)]     // ABCD
    [InlineData("LittleEndian", (ushort)0x0000, (ushort)0xC03F)]  // DCBA（完全小端）
    public async Task Float_Read_DirectTag_RecoversDeviceValue(
        string endian,
        ushort first,
        ushort second)
    {
        using var server = new FakeModbusTcpServer();
        server.SetRegisters(ChildOffset, first, second);

        var value = await ReadViaDirectAsync(server, Direct("FLOAT", endian));

        Assert.Equal(1.5f, (float)value!);
    }

    [Theory]
    [InlineData("BigEndian", (ushort)0x3FC0, (ushort)0x0000)]
    [InlineData("LittleEndian", (ushort)0x0000, (ushort)0xC03F)]
    public async Task Float_Read_CbntMember_RecoversDeviceValue(
        string endian,
        ushort first,
        ushort second)
    {
        using var server = new FakeModbusTcpServer();
        server.SetRegisters(ChildOffset, first, second);

        var value = await ReadViaCbntAsync(server, Cbnt("FLOAT", endian));

        Assert.Equal(1.5f, (float)value!);
    }

    /// <summary>
    /// 设备里的物理值都是 0x123456789ABCDEF0（<c>A</c>=0x12 … <c>H</c>=0xF0），
    /// 两种缺省排布（完全大端 / 完全小端）都该还原成同一个值。<c>interpret</c> 的各种排列见
    /// <see cref="ModbusInterpretTests"/>。
    /// </summary>
    [Theory]
    [InlineData("BigEndian", (ushort)0x1234, (ushort)0x5678, (ushort)0x9ABC, (ushort)0xDEF0)]     // ABCDEFGH
    [InlineData("LittleEndian", (ushort)0xF0DE, (ushort)0xBC9A, (ushort)0x7856, (ushort)0x3412)]  // HGFEDCBA
    public async Task Int64_Read_DirectTag_RecoversDeviceValue(
        string endian,
        ushort r0,
        ushort r1,
        ushort r2,
        ushort r3)
    {
        using var server = new FakeModbusTcpServer();
        server.SetRegisters(ChildOffset, r0, r1, r2, r3);

        var value = await ReadViaDirectAsync(server, Direct("INT64", endian));

        Assert.Equal(0x123456789ABCDEF0L, (long)value!);
    }

    [Theory]
    [InlineData("BigEndian", (ushort)0x1234, (ushort)0x5678, (ushort)0x9ABC, (ushort)0xDEF0)]
    [InlineData("LittleEndian", (ushort)0xF0DE, (ushort)0xBC9A, (ushort)0x7856, (ushort)0x3412)]
    public async Task Int64_Read_CbntMember_RecoversDeviceValue(
        string endian,
        ushort r0,
        ushort r1,
        ushort r2,
        ushort r3)
    {
        using var server = new FakeModbusTcpServer();
        server.SetRegisters(ChildOffset, r0, r1, r2, r3);

        var value = await ReadViaCbntAsync(server, Cbnt("INT64", endian));

        Assert.Equal(0x123456789ABCDEF0L, (long)value!);
    }

    #endregion

    #region BYTE：endian 选择取寄存器哪一半

    [Theory]
    [InlineData("BigEndian", (byte)0x12)]     // 取高字节
    [InlineData("LittleEndian", (byte)0x34)]  // 取低字节
    public async Task Byte_Read_DirectTag_PicksConfiguredHalf(string endian, byte expected)
    {
        using var server = ServerWithRegisters(0x1234);

        var value = await ReadViaDirectAsync(server, Direct("BYTE", endian));

        Assert.Equal(expected, (byte)value!);
    }

    [Theory]
    [InlineData("BigEndian", (byte)0x12)]
    [InlineData("LittleEndian", (byte)0x34)]
    public async Task Byte_Read_CbntMember_PicksConfiguredHalf(string endian, byte expected)
    {
        using var server = ServerWithRegisters(0x1234);

        var value = await ReadViaCbntAsync(server, Cbnt("BYTE", endian));

        Assert.Equal(expected, (byte)value!);
    }

    #endregion

    #region BIT / DI / DO：与端序无关

    [Theory]
    [InlineData("BigEndian")]
    [InlineData("LittleEndian")]
    public async Task Bit_Read_DirectTag_IgnoresEndian(string endian)
    {
        // 寄存器值 0x0200 的 bit9 = 1
        using var server = ServerWithRegisters(0x0200);

        var value = await ReadViaDirectAsync(server, Direct("BIT", endian, "40070.9"));

        Assert.True((bool)value!);
    }

    [Theory]
    [InlineData("BigEndian")]
    [InlineData("LittleEndian")]
    public async Task Bit_Read_CbntMember_IgnoresEndian(string endian)
    {
        using var server = ServerWithRegisters(0x0200);

        var value = await ReadViaCbntAsync(server, Cbnt("BIT", endian, "40070.9"));

        Assert.True((bool)value!);
    }

    [Theory]
    [InlineData("BigEndian")]
    [InlineData("LittleEndian")]
    public async Task OutputCoil_Write_DirectTag_IgnoresEndian(string endian)
    {
        using var server = new FakeModbusTcpServer();
        server.SetBits(19, false);
        using var proj = await this.LoadProjectAsync(server, Direct("DO", endian, "00020"));
        var tag = proj.Tags.SelectGrp("g1")!.SelectTag("v");

        tag.Value = true;
        await tag.WriteAsync(CancellationToken.None);

        Assert.Equal(new bool[] { true }, server.LastBitsWritten);
        Assert.Equal((ushort)19, server.LastBitsWriteStart);
    }

    [Theory]
    [InlineData("BigEndian")]
    [InlineData("LittleEndian")]
    public async Task OutputCoil_Read_CbntMember_IgnoresEndian(string endian)
    {
        using var server = new FakeModbusTcpServer();
        server.SetBits(19, true);
        using var proj = await this.LoadProjectAsync(server, $@"
        <TagCbnt name='c' address='00001' access='RW'>
            <Tag name='v' address='00020' type='DO' endian='{endian}'></Tag>
        </TagCbnt>");
        var grp = proj.Tags.SelectGrp("g1")!;

        await grp.SelectCbnt("c")!.ReadAsync(CancellationToken.None);

        Assert.True((bool)grp.SelectTag("c/v").Value!);
    }

    [Theory]
    [InlineData("BigEndian")]
    [InlineData("LittleEndian")]
    public async Task InputContact_Read_DirectTag_IgnoresEndian(string endian)
    {
        using var server = new FakeModbusTcpServer();
        server.SetBits(19, true);
        using var proj = await this.LoadProjectAsync(server, Direct("DI", endian, "10020"));
        var tag = proj.Tags.SelectGrp("g1")!.SelectTag("v");

        await tag.ReadAsync(CancellationToken.None);

        Assert.True((bool)tag.Value!);
    }

    #endregion

    #region 硬约束：两种承载方式（直接测点 / 组合成员）必须一致

    /// <summary>
    /// 同一份寄存器数据，放在 <c>TagGrp</c> 下的直接测点与放在 <c>TagCbnt</c> 下的组合成员必须解读出同一个物理值
    /// （<c>ModbusTcp/Notes.md</c> §2 的硬约束；曾被破坏过：16 位组合成员曾完全忽略 <c>endian</c>）。<br/>
    /// <br/>
    /// 各类型的"期望值是多少"由上面成对用例分别钉住，这里只把"两条路径相等"本身写成断言——
    /// 免得这条契约只能靠"两处硬编码常量恰好相同"来间接保证。
    /// </summary>
    [Theory]
    [InlineData("INT16", "LittleEndian", new ushort[] { 0x3412 })]
    [InlineData("UINT16", "BigEndian", new ushort[] { 0xCDEF })]
    [InlineData("INT32", "BigEndian", new ushort[] { 0x1234, 0x5678 })]
    [InlineData("UINT32", "LittleEndian", new ushort[] { 0x7856, 0x3412 })]
    [InlineData("INT64", "BigEndian", new ushort[] { 0x1234, 0x5678, 0x9ABC, 0xDEF0 })]
    [InlineData("FLOAT", "LittleEndian", new ushort[] { 0x0000, 0xC03F })]
    [InlineData("BYTE", "BigEndian", new ushort[] { 0x1234 })]
    public async Task CrossPath_Read_DirectTagAndCbntMember_Agree(string type, string endian, ushort[] registers)
    {
        using var server = ServerWithRegisters(registers);

        var direct = await ReadViaDirectAsync(server, Direct(type, endian));
        var viaCbnt = await ReadViaCbntAsync(server, Cbnt(type, endian));

        Assert.Equal(direct, viaCbnt);
    }

    /// <summary>
    /// 写入侧同理：同一个物理值，两条路径发出的寄存器内容必须相同。<br/>
    /// （组合成员写的是整个组合缓存，所以只比较子测点所在的那一段。）
    /// </summary>
    [Theory]
    [InlineData("INT16", "LittleEndian", (short)0x1234, 1)]
    [InlineData("UINT16", "BigEndian", (ushort)0xCDEF, 1)]
    [InlineData("INT32", "BigEndian", 0x12345678, 2)]
    [InlineData("UINT32", "LittleEndian", 0x12345678u, 2)]
    [InlineData("INT64", "BigEndian", 0x123456789ABCDEF0L, 4)]
    [InlineData("FLOAT", "LittleEndian", 1.5f, 2)]
    [InlineData("BYTE", "BigEndian", (byte)0x12, 1)]
    public async Task CrossPath_Write_DirectTagAndCbntMember_SendSameRegisters(
        string type,
        string endian,
        object value,
        int registerCount)
    {
        using var directServer = new FakeModbusTcpServer();
        using var cbntServer = new FakeModbusTcpServer();

        await WriteViaDirectAsync(directServer, Direct(type, endian), value);
        await WriteViaCbntAsync(cbntServer, Cbnt(type, endian), value);

        Assert.Equal(
            directServer.GetRegisters(ChildOffset, registerCount),
            cbntServer.GetRegisters(ChildOffset, registerCount));
    }

    #endregion
}
