using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using StdUnit.Tags.SimpleFiles;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
// net472 没有 File.ReadAllTextAsync / WriteAllTextAsync（.NET Core 2.0+）。
// 本文件大量用到它们，故把 File 别名到测试垫片：两个目标框架下写法一致，
// 具体差异（#if）收在 StdUnit.Tags.Tests.Compat.TestFileCompat 里一处。
using File = StdUnit.Tags.Tests.Compat.TestFileCompat;

namespace StdUnit.Tags.Tests.SimpleFilesTags;

public class DirectTagTests : IDisposable
{
    private readonly string _tempDir;
    private readonly SimpleFilesTagChannel _channel;
    private readonly TagGrp _grp;

    public DirectTagTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"SimpleFilesTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);
        var settings = new SimpleFilesSettings(_tempDir);
        var chdescriptor = new SimpleFilesTagChannelDescriptor()
        {
            Name = "test-channel",
            BaseDir = settings.BaseDir,
        };
        var logger = NullLogger<SimpleFilesTagChannel>.Instance;
        _channel = new SimpleFilesTagChannel(chdescriptor, logger);
        _grp = new TagGrp(new TagGrpDescriptor { Name = "test-grp", IsEntry = true }, _channel);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    private ITag CreateTag(TagDescriptor descriptor)
    {
        var factory = new SimpleFilesDirectTagFactory(_grp.IntoTagContainer());
        return factory.Create(descriptor, _channel);
    }

    private static TagDescriptor MakeDescriptor(string tagName, string rawAddress, string tagKind, int tagSize = 0)
        => new()
        {
            TagName = tagName,
            RawAddress = rawAddress,
            TagKind = tagKind,
            TagSize = tagSize,
        };

    #region Bit (BIT)

    [Fact]
    public async Task Bit_ReadAsync_FromFile_True_ReturnsTrue()
    {
        var fileName = "bit_true.txt";
        await File.WriteAllTextAsync(Path.Combine(_tempDir, fileName), "true");
        var tag = CreateTag(MakeDescriptor("b", fileName, BuiltinTagKinds.BIT));

        await tag.ReadAsync(CancellationToken.None);

        Assert.True((bool)tag.Value!);
    }

    [Fact]
    public async Task Bit_ReadAsync_FromFile_False_ReturnsFalse()
    {
        var fileName = "bit_false.txt";
        await File.WriteAllTextAsync(Path.Combine(_tempDir, fileName), "false");
        var tag = CreateTag(MakeDescriptor("b", fileName, BuiltinTagKinds.BIT));

        await tag.ReadAsync(CancellationToken.None);

        Assert.False((bool)tag.Value!);
    }

    [Fact]
    public async Task Bit_WriteAsync_WritesTrue()
    {
        var fileName = "bit_out.txt";
        var fullPath = Path.Combine(_tempDir, fileName);
        await File.WriteAllTextAsync(fullPath, "false");
        var tag = CreateTag(MakeDescriptor("b", fileName, BuiltinTagKinds.BIT));
        tag.Value = true;

        await tag.WriteAsync(CancellationToken.None);

        var content = await File.ReadAllTextAsync(fullPath);
        Assert.Equal("True", content);
    }

    [Fact]
    public async Task Bit_WriteAsync_WritesFalse()
    {
        var fileName = "bit_out_f.txt";
        var fullPath = Path.Combine(_tempDir, fileName);
        await File.WriteAllTextAsync(fullPath, "true");
        var tag = CreateTag(MakeDescriptor("b", fileName, BuiltinTagKinds.BIT));
        tag.Value = false;

        await tag.WriteAsync(CancellationToken.None);

        var content = await File.ReadAllTextAsync(fullPath);
        Assert.Equal("False", content);
    }

