using StdUnit.Tags;
using StdUnit.Tags.ModbusTcp;
using System;
using Xunit;

namespace StdUnit.Tags.Tests.ModbusTags;

/// <summary>
/// 测试 Modbus 字空间（寄存器）组合子的字节序解读。<br/>
/// 模型：缓存 = 寄存器数组（每元素 = NModbus 解析后的寄存器值），
/// 16 位直接取值；32/64 位由 EndianKind 描述寄存器顺序（BigEndian = 高寄存器在前）；字节取高/低字节；位取第 nth 位（0~15）。
/// </summary>
public class ModbusRegisterCbntorTests
{
    private static ModbusRegisterTagCbnt CreateCbnt(int cacheSizeBytes)
    {
        var cbnt = new ModbusRegisterTagCbnt(new TagCbntDescriptor { Name = "g", StartAddress = "40001" });
        cbnt.ResizeCache(cacheSizeBytes);
        return cbnt;
    }

    private static TagDescriptor CreateDescriptor(string name, string kind, int tagSize, EndianKinds endian) => new()
    {
        TagName = name,
        RawAddress = "40001",
        TagKind = kind,
        TagSize = tagSize,
        EndianKind = endian,
    };

    #region 16 位（单寄存器，直接取值，无字节序分支）

    [Theory]
    [InlineData(EndianKinds.BigEndian)]
    [InlineData(EndianKinds.LittleEndian)]
    public void UInt16_ReadsRegisterValue_Directly(EndianKinds endian)
    {
        var cbnt = CreateCbnt(2);
        cbnt.Cache.Span[0] = 0x1234;
        var tag = new ModbusRegisterUInt16Cbntor(CreateDescriptor("u16", BuiltinTagKinds.UINT16, 2, endian), cbnt, 0, false);

        Assert.Equal(0x1234, (ushort)tag.Value!);
    }

    [Theory]
    [InlineData(EndianKinds.BigEndian)]
    [InlineData(EndianKinds.LittleEndian)]
    public void Int16_ReadsRegisterValue_Directly(EndianKinds endian)
    {
        var cbnt = CreateCbnt(2);
        cbnt.Cache.Span[0] = unchecked((ushort)(-2)); // 0xFFFE
        var tag = new ModbusRegisterInt16Cbntor(CreateDescriptor("i16", BuiltinTagKinds.INT16, 2, endian), cbnt, 0, false);

        Assert.Equal(-2, (short)tag.Value!);
    }

    [Theory]
    [InlineData(EndianKinds.BigEndian)]
    [InlineData(EndianKinds.LittleEndian)]
    public void UInt16_WriteBack(EndianKinds endian)
    {
        var cbnt = CreateCbnt(2);
        var tag = new ModbusRegisterUInt16Cbntor(CreateDescriptor("u16", BuiltinTagKinds.UINT16, 2, endian), cbnt, 0, false);

        tag.Value = (ushort)0xABCD;

        Assert.Equal(0xABCD, cbnt.Cache.Span[0]);
    }

    #endregion

    #region 32 位（2 寄存器，word order）

    [Fact]
    public void Int32_BigEndian_HighRegisterFirst()
    {
        var cbnt = CreateCbnt(4);
        cbnt.Cache.Span[0] = 0x1234;
        cbnt.Cache.Span[1] = 0x5678;
        var tag = new ModbusRegisterInt32Cbntor(CreateDescriptor("i32", BuiltinTagKinds.INT32, 4, EndianKinds.BigEndian), cbnt, 0, false);

        Assert.Equal(0x12345678, (int)tag.Value!);
    }

    [Fact]
    public void Int32_LittleEndian_LowRegisterFirst()
    {
        var cbnt = CreateCbnt(4);
        cbnt.Cache.Span[0] = 0x1234;
        cbnt.Cache.Span[1] = 0x5678;
        var tag = new ModbusRegisterInt32Cbntor(CreateDescriptor("i32", BuiltinTagKinds.INT32, 4, EndianKinds.LittleEndian), cbnt, 0, false);

        Assert.Equal(0x56781234, (int)tag.Value!);
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

    [Fact]
    public void Float_LittleEndian_LowRegisterFirst()
    {
        var cbnt = CreateCbnt(4);
        cbnt.Cache.Span[0] = 0x0000;
        cbnt.Cache.Span[1] = 0x3F80;
        var tag = new ModbusRegisterFloatCbntor(CreateDescriptor("f32", BuiltinTagKinds.FLOAT, 4, EndianKinds.LittleEndian), cbnt, 0, false);

        Assert.Equal(1.0f, (float)tag.Value!);
    }

    #endregion

    #region 64 位（4 寄存器，word order）

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

    [Fact]
    public void Int64_LittleEndian_LowRegisterFirst()
    {
        var cbnt = CreateCbnt(8);
        cbnt.Cache.Span[0] = 0x1234;
        cbnt.Cache.Span[1] = 0x5678;
        cbnt.Cache.Span[2] = 0x9ABC;
        cbnt.Cache.Span[3] = 0xDEF0;
        var tag = new ModbusRegisterInt64Cbntor(CreateDescriptor("i64", BuiltinTagKinds.INT64, 8, EndianKinds.LittleEndian), cbnt, 0, false);

        Assert.Equal(unchecked((long)0xDEF09ABC56781234UL), (long)tag.Value!);
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
