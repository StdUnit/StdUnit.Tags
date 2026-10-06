using StdUnit.Tags.ModbusTcp;
using StdUnit.Tags.S7;
using StdUnit.Tags.Tests.Fakes;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Xunit;

namespace StdUnit.Tags.Tests.Core.Exceptions;

/// <summary>
/// 加载期异常族（<see cref="TagsProjectLoadException"/> 及其子类）的行为测试：<br/>
/// 1. 异常族的继承形状（一个 catch 就能兜住全部加载期错误）；<br/>
/// 2. XML 属性非法 → <see cref="TagsProjectXmlException"/> 并带定位上下文；<br/>
/// 3. 地址非法 → <see cref="TagsProjectAddressException"/> 并带原始地址；<br/>
/// 4. 配置语义不自洽（重名测点 / 未声明通道 / 未注册驱动）→ <see cref="TagsProjectConfigurationException"/>。
/// </summary>
public class LoadTimeExceptionTests
{
    #region 异常族形状

    /// <summary>
    /// XSD 校验异常同时是"校验异常"和"加载期异常"：老代码 catch TagsProjectSchemaException 不受影响，
    /// 新代码 catch 基类即可统一处理。父链直接落在 <see cref="Exception"/> 上——
    /// 库里**没有**"能兜住全部异常"的根类型（运行期用 BCL 类型）。
    /// </summary>
    [Fact]
    public void SchemaException_DerivesFromValidationAndLoadException()
    {
        var errors = new[] { "第一处", "第二处" };
        var ex = new TagsProjectSchemaException(errors);

        Assert.IsAssignableFrom<TagsProjectValidationException>(ex);
        Assert.IsAssignableFrom<TagsProjectLoadException>(ex);
        Assert.Same(typeof(Exception), typeof(TagsProjectLoadException).BaseType);
        Assert.Same(errors, ex.Errors);
        Assert.Contains("schema", ex.Message);
        Assert.Contains("第一处", ex.Message);
        Assert.Contains("第二处", ex.Message);
    }

    /// <summary>通用校验异常的 Message 汇总全部条目，Errors 保留结构化明细。</summary>
    [Fact]
    public void ValidationException_AggregatesAllErrors()
    {
        var ex = new TagsProjectValidationException(new[] { "e1", "e2", "e3" });

        Assert.Equal(3, ex.Errors.Count);
        Assert.Contains("e1", ex.Message);
        Assert.Contains("e3", ex.Message);
    }

    /// <summary>地址异常同样是加载期异常，便于统一 catch。</summary>
    [Fact]
    public void AddressException_DerivesFromLoadException()
    {
        var ex = Assert.Throws<TagsProjectAddressException>(() => ModBusTcpAddressParser.Parse("50001.0"));

        Assert.IsAssignableFrom<TagsProjectLoadException>(ex);
        Assert.Contains("50001.0", ex.Message);
    }

    #endregion

    #region XML 值非法

    /// <summary>未知的 endian 值在解析描述符时立刻失败，并带上完整测点路径。</summary>
    [Fact]
    public void ToTagDescriptor_UnknownEndian_ThrowsXmlExceptionWithLocation()
    {
        var root = XElement.Parse("""
            <TagGrp name="g1">
                <TagCbnt name="输入">
                    <Tag name="bit" address="DB1.0" endian="Tiny"/>
                </TagCbnt>
            </TagGrp>
            """);
        var tagElement = root.Descendants("Tag").Single();

        var ex = Assert.Throws<TagsProjectXmlException>(() => tagElement.ToTagDescriptor());

        Assert.Contains("Tiny", ex.Message);
        Assert.Equal("TagGrp(g1)/TagCbnt(输入)/Tag(bit)", ex.Location);
    }

    /// <summary>缺少 name 属性时报错元素本身（此时还无法给出名字，用 '?' 占位）。</summary>
    [Fact]
    public void ToTagDescriptor_MissingName_ThrowsXmlException()
    {
        var element = XElement.Parse("<Tag address='DB1.0'/>");

        var ex = Assert.Throws<TagsProjectXmlException>(() => element.ToTagDescriptor());

        Assert.Contains("name", ex.Message);
        Assert.Equal("Tag(?)", ex.Location);
    }

    /// <summary>scanInterval 不是整数时给出毫秒语义提示。</summary>
    [Fact]
    public void ToTagGrpDescriptor_InvalidScanInterval_ThrowsXmlException()
    {
        var element = XElement.Parse("<TagGrp name='g1' scanInterval='abc'/>");

        var ex = Assert.Throws<TagsProjectXmlException>(() => element.ToTagGrpDescriptor());

        Assert.Contains("abc", ex.Message);
        Assert.Equal("TagGrp(g1)", ex.Location);
    }

