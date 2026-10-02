using Itminus.Tags.S7;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Xunit;

namespace Itminus.Tags.Tests.S7Tags
{
    public class StrTagCbntorTests
    {

        [Fact]
        public void CreateStrTag_Throws_When_maxlen_NotSpecified()
        {
            var channelFactory = new S7TagChannelFactory(new LoggerFactory());
            var channel = channelFactory.Create(new TagChannelDescriptor()
            {
                Driver = "S7",
                Name = "S7-1",
                Extras = new Dictionary<string, XElement>() { }
            });
            var builder = new S7TagCbntBuilder()
                .WithCbntDescriptor(new TagCbntDescriptor { Name = "cbnt1", StartAddress = "DB200.100" });

            var descriptor = new TagDescriptor()
            {
                TagName = "str1",
                RawAddress = "DB200.102",
                TagKind = BuiltinTagKinds.STR,
            };

            Assert.Throws<InvalidDataException>(() =>
            {
                var cbnt = builder.AddTags([descriptor], channel).Build(channel);
                var tag = cbnt.SelectTag("str1");
                Assert.NotNull(tag);
            });
        }


        [Fact]
        public void StrTag_GetAndSet_WithExactLength()
        {
            var channelFactory = new S7TagChannelFactory(new LoggerFactory());
            var channel = channelFactory.Create(new TagChannelDescriptor()
            {
                Driver = "S7",
                Name = "S7-1",
                Extras = new Dictionary<string, XElement>() { }
            });
            var builder = new S7TagCbntBuilder()
                .WithCbntDescriptor(new TagCbntDescriptor { Name = "cbnt1", StartAddress = "DB200.100" });

            byte maxLen = 10;
            var descriptor = new TagDescriptor()
            {
                TagName = "str1",
                RawAddress = "DB200.102",
                TagKind = BuiltinTagKinds.STR,
            };
            descriptor.Extras["maxlen"] = new XAttribute("maxlen", maxLen);

            var cbnt = builder.AddTags([descriptor], channel).Build(channel);
            var tag = cbnt.SelectTag("str1");
            var strTag = tag as S7StrTagCbntor;
            Assert.NotNull(strTag);

            Assert.Equal(new byte[] { (byte)maxLen, (byte)0 }, ((TagCbnt<byte>)cbnt).Cache.Span.Slice(2, 2).ToArray());


            tag.Value = "ABCDE";
            Assert.Equal("ABCDE", tag.Value);
            Assert.Equal("ABCDE", tag.GetTagValue<string>());
            Assert.Equal(new byte[] { (byte)maxLen, (byte)5 }, ((TagCbnt<byte>)cbnt).Cache.Span.Slice(2, 2).ToArray());
            Assert.Equal(5, strTag.Strlen);
            Assert.Equal(10, strTag.Maxlen);


            var newVal1 = "WXYZ";
            tag.Value = newVal1;
            var got = tag.Value as string;
            Assert.Equal(newVal1, got);
            Assert.Equal(new byte[] { (byte)maxLen, (byte)4 }, ((TagCbnt<byte>)cbnt).Cache.Span.Slice(2, 2).ToArray());
            var span = ((TagCbnt<byte>)cbnt).Cache.Span.Slice(2 + 2, 4);
            // net472 无 Encoding.GetString(ReadOnlySpan<byte>)（.NET Core 2.1+），先拷贝成数组
            var roundtrip = Encoding.ASCII.GetString(span.ToArray());
            Assert.Equal(newVal1, roundtrip);
            Assert.Equal(4, strTag.Strlen);
            Assert.Equal(10, strTag.Maxlen);
        }