    [Fact]
    public async Task Bit_ReadAsync_InvalidContent_Throws()
    {
        var fileName = "bit_bad.txt";
        await File.WriteAllTextAsync(Path.Combine(_tempDir, fileName), "not-bool");
        var tag = CreateTag(MakeDescriptor("b", fileName, BuiltinTagKinds.BIT));

        var ex = await Assert.ThrowsAsync<InvalidDataException>(() => tag.ReadAsync(CancellationToken.None));
        // 解析失败必须能定位到测点与文件（子类只能拿到文本，路径由 TagBase 补上）
        Assert.Contains("Tag(b)", ex.Message);
        Assert.Contains(fileName, ex.Message);
        Assert.Contains("not-bool", ex.Message);
        Assert.IsType<InvalidDataException>(ex.InnerException);
    }

    #endregion

    #region Byte (BYTE)

    [Fact]
    public async Task Byte_ReadAsync_FromFile_ReturnsParsedValue()
    {
        var fileName = "byte_val.txt";
        await File.WriteAllTextAsync(Path.Combine(_tempDir, fileName), "200");
        var tag = CreateTag(MakeDescriptor("by", fileName, BuiltinTagKinds.BYTE));

        await tag.ReadAsync(CancellationToken.None);

        Assert.Equal((byte)200, tag.Value);
    }

    [Fact]
    public async Task Byte_WriteAsync_WritesCorrectContent()
    {
        var fileName = "byte_out.txt";
        var fullPath = Path.Combine(_tempDir, fileName);
        await File.WriteAllTextAsync(fullPath, "0");
        var tag = CreateTag(MakeDescriptor("by", fileName, BuiltinTagKinds.BYTE));
        tag.Value = (byte)255;

        await tag.WriteAsync(CancellationToken.None);

        var content = await File.ReadAllTextAsync(fullPath);
        Assert.Equal("255", content);
    }

    [Fact]
    public async Task Byte_ReadAsync_InvalidContent_Throws()
    {
        var fileName = "byte_bad.txt";
        await File.WriteAllTextAsync(Path.Combine(_tempDir, fileName), "256");
        var tag = CreateTag(MakeDescriptor("by", fileName, BuiltinTagKinds.BYTE));

        await Assert.ThrowsAsync<InvalidDataException>(() => tag.ReadAsync(CancellationToken.None));
    }

    #endregion

    #region Short (INT16)

    [Fact]
    public async Task Short_ReadAsync_FromFile_ReturnsParsedValue()
    {
        var fileName = "short_val.txt";
        await File.WriteAllTextAsync(Path.Combine(_tempDir, fileName), "-1234");
        var tag = CreateTag(MakeDescriptor("s", fileName, BuiltinTagKinds.INT16));

        await tag.ReadAsync(CancellationToken.None);

        Assert.Equal((short)-1234, tag.Value);
    }

    [Fact]
    public async Task Short_WriteAsync_WritesCorrectContent()
    {
        var fileName = "short_out.txt";
        var fullPath = Path.Combine(_tempDir, fileName);
        await File.WriteAllTextAsync(fullPath, "0");
        var tag = CreateTag(MakeDescriptor("s", fileName, BuiltinTagKinds.INT16));
        tag.Value = (short)32767;

        await tag.WriteAsync(CancellationToken.None);

        var content = await File.ReadAllTextAsync(fullPath);
        Assert.Equal("32767", content);
    }

    [Fact]
    public async Task Short_ReadAsync_InvalidContent_Throws()
    {
        var fileName = "short_bad.txt";
        await File.WriteAllTextAsync(Path.Combine(_tempDir, fileName), "not-a-number");
        var tag = CreateTag(MakeDescriptor("s", fileName, BuiltinTagKinds.INT16));

        var ex = await Assert.ThrowsAsync<InvalidDataException>(() => tag.ReadAsync(CancellationToken.None));
        Assert.Contains("Tag(s)", ex.Message);
        Assert.Contains(fileName, ex.Message);
    }

    #endregion

    #region UShort (UINT16)

    [Fact]
    public async Task UShort_ReadAsync_FromFile_ReturnsParsedValue()
    {
        var fileName = "ushort_val.txt";
        await File.WriteAllTextAsync(Path.Combine(_tempDir, fileName), "65000");
        var tag = CreateTag(MakeDescriptor("us", fileName, BuiltinTagKinds.UINT16));

        await tag.ReadAsync(CancellationToken.None);

        Assert.Equal((ushort)65000, tag.Value);
    }

