using StdUnit.Tags;
using StdUnit.Tags.ModbusTcp;
using System;
using Xunit;

namespace StdUnit.Tags.Tests.ModbusTags;

/// <summary>
/// 测试 Modbus 字空间（寄存器）组合子的字节序解读。<br/>
/// 模型：缓存 = 寄存器数组（每元素 = NModbus 解析后的寄存器值）。<br/>
/// <c>endian</c> 统一描述<b>每个 16 位单元内部两个字节</b>的顺序（BigEndian 直取、LittleEndian 交换两字节，
/// 与直接测点、与 S7 同名同义）；32/64 位的<b>寄存器之间的顺序</b>由 <c>interpret</c> 描述
/// （见 <c>ModbusInterpret</c> / <c>ModbusValueInterpreter&lt;T&gt;</c>；不写 interpret 时 BigEndian = 完全大端、LittleEndian = 完全小端）；
/// 字节取高/低字节；位取第 nth 位（0~15）。
/// </summary>
public class ModbusRegisterCbntorTests
{
    private static ModbusRegisterTagCbnt CreateCbnt(int cacheSizeBytes)
    {
        var cbnt = new ModbusRegisterTagCbnt(new TagCbntDescriptor { Name = "g", StartAddress = "40001" });
        cbnt.ResizeCache(cacheSizeBytes);
        return cbnt;
    }

    private static TagDescriptor CreateDescriptor(
        string name,
        string kind,
        int tagSize,
        EndianKinds endian,
        string? interpret = null)
    {
        var descriptor = new TagDescriptor
        {
            TagName = name,
            RawAddress = "40001",
            TagKind = kind,
            TagSize = tagSize,
            EndianKind = endian,
        };

        if (interpret is not null)
        {
            descriptor.Extras["interpret"] = new System.Xml.Linq.XAttribute("interpret", interpret);
        }

        return descriptor;
    }

    private static EndianKinds Parse(string endian) =>
        endian == "BigEndian" ? EndianKinds.BigEndian : EndianKinds.LittleEndian;

    #region 16 位（单寄存器，EndianKind = 寄存器内两个字节的顺序）

    [Fact]
    public void UInt16_BigEndian_TakesRegisterAsIs()
    {
        var cbnt = CreateCbnt(2);
        cbnt.Cache.Span[0] = 0x1234;
        var tag = new ModbusRegisterUInt16Cbntor(CreateDescriptor("u16", BuiltinTagKinds.UINT16, 2, EndianKinds.BigEndian), cbnt, 0, false);

        Assert.Equal(0x1234, (ushort)tag.Value!);
    }

    [Fact]
    public void UInt16_LittleEndian_SwapsBytesInsideRegister()
    {
        // 设备把 0x1234 按"低字节在前"存放：设备端字节 [34,12] → NModbus 按协议解析回 0x3412
        var cbnt = CreateCbnt(2);
        cbnt.Cache.Span[0] = 0x3412;
        var tag = new ModbusRegisterUInt16Cbntor(CreateDescriptor("u16", BuiltinTagKinds.UINT16, 2, EndianKinds.LittleEndian), cbnt, 0, false);

        Assert.Equal(0x1234, (ushort)tag.Value!);
    }

    [Fact]
    public void Int16_BigEndian_TakesRegisterAsIs()
    {
        var cbnt = CreateCbnt(2);
        cbnt.Cache.Span[0] = unchecked((ushort)(-2)); // 0xFFFE
        var tag = new ModbusRegisterInt16Cbntor(CreateDescriptor("i16", BuiltinTagKinds.INT16, 2, EndianKinds.BigEndian), cbnt, 0, false);

        Assert.Equal(-2, (short)tag.Value!);
    }

    [Fact]
    public void Int16_LittleEndian_SwapsBytesInsideRegister()
    {
        // 设备把 0xFFFE 按"低字节在前"存放：设备端字节 [FE,FF] → NModbus 解析回 0xFEFF
        var cbnt = CreateCbnt(2);
        cbnt.Cache.Span[0] = 0xFEFF;
        var tag = new ModbusRegisterInt16Cbntor(CreateDescriptor("i16", BuiltinTagKinds.INT16, 2, EndianKinds.LittleEndian), cbnt, 0, false);

        Assert.Equal(-2, (short)tag.Value!);
    }

    [Fact]
    public void UInt16_BigEndian_WriteBack_KeepsRegisterAsIs()
    {
        var cbnt = CreateCbnt(2);
        var tag = new ModbusRegisterUInt16Cbntor(CreateDescriptor("u16", BuiltinTagKinds.UINT16, 2, EndianKinds.BigEndian), cbnt, 0, false);

        tag.Value = (ushort)0xABCD;

        Assert.Equal(0xABCD, cbnt.Cache.Span[0]);
    }

