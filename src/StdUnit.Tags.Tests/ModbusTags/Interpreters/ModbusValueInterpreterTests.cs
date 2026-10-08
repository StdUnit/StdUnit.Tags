using StdUnit.Tags;
using StdUnit.Tags.ModbusTcp;
using System;
using System.Xml.Linq;
using Xunit;

namespace StdUnit.Tags.Tests.ModbusTags;

/// <summary>
/// <c>ModbusValueInterpreter&lt;T&gt;</c> 的直接单元测试：只测"寄存器数组 ↔ 目标类型"的换算本身，
/// 不经过测点、组合与通道。<br/>
/// 钉住两件事：① 每种排布下 4/8 个字节的落点；② 同一串字节在有符号与无符号类型上的不同解读
/// （换算用 <c>BinaryPrimitives</c> 的强类型方法，不经过 <c>ulong</c> 中转）。
/// </summary>
public class ModbusValueInterpreterTests
{
    /// <summary><c>TagKind</c>/<c>TagSize</c> 在这里只用于报错消息，与换算无关（字节数由解读器类型给出）</summary>
    private static TagDescriptor Descriptor(EndianKinds endian, string? interpret = null)
    {
        var descriptor = new TagDescriptor
        {
            TagName = "v",
            RawAddress = "40001",
            TagKind = BuiltinTagKinds.UINT32,
            TagSize = 4,
            EndianKind = endian,
        };

        if (interpret is not null)
        {
            descriptor.Extras["interpret"] = new XAttribute("interpret", interpret);
        }

        return descriptor;
    }

    private static EndianKinds Parse(string endian) =>
        endian == "BigEndian" ? EndianKinds.BigEndian : EndianKinds.LittleEndian;

    /// <summary>某个排布该配的描述符（写法取自该排布的规范记法，<c>endian</c> 用"哪种能解析通过"的那一种）</summary>
    private static TagDescriptor DescriptorFor(ReadOnlyMemory<byte> packing, int byteCount)
    {
        var notation = ModbusInterpret.DescribeNotation(packing, byteCount);
        foreach (var endian in new[] { EndianKinds.BigEndian, EndianKinds.LittleEndian })
        {
            var descriptor = Descriptor(endian, notation);
            try
            {
                if (ModbusInterpret.VariantIndex(descriptor, byteCount, "Tag(v)") >= 0)
                {
                    return descriptor;
                }
            }
            catch (TagsProjectXmlException)
            {
                // 每对字符的先后与 endian 绑定，这一种不接受，试下一种
            }
        }

        throw new InvalidOperationException($"记法 '{notation}' 在两种 endian 下都不合法");
    }

    private static (int ByteCount, int RegisterCount) Width<T>(ModbusValueInterpreter<T> interpreter)
        where T : unmanaged => (interpreter.ByteCount, interpreter.RegisterCount);

    /// <summary>写进去再读回来必须还是同一个值（寄存器缓冲复用，覆盖多组值）</summary>
    private static void AssertRoundTrips<T>(ModbusValueInterpreter<T> interpreter, params T[] values)
        where T : unmanaged
    {
        var registers = new ushort[interpreter.RegisterCount];
        foreach (var value in values)
        {
            interpreter.Write(value, registers);
            Assert.Equal(value, interpreter.Read(registers));
        }
    }

    private static readonly uint[] UInt32Values = { 0u, 1u, 0x12345678u, 0x80000000u, uint.MaxValue };

    private static readonly int[] Int32Values = { 0, 1, -1, -2, 0x12345678, int.MinValue, int.MaxValue };

    private static readonly float[] FloatValues =
    {
        0f, -0f, 1.25f, -1.5f, float.Epsilon, float.MaxValue, float.MinValue,
        float.NaN, float.PositiveInfinity, float.NegativeInfinity,
    };

    private static readonly ulong[] UInt64Values = { 0UL, 1UL, 0x123456789ABCDEF0UL, 0x8000000000000000UL, ulong.MaxValue };

    private static readonly long[] Int64Values = { 0L, 1L, -1L, 0x123456789ABCDEF0L, long.MinValue, long.MaxValue };

    #region 32 位