    [Fact]
    public async Task UShort_WriteAsync_WritesCorrectContent()
    {
        var fileName = "ushort_out.txt";
        var fullPath = Path.Combine(_tempDir, fileName);
        await File.WriteAllTextAsync(fullPath, "0");
        var tag = CreateTag(MakeDescriptor("us", fileName, BuiltinTagKinds.UINT16));
        tag.Value = (ushort)65535;

        await tag.WriteAsync(CancellationToken.None);

        var content = await File.ReadAllTextAsync(fullPath);
        Assert.Equal("65535", content);
    }

    [Fact]
    public async Task UShort_ReadAsync_InvalidContent_Throws()
    {
        var fileName = "ushort_bad.txt";
        await File.WriteAllTextAsync(Path.Combine(_tempDir, fileName), "abc");
        var tag = CreateTag(MakeDescriptor("us", fileName, BuiltinTagKinds.UINT16));

        await Assert.ThrowsAsync<InvalidDataException>(() => tag.ReadAsync(CancellationToken.None));
    }

    #endregion

    #region Int (INT32)

    [Fact]
    public async Task Int_ReadAsync_FromFile_ReturnsParsedValue()
    {
        var fileName = "int_val.txt";
        await File.WriteAllTextAsync(Path.Combine(_tempDir, fileName), "-2000000");
        var tag = CreateTag(MakeDescriptor("i", fileName, BuiltinTagKinds.INT32));

        await tag.ReadAsync(CancellationToken.None);

        Assert.Equal(-2000000, tag.Value);
    }

    [Fact]
    public async Task Int_WriteAsync_WritesCorrectContent()
    {
        var fileName = "int_out.txt";
        var fullPath = Path.Combine(_tempDir, fileName);
        await File.WriteAllTextAsync(fullPath, "0");
        var tag = CreateTag(MakeDescriptor("i", fileName, BuiltinTagKinds.INT32));
        tag.Value = 2147483647;

        await tag.WriteAsync(CancellationToken.None);

        var content = await File.ReadAllTextAsync(fullPath);
        Assert.Equal("2147483647", content);
    }

    [Fact]
    public async Task Int_ReadAsync_InvalidContent_Throws()
    {
        var fileName = "int_bad.txt";
        await File.WriteAllTextAsync(Path.Combine(_tempDir, fileName), "not-int");
        var tag = CreateTag(MakeDescriptor("i", fileName, BuiltinTagKinds.INT32));

        await Assert.ThrowsAsync<InvalidDataException>(() => tag.ReadAsync(CancellationToken.None));
    }

    #endregion

    #region UInt (UINT32)

    [Fact]
    public async Task UInt_ReadAsync_FromFile_ReturnsParsedValue()
    {
        var fileName = "uint_val.txt";
        await File.WriteAllTextAsync(Path.Combine(_tempDir, fileName), "4000000000");
        var tag = CreateTag(MakeDescriptor("ui", fileName, BuiltinTagKinds.UINT32));

        await tag.ReadAsync(CancellationToken.None);

        Assert.Equal(4000000000u, tag.Value);
    }

    [Fact]
    public async Task UInt_WriteAsync_WritesCorrectContent()
    {
        var fileName = "uint_out.txt";
        var fullPath = Path.Combine(_tempDir, fileName);
        await File.WriteAllTextAsync(fullPath, "0");
        var tag = CreateTag(MakeDescriptor("ui", fileName, BuiltinTagKinds.UINT32));
        tag.Value = 4294967295u;

        await tag.WriteAsync(CancellationToken.None);

        var content = await File.ReadAllTextAsync(fullPath);
        Assert.Equal("4294967295", content);
    }

    [Fact]
    public async Task UInt_ReadAsync_InvalidContent_Throws()
    {
        var fileName = "uint_bad.txt";
        await File.WriteAllTextAsync(Path.Combine(_tempDir, fileName), "not-uint");
        var tag = CreateTag(MakeDescriptor("ui", fileName, BuiltinTagKinds.UINT32));

        await Assert.ThrowsAsync<InvalidDataException>(() => tag.ReadAsync(CancellationToken.None));
    }