    [Fact]
    public void UInt16_LittleEndian_WriteBack_SwapsBytesInsideRegister()
    {
        var cbnt = CreateCbnt(2);
        var tag = new ModbusRegisterUInt16Cbntor(CreateDescriptor("u16", BuiltinTagKinds.UINT16, 2, EndianKinds.LittleEndian), cbnt, 0, false);

        tag.Value = (ushort)0xABCD;

        Assert.Equal(0xCDAB, cbnt.Cache.Span[0]);
    }

    #endregion

    #region 32 位（2 寄存器：endian 管寄存器内部，interpret 管寄存器之间）

    /// <summary>完全大端（标准设备）：设备端字节 <c>12 34 56 78</c></summary>
    [Fact]
    public void Int32_Abcd_HighRegisterFirst()
    {
        var cbnt = CreateCbnt(4);
        cbnt.Cache.Span[0] = 0x1234;
        cbnt.Cache.Span[1] = 0x5678;
        var tag = new ModbusRegisterInt32Cbntor(CreateDescriptor("i32", BuiltinTagKinds.INT32, 4, EndianKinds.BigEndian), cbnt, 0, false);

        Assert.Equal(0x12345678, (int)tag.Value!);
    }

    /// <summary>四种排布（endian + interpret）都读到同一个物理值 0x12345678</summary>
    [Theory]
    [InlineData("BigEndian", null, 0x1234, 0x5678)]        // ABCD：设备端字节 12 34 56 78
    [InlineData("BigEndian", "CDAB", 0x5678, 0x1234)]      // CDAB：字交换
    [InlineData("LittleEndian", "BADC", 0x3412, 0x7856)]   // BADC：字节交换
    [InlineData("LittleEndian", null, 0x7856, 0x3412)]     // DCBA：完全小端
    public void Int32_AllLayouts_RecoverSameValue(string endian, string? interpret, ushort first, ushort second)
    {
        var cbnt = CreateCbnt(4);
        cbnt.Cache.Span[0] = first;
        cbnt.Cache.Span[1] = second;
        var tag = new ModbusRegisterInt32Cbntor(
            CreateDescriptor("i32", BuiltinTagKinds.INT32, 4, Parse(endian), interpret), cbnt, 0, false);

        Assert.Equal(0x12345678, (int)tag.Value!);
    }

    /// <summary>写回时也按同一排布落地：设备里那 4 个字节的顺序与 interpret 记法一致</summary>
    [Theory]
    [InlineData("BigEndian", null, 0x1234, 0x5678)]
    [InlineData("BigEndian", "CDAB", 0x5678, 0x1234)]
    [InlineData("LittleEndian", "BADC", 0x3412, 0x7856)]
    [InlineData("LittleEndian", null, 0x7856, 0x3412)]
    public void Int32_AllLayouts_WriteBack(string endian, string? interpret, ushort first, ushort second)
    {
        var cbnt = CreateCbnt(4);
        var tag = new ModbusRegisterInt32Cbntor(
            CreateDescriptor("i32", BuiltinTagKinds.INT32, 4, Parse(endian), interpret), cbnt, 0, false);

        tag.Value = 0x12345678;

        Assert.Equal(first, cbnt.Cache.Span[0]);
        Assert.Equal(second, cbnt.Cache.Span[1]);
    }

    [Fact]
    public void Int32_BigEndian_WriteBack()
    {
        var cbnt = CreateCbnt(4);
        var tag = new ModbusRegisterInt32Cbntor(CreateDescriptor("i32", BuiltinTagKinds.INT32, 4, EndianKinds.BigEndian), cbnt, 0, false);

        tag.Value = 0x11223344;

        Assert.Equal(0x1122, cbnt.Cache.Span[0]);
        Assert.Equal(0x3344, cbnt.Cache.Span[1]);
    }

    [Fact]
    public void UInt32_BigEndian_HighRegisterFirst()
    {
        var cbnt = CreateCbnt(4);
        cbnt.Cache.Span[0] = 0x8000;
        cbnt.Cache.Span[1] = 0x0001;
        var tag = new ModbusRegisterUInt32Cbntor(CreateDescriptor("u32", BuiltinTagKinds.UINT32, 4, EndianKinds.BigEndian), cbnt, 0, false);

        Assert.Equal(0x80000001u, (uint)tag.Value!);
    }

    [Fact]
    public void Float_BigEndian_HighRegisterFirst()
    {
        var cbnt = CreateCbnt(4);
        cbnt.Cache.Span[0] = 0x3F80; // 1.0f 高 16 位
        cbnt.Cache.Span[1] = 0x0000; // 1.0f 低 16 位
        var tag = new ModbusRegisterFloatCbntor(CreateDescriptor("f32", BuiltinTagKinds.FLOAT, 4, EndianKinds.BigEndian), cbnt, 0, false);

        Assert.Equal(1.0f, (float)tag.Value!);
    }