        [Fact]
        public void StrTag_Set_Throws_When_TooLong()
        {
            var channelFactory = new S7TagChannelFactory(new LoggerFactory());
            var channel = channelFactory.Create(new TagChannelDescriptor()
            {
                Driver = "S7",
                Name = "S7-1",
                Extras = new Dictionary<string, XElement>() { }
            });
            var builder = new S7TagCbntBuilder()
                .WithCbntDescriptor(new TagCbntDescriptor { Name = "cbnt1", StartAddress = "DB200.100" });

            byte maxLen = 10;
            var descriptor = new TagDescriptor()
            {
                TagName = "str1",
                RawAddress = "DB200.102",
                TagKind = BuiltinTagKinds.STR,
            };
            descriptor.Extras["maxlen"] = new XAttribute("maxlen", maxLen);

            var cbnt = builder.AddTags([descriptor], channel).Build(channel);
            var tag = cbnt.SelectTag("str1");

            Assert.Throws<ArgumentException>(() => tag.Value = "1234567890-");
        }

        [Fact]
        public void StrTag_Set_When_MaxLen()
        {
            var channelFactory = new S7TagChannelFactory(new LoggerFactory());
            var channel = channelFactory.Create(new TagChannelDescriptor()
            {
                Driver = "S7",
                Name = "S7-1",
                Extras = new Dictionary<string, XElement>() { }
            });
            var builder = new S7TagCbntBuilder()
                .WithCbntDescriptor(new TagCbntDescriptor { Name = "cbnt1", StartAddress = "DB200.100" });

            byte maxLen = 10;
            var descriptor = new TagDescriptor()
            {
                TagName = "str1",
                RawAddress = "DB200.102",
                TagKind = BuiltinTagKinds.STR,
            };
            descriptor.Extras["maxlen"] = new XAttribute("maxlen", maxLen);

            var cbnt = builder.AddTags([descriptor], channel).Build(channel);
            var tag = cbnt.SelectTag("str1");
            tag.Value = "0123456789";
            Assert.Equal("0123456789", tag.GetTagValue<string>());

            var strTag = tag as S7StrTagCbntor;
            Assert.NotNull(strTag);
            Assert.Equal(10, strTag.Strlen);
            Assert.Equal(10, strTag.Maxlen);
        }

        [Fact]
        public void StrTag_PlcOverwritesMaxLen()
        {
            var channelFactory = new S7TagChannelFactory(new LoggerFactory());
            var channel = channelFactory.Create(new TagChannelDescriptor()
            {
                Driver = "S7",
                Name = "S7-1",
                Extras = new Dictionary<string, XElement>() { }
            });
            var builder = new S7TagCbntBuilder()
                .WithCbntDescriptor(new TagCbntDescriptor { Name = "cbnt1", StartAddress = "DB200.100" });

            byte maxLen = 10;
            var descriptor = new TagDescriptor()
            {
                TagName = "str1",
                RawAddress = "DB200.102",
                TagKind = BuiltinTagKinds.STR,
            };
            descriptor.Extras["maxlen"] = new XAttribute("maxlen", maxLen);

            var cbnt = builder.AddTags([descriptor], channel).Build(channel);
            var tag = cbnt.SelectTag("str1");
            var strTag = Assert.IsType<S7StrTagCbntor>(tag);

            Span<byte> cache = stackalloc byte[]
            {
                0, 0,
                0, 5,  // 模拟PLC把MaxLen改成了0
                (byte)'A', (byte)'B', (byte)'C', (byte)'D', (byte)'E',
                0, 0, 0, 0, 0,
            };
            cache.CopyTo(((TagCbnt<byte>)cbnt).Cache.Span);

            // 读取不会崩溃
            Assert.Equal("ABCDE", tag.Value);
            Assert.Equal(10, strTag.Maxlen);
            Assert.Equal(5, strTag.Strlen);
            Assert.Equal(new byte[] { 0, 5 }, ((TagCbnt<byte>)cbnt).Cache.Span.Slice(2, 2).ToArray());

            // 写入时仍然使用自己的MaxLen
            tag.Value = "WXYZ";
            Assert.Equal("WXYZ", tag.Value);
            Assert.Equal(10, strTag.Maxlen);
            Assert.Equal(4, strTag.Strlen);
            Assert.Equal(new byte[] { 10, 4 }, ((TagCbnt<byte>)cbnt).Cache.Span.Slice(2, 2).ToArray());
        }
    }
}