    #endregion

    #region Float (FLOAT)

    [Fact]
    public async Task Float_ReadAsync_FromFile_ReturnsParsedValue()
    {
        var fileName = "float_val.txt";
        await File.WriteAllTextAsync(Path.Combine(_tempDir, fileName), "3.14");
        var tag = CreateTag(MakeDescriptor("f", fileName, BuiltinTagKinds.FLOAT));

        await tag.ReadAsync(CancellationToken.None);

        Assert.Equal(3.14f, tag.Value);
    }

    [Fact]
    public async Task Float_WriteAsync_WritesCorrectContent()
    {
        var fileName = "float_out.txt";
        var fullPath = Path.Combine(_tempDir, fileName);
        await File.WriteAllTextAsync(fullPath, "0");
        var tag = CreateTag(MakeDescriptor("f", fileName, BuiltinTagKinds.FLOAT));
        tag.Value = -2.5f;

        await tag.WriteAsync(CancellationToken.None);

        var content = await File.ReadAllTextAsync(fullPath);
        Assert.Equal("-2.5", content);
    }

    [Fact]
    public async Task Float_ReadAsync_InvalidContent_Throws()
    {
        var fileName = "float_bad.txt";
        await File.WriteAllTextAsync(Path.Combine(_tempDir, fileName), "not-float");
        var tag = CreateTag(MakeDescriptor("f", fileName, BuiltinTagKinds.FLOAT));

        await Assert.ThrowsAsync<InvalidDataException>(() => tag.ReadAsync(CancellationToken.None));
    }

    #endregion

    #region Double (DOUBLE)

    [Fact]
    public async Task Double_ReadAsync_FromFile_ReturnsParsedValue()
    {
        var fileName = "double_val.txt";
        await File.WriteAllTextAsync(Path.Combine(_tempDir, fileName), "3.14159265358979");
        var tag = CreateTag(MakeDescriptor("d", fileName, BuiltinTagKinds.DOUBLE));

        await tag.ReadAsync(CancellationToken.None);

        Assert.Equal(3.14159265358979, tag.Value);
    }

    [Fact]
    public async Task Double_WriteAsync_WritesCorrectContent()
    {
        var fileName = "double_out.txt";
        var fullPath = Path.Combine(_tempDir, fileName);
        await File.WriteAllTextAsync(fullPath, "0");
        var tag = CreateTag(MakeDescriptor("d", fileName, BuiltinTagKinds.DOUBLE));
        tag.Value = -1.5e10;

        await tag.WriteAsync(CancellationToken.None);

        var content = await File.ReadAllTextAsync(fullPath);
        Assert.Equal("-15000000000", content);
    }

    [Fact]
    public async Task Double_ReadAsync_InvalidContent_Throws()
    {
        var fileName = "double_bad.txt";
        await File.WriteAllTextAsync(Path.Combine(_tempDir, fileName), "not-double");
        var tag = CreateTag(MakeDescriptor("d", fileName, BuiltinTagKinds.DOUBLE));

        await Assert.ThrowsAsync<InvalidDataException>(() => tag.ReadAsync(CancellationToken.None));
    }

    #endregion

    #region String (STR)

    [Fact]
    public async Task String_ReadAsync_FromFile_ReturnsContent()
    {
        var fileName = "str_val.txt";
        await File.WriteAllTextAsync(Path.Combine(_tempDir, fileName), "Hello World");
        var tag = CreateTag(MakeDescriptor("s", fileName, BuiltinTagKinds.STR));

        await tag.ReadAsync(CancellationToken.None);

        Assert.Equal("Hello World", tag.Value);
    }