    /// <summary>1.0f = 0x3F800000，字交换后寄存器是 <c>[00 00][3F 80]</c></summary>
    [Fact]
    public void Float_TaggedCdab_LowRegisterFirst()
    {
        var cbnt = CreateCbnt(4);
        cbnt.Cache.Span[0] = 0x0000;
        cbnt.Cache.Span[1] = 0x3F80;
        var tag = new ModbusRegisterFloatCbntor(CreateDescriptor("f32", BuiltinTagKinds.FLOAT, 4, EndianKinds.BigEndian, "CDAB"), cbnt, 0, false);

        Assert.Equal(1.0f, (float)tag.Value!);
    }

    [Fact]
    public void Float_LittleEndian_FullyReversed()
    {
        var cbnt = CreateCbnt(4);
        cbnt.Cache.Span[0] = 0x0000;
        cbnt.Cache.Span[1] = 0x803F;
        var tag = new ModbusRegisterFloatCbntor(CreateDescriptor("f32", BuiltinTagKinds.FLOAT, 4, EndianKinds.LittleEndian), cbnt, 0, false);

        Assert.Equal(1.0f, (float)tag.Value!);
    }

    #endregion

    #region 64 位（4 寄存器：记法为 8 个字符）

    [Fact]
    public void Int64_BigEndian_HighRegisterFirst()
    {
        var cbnt = CreateCbnt(8);
        cbnt.Cache.Span[0] = 0x1234;
        cbnt.Cache.Span[1] = 0x5678;
        cbnt.Cache.Span[2] = 0x9ABC;
        cbnt.Cache.Span[3] = 0xDEF0;
        var tag = new ModbusRegisterInt64Cbntor(CreateDescriptor("i64", BuiltinTagKinds.INT64, 8, EndianKinds.BigEndian), cbnt, 0, false);

        Assert.Equal(0x123456789ABCDEF0L, (long)tag.Value!);
    }

    /// <summary>
    /// 64 位同样是四种排布，记法用 8 个字符（<c>A</c> = 最高字节）：<c>GHEFCDAB</c> = 按 16 位单元整体倒着排
    /// （等价于 32 位的 <c>CDAB</c>），<c>BADCFEHG</c> = 每个 16 位单元内部换字节（等价于 <c>BADC</c>）、
    /// <c>HGFEDCBA</c> = 完全小端。
    /// </summary>
    [Theory]
    [InlineData("BigEndian", null, 0x1234, 0x5678, 0x9ABC, 0xDEF0)]
    [InlineData("BigEndian", "GHEFCDAB", 0xDEF0, 0x9ABC, 0x5678, 0x1234)]
    [InlineData("BigEndian", "CDABGHEF", 0x5678, 0x1234, 0xDEF0, 0x9ABC)]
    [InlineData("LittleEndian", "BADCFEHG", 0x3412, 0x7856, 0xBC9A, 0xF0DE)]
    [InlineData("LittleEndian", null, 0xF0DE, 0xBC9A, 0x7856, 0x3412)]
    public void Int64_AllLayouts_RecoverSameValue(
        string endian,
        string? interpret,
        ushort r0,
        ushort r1,
        ushort r2,
        ushort r3)
    {
        var cbnt = CreateCbnt(8);
        cbnt.Cache.Span[0] = r0;
        cbnt.Cache.Span[1] = r1;
        cbnt.Cache.Span[2] = r2;
        cbnt.Cache.Span[3] = r3;
        var tag = new ModbusRegisterInt64Cbntor(
            CreateDescriptor("i64", BuiltinTagKinds.INT64, 8, Parse(endian), interpret), cbnt, 0, false);

        Assert.Equal(0x123456789ABCDEF0L, (long)tag.Value!);
    }

    [Fact]
    public void UInt64_BigEndian_WriteBack()
    {
        var cbnt = CreateCbnt(8);
        var tag = new ModbusRegisterUInt64Cbntor(CreateDescriptor("u64", BuiltinTagKinds.UINT64, 8, EndianKinds.BigEndian), cbnt, 0, false);

        tag.Value = 0x0102030405060708UL;

        Assert.Equal(0x0102, cbnt.Cache.Span[0]);
        Assert.Equal(0x0304, cbnt.Cache.Span[1]);
        Assert.Equal(0x0506, cbnt.Cache.Span[2]);
        Assert.Equal(0x0708, cbnt.Cache.Span[3]);
    }

