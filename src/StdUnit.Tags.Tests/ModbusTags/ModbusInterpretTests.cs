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
/// <c>interpret</c> 属性：Modbus 多寄存器数值里"字节的先后顺序"的完整记法（记法与校验见 <c>ModbusInterpret</c>，
/// 换算见 <c>ModbusValueInterpreter&lt;T&gt;</c>，背景见驱动包 <c>Notes.md</c>）。<br/>
/// <br/>
/// 分工：<c>endian</c> 只管<b>每个 16 位单元内部</b>两个字节的顺序（与 S7 同名同义，协议规定的标准是 BigEndian）；
/// <c>interpret</c> 管这些单元<b>之间</b>的顺序。字符数必须等于该数值的字节数（32 位 4 个、64 位 8 个），
/// 所以 16 位、<c>BIT</c>、<c>BYTE</c>、位空间上没有可排的东西，写 <c>interpret</c> 一律报错。<br/>
/// <br/>
/// 因为是驱动私有的属性，它走 <c>Extras</c>，Core 完全不感知（也因此对其它驱动无效）。
/// </summary>
public class ModbusInterpretTests : IDisposable
{
    private readonly ServiceProvider _root;
    private readonly IServiceScope _scope;

    /// <summary>
    /// c'tor
    /// </summary>
    public ModbusInterpretTests()
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

