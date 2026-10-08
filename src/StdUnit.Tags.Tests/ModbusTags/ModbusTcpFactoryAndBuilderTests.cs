using StdUnit.Tags;
using StdUnit.Tags.ModbusTcp;
using Moq;
using NModbus;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace StdUnit.Tags.Tests.ModbusTags;

/// <summary>
/// Modbus 三个测点工厂（<see cref="ModbusTcpDirectTagFactory"/> / <see cref="ModbusBitTagFactory"/> /
/// <see cref="ModbusRegisterTagFactory"/>）与 <see cref="ModbusTcpDirectTagBuilder"/> 的分支与报错路径：<br/>
/// 未支持的测点种类、地址区域与承载方式不匹配、测点自带通道形态不对——都必须在<b>加载/构建期</b>给出可定位的报错。
/// </summary>
public class ModbusTcpFactoryAndBuilderTests
{
    private static (TestModbusTcpChannel Channel, Mock<IModbusMaster> Mock) CreateChannel()
    {
        var mock = new Mock<IModbusMaster>(MockBehavior.Strict);
        var channel = new TestModbusTcpChannel(new ModbusTcpTagChannelDescriptor { Name = "mb1" }, mock);
        return (channel, mock);
    }

    private static (TestModbusTcpChannel Channel, TagContainer Container) CreateContainer()
    {
        var (channel, _) = CreateChannel();
        var grp = new TagGrp(new TagGrpDescriptor { Name = "grp", IsEntry = true }, channel);
        return (channel, TagContainer.From(grp));
    }

    private static TagDescriptor Descriptor(
        string kind,
        string address,
        int tagSize = 0,
        EndianKinds endian = EndianKinds.LittleEndian) => new()
    {
        TagName = "v",
        RawAddress = address,
        TagKind = kind,
        TagSize = tagSize,
        EndianKind = endian,
    };

    #region 直接测点工厂

    /// <summary>工厂不认识测点种类时点名报错（而不是悄悄退化成别的解读）</summary>
    [Fact]
    public void DirectTagFactory_UnknownKind_Throws()
    {
        var (_, container) = CreateContainer();

        var ex = Assert.Throws<TagsProjectConfigurationException>(
            () => new ModbusTcpDirectTagFactory(container).Create(Descriptor(BuiltinTagKinds.Unknown, "1~40001"), null));

        Assert.Contains(BuiltinTagKinds.Unknown, ex.Message);
    }

    /// <summary>BIT 落在寄存器区（3x/4x）⇒ 按寄存器里的第 nth 位解读</summary>
    [Fact]
    public void DirectTagFactory_BitOnInputRegisters_CreatesInputRegisterBitTag()
    {
        var (_, container) = CreateContainer();

        var tag = new ModbusTcpDirectTagFactory(container).Create(Descriptor(BuiltinTagKinds.BIT, "1~30021.7"), null);

        Assert.IsType<InputRegisterBitDirectTag>(tag);
    }

    /// <summary>BIT 落在位空间（0x/1x）⇒ 就是 DI/DO 本身（此时 <c>.nth</c> 无意义，只有 0 被允许）</summary>
    [Theory]
    [InlineData("1~10020", typeof(InputContactDirectTag))]
    [InlineData("1~00020", typeof(OutputCoilDirectTag))]
    public void DirectTagFactory_BitOnBitSpace_CreatesBitSpaceTag(string address, Type expected)
    {
        var (_, container) = CreateContainer();

        var tag = new ModbusTcpDirectTagFactory(container).Create(Descriptor(BuiltinTagKinds.BIT, address), null);

        Assert.IsType(expected, tag);
    }

    /// <summary>UINT64 直接测点（64 位无符号）</summary>
    [Fact]
    public async Task DirectTagFactory_UInt64Tag_ReadsEightBytes()
    {
        var (channel, mock) = CreateChannel();
        var grp = new TagGrp(new TagGrpDescriptor { Name = "grp", IsEntry = true }, channel);
        var container = TagContainer.From(grp);
        mock
            .Setup(x => x.ReadHoldingRegistersAsync(1, (ushort)0, (ushort)4))
            .ReturnsAsync(new ushort[] { 0x0102, 0x0304, 0x0506, 0x0708 });
        await channel.EnsureConnectedAsync(false, CancellationToken.None);

        var tag = new ModbusTcpDirectTagFactory(container).Create(
            Descriptor(BuiltinTagKinds.UINT64, "1~40001", 8, EndianKinds.BigEndian),
            channel);

        Assert.IsType<UInt64DirectTag>(tag);
        await tag.ReadAsync(CancellationToken.None);
        Assert.Equal(0x0102030405060708UL, (ulong)tag.Value!);
    }

    #endregion

    #region 位空间组合子工厂

    private static ModbusBitTagFactory CreateBitFactory(string cbntStartAddress = "10001")
    {
        var builder = new ModbusBitTagCbntBuilder();
        builder.WithCbntDescriptor(new TagCbntDescriptor { Name = "c", StartAddress = cbntStartAddress }).WithChannel(null!);
        return new ModbusBitTagFactory(builder, builder.TypedCbnt);
    }

    /// <summary>DI 必须落在离散输入区（1x）</summary>
    [Fact]
    public void BitTagFactory_DiOnRegisterAddress_Throws()
    {
        var ex = Assert.Throws<TagsProjectConfigurationException>(
            () => CreateBitFactory().CreateDITag(Descriptor(BuiltinTagKinds.DI, "40001", 1)));

        Assert.Contains("DI", ex.Message);
    }