    [Theory]
    [InlineData("BigEndian", null, (ushort)0x1234, (ushort)0x5678)]       // ABCD
    [InlineData("BigEndian", "CDAB", (ushort)0x5678, (ushort)0x1234)]     // CDAB
    [InlineData("LittleEndian", "BADC", (ushort)0x3412, (ushort)0x7856)]  // BADC
    [InlineData("LittleEndian", null, (ushort)0x7856, (ushort)0x3412)]    // DCBA
    public void UInt32_ReadsEachLayout(string endian, string? interpret, ushort first, ushort second)
    {
        var interpreter = ModbusUInt32Interpreter.For(Descriptor(Parse(endian), interpret));

        Assert.Equal(4, interpreter.ByteCount);
        Assert.Equal(2, interpreter.RegisterCount);
        Assert.Equal(0x12345678u, interpreter.Read(new[] { first, second }));
    }

    [Theory]
    [InlineData("BigEndian", null, (ushort)0x1234, (ushort)0x5678)]
    [InlineData("BigEndian", "CDAB", (ushort)0x5678, (ushort)0x1234)]
    [InlineData("LittleEndian", "BADC", (ushort)0x3412, (ushort)0x7856)]
    [InlineData("LittleEndian", null, (ushort)0x7856, (ushort)0x3412)]
    public void UInt32_WritesEachLayout(string endian, string? interpret, ushort first, ushort second)
    {
        var interpreter = ModbusUInt32Interpreter.For(Descriptor(Parse(endian), interpret));
        var registers = new ushort[2];

        interpreter.Write(0x12345678u, registers);

        Assert.Equal(new[] { first, second }, registers);
    }

    /// <summary>同一串字节（设备端字节 <c>80 00 00 01</c>）：有符号读成负数、无符号读成正数</summary>
    [Fact]
    public void Int32_And_UInt32_ShareBytesButDifferInSign()
    {
        var registers = new[] { (ushort)0x8000, (ushort)0x0001 };

        Assert.Equal(unchecked((int)0x80000001), ModbusInt32Interpreter.For(Descriptor(EndianKinds.BigEndian)).Read(registers));
        Assert.Equal(0x80000001u, ModbusUInt32Interpreter.For(Descriptor(EndianKinds.BigEndian)).Read(registers));
    }

    /// <summary>
    /// 负数只是最高字节的不同取值，搬运规则与正数完全一样（<c>-2</c> = <c>FF FF FF FE</c>）：
    /// 这条同时把"有符号类型没有额外处理"和四种排布的落点钉住。
    /// </summary>
    [Theory]
    [InlineData("BigEndian", null, (ushort)0xFFFF, (ushort)0xFFFE)]       // ABCD
    [InlineData("BigEndian", "CDAB", (ushort)0xFFFE, (ushort)0xFFFF)]     // 字交换
    [InlineData("LittleEndian", "BADC", (ushort)0xFFFF, (ushort)0xFEFF)]  // 字节交换
    [InlineData("LittleEndian", null, (ushort)0xFEFF, (ushort)0xFFFF)]    // 完全小端
    public void Int32_NegativeValue_LeavesEachLayout(string endian, string? interpret, ushort first, ushort second)
    {
        var interpreter = ModbusInt32Interpreter.For(Descriptor(Parse(endian), interpret));
        var registers = new ushort[2];

        interpreter.Write(-2, registers);

        Assert.Equal(new ushort[] { first, second }, registers);
        Assert.Equal(-2, interpreter.Read(registers));
    }

    [Fact]
    public void Float_1Point5_RoundTrips()
    {
        var interpreter = ModbusFloatInterpreter.For(Descriptor(EndianKinds.BigEndian));
        var registers = new ushort[2];

        interpreter.Write(1.5f, registers);

        Assert.Equal(new ushort[] { 0x3FC0, 0x0000 }, registers);
        Assert.Equal(1.5f, interpreter.Read(registers));
    }