    [Fact]
    public async Task String_WriteAsync_WritesCorrectContent()
    {
        var fileName = "str_out.txt";
        var fullPath = Path.Combine(_tempDir, fileName);
        await File.WriteAllTextAsync(fullPath, "");
        var tag = CreateTag(MakeDescriptor("s", fileName, BuiltinTagKinds.STR));
        tag.Value = "Test String";

        await tag.WriteAsync(CancellationToken.None);

        var content = await File.ReadAllTextAsync(fullPath);
        Assert.Equal("Test String", content);
    }

    [Fact]
    public async Task String_ReadAsync_EmptyFile_ReturnsEmptyString()
    {
        var fileName = "str_empty.txt";
        await File.WriteAllTextAsync(Path.Combine(_tempDir, fileName), "");
        var tag = CreateTag(MakeDescriptor("s", fileName, BuiltinTagKinds.STR));

        await tag.ReadAsync(CancellationToken.None);

        Assert.Equal("", tag.Value);
    }

    #endregion

    #region JsonTag
    record MyJson(string name, int age);
    [Fact]
    public async Task JsonTag_ReadAsync_FromFile_ReturnsParsedObject()
    {
        var fileName = "json_val.txt";
        var jsonContent = "{\"name\":\"Alice\",\"age\":30}";
        var normailizedPath = Path.Combine(_tempDir, fileName);
        await File.WriteAllTextAsync(normailizedPath, jsonContent);

        var tagdescriptor = MakeDescriptor("j", fileName, "Json");
        tagdescriptor.NormalizedAddress = normailizedPath;

        var tag = new JsonDirectTag<MyJson>(
            descriptor: tagdescriptor,
            thisChannel: new SimpleFilesTagChannel(
                new SimpleFilesTagChannelDescriptor { Name = "test-channel", BaseDir = _tempDir },
                NullLogger<SimpleFilesTagChannel>.Instance
            ),
            container: _grp.IntoTagContainer()
        );
        await tag.ReadAsync(CancellationToken.None);

        Assert.Equal(JsonSerializer.Deserialize<MyJson>(jsonContent), tag.Value);
    }

    [Fact]
    public async Task JsonTag_WriteAsync_IntoFile_SinksJsonString()
    {
        var fileName = "json_val2.txt";
        var normailizedPath = Path.Combine(_tempDir, fileName);
        var tagdescriptor = MakeDescriptor("j", fileName, "Json");
        tagdescriptor.NormalizedAddress = normailizedPath;
        tagdescriptor.Extras = new Dictionary<string, XAttribute>
        {
            ["autoCreateFile"] = new XAttribute("autoCreateFile", "true")
        };
        var tag = new JsonDirectTag<MyJson>(
            descriptor: tagdescriptor,
            thisChannel: new SimpleFilesTagChannel(
                new SimpleFilesTagChannelDescriptor { Name = "test-channel", BaseDir = _tempDir },
                NullLogger<SimpleFilesTagChannel>.Instance
            ),
            container: _grp.IntoTagContainer()
        );
        var raw = new MyJson("Bob", 25);
        tag.Value = raw;
        await tag.WriteAsync(CancellationToken.None);
        var content = await File.ReadAllTextAsync(normailizedPath);
        var json = JsonSerializer.Deserialize<MyJson>(content);
        Assert.Equal(raw, json);
    }

    [Fact]
    public async Task JsonTag_ReadAsync_InvalidJson_Throws()
    {
        var fileName = "json_invalid.txt";
        var normailizedPath = Path.Combine(_tempDir, fileName);
        await File.WriteAllTextAsync(normailizedPath, "not-a-json");

        var tagdescriptor = MakeDescriptor("j", fileName, "Json");
        tagdescriptor.NormalizedAddress = normailizedPath;

        var tag = new JsonDirectTag<MyJson>(
            descriptor: tagdescriptor,
            thisChannel: new SimpleFilesTagChannel(
                new SimpleFilesTagChannelDescriptor { Name = "test-channel", BaseDir = _tempDir },
                NullLogger<SimpleFilesTagChannel>.Instance
            ),
            container: _grp.IntoTagContainer()
        );

        await Assert.ThrowsAsync<JsonException>(() => tag.ReadAsync(CancellationToken.None));
    }