    /// <summary>DO 必须落在线圈区（0x）</summary>
    [Fact]
    public void BitTagFactory_DoOnInputContactsAddress_Throws()
    {
        var ex = Assert.Throws<TagsProjectConfigurationException>(
            () => CreateBitFactory("00001").CreateDOTag(Descriptor(BuiltinTagKinds.DO, "10020", 1)));

        Assert.Contains("DO", ex.Message);
    }

    /// <summary>位空间组合里写寄存器的测点种类（如 INT16）⇒ 点名告知该用寄存器组合</summary>
    [Fact]
    public void BitTagFactory_RegisterKind_Throws()
    {
        var ex = Assert.Throws<TagsProjectConfigurationException>(
            () => CreateBitFactory().CreateTag(Descriptor(BuiltinTagKinds.INT16, "10020", 2)));

        Assert.Contains(nameof(ModbusRegisterTagFactory), ex.Message);
    }

    #endregion

    #region 寄存器空间组合子工厂

    private static ModbusRegisterTagFactory CreateRegisterFactory(string cbntStartAddress = "40001")
    {
        var builder = new ModbusRegisterTagCbntBuilder();
        builder.WithCbntDescriptor(new TagCbntDescriptor { Name = "c", StartAddress = cbntStartAddress }).WithChannel(null!);
        return new ModbusRegisterTagFactory(builder, builder.TypedCbnt);
    }

    /// <summary>寄存器组合里的地址必须落在 3x/4x</summary>
    [Fact]
    public void RegisterTagFactory_BitSpaceAddress_Throws()
    {
        var ex = Assert.Throws<TagsProjectConfigurationException>(
            () => CreateRegisterFactory().CreateTag(Descriptor(BuiltinTagKinds.INT16, "10020", 2)));

        Assert.Contains("不可作为寄存器测点", ex.Message);
    }

    /// <summary>测点种类不认识，且 <c>tagsize</c> 缺省（无法归一）</summary>
    [Fact]
    public void RegisterTagFactory_UnknownKindWithoutSize_Throws()
    {
        var ex = Assert.Throws<TagsProjectConfigurationException>(
            () => CreateRegisterFactory().CreateTag(Descriptor(BuiltinTagKinds.Unknown, "40001")));

        Assert.Contains(BuiltinTagKinds.Unknown, ex.Message);
    }

    /// <summary>测点种类不认识，但显式写了 <c>tagsize</c>（归一那一步绕不过去，仍要点名报错）</summary>
    [Fact]
    public void RegisterTagFactory_UnknownKindWithExplicitSize_Throws()
    {
        var ex = Assert.Throws<TagsProjectConfigurationException>(
            () => CreateRegisterFactory().CreateTag(Descriptor(BuiltinTagKinds.Unknown, "40001", 2)));

        Assert.Contains(BuiltinTagKinds.Unknown, ex.Message);
    }

    #endregion

    #region 直接测点构建器

    /// <summary>测点自带的通道就是 <see cref="ModbusTcpChannel"/> ⇒ 直接用它（不冒泡到测点组）</summary>
    [Fact]
    public async Task DirectTagBuilder_OwnModbusChannel_UsesIt()
    {
        var (channel, mock) = CreateChannel();
        var grp = new TagGrp(new TagGrpDescriptor { Name = "grp", IsEntry = true }, channel);
        mock
            .Setup(x => x.ReadHoldingRegistersAsync(1, (ushort)0, (ushort)1))
            .ReturnsAsync(new ushort[] { 0x1234 });
        await channel.EnsureConnectedAsync(false, CancellationToken.None);

        var builder = new ModbusTcpDirectTagBuilder();
        builder.WithTagDescriptor(Descriptor(BuiltinTagKinds.INT16, "1~40001", 2, EndianKinds.BigEndian));
        builder.WithParent(grp);
        builder.WithChannel(channel);

        var tag = builder.Build(channel);
        await tag.ReadAsync(CancellationToken.None);

        Assert.Equal((short)0x1234, (short)tag.Value!);
        Assert.Equal(channel, tag.Channel);
    }

    /// <summary>测点自带通道不是 <see cref="ModbusTcpChannel"/>（但驱动名写着 ModbusTcp）⇒ 报错并点名</summary>
    [Fact]
    public void DirectTagBuilder_OwnChannelOfWrongType_Throws()
    {
        var (channel, _) = CreateChannel();
        var grp = new TagGrp(new TagGrpDescriptor { Name = "grp", IsEntry = true }, channel);
        var builder = new ModbusTcpDirectTagBuilder();
        builder.WithTagDescriptor(Descriptor(BuiltinTagKinds.INT16, "1~40001", 2));
        builder.WithParent(grp);
        builder.WithChannel(new NotAModbusChannel());

        var ex = Assert.Throws<TagsProjectConfigurationException>(() => builder.Build(channel));

        Assert.Contains(nameof(ModbusTcpChannel), ex.Message);
    }

    #endregion

    /// <summary>冒充 ModbusTcp 驱动、但不是 <see cref="ModbusTcpChannel"/> 的通道</summary>
    private sealed class NotAModbusChannel : ITagChannel
    {
        public TagChannelDescriptor Descriptor { get; } = new TagChannelDescriptor
        {
            Name = "not-a-modbus-channel",
            Driver = ModbusTcpNames.DriverName,
        };

        public Task EnsureConnectedAsync(bool force, CancellationToken ct) => Task.CompletedTask;

        public Task DisconnectAsync(CancellationToken ct) => Task.CompletedTask;

        public void Dispose()
        {
        }
    }
}