    /// <summary>
    /// float 的落点：位模式与 <c>uint</c> 完全相同，符号位只是最高字节的最高位。
    /// <c>1.5f</c> = <c>3F C0 00 00</c>、<c>1.25f</c> = <c>3F A0 00 00</c>（后者才能把"字节交换"与"字交换"区分开）、
    /// <c>-1.5f</c> = <c>BF C0 00 00</c>（完全小端 → 设备端字节 <c>00 00 C0 BF</c>）。
    /// </summary>
    [Theory]
    [InlineData(1.5f, "BigEndian", null, (ushort)0x3FC0, (ushort)0x0000)]
    [InlineData(1.5f, "BigEndian", "CDAB", (ushort)0x0000, (ushort)0x3FC0)]
    [InlineData(1.5f, "LittleEndian", "BADC", (ushort)0xC03F, (ushort)0x0000)]
    [InlineData(1.25f, "LittleEndian", "BADC", (ushort)0xA03F, (ushort)0x0000)]
    [InlineData(1.25f, "LittleEndian", null, (ushort)0x0000, (ushort)0xA03F)]
    [InlineData(-1.5f, "BigEndian", null, (ushort)0xBFC0, (ushort)0x0000)]
    [InlineData(-1.5f, "LittleEndian", null, (ushort)0x0000, (ushort)0xC0BF)]
    public void Float_LeavesEachLayout(float value, string endian, string? interpret, ushort first, ushort second)
    {
        var interpreter = ModbusFloatInterpreter.For(Descriptor(Parse(endian), interpret));
        var registers = new ushort[2];

        interpreter.Write(value, registers);

        Assert.Equal(new ushort[] { first, second }, registers);
        Assert.Equal(value, interpreter.Read(registers));
    }

    /// <summary>五种数值类型的宽度（每个类型自己说了算，与描述符上的 <c>tagSize</c> 无关）</summary>
    [Fact]
    public void Interpreters_ExposeTheirWidth()
    {
        Assert.Equal((4, 2), Width(ModbusUInt32Interpreter.For(Descriptor(EndianKinds.BigEndian))));
        Assert.Equal((4, 2), Width(ModbusInt32Interpreter.For(Descriptor(EndianKinds.BigEndian))));
        Assert.Equal((4, 2), Width(ModbusFloatInterpreter.For(Descriptor(EndianKinds.BigEndian))));
        Assert.Equal((8, 4), Width(ModbusUInt64Interpreter.For(Descriptor(EndianKinds.BigEndian))));
        Assert.Equal((8, 4), Width(ModbusInt64Interpreter.For(Descriptor(EndianKinds.BigEndian))));
    }

    /// <summary>记法长度校验看的是"该类型占几字节"，描述符上写错的 <c>tagSize</c> 不该改变解读宽度</summary>
    [Fact]
    public void NotationLength_ComesFromType_NotTagSize()
    {
        var descriptor = Descriptor(EndianKinds.BigEndian, "ABCD");
        descriptor.TagSize = 2;

        var interpreter = ModbusUInt32Interpreter.For(descriptor);

        Assert.Equal(2, interpreter.RegisterCount);
    }

    #endregion

    #region 64 位

    [Theory]
    [InlineData("BigEndian", null, (ushort)0x1234, (ushort)0x5678, (ushort)0x9ABC, (ushort)0xDEF0)]
    [InlineData("BigEndian", "GHEFCDAB", (ushort)0xDEF0, (ushort)0x9ABC, (ushort)0x5678, (ushort)0x1234)]
    [InlineData("LittleEndian", "BADCFEHG", (ushort)0x3412, (ushort)0x7856, (ushort)0xBC9A, (ushort)0xF0DE)]
    [InlineData("LittleEndian", null, (ushort)0xF0DE, (ushort)0xBC9A, (ushort)0x7856, (ushort)0x3412)]
    public void UInt64_ReadsAndWritesEachLayout(
        string endian,
        string? interpret,
        ushort r0,
        ushort r1,
        ushort r2,
        ushort r3)
    {
        var interpreter = ModbusUInt64Interpreter.For(Descriptor(Parse(endian), interpret));
        var registers = new[] { r0, r1, r2, r3 };

        Assert.Equal(8, interpreter.ByteCount);
        Assert.Equal(4, interpreter.RegisterCount);
        Assert.Equal(0x123456789ABCDEF0UL, interpreter.Read(registers));

        var written = new ushort[4];
        interpreter.Write(0x123456789ABCDEF0UL, written);
        Assert.Equal(registers, written);
    }