    [Fact]
    public async Task JsonTag_ReadAsync_JsonNullLiteral_ReturnsNullValue()
    {
        var fileName = "json_null.txt";
        var normailizedPath = Path.Combine(_tempDir, fileName);
        await File.WriteAllTextAsync(normailizedPath, "null");

        var tagdescriptor = MakeDescriptor("j", fileName, "Json");
        tagdescriptor.NormalizedAddress = normailizedPath;

        var tag = new JsonDirectTag<MyJson>(
            descriptor: tagdescriptor,
            thisChannel: new SimpleFilesTagChannel(
                new SimpleFilesTagChannelDescriptor { Name = "test-channel", BaseDir = _tempDir },
                NullLogger<SimpleFilesTagChannel>.Instance
            ),
            container: _grp.IntoTagContainer()
        );
        await tag.ReadAsync(CancellationToken.None);

        Assert.Null(tag.Value);
    }
    #endregion


    #region AutoCreateFile

    [Theory]
    [InlineData(BuiltinTagKinds.INT16, typeof(short))]
    [InlineData(BuiltinTagKinds.UINT16, typeof(ushort))]
    [InlineData(BuiltinTagKinds.INT32, typeof(int))]
    [InlineData(BuiltinTagKinds.UINT32, typeof(uint))]
    [InlineData(BuiltinTagKinds.FLOAT, typeof(float))]
    [InlineData(BuiltinTagKinds.DOUBLE, typeof(double))]
    public async Task AutoCreateFile_WhenFileMissing_CreatesAndWritesDefaultZero(string tagKind, Type expectedType)
    {
        var fileName = $"auto_{tagKind}.txt";
        var fullPath = Path.Combine(_tempDir, fileName);
        Assert.False(File.Exists(fullPath));

        var descriptor = MakeDescriptor("t", fileName, tagKind);
        descriptor.Extras = new Dictionary<string, XAttribute>
        {
            ["autoCreateFile"] = new XAttribute("autoCreateFile", "true")
        };
        var tag = CreateTag(descriptor);

        await tag.ReadAsync(CancellationToken.None);

        Assert.True(File.Exists(fullPath));
        var content = await File.ReadAllTextAsync(fullPath);
        Assert.Equal("0", content);
        Assert.Equal(expectedType, tag.Value?.GetType());
        Assert.Equal(0, Convert.ToDouble(tag.Value!));
    }

    [Fact]
    public async Task AutoCreateFile_String_WhenFileMissing_CreatesEmptyFile()
    {
        var fileName = "auto_str.txt";
        var fullPath = Path.Combine(_tempDir, fileName);
        Assert.False(File.Exists(fullPath));

        var descriptor = MakeDescriptor("t", fileName, BuiltinTagKinds.STR);
        descriptor.Extras = new Dictionary<string, XAttribute>
        {
            ["autoCreateFile"] = new XAttribute("autoCreateFile", "true")
        };
        var tag = CreateTag(descriptor);

        await tag.ReadAsync(CancellationToken.None);

        Assert.True(File.Exists(fullPath));
        var content = await File.ReadAllTextAsync(fullPath);
        Assert.Equal("", content);
        // ReadAsync 创建文件后不会自动读取，因此值为 null
        Assert.Null(tag.Value);
    }

    [Fact]
    public async Task AutoCreateFile_False_WhenFileMissing_Skips()
    {
        var fileName = "noauto.txt";
        var fullPath = Path.Combine(_tempDir, fileName);
        Assert.False(File.Exists(fullPath));

        var descriptor = MakeDescriptor("t", fileName, BuiltinTagKinds.INT32);
        descriptor.Extras = new Dictionary<string, XAttribute>
        {
            ["autoCreateFile"] = new XAttribute("autoCreateFile", "false")
        };
        var tag = CreateTag(descriptor);

        await tag.ReadAsync(CancellationToken.None);

        Assert.False(File.Exists(fullPath));
        // int 的默认值是 0 而非 null
        Assert.Equal(0, tag.Value);
    }