    /// <summary>通道元素缺少 driver 属性。</summary>
    [Fact]
    public void ToTagChannelDescriptor_MissingDriver_ThrowsXmlException()
    {
        var element = XElement.Parse("<Channel name='ch1'/>");

        var ex = Assert.Throws<TagsProjectXmlException>(() => element.ToTagChannelDescriptor());

        Assert.Contains("driver", ex.Message);
        Assert.Equal("Channel(ch1)", ex.Location);
    }

    #endregion

    #region 地址非法

    [Theory]
    [InlineData("D")]
    [InlineData("XYZ")]
    [InlineData("DB")]
    [InlineData("DB200.x")]
    [InlineData("$$abc")]
    public void S7AddressParser_InvalidAddress_ThrowsAddressException(string addr)
    {
        var ex = Assert.Throws<TagsProjectAddressException>(() => S7AddressParser.Parse(addr));

        Assert.Contains(addr, ex.Message);
    }

    /// <summary>
    /// 回归：MB 区的位寻址地址（<c>MB.&lt;start&gt;.&lt;bit&gt;</c>）必须能解析，
    /// 且 Format() 的输出可以再解析回去（此前起始地址片段带上了 '.' 导致必然解析失败）。
    /// </summary>
    [Fact]
    public void S7AddressParser_ParsesMBAddressWithBit_AndRoundtrips()
    {
        var addr = S7AddressParser.Parse("MB.100.3");

        Assert.Equal(AreaKinds.MB, addr.Area);
        Assert.Equal(100, addr.StartAddress);
        Assert.True(addr.UseBit);
        Assert.Equal(3, addr.NthBit);
        Assert.Equal("MB.100.3", addr.Format());

        var again = S7AddressParser.Parse(addr.Format());
        Assert.Equal(addr.StartAddress, again.StartAddress);
        Assert.Equal(addr.NthBit, again.NthBit);
    }

    /// <summary>寄存器位地址超出 0~15 时给出带地址与测点名的地址异常。</summary>
    [Fact]
    public void ModbusRegisterBitCbntor_OutOfRangeNthBit_ThrowsAddressException()
    {
        var descriptor = new TagDescriptor { TagName = "bit20", RawAddress = "1~40001.20", TagKind = BuiltinTagKinds.BIT };

        var ex = Assert.Throws<TagsProjectAddressException>(
            () => new ModbusRegisterBitCbntor(descriptor, new TestModbusUshortCbnt(), 0, false, nthBit: 20));

        Assert.Contains("40001.20", ex.Message);
        Assert.Equal("Tag(bit20)", ex.Location);
    }

    #endregion

    #region 配置语义不自洽：重名测点

    /// <summary>
    /// 同一父节点下重名的直接测点：错误消息带完整路径，而不是字典的
    /// "An item with the same key has already been added"。
    /// </summary>
    [Fact]
    public void LoadTagGroup_DuplicateSiblingTags_ThrowsConfigurationExceptionWithPath()
    {
        var loader = new CompositeTagsLoader();
        var channel = new FakedChannel(new TagChannelDescriptor { Name = "ch1", Driver = "FakedDriver" });
        var parent = new TagGrp(new TagGrpDescriptor { Name = "g1" }, channel);
        loader.AddDirectTagBuilder<SimpleTagBuilder>("FakedDriver");

        loader.LoadTagGroup(parent, new TagDescriptor { TagName = "dup", ChannelName = "ch1" }, new[] { channel });

        var ex = Assert.Throws<TagsProjectConfigurationException>(() =>
            loader.LoadTagGroup(parent, new TagDescriptor { TagName = "dup", ChannelName = "ch1" }, new[] { channel }));

        Assert.Contains("dup", ex.Message);
        Assert.Contains("唯一", ex.Message);
        Assert.Equal("TagGrp(g1)/Tag(dup)", ex.Location);
    }

    /// <summary>同一父节点下重名的测点组：同样带完整路径。</summary>
    [Fact]
    public void LoadTagGroup_DuplicateSiblingGroups_ThrowsConfigurationExceptionWithPath()
    {
        var loader = new CompositeTagsLoader();
        var channel = new FakedChannel(new TagChannelDescriptor { Name = "ch1", Driver = "FakedDriver" });
        var parent = new TagGrp(new TagGrpDescriptor { Name = "root" }, channel);

        loader.LoadTagGroup(parent, new TagGrpDescriptor { Name = "g1" }, new[] { channel });

        var ex = Assert.Throws<TagsProjectConfigurationException>(() =>
            loader.LoadTagGroup(parent, new TagGrpDescriptor { Name = "g1" }, new[] { channel }));

        Assert.Equal("TagGrp(root)/TagGrp(g1)", ex.Location);
    }