    [Fact]
    public void Int64_MinValue_RoundTrips()
    {
        var interpreter = ModbusInt64Interpreter.For(Descriptor(EndianKinds.BigEndian));
        var registers = new ushort[4];

        interpreter.Write(long.MinValue, registers);

        Assert.Equal(new ushort[] { 0x8000, 0x0000, 0x0000, 0x0000 }, registers);
        Assert.Equal(long.MinValue, interpreter.Read(registers));
    }

    [Fact]
    public void UInt64_MaxValue_RoundTrips()
    {
        var interpreter = ModbusUInt64Interpreter.For(Descriptor(EndianKinds.LittleEndian));
        var registers = new ushort[4];

        interpreter.Write(ulong.MaxValue, registers);

        // 全 1 的字节序列在哪种排布下都一样
        Assert.Equal(new ushort[] { 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF }, registers);
        Assert.Equal(ulong.MaxValue, interpreter.Read(registers));
    }

    #endregion

    #region 记法校验（构造解读器时即发生）

    [Fact]
    public void Int32_With16BitNotation_Throws()
    {
        var ex = Assert.Throws<TagsProjectXmlException>(
            () => ModbusInt32Interpreter.For(Descriptor(EndianKinds.BigEndian, "AB")));

        Assert.Contains("字节数", ex.Message);
    }

    [Fact]
    public void UInt64_With32BitNotation_Throws()
    {
        var ex = Assert.Throws<TagsProjectXmlException>(
            () => ModbusUInt64Interpreter.For(Descriptor(EndianKinds.BigEndian, "ABCD")));

        Assert.Contains("8", ex.Message);
    }

    /// <summary>报错消息里带测点名（位置上下文）</summary>
    [Fact]
    public void NotationError_CarriesTagName()
    {
        var ex = Assert.Throws<TagsProjectXmlException>(
            () => ModbusInt32Interpreter.For(Descriptor(EndianKinds.LittleEndian, "ABCD")));

        Assert.Equal("Tag(v)", ex.Location);
    }

    #endregion

    #region 排布的种类有限 + 实例复用
    /// <summary>32 位 4 种、64 位 48 种（2 种 endian × 单元全排列），且下标 0 是恒等排布</summary>
    [Fact]
    public void Packings_AreFiniteAndStartWithIdentity()
    {
        Assert.Equal(4, ModbusInterpret.EnumeratePackings(4).Length);
        Assert.Equal(48, ModbusInterpret.EnumeratePackings(8).Length);
        Assert.True(ModbusInterpret.EnumeratePackings(4)[0].IsEmpty);
        Assert.True(ModbusInterpret.EnumeratePackings(8)[0].IsEmpty);
    }

    /// <summary>枚举里没有重复、也没有遗漏（重复会让某个排布读错）</summary>
    [Fact]
    public void Packings_HaveNoDuplicates()
    {
        foreach (var byteCount in new[] { 4, 8 })
        {
            var packings = ModbusInterpret.EnumeratePackings(byteCount);
            var distinct = packings.Select(p => string.Join(",", p.ToArray())).Distinct().Count();
            Assert.Equal(packings.Length, distinct);
        }
    }

    /// <summary>显式记法必须落在自己的编号上（编号错位正是 CDAB 被读成别的排布的原因）</summary>
    [Theory]
    [InlineData("BigEndian", null, 0)]
    [InlineData("BigEndian", "CDAB", 1)]
    [InlineData("LittleEndian", "BADC", 2)]
    [InlineData("LittleEndian", null, 3)]
    [InlineData("LittleEndian", "DCBA", 3)]
    public void VariantIndex_MatchesNotation(string endian, string? interpret, int expected)
    {
        Assert.Equal(
            expected,
            ModbusInterpret.VariantIndex(Descriptor(Parse(endian), interpret), 4, "Tag(v)"));
    }