    /// <summary>完全小端写回：4 个寄存器整体倒着放</summary>
    [Fact]
    public void UInt64_LittleEndian_WriteBack_FullyReversed()
    {
        var cbnt = CreateCbnt(8);
        var tag = new ModbusRegisterUInt64Cbntor(CreateDescriptor("u64", BuiltinTagKinds.UINT64, 8, EndianKinds.LittleEndian), cbnt, 0, false);

        tag.Value = 0x0102030405060708UL;

        Assert.Equal(0x0807, cbnt.Cache.Span[0]);
        Assert.Equal(0x0605, cbnt.Cache.Span[1]);
        Assert.Equal(0x0403, cbnt.Cache.Span[2]);
        Assert.Equal(0x0201, cbnt.Cache.Span[3]);
    }

    #endregion

    #region 字节（寄存器内高/低字节）

    [Fact]
    public void Byte_BigEndian_TakesHighByte()
    {
        var cbnt = CreateCbnt(2);
        cbnt.Cache.Span[0] = 0xABCD;
        var tag = new ModbusRegisterByteCbntor(CreateDescriptor("b", BuiltinTagKinds.BYTE, 2, EndianKinds.BigEndian), cbnt, 0, false);

        Assert.Equal(0xAB, (byte)tag.Value!);
    }

    [Fact]
    public void Byte_LittleEndian_TakesLowByte()
    {
        var cbnt = CreateCbnt(2);
        cbnt.Cache.Span[0] = 0xABCD;
        var tag = new ModbusRegisterByteCbntor(CreateDescriptor("b", BuiltinTagKinds.BYTE, 2, EndianKinds.LittleEndian), cbnt, 0, false);

        Assert.Equal(0xCD, (byte)tag.Value!);
    }

    [Fact]
    public void Byte_BigEndian_WriteHighByte_KeepsLowByte()
    {
        var cbnt = CreateCbnt(2);
        cbnt.Cache.Span[0] = 0xABCD;
        var tag = new ModbusRegisterByteCbntor(CreateDescriptor("b", BuiltinTagKinds.BYTE, 2, EndianKinds.BigEndian), cbnt, 0, false);

        tag.Value = (byte)0x12;

        Assert.Equal(0x12CD, cbnt.Cache.Span[0]);
    }

    #endregion

    #region 位（寄存器内第 nth 位，0~15，与字节序无关）

    [Fact]
    public void Bit_ReadsNthBit()
    {
        var cbnt = CreateCbnt(2);
        cbnt.Cache.Span[0] = 0x0008; // bit3
        var tagTrue = new ModbusRegisterBitCbntor(CreateDescriptor("b3", BuiltinTagKinds.BIT, 2, EndianKinds.BigEndian), cbnt, 0, false, 3);
        var tagFalse = new ModbusRegisterBitCbntor(CreateDescriptor("b2", BuiltinTagKinds.BIT, 2, EndianKinds.BigEndian), cbnt, 0, false, 2);

        Assert.True((bool)tagTrue.Value!);
        Assert.False((bool)tagFalse.Value!);
    }

    [Fact]
    public void Bit_HighNthBit_ReadsHighByteBit()
    {
        var cbnt = CreateCbnt(2);
        cbnt.Cache.Span[0] = 0x8000; // bit15
        var tag = new ModbusRegisterBitCbntor(CreateDescriptor("b15", BuiltinTagKinds.BIT, 2, EndianKinds.BigEndian), cbnt, 0, false, 15);

        Assert.True((bool)tag.Value!);
    }

    [Fact]
    public void Bit_SetAndClear_ModifiesRegister()
    {
        var cbnt = CreateCbnt(2);
        cbnt.Cache.Span[0] = 0x0000;
        var tag = new ModbusRegisterBitCbntor(CreateDescriptor("b0", BuiltinTagKinds.BIT, 2, EndianKinds.BigEndian), cbnt, 0, false, 0);

        tag.Value = true;
        Assert.Equal(0x0001, cbnt.Cache.Span[0]);

        tag.Value = false;
        Assert.Equal(0x0000, cbnt.Cache.Span[0]);
    }

    #endregion

    #region 只读（输入寄存器）

    [Fact]
    public void ReadOnly_Cbntor_WriteThrows()
    {
        var cbnt = CreateCbnt(2);
        var tag = new ModbusRegisterUInt16Cbntor(CreateDescriptor("u16", BuiltinTagKinds.UINT16, 2, EndianKinds.BigEndian), cbnt, 0, isReadOnly: true);

        Assert.Throws<NotSupportedException>(() => tag.Value = (ushort)5);
    }

    [Fact]
    public void ReadOnly_Cbntor_ReadStillWorks()
    {
        var cbnt = CreateCbnt(2);
        cbnt.Cache.Span[0] = 0x1234;
        var tag = new ModbusRegisterUInt16Cbntor(CreateDescriptor("u16", BuiltinTagKinds.UINT16, 2, EndianKinds.BigEndian), cbnt, 0, isReadOnly: true);

        Assert.Equal(0x1234, (ushort)tag.Value!);
    }

    #endregion
}