    [Fact]
    public async Task AutoCreateFile_Default_WhenFileMissing_Skips()
    {
        var fileName = "nodefault.txt";
        var fullPath = Path.Combine(_tempDir, fileName);
        Assert.False(File.Exists(fullPath));

        var tag = CreateTag(MakeDescriptor("t", fileName, BuiltinTagKinds.INT32));

        await tag.ReadAsync(CancellationToken.None);

        Assert.False(File.Exists(fullPath));
        // int 的默认值是 0 而非 null
        Assert.Equal(0, tag.Value);
    }

    [Fact]
    public async Task WriteAsync_AutoCreateFileTrue_WhenFileMissing_CreatesAndWritesValue()
    {
        var fileName = "write_auto_int.txt";
        var fullPath = Path.Combine(_tempDir, fileName);
        Assert.False(File.Exists(fullPath));

        var descriptor = MakeDescriptor("t", fileName, BuiltinTagKinds.INT32);
        descriptor.Extras = new Dictionary<string, XAttribute>
        {
            ["autoCreateFile"] = new XAttribute("autoCreateFile", "true")
        };
        var tag = CreateTag(descriptor);
        tag.Value = 42;

        await tag.WriteAsync(CancellationToken.None);

        Assert.True(File.Exists(fullPath));
        var content = await File.ReadAllTextAsync(fullPath);
        Assert.Equal("42", content);
        Assert.False(tag.IsDirty);
    }

    [Fact]
    public async Task WriteAsync_AutoCreateFileTrue_WhenFileMissing_String_WritesValue()
    {
        var fileName = "write_auto_str.txt";
        var fullPath = Path.Combine(_tempDir, fileName);
        Assert.False(File.Exists(fullPath));

        var descriptor = MakeDescriptor("t", fileName, BuiltinTagKinds.STR);
        descriptor.Extras = new Dictionary<string, XAttribute>
        {
            ["autoCreateFile"] = new XAttribute("autoCreateFile", "true")
        };
        var tag = CreateTag(descriptor);
        tag.Value = "hello";

        await tag.WriteAsync(CancellationToken.None);

        Assert.True(File.Exists(fullPath));
        var content = await File.ReadAllTextAsync(fullPath);
        Assert.Equal("hello", content);
    }

    [Fact]
    public async Task WriteAsync_AutoCreateFileFalse_WhenFileMissing_DoesNothing()
    {
        var fileName = "write_noauto.txt";
        var fullPath = Path.Combine(_tempDir, fileName);
        Assert.False(File.Exists(fullPath));

        var descriptor = MakeDescriptor("t", fileName, BuiltinTagKinds.INT32);
        descriptor.Extras = new Dictionary<string, XAttribute>
        {
            ["autoCreateFile"] = new XAttribute("autoCreateFile", "false")
        };
        var tag = CreateTag(descriptor);
        tag.Value = 99;

        await tag.WriteAsync(CancellationToken.None);

        Assert.False(File.Exists(fullPath));
    }

    [Fact]
    public async Task AutoCreateFile_InvalidValue_Throws()
    {
        var fileName = "bad_auto.txt";
        var fullPath = Path.Combine(_tempDir, fileName);
        Assert.False(File.Exists(fullPath));

        var descriptor = MakeDescriptor("t", fileName, BuiltinTagKinds.INT32);
        descriptor.Extras = new Dictionary<string, XAttribute>
        {
            ["autoCreateFile"] = new XAttribute("autoCreateFile", "not-a-bool")
        };
        var tag = CreateTag(descriptor);

        var ex = await Assert.ThrowsAsync<TagsProjectXmlException>(() => tag.ReadAsync(CancellationToken.None));
        Assert.Contains("autoCreateFile", ex.Message);
        Assert.Contains("not-a-bool", ex.Message);
    }

