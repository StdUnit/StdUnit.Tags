using StdUnit.Tags;
using StdUnit.Tags.S7;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using StdUnit.Tags.Rx;

namespace StdUnit.Tags.Tests.Core.TagCbnts;



public class TagCbnt_Tests
{
    internal class MockChannel : IContinuousBytesBasedTagChannel
    {
        private byte[] _bytes = new byte[4]
        {
            0x03, 0x00, 0x00, 0x00,
        };

        public TagChannelDescriptor Descriptor => new TagChannelDescriptor
        {
            Name = "Mock",
            Driver = "MOCK",
        };

        public Task DisconnectAsync(CancellationToken ct)
        {
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            return;
        }

        public Task EnsureConnectedAsync(bool force, CancellationToken ct)
        {
            return Task.CompletedTask;
        }

        public Task<byte[]> ReadAsync(string address, int count, CancellationToken ct)
        {
            return Task.FromResult(this._bytes);
        }

        public Task WriteAsync(string address, byte[] bytes, CancellationToken ct)
        {
            this._bytes = bytes;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Test_TagCnbtRWTriggerTagSyncsEvents()
    {
        var channel = new MockChannel();
        var cbnt = new TestByteTagCbnt(new TagCbntDescriptor { Name = "mock tag cbnt", StartAddress = "0.0", IsEnabled = true })
        {
            Channel = channel,
        };
        cbnt.ResizeCache(4);
        var tag1 = new S7BitTagCbntor(
            new TagDescriptor() { RawAddress = "0.0", TagSize = 1, TagKind = BuiltinTagKinds.BIT, TagName = "测点1" },
            cbnt,
            0,
            0,
            0
            );
        var tag2 = new S7BitTagCbntor(
            new TagDescriptor() { RawAddress = "0.1", TagSize = 1, TagKind = BuiltinTagKinds.BIT, TagName = "测点2" },
            cbnt,
            0,
            0,
            1
            );

        var x = 10;
        var y = 20;

        tag1.OnTagRead += (o, args) =>
        {
            if (true.Equals(args.NewValue))
            {
                x++;
            }
        };
        tag2.OnTagRead += (o, args) =>
        {
            if (true.Equals(args.NewValue))
            {
                y++;
            }
        };
        tag1.OnTagWritten += (o, args) =>
        {
            if (true.Equals(args.NewValue))
            {
                x--;
            }
        };
        tag2.OnTagWritten += (o, args) =>
        {
            if (true.Equals(args.NewValue))
            {
                y--;
            }
        };

        cbnt.Children["tag1"] = tag1;
        cbnt.Children["tag2"] = tag2;

        var ct = CancellationToken.None;
        await cbnt.ReadAsync(ct);
        Assert.Equal(11, x);
        Assert.Equal(21, y);

        await cbnt.ReadAsync(ct);
        Assert.Equal(12, x);
        Assert.Equal(22, y);


        await cbnt.WriteAsync(ct);
        Assert.Equal(11, x);
        Assert.Equal(21, y);

        await cbnt.WriteAsync(ct);
        Assert.Equal(10, x);
        Assert.Equal(20, y);
    }


    [Fact]
    public async Task Test_TagCnbtRWTriggerTagSyncsEventsObservable()
    {
        var channel = new MockChannel();
        var cbnt = new TestByteTagCbnt(new TagCbntDescriptor { Name = "mock tag cbnt", StartAddress = "0.0", IsEnabled = true })
        {
            Channel = channel,
        };
        cbnt.ResizeCache(4);
        var tag1 = new S7BitTagCbntor(
            new TagDescriptor() { RawAddress = "0.0", TagSize = 1, TagKind = BuiltinTagKinds.BIT, TagName = "测点1" },
            cbnt,
            0,
            0,
            0
            );
        var tag2 = new S7BitTagCbntor(
            new TagDescriptor() { RawAddress = "0.1", TagSize = 1, TagKind = BuiltinTagKinds.BIT, TagName = "测点2" },
            cbnt,
            tagOffset: 0,
            cacheOffset: 0,
            nthBit: 1
            );

        var x = 10;
        var y = 20;


        var obs1 = tag1.Watch();
        var obs2 = tag2.Watch();

        using var d1 = obs1.Subscribe(ev =>
        {
            Assert.Equal(tag1, ev.Sender);
            if (true.Equals(ev.EventArgs.NewValue))
            {
                x++;
            }
        });

        using var d2 = obs2.Subscribe(ev =>
        {
            Assert.Equal(tag2, ev.Sender);
            if (true.Equals(ev.EventArgs.NewValue))
            {
                y++;
            }
        });

        cbnt.Children["tag1"] = tag1;
        cbnt.Children["tag2"] = tag2;
        var ct = CancellationToken.None;
        await cbnt.ReadAsync(ct);
        Assert.Equal(11, x);
        Assert.Equal(21, y);

        await cbnt.ReadAsync(ct);
        Assert.Equal(12, x);
        Assert.Equal(22, y);


        await cbnt.WriteAsync(ct);
        Assert.Equal(13, x);
        Assert.Equal(23, y);

        await cbnt.WriteAsync(ct);
        Assert.Equal(14, x);
        Assert.Equal(24, y);
    }
}