    /// <summary>只加载不连接（用于校验加载期报错）</summary>
    private ITagsProject CreateProject(string grpInnerXml)
    {
        var xml = XElement.Parse($@"
<root>
    <Channel name='ModbusTcp-2' driver='ModbusTcp'>
        <IpAddr>127.0.0.1</IpAddr>
        <Port>502</Port>
    </Channel>
    <TagGrp name='g1' isEntry='true' isEnabled='true' channel='ModbusTcp-2' scanInterval='10'>
{grpInnerXml}
    </TagGrp>
</root>");
        var factory = this._scope.ServiceProvider.GetRequiredService<ITagsProjectFactory>();
        return factory.Create(string.Empty, xml);
    }

    /// <summary>连到假服务端的真实项目（真通道、真 socket）</summary>
    private async Task<ITagsProject> LoadConnectedProjectAsync(FakeModbusTcpServer server, string grpInnerXml)
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

    /// <summary>直接测点（挂在 <c>TagGrp</c> 下）</summary>
    private static string Direct(string type, string endian, string interpret, string address = "40070") =>
        $@"<Tag name='v' address='{address}' type='{type}' endian='{endian}' interpret='{interpret}' />";

    /// <summary>组合成员（挂在 <c>TagCbnt</c> 下）</summary>
    private static string CbntMember(string type, string endian, string interpret, string address = "40070") => $@"
        <TagCbnt name='c' address='40001' access='RW'>
            <Tag name='v' address='{address}' type='{type}' endian='{endian}' interpret='{interpret}' />
        </TagCbnt>";

    #endregion

    #region 64 位：记法要写 8 个字符（A = 最高字节）

    /// <summary>
    /// 设备里存的物理值都是 0x123456789ABCDEF0，区别只在 4 个寄存器怎么摆；四种排布都应还原成同一个值。
    /// </summary>
    [Theory]
    [InlineData("BigEndian", "ABCDEFGH", (ushort)0x1234, (ushort)0x5678, (ushort)0x9ABC, (ushort)0xDEF0)]
    [InlineData("BigEndian", "GHEFCDAB", (ushort)0xDEF0, (ushort)0x9ABC, (ushort)0x5678, (ushort)0x1234)]
    [InlineData("BigEndian", "CDABGHEF", (ushort)0x5678, (ushort)0x1234, (ushort)0xDEF0, (ushort)0x9ABC)]
    [InlineData("LittleEndian", "BADCFEHG", (ushort)0x3412, (ushort)0x7856, (ushort)0xBC9A, (ushort)0xF0DE)]
    [InlineData("LittleEndian", "HGFEDCBA", (ushort)0xF0DE, (ushort)0xBC9A, (ushort)0x7856, (ushort)0x3412)]
    public async Task Int64_Read_DirectTag_RecoversDeviceValue(
        string endian,
        string interpret,
        ushort r0,
        ushort r1,
        ushort r2,
        ushort r3)
    {
        using var server = new FakeModbusTcpServer();
        server.SetRegisters(ChildOffset, r0, r1, r2, r3);
        using var proj = await this.LoadConnectedProjectAsync(server, Direct("INT64", endian, interpret));

        var tag = proj.Tags.SelectGrp("g1")!.SelectTag("v");
        await tag.ReadAsync(CancellationToken.None);

        Assert.Equal(0x123456789ABCDEF0L, (long)tag.Value!);
    }

    [Theory]
    [InlineData("BigEndian", "ABCDEFGH", (ushort)0x1234, (ushort)0x5678, (ushort)0x9ABC, (ushort)0xDEF0)]
    [InlineData("BigEndian", "GHEFCDAB", (ushort)0xDEF0, (ushort)0x9ABC, (ushort)0x5678, (ushort)0x1234)]
    [InlineData("BigEndian", "CDABGHEF", (ushort)0x5678, (ushort)0x1234, (ushort)0xDEF0, (ushort)0x9ABC)]
    [InlineData("LittleEndian", "BADCFEHG", (ushort)0x3412, (ushort)0x7856, (ushort)0xBC9A, (ushort)0xF0DE)]
    [InlineData("LittleEndian", "HGFEDCBA", (ushort)0xF0DE, (ushort)0xBC9A, (ushort)0x7856, (ushort)0x3412)]
    public async Task Int64_Write_DirectTag_UsesConfiguredLayout(
        string endian,
        string interpret,
        ushort r0,
        ushort r1,
        ushort r2,
        ushort r3)
    {
        using var server = new FakeModbusTcpServer();
        using var proj = await this.LoadConnectedProjectAsync(server, Direct("INT64", endian, interpret));

        var tag = proj.Tags.SelectGrp("g1")!.SelectTag("v");
        tag.Value = 0x123456789ABCDEF0L;
        await tag.WriteAsync(CancellationToken.None);

        Assert.Equal(new ushort[] { r0, r1, r2, r3 }, server.GetRegisters(ChildOffset, 4));
    }

    /// <summary>组合成员与直接测点必须解读出同一个值（同一份 XML 换个挂法不能换语义）</summary>
    [Theory]
    [InlineData("BigEndian", "ABCDEFGH", (ushort)0x1234, (ushort)0x5678, (ushort)0x9ABC, (ushort)0xDEF0)]
    [InlineData("BigEndian", "GHEFCDAB", (ushort)0xDEF0, (ushort)0x9ABC, (ushort)0x5678, (ushort)0x1234)]
    [InlineData("BigEndian", "CDABGHEF", (ushort)0x5678, (ushort)0x1234, (ushort)0xDEF0, (ushort)0x9ABC)]
    [InlineData("LittleEndian", "BADCFEHG", (ushort)0x3412, (ushort)0x7856, (ushort)0xBC9A, (ushort)0xF0DE)]
    [InlineData("LittleEndian", "HGFEDCBA", (ushort)0xF0DE, (ushort)0xBC9A, (ushort)0x7856, (ushort)0x3412)]
    public async Task Int64_Read_CbntMember_RecoversDeviceValue(
        string endian,
        string interpret,
        ushort r0,
        ushort r1,
        ushort r2,
        ushort r3)
    {
        using var server = new FakeModbusTcpServer();
        server.SetRegisters(ChildOffset, r0, r1, r2, r3);
        using var proj = await this.LoadConnectedProjectAsync(server, CbntMember("INT64", endian, interpret));
        var grp = proj.Tags.SelectGrp("g1")!;

        await grp.SelectCbnt("c")!.ReadAsync(CancellationToken.None);

        Assert.Equal(0x123456789ABCDEF0L, (long)grp.SelectTag("c/v").Value!);
    }

    #endregion

    #region 记法与 endian 的关系

    /// <summary>
    /// 显式写出与默认完全等价的记法（<c>BigEndian</c> + <c>ABCD</c>、<c>LittleEndian</c> + <c>DCBA</c>）
    /// 必须与不写 interpret 结果一致——不写只是省略，不是另一套语义。
    /// </summary>
    [Theory]
    [InlineData("BigEndian", "ABCD", (ushort)0x1234, (ushort)0x5678)]
    [InlineData("LittleEndian", "DCBA", (ushort)0x7856, (ushort)0x3412)]
    public async Task Int32_ExplicitDefaultNotation_EqualsImplicit(
        string endian,
        string interpret,
        ushort first,
        ushort second)
    {
        using var explicitServer = new FakeModbusTcpServer();
        explicitServer.SetRegisters(ChildOffset, first, second);
        using var explicitProj = await this.LoadConnectedProjectAsync(explicitServer, Direct("INT32", endian, interpret));
        var explicitTag = explicitProj.Tags.SelectGrp("g1")!.SelectTag("v");
        await explicitTag.ReadAsync(CancellationToken.None);

        using var implicitServer = new FakeModbusTcpServer();
        implicitServer.SetRegisters(ChildOffset, first, second);
        using var implicitProj = await this.LoadConnectedProjectAsync(
            implicitServer,
            $@"<Tag name='v' address='40070' type='INT32' endian='{endian}' />");
        var implicitTag = implicitProj.Tags.SelectGrp("g1")!.SelectTag("v");
        await implicitTag.ReadAsync(CancellationToken.None);

        Assert.Equal(0x12345678, (int)explicitTag.Value!);
        Assert.Equal(explicitTag.Value, implicitTag.Value);
    }

    /// <summary>
    /// 每两个连续字符必须是同一个 16 位单元里的两个字节，且先后与 <c>endian</c> 一致：
    /// <c>BigEndian</c> 只接受 <c>AB</c>/<c>CD</c>…，<c>LittleEndian</c> 只接受 <c>BA</c>/<c>DC</c>…。
    /// 于是"字交换"与"字节交换"各有唯一写法，不会被两种配置表达成同一件事。
    /// </summary>
    [Theory]
    [InlineData("BigEndian", "BADC")]
    [InlineData("BigEndian", "DCBA")]
    [InlineData("LittleEndian", "ABCD")]
    [InlineData("LittleEndian", "CDAB")]
    [InlineData("LittleEndian", "ACBD")]
    public void Interpret_ConflictingWithEndian_Throws(string endian, string interpret)
    {
        var ex = Assert.Throws<TagsProjectXmlException>(
            () => this.CreateProject(Direct("INT32", endian, interpret)));

        Assert.Contains("endian", ex.Message);
        Assert.Contains(interpret, ex.Message);
    }

    #endregion

    #region 加载期校验：长度 / 字符集 / 重复

    [Theory]
    [InlineData("ABC")]      // 少一个
    [InlineData("ABCDE")]    // 多一个
    [InlineData("AB")]       // 只剩一个 16 位单元
    public void Int32_Interpret_WithWrongLength_Throws(string interpret)
    {
        var ex = Assert.Throws<TagsProjectXmlException>(
            () => this.CreateProject(Direct("INT32", "BigEndian", interpret)));

        Assert.Contains("字节数", ex.Message);
    }

    /// <summary>64 位必须写 8 个字符，32 位的记法不能直接搬过来</summary>
    [Fact]
    public void Int64_Interpret_WithFourLetters_Throws()
    {
        var ex = Assert.Throws<TagsProjectXmlException>(
            () => this.CreateProject(Direct("INT64", "BigEndian", "ABCD")));

        Assert.Contains("8", ex.Message);
    }

    /// <summary>小写、越界字母都不是合法记法（大写 A 起、按字节数截断）</summary>
    [Theory]
    [InlineData("ABCZ")]
    [InlineData("abcd")]
    [InlineData("AB1D")]
    public void Int32_Interpret_WithIllegalLetter_Throws(string interpret)
    {
        var ex = Assert.Throws<TagsProjectXmlException>(
            () => this.CreateProject(Direct("INT32", "BigEndian", interpret)));

        Assert.Contains("非法字符", ex.Message);
    }

    [Theory]
    [InlineData("ABCA")]
    [InlineData("AAAA")]
    public void Int32_Interpret_WithRepeatedLetter_Throws(string interpret)
    {
        var ex = Assert.Throws<TagsProjectXmlException>(
            () => this.CreateProject(Direct("INT32", "BigEndian", interpret)));

        Assert.Contains("重复", ex.Message);
    }

    /// <summary>组合成员同样受校验（两条承载路径共用同一个排布解析）</summary>
    [Fact]
    public void CbntMember_Interpret_WithConflict_Throws()
    {
        var ex = Assert.Throws<TagsProjectXmlException>(
            () => this.CreateProject(CbntMember("UINT32", "BigEndian", "DCBA")));

        Assert.Contains("interpret", ex.Message);
    }

    #endregion

    #region 加载期校验：没有"寄存器之间"可排的测点一律拒绝

    /// <summary>16 位只占一个寄存器、只有一对字节，顺序由 <c>endian</c> 表达即可</summary>
    [Theory]
    [InlineData("INT16")]
    [InlineData("UINT16")]
    [InlineData("BYTE")]
    public void DirectTag_SingleUnit_WithInterpret_Throws(string type)
    {
        var ex = Assert.Throws<TagsProjectConfigurationException>(
            () => this.CreateProject(Direct(type, "BigEndian", "ABCD")));

        Assert.Contains("interpret", ex.Message);
    }

    [Fact]
    public void CbntMember_SingleUnit_WithInterpret_Throws()
    {
        var ex = Assert.Throws<TagsProjectConfigurationException>(
            () => this.CreateProject(CbntMember("UINT16", "BigEndian", "ABCD")));

        Assert.Contains("interpret", ex.Message);
    }

    /// <summary>寄存器空间里的位测点只有一位</summary>
    [Fact]
    public void RegisterBit_WithInterpret_Throws()
    {
        var ex = Assert.Throws<TagsProjectConfigurationException>(
            () => this.CreateProject(Direct("BIT", "BigEndian", "ABCD", "40070.9")));

        Assert.Contains("interpret", ex.Message);
    }

    /// <summary>位空间（DI/DO）没有寄存器，更没有字节顺序</summary>
    [Fact]
    public void BitSpaceDirectTag_WithInterpret_Throws()
    {
        var ex = Assert.Throws<TagsProjectConfigurationException>(
            () => this.CreateProject(Direct("DI", "BigEndian", "ABCD", "10020")));

        Assert.Contains("interpret", ex.Message);
    }

    /// <summary>组合本身没有数值，属性必须写在子测点上</summary>
    [Fact]
    public void TagCbnt_WithInterpret_Throws()
    {
        var ex = Assert.Throws<TagsProjectConfigurationException>(() => this.CreateProject(@"
        <TagCbnt name='c' address='40001' access='RW' interpret='ABCD'>
            <Tag name='v' address='40070' type='UINT32' endian='BigEndian' />
        </TagCbnt>"));

        Assert.Contains("interpret", ex.Message);
    }

    #endregion

    #region 记法的直接单元测试（不经过 XML：读取 / 归一 / 文案 / 拒绝策略）

    private static TagDescriptor Descriptor(EndianKinds endian, string? interpret = null, string kind = BuiltinTagKinds.UINT32)
    {
        var descriptor = new TagDescriptor
        {
            TagName = "v",
            RawAddress = "40001",
            TagKind = kind,
            TagSize = 4,
            EndianKind = endian,
        };

        if (interpret is not null)
        {
            descriptor.Extras["interpret"] = new XAttribute("interpret", interpret);
        }

        return descriptor;
    }

    /// <summary>记法两端的空白会被裁掉（手写 XML 常带空格）</summary>
    [Fact]
    public void Parse_TrimsSurroundingSpaces()
    {
        var plain = ModbusInterpret.Parse(Descriptor(EndianKinds.BigEndian, "CDAB"), 4, "Tag(v)");
        var padded = ModbusInterpret.Parse(Descriptor(EndianKinds.BigEndian, "  CDAB  "), 4, "Tag(v)");

        Assert.True(plain.Span.SequenceEqual(padded.Span));
    }

    /// <summary>写成空白的 <c>interpret</c> 等同于没写（按 <c>endian</c> 的缺省语义）</summary>
    [Theory]
    [InlineData("BigEndian", "")]
    [InlineData("BigEndian", "   ")]
    [InlineData("LittleEndian", " ")]
    public void Parse_WhitespaceOnly_TreatedAsAbsent(string endian, string interpret)
    {
        var typed = ModbusInterpret.Parse(Descriptor(ModbusInterpretEndian(endian), interpret), 4, "Tag(v)");
        var absent = ModbusInterpret.Parse(Descriptor(ModbusInterpretEndian(endian)), 4, "Tag(v)");

        Assert.Equal(absent.IsEmpty, typed.IsEmpty);
        Assert.True(absent.Span.SequenceEqual(typed.Span));
    }

    /// <summary>
    /// 显式写出"该 <c>endian</c> 的默认排布"＝不写 <c>interpret</c>：同一张表、同一个编号、同一个实例。<br/>
    /// 注意"空表"只表示<b>恒等映射</b>（A 在第 0 位、B 在第 1 位……），所以只有 <c>BigEndian</c> + <c>ABCD…</c>
    /// 会归一成空表；<c>LittleEndian</c> 的默认是"完全小端"，那是一张真实存在的置换表。
    /// </summary>
    [Theory]
    [InlineData("BigEndian", "ABCD", 4)]
    [InlineData("LittleEndian", "DCBA", 4)]
    [InlineData("BigEndian", "ABCDEFGH", 8)]
    [InlineData("LittleEndian", "HGFEDCBA", 8)]
    public void Parse_ExplicitDefault_EqualsImplicit(string endian, string interpret, int byteCount)
    {
        var typed = ModbusInterpret.Parse(Descriptor(ModbusInterpretEndian(endian), interpret), byteCount, "Tag(v)");
        var absent = ModbusInterpret.Parse(Descriptor(ModbusInterpretEndian(endian)), byteCount, "Tag(v)");

        Assert.Equal(absent.IsEmpty, typed.IsEmpty);
        Assert.True(absent.Span.SequenceEqual(typed.Span));
    }

    /// <summary>恒等映射（= 不需要搬运）归一成空表，读路径由此走直通分支</summary>
    [Fact]
    public void Parse_IdentityMapping_IsNormalizedToEmpty()
    {
        Assert.True(ModbusInterpret.Parse(Descriptor(EndianKinds.BigEndian, "ABCD"), 4, "Tag(v)").IsEmpty);
        Assert.True(ModbusInterpret.Parse(Descriptor(EndianKinds.BigEndian, "ABCDEFGH"), 8, "Tag(v)").IsEmpty);
        Assert.True(ModbusInterpret.Parse(Descriptor(EndianKinds.BigEndian), 4, "Tag(v)").IsEmpty);
    }

    /// <summary>规范记法的文案（32 位四种 + 64 位四种；这条钉住"记法 ↔ 排布"的文字对应）</summary>
    [Theory]
    [InlineData("BigEndian", null, 4, "ABCD")]
    [InlineData("BigEndian", "CDAB", 4, "CDAB")]
    [InlineData("LittleEndian", "BADC", 4, "BADC")]
    [InlineData("LittleEndian", null, 4, "DCBA")]
    [InlineData("BigEndian", null, 8, "ABCDEFGH")]
    [InlineData("BigEndian", "GHEFCDAB", 8, "GHEFCDAB")]
    [InlineData("BigEndian", "CDABGHEF", 8, "CDABGHEF")]
    [InlineData("LittleEndian", "BADCFEHG", 8, "BADCFEHG")]
    [InlineData("LittleEndian", null, 8, "HGFEDCBA")]
    public void DescribeNotation_MatchesKnownLayouts(string endian, string? interpret, int byteCount, string expected)
    {
        var packed = ModbusInterpret.Parse(Descriptor(ModbusInterpretEndian(endian), interpret), byteCount, "Tag(v)");

        Assert.Equal(expected, ModbusInterpret.DescribeNotation(packed, byteCount));
    }

    /// <summary>只占一个 16 位单元的种类（含"未知"）写 <c>interpret</c> 一律拒绝</summary>
    [Theory]
    [InlineData(BuiltinTagKinds.BIT)]
    [InlineData(BuiltinTagKinds.BYTE)]
    [InlineData(BuiltinTagKinds.INT16)]
    [InlineData(BuiltinTagKinds.UINT16)]
    [InlineData(BuiltinTagKinds.DI)]
    [InlineData(BuiltinTagKinds.DO)]
    [InlineData(BuiltinTagKinds.Unknown)]
    public void RejectForSingleUnit_SingleUnitKinds_Throw(string kind)
    {
        var ex = Assert.Throws<TagsProjectConfigurationException>(
            () => ModbusInterpret.RejectForSingleUnit(Descriptor(EndianKinds.BigEndian, "ABCD", kind), "Tag(v)"));

        Assert.Contains("interpret", ex.Message);
    }

    /// <summary>多寄存器种类不受这条策略影响（它们的记法由解读器校验）</summary>
    [Theory]
    [InlineData(BuiltinTagKinds.INT32)]
    [InlineData(BuiltinTagKinds.UINT32)]
    [InlineData(BuiltinTagKinds.FLOAT)]
    [InlineData(BuiltinTagKinds.INT64)]
    [InlineData(BuiltinTagKinds.UINT64)]
    public void RejectForSingleUnit_MultiRegisterKinds_Pass(string kind)
    {
        ModbusInterpret.RejectForSingleUnit(Descriptor(EndianKinds.BigEndian, "ABCD", kind), "Tag(v)");
    }

    /// <summary>没有 <c>interpret</c> 的单 16 位种类也不该被拒（拒绝只针对"写了却无意义"）</summary>
    [Fact]
    public void RejectForSingleUnit_WithoutInterpret_Pass()
    {
        ModbusInterpret.RejectForSingleUnit(Descriptor(EndianKinds.BigEndian, kind: BuiltinTagKinds.UINT16), "Tag(v)");
    }

    /// <summary>组合本身没有数值：写了就拒（报错里带组合名与属性值），没写就放过</summary>
    [Fact]
    public void RejectOnCbnt_WithAndWithoutInterpret()
    {
        var extras = new Dictionary<string, XAttribute>
        {
            [ModbusInterpret.AttributeName] = new XAttribute(ModbusInterpret.AttributeName, "CDAB"),
        };

        var ex = Assert.Throws<TagsProjectConfigurationException>(
            () => ModbusInterpret.RejectOnCbnt(extras, "c"));
        Assert.Contains("c", ex.Message);
        Assert.Contains("CDAB", ex.Message);

        ModbusInterpret.RejectOnCbnt(new Dictionary<string, XAttribute>(), "c");
        ModbusInterpret.RejectOnCbnt(null, "c");
    }

    private static EndianKinds ModbusInterpretEndian(string endian) =>
        endian == "BigEndian" ? EndianKinds.BigEndian : EndianKinds.LittleEndian;

    #endregion
}