    /// <summary>测点组合内部的子测点重名：路径包含所属组合。</summary>
    [Fact]
    public void LoadTagGroup_DuplicateChildrenInsideTagCbnt_ThrowsConfigurationExceptionWithPath()
    {
        var loader = new CompositeTagsLoader();
        var channel = new FakedChannel(new TagChannelDescriptor { Name = "ch1", Driver = "FakedDriver" });
        var parent = new TagGrp(new TagGrpDescriptor { Name = "g1" }, channel);
        loader.AddTagsCbntBuilder<SimpleCbntBuilder>("FakedDriver");

        var cbnt = new TagCbntDescriptor { Name = "c1", ChannelName = "ch1" };
        cbnt.Children.Add(new TagDescriptor { TagName = "t1" });
        cbnt.Children.Add(new TagDescriptor { TagName = "t1" });

        var ex = Assert.Throws<TagsProjectConfigurationException>(() =>
            loader.LoadTagGroup(parent, cbnt, new[] { channel }));

        Assert.Contains("t1", ex.Message);
        Assert.Equal("TagGrp(g1)/TagCbnt(c1)/Tag(t1)", ex.Location);
    }

    #endregion

    #region 配置语义不自洽：其它

    /// <summary>引用了未声明的通道：消息里列出已声明的通道名，位置指向出错的测点。</summary>
    [Fact]
    public void LoadTagGroup_UnknownChannelReference_ThrowsConfigurationExceptionWithPath()
    {
        var loader = new CompositeTagsLoader();
        var channel = new FakedChannel(new TagChannelDescriptor { Name = "ch1", Driver = "FakedDriver" });
        var parent = new TagGrp(new TagGrpDescriptor { Name = "g1" }, channel);
        loader.AddDirectTagBuilder<SimpleTagBuilder>("FakedDriver");

        var ex = Assert.Throws<TagsProjectConfigurationException>(() =>
            loader.LoadTagGroup(parent, new TagDescriptor { TagName = "t1", ChannelName = "nope" }, new[] { channel }));

        Assert.Contains("nope", ex.Message);
        Assert.Contains("ch1", ex.Message);
        Assert.Equal("TagGrp(g1)/Tag(t1)", ex.Location);
    }

    /// <summary>未注册驱动的通道工厂：错误消息列出已注册的驱动。</summary>
    [Fact]
    public void CompositeChannelFactory_UnregisteredDriver_ThrowsConfigurationException()
    {
        var factory = new CompositeTagChannelFactory();

        var ex = Assert.Throws<TagsProjectConfigurationException>(() =>
            factory.Create(new TagChannelDescriptor { Name = "ch1", Driver = "NoSuchDriver" }));

        Assert.Contains("NoSuchDriver", ex.Message);
        Assert.Equal("Channel(ch1)", ex.Location);
    }

    #endregion

    #region Helper Types

    private sealed class SimpleTagBuilder : TagBuilderBase
    {
        protected override ITag Fallback(ITagChannel channel) =>
            new FakedTag(TagDescriptor, channel as FakedChannel, Parent);
    }

    private sealed class SimpleCbntBuilder : TagCbntBuilderBase
    {
        public SimpleCbntBuilder() : base(new TestByteTagCbnt(new TagCbntDescriptor { Name = "c1" })) { }

        protected override ITagCbntor Fallback(TagDescriptor descriptor, ITagChannel channel) =>
            new SimpleCbntor(descriptor, this.TagCbnt, 0, 0);

        protected override TagCbntBuilderBase AutoLayout() => this;
    }

    private sealed class SimpleCbntor : TagCbntor
    {
        public SimpleCbntor(TagDescriptor tagDescriptor, ITagCbnt tagCbnt, int tagOffset, int cacheOffset)
            : base(tagDescriptor, tagCbnt, tagOffset, cacheOffset)
        {
        }

        public override object? Value { get; set; }

        public override Task ReadAsync(CancellationToken ct) => Task.CompletedTask;

        public override Task WriteAsync(CancellationToken ct) => Task.CompletedTask;
    }

    /// <summary>Modbus 寄存器组合子需要的 <c>TagCbnt&lt;ushort&gt;</c> 测试实现。</summary>
    private sealed class TestModbusUshortCbnt : TagCbnt<ushort>
    {
        public TestModbusUshortCbnt() : base(new TagCbntDescriptor { Name = "mb-cbnt", StartAddress = "1~40001" })
        {
        }

        public override Task ReadAsync(CancellationToken ct) => Task.CompletedTask;

        public override Task WriteAsync(CancellationToken ct) => Task.CompletedTask;
    }

    #endregion
}