    /// <summary>同排布共用同一个实例；不同排布是不同实例</summary>
    [Fact]
    public void For_ReturnsSharedInstances()    {
        var cdab = ModbusUInt32Interpreter.For(Descriptor(EndianKinds.BigEndian, "CDAB"));
        var cdabAgain = ModbusUInt32Interpreter.For(Descriptor(EndianKinds.BigEndian, "CDAB"));
        var identity = ModbusUInt32Interpreter.For(Descriptor(EndianKinds.BigEndian));
        var dcba = ModbusUInt32Interpreter.For(Descriptor(EndianKinds.LittleEndian));

        Assert.Same(cdab, cdabAgain);
        Assert.NotSame(cdab, identity);
        Assert.NotSame(identity, dcba);

        // 恒等排布不带落位表（走直通分支）
        Assert.True(ModbusInterpret.Parse(Descriptor(EndianKinds.BigEndian), 4, "Tag(v)").IsEmpty);
    }

    /// <summary>
    /// 枚举与校验必须同源：<b>枚举里每一组排布写成记法后，都要被 <c>Parse</c> 接受并还原成同一组排布</b>，
    /// 且只在自己的 <c>endian</c> 下合法；同时 <c>VariantIndex</c> 必须给出枚举里的那个下标。<br/>
    /// 这条不变式正是"记法 → 编号"能否正确对应的前提（枚举顺序错了就会在这里红）。
    /// </summary>
    [Fact]
    public void EnumeratedPackings_RoundTripThroughNotation()
    {
        foreach (var byteCount in new[] { 4, 8 })
        {
            var packings = ModbusInterpret.EnumeratePackings(byteCount);
            for (var i = 0; i < packings.Length; i++)
            {
                var notation = ModbusInterpret.DescribeNotation(packings[i], byteCount);

                var matched = 0;
                foreach (var endian in new[] { EndianKinds.BigEndian, EndianKinds.LittleEndian })
                {
                    var descriptor = Descriptor(endian, notation);
                    if (endian == EndianKinds.BigEndian && byteCount == 8)
                    {
                        descriptor.TagSize = 8;
                    }

                    try
                    {
                        var parsed = ModbusInterpret.Parse(descriptor, byteCount, "Tag(v)");
                        Assert.True(packings[i].Span.SequenceEqual(parsed.Span));
                        Assert.Equal(i, ModbusInterpret.VariantIndex(descriptor, byteCount, "Tag(v)"));
                        matched++;
                    }
                    catch (TagsProjectXmlException)
                    {
                        // 每对字符的先后与 endian 绑定，所以只有其中一种 endian 能接受这个记法
                    }
                }

                Assert.Equal(1, matched);
            }
        }
    }

    /// <summary>
    /// 每一组排布 × 每一种类型都要能"写进去再读回来"（含 0 / 边界值 / 负数 / NaN / 无穷）：
    /// 这是"搬运是双射"的底线，排布错了或方向写反了都会在这里红。
    /// </summary>
    [Fact]
    public void EveryType_RoundTripsEveryLayout()
    {
        foreach (var byteCount in new[] { 4, 8 })
        {
            foreach (var packing in ModbusInterpret.EnumeratePackings(byteCount))
            {
                var descriptor = DescriptorFor(packing, byteCount);
                if (byteCount == 4)
                {
                    AssertRoundTrips(ModbusUInt32Interpreter.For(descriptor), UInt32Values);
                    AssertRoundTrips(ModbusInt32Interpreter.For(descriptor), Int32Values);
                    AssertRoundTrips(ModbusFloatInterpreter.For(descriptor), FloatValues);
                }
                else
                {
                    AssertRoundTrips(ModbusUInt64Interpreter.For(descriptor), UInt64Values);
                    AssertRoundTrips(ModbusInt64Interpreter.For(descriptor), Int64Values);
                }
            }
        }
    }

    /// <summary>实例是静态池里的共享对象，多线程首次取用不该各造一份（无锁但不重复造）</summary>
    [Fact]
    public void For_IsSharedAcrossThreads()
    {
        var expected = ModbusUInt32Interpreter.For(Descriptor(EndianKinds.BigEndian, "CDAB"));
        var mismatches = 0;

        Parallel.For(0, 64, _ =>
        {
            var actual = ModbusUInt32Interpreter.For(Descriptor(EndianKinds.BigEndian, "CDAB"));
            if (!ReferenceEquals(expected, actual))
            {
                Interlocked.Increment(ref mismatches);
            }
        });

        Assert.Equal(0, mismatches);
    }

    #endregion
}
