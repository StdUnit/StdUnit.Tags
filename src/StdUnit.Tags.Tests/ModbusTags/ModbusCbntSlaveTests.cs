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
/// <c>TagCbnt</c> 上的 <c>slave</c> 属性应被合成进组合的起始地址（位空间与寄存器空间一致）。<br/>
/// 语义见 <see cref="ModbusCbntSlaveAddress"/>。
/// </summary>
public class ModbusCbntSlaveTests : IDisposable
{
    private readonly ServiceProvider _root;
    private readonly IServiceScope _scope;

    /// <summary>
    /// c'tor
    /// </summary>
    public ModbusCbntSlaveTests()
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

    private ITagsProject CreateProject(string cbntXml)
    {
        var xml = XElement.Parse($@"
<root>
    <Channel name='ModbusTcp-2' driver='ModbusTcp'>
        <IpAddr>localhost</IpAddr>
        <Port>502</Port>
    </Channel>
    <TagGrp name='g1' isEntry='true' isEnabled='true' channel='ModbusTcp-2' scanInterval='10'>
{cbntXml}
    </TagGrp>
</root>");
        var factory = this._scope.ServiceProvider.GetRequiredService<ITagsProjectFactory>();
        return factory.Create(string.Empty, xml);
    }

    [Fact]
    public void BitSpace_WithSlaveAttribute_ShouldRewriteStartAddress()
    {
        using var proj = this.CreateProject(@"
        <TagCbnt name='输出' address='00001' slave='3' access='RW'>
            <Tag name='绿灯' address='00020' type='DO'></Tag>
            <Tag name='红灯' address='00021' type='DO'></Tag>
        </TagCbnt>");

        var cbnt = proj.Tags.SelectGrp("g1")!.SelectCbnt("输出")!;

        // 前导零保持原样，只有从站号被改写
        Assert.Equal("3~00001", cbnt.StartAddress);
        // 偏移仍以起始地址为基准：00020 / 00021 的偏移是 19 / 20，缓存 21 位
        Assert.Equal(21, ((TagCbnt<bool>)cbnt).CacheSize);
    }

    [Fact]
    public void RegisterSpace_WithSlaveAttribute_ShouldRewriteStartAddress()
    {
        using var proj = this.CreateProject(@"
        <TagCbnt name='采集' address='40001' slave='3' access='RO'>
            <Tag name='int16' address='40021' type='INT16'></Tag>
        </TagCbnt>");

        var cbnt = proj.Tags.SelectGrp("g1")!.SelectCbnt("采集")!;

        Assert.Equal("3~40001", cbnt.StartAddress);
        // 40021 的偏移是寄存器 20、字节 40，加 2 字节 => 42
        Assert.Equal(42, ((TagCbnt<ushort>)cbnt).CacheSize);
    }

    [Fact]
    public void WithSlaveAttribute_ShouldOverrideSlaveInAddress()
    {
        using var proj = this.CreateProject(@"
        <TagCbnt name='输入' address='1~10001' slave='3' access='RO'>
            <Tag name='按钮' address='1~10001' type='DI'></Tag>
        </TagCbnt>");

        var cbnt = proj.Tags.SelectGrp("g1")!.SelectCbnt("输入")!;

        Assert.Equal("3~10001", cbnt.StartAddress);
    }

    [Fact]
    public void WithoutSlaveAttribute_ShouldKeepStartAddress()
    {
        using var proj = this.CreateProject(@"
        <TagCbnt name='输入' address='10001' access='RO'>
            <Tag name='按钮' address='10001' type='DI'></Tag>
        </TagCbnt>");

        var cbnt = proj.Tags.SelectGrp("g1")!.SelectCbnt("输入")!;

        Assert.Equal("10001", cbnt.StartAddress);
    }

    [Fact]
    public void InvalidSlaveAttribute_ShouldThrow()
    {
        Assert.Throws<TagsProjectXmlException>(() => this.CreateProject(@"
        <TagCbnt name='输入' address='10001' slave='abc' access='RO'>
            <Tag name='按钮' address='10001' type='DI'></Tag>
        </TagCbnt>"));
    }

    [Fact]
    public async Task BitSpace_Read_ShouldUseSynthesizedAddress()
    {
        using var proj = this.CreateProject(@"
        <TagCbnt name='输出' address='00001' slave='3' access='RW'>
            <Tag name='绿灯' address='00020' type='DO'></Tag>
        </TagCbnt>");

        var cbnt = proj.Tags.SelectGrp("g1")!.SelectCbnt("输出")!;
        var fake = new FakeModbusChannel();
        cbnt.Channel = fake;

        await cbnt.ReadAsync(CancellationToken.None);

        Assert.Equal("3~00001", fake.LastBitAddress);
    }

    [Fact]
    public async Task RegisterSpace_Read_ShouldUseSynthesizedAddress()
    {
        using var proj = this.CreateProject(@"
        <TagCbnt name='采集' address='40001' slave='3' access='RO'>
            <Tag name='int16' address='40021' type='INT16'></Tag>
        </TagCbnt>");

        var cbnt = proj.Tags.SelectGrp("g1")!.SelectCbnt("采集")!;
        var fake = new FakeModbusChannel();
        cbnt.Channel = fake;

        await cbnt.ReadAsync(CancellationToken.None);

        Assert.Equal("3~40001", fake.LastRegisterAddress);
    }
}
