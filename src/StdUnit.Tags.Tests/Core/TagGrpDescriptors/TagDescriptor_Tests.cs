using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace StdUnit.Tags.Tests.Core.TagGrpDescriptors;

public class TagGrpDescriptor_Tests
{
    public TagGrpDescriptor_Tests()
    {
    }


    /// <summary>
    /// 测试 TagGrpDescriptor 的 ToXElement() 和 ToTagGrpDescriptor() 的 Roundtrip
    /// </summary>
    [Fact]
    public void Test_ConversionWithXElement()
    {
        /* 先构造一个如下的点表: 
            <TagGrp name="Group1" channel="ChannelA" isEntry="true" isEnabled="true" scanInterval="1000">
                <TagCbnt name="TagCbnt1" startAddress="D100" isEnabled="true" access="RW">
                    <Tag name="Tag1" address="DB100.100" type="INT16" endian="BigEndian" access="RW"/>
                </TagCbnt>
                <TagCbnt name="Tag2" startAddress="D200" isEnabled="true" access="RO">
                    <Tag name="Tag3" address="DB100.200" type="FLOAT" endian="LittleEndian" access="RO"/>
                </TagCbnt>
            </TagGrp>
        */
        var grp1 = new TagGrpDescriptor
        {
            Name = "Group1",
            ChannelName = "ChannelA",
            IsEntry = true,
            IsEnabled = true,
            ScanInterval = 1000,
        };

        var cbnt1 = new TagCbntDescriptor
        {
            Name = "TagCbnt1",
            StartAddress = "D100",
            IsEnabled = true,
            AccessMode = TagAccessMode.RW,
        };
        cbnt1.Children.Add(new TagDescriptor
        {
            TagName = "Tag1",
            RawAddress = "DB100.100",
            TagKind = BuiltinTagKinds.INT16,
            EndianKind = EndianKinds.BigEndian,
            AccessMode = TagAccessMode.RW,
        });
        grp1.Children.Add(cbnt1);

        var cbnt2 = new TagCbntDescriptor
        {
            Name = "Tag2",
            StartAddress = "D200",
            IsEnabled = true,
            AccessMode = TagAccessMode.RO,
        };
        cbnt2.Children.Add(new TagDescriptor
        {
            TagName = "Tag3",
            RawAddress = "DB200.200",
            TagKind = BuiltinTagKinds.FLOAT,
            EndianKind = EndianKinds.LittleEndian,
            AccessMode = TagAccessMode.RO,
        });

        grp1.Children.Add(cbnt2);

        var element1 = grp1.ToXElement();
        var xml1 = element1.ToString();

        var grp2 = element1.ToTagGrpDescriptor();
        var element2 = grp2.ToXElement();
        var xml2 = element2.ToString();
        Assert.Equal(xml1, xml2);
    }


    /// <summary>
    /// Tag 作为 TagGrp 的直接子节点，测试 ToXElement() 和 ToTagGrpDescriptor() 的 Roundtrip
    /// </summary>
    [Fact]
    public void ToXElement_WithDirectTagChild_Roundtrips()
    {
        var grp = new TagGrpDescriptor
        {
            Name = "GrpWithTag",
            ScanInterval = 500,
        };
        grp.Children.Add(new TagDescriptor
        {
            TagName = "DirectTag",
            RawAddress = "DB100.0",
            TagKind = BuiltinTagKinds.INT32,
            TagSize = 4,
            EndianKind = EndianKinds.LittleEndian,
            AccessMode = TagAccessMode.RO,
        });

        var element = grp.ToXElement();
        var recovered = element.ToTagGrpDescriptor();

        Assert.Equal("GrpWithTag", recovered.Name);
        Assert.Single(recovered.Children);
        var child = Assert.IsType<TagDescriptor>(recovered.Children[0]);
        Assert.Equal("DirectTag", child.TagName);
        Assert.Equal("DB100.0", child.RawAddress);
        Assert.Equal(TagAccessMode.RO, child.AccessMode);
    }


    /// <summary>
    /// TagGrp 作为 TagGrp 的直接子节点（嵌套）
    /// </summary>
    [Fact]
    public void ToXElement_WithNestedTagGrpChild_Roundtrips()
    {
        var inner = new TagGrpDescriptor
        {
            Name = "InnerGrp",
            IsEntry = true,
            ScanInterval = 200,
            AccessMode = TagAccessMode.RW,
        };
        inner.Children.Add(new TagDescriptor
        {
            TagName = "InnerTag",
            RawAddress = "0.0",
            TagKind = BuiltinTagKinds.BYTE,
            TagSize = 1,
        });

        var outer = new TagGrpDescriptor
        {
            Name = "OuterGrp",
            IsEntry = false,
        };
        outer.Children.Add(inner);

        var element = outer.ToXElement();
        var recovered = element.ToTagGrpDescriptor();

        Assert.Equal("OuterGrp", recovered.Name);
        Assert.Single(recovered.Children);
        var innerRecovered = Assert.IsType<TagGrpDescriptor>(recovered.Children[0]);
        Assert.Equal("InnerGrp", innerRecovered.Name);
        Assert.True(innerRecovered.IsEntry);
        Assert.Equal(TagAccessMode.RW, innerRecovered.AccessMode);
        Assert.Equal(200, innerRecovered.ScanInterval);
        Assert.Single(innerRecovered.Children);
        Assert.IsType<TagDescriptor>(innerRecovered.Children[0]);
    }


    /// <summary>
    /// AccessMode 为空时不输出 access 属性
    /// </summary>
    [Fact]
    public void ToXElement_WhenAccessModeNull_OmitsAccessAttribute()
    {
        var grp = new TagGrpDescriptor
        {
            Name = "NoAccess",
        };

        var xml = grp.ToXElement().ToString();

        Assert.DoesNotContain("access=\"", xml);
    }


    /// <summary>
    /// 混合子节点（Tag + Cbnt + Grp）
    /// </summary>
    [Fact]
    public void ToXElement_WithMixedChildren_Roundtrips()
    {
        var grp = new TagGrpDescriptor
        {
            Name = "MixedGrp",
        };
        grp.Children.Add(new TagDescriptor { TagName = "tag1", RawAddress = "0", TagKind = BuiltinTagKinds.BYTE, TagSize = 1 });
        grp.Children.Add(new TagCbntDescriptor { Name = "cbnt1", StartAddress = "10" });
        grp.Children.Add(new TagGrpDescriptor { Name = "grp1" });

        var element = grp.ToXElement();
        var recovered = element.ToTagGrpDescriptor();

        Assert.Equal(3, recovered.Children.Count);
        Assert.IsType<TagDescriptor>(recovered.Children[0]);
        Assert.IsType<TagCbntDescriptor>(recovered.Children[1]);
        Assert.IsType<TagGrpDescriptor>(recovered.Children[2]);
    }
}