    [Fact]
    public void AutoCreateFile_LegacyKey_ThrowsWithMigrationHint()
    {
        // 旧 key AutoCreateFile 自 1.0 起已移除：报错而不是静默忽略（否则文件不再自动创建却无人察觉）
        var descriptor = MakeDescriptor("t", "legacy_auto.txt", BuiltinTagKinds.INT32);
        descriptor.Extras = new Dictionary<string, XAttribute>
        {
            ["AutoCreateFile"] = new XAttribute("AutoCreateFile", "true")
        };

        var ex = Assert.Throws<TagsProjectConfigurationException>(() => CreateTag(descriptor));
        Assert.Contains("AutoCreateFile", ex.Message);
        Assert.Contains("autoCreateFile", ex.Message);
        Assert.Equal("Tag(t)", ex.Location);
    }

    [Fact]
    public void AutoCreateFile_LegacyKeyAlongsideNewKey_Throws()
    {
        // 不再有"新 key 优先"的兼容：只要出现旧 key 就报错
        var descriptor = MakeDescriptor("t", "both_auto.txt", BuiltinTagKinds.INT32);
        descriptor.Extras = new Dictionary<string, XAttribute>
        {
            ["AutoCreateFile"] = new XAttribute("AutoCreateFile", "false"),
            ["autoCreateFile"] = new XAttribute("autoCreateFile", "true"),
        };

        Assert.Throws<TagsProjectConfigurationException>(() => CreateTag(descriptor));
    }

    #endregion

    #region Factory

    [Theory]
    [InlineData(BuiltinTagKinds.INT16, typeof(ShortDirectTag))]
    [InlineData(BuiltinTagKinds.UINT16, typeof(UShortDirectTag))]
    [InlineData(BuiltinTagKinds.INT32, typeof(IntDirectTag))]
    [InlineData(BuiltinTagKinds.UINT32, typeof(UIntDirectTag))]
    [InlineData(BuiltinTagKinds.FLOAT, typeof(FloatDirectTag))]
    [InlineData(BuiltinTagKinds.DOUBLE, typeof(DoubleDirectTag))]
    [InlineData(BuiltinTagKinds.STR, typeof(StringDirectTag))]
    [InlineData(BuiltinTagKinds.BIT, typeof(BitDirectTag))]
    [InlineData(BuiltinTagKinds.BYTE, typeof(ByteDirectTag))]
    public void Factory_CreatesCorrectTagType(string tagKind, Type expectedType)
    {
        var tag = CreateTag(MakeDescriptor("t", "some_file.txt", tagKind));
        Assert.IsType(expectedType, tag);
    }

    [Fact]
    public void Factory_UnknownKind_Throws()
    {
        var descriptor = MakeDescriptor("t", "x.txt", "UNSUPPORTED");
        var factory = new SimpleFilesDirectTagFactory(_grp.IntoTagContainer());

        var ex = Assert.Throws<TagsProjectConfigurationException>(() => factory.Create(descriptor, _channel));
        Assert.Contains("UNSUPPORTED", ex.Message);
    }

    #endregion

    #region NormalizedAddress

    [Fact]
    public void NormalizedAddress_CombinesBaseDirWithRawAddress()
    {
        var tag = CreateTag(MakeDescriptor("t", "sub/file.txt", BuiltinTagKinds.INT32));

        var expectedPath = Path.Combine(_tempDir, "sub/file.txt");
        Assert.Equal(expectedPath, tag.NormalizedAddress());
    }

    [Fact]
    public void NormalizedAddress_WithoutBaseDir_UsesRawAddressAsIs()
    {
        var settings = new SimpleFilesSettings(null);
        var chdescriptor = new SimpleFilesTagChannelDescriptor()
        {
            Name = "no-base",
            BaseDir = settings.BaseDir,
        };
        var logger = NullLogger<SimpleFilesTagChannel>.Instance;
        var channel = new SimpleFilesTagChannel(chdescriptor, logger);
        var grp = new TagGrp(new TagGrpDescriptor { Name = "g", IsEntry = true }, channel);

        var descriptor = MakeDescriptor("t", @"C:\absolute\path.txt", BuiltinTagKinds.INT32);
        var factory = new SimpleFilesDirectTagFactory(grp.IntoTagContainer());
        var tag = factory.Create(descriptor, channel);

        Assert.Equal(@"C:\absolute\path.txt", tag.NormalizedAddress());
    }

    #endregion
}
