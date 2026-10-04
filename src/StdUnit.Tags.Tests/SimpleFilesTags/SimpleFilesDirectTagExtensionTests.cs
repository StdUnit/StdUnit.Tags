using StdUnit.Tags;
using StdUnit.Tags.SimpleFiles;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Xunit;

// net472 没有 File.ReadAllTextAsync / WriteAllTextAsync（.NET Core 2.0+），见 Compat.TestFileCompat。
using File = StdUnit.Tags.Tests.Compat.TestFileCompat;

namespace StdUnit.Tags.Tests.SimpleFilesTags;

/// <summary>
/// 验证外部可以：
/// 1. 继承 <see cref="SimpleFilesDirectTagBase{T}"/> 编写自定义测点（如 JSON 反序列化到特定类型）
/// 2. 通过 <see cref="TagsProject_Extensions.AddSimpleFilesDirectTagBuilder"/> 的 configure 钩子 + 
///    <see cref="SimpleFilesDirectTagBuilder.WithFactory"/> 注入创建委托，
///    无需编写自定义 DirectTagBuilder，即可实现「基本类型读写 + JSON 到特定类型读写」共存
/// </summary>
public class SimpleFilesDirectTagExtensionTests
{
    #region 外部扩展的自定义类型（模拟用户自己的扩展代码）

    /// <summary>
    /// 测试用的 JSON POCO
    /// </summary>
    record JsonPoint(int X, int Y);

    /// <summary>
    /// 外部自定义的 JSON 测点：继承 <see cref="SimpleFilesDirectTagBase{T}"/>，<br/>
    /// 文件内容为 JSON 文本，反序列化为 <see cref="JsonPoint"/> 类型的测点值。<br/>
    /// 只需实现对称的 <see cref="SimpleFilesDirectTagBase{T}.ParseValue"/> 与 <see cref="SimpleFilesDirectTagBase{T}.FormatValue"/>，
    /// 读写与自动创建默认文件均由基类完成。
    /// </summary>
    class JsonPointDirectTag : SimpleFilesDirectTagBase<JsonPoint>
    {
        /// <summary>
        /// c'tor
        /// </summary>
        public JsonPointDirectTag(TagDescriptor descriptor, SimpleFilesTagChannel? thisChannel, TagContainer container)
            : base(descriptor, thisChannel, container)
        {
        }

        /// <inheritdoc/>
        protected override JsonPoint? ParseValue(string text)
        {
            return JsonSerializer.Deserialize<JsonPoint>(text);
        }

        /// <inheritdoc/>
        protected override string FormatValue(JsonPoint? value)
        {
            var val = value ?? new JsonPoint(0, 0);
            return JsonSerializer.Serialize(val);
        }
    }

    #endregion

    #region Helper

    /// <summary>
    /// 创建独立临时目录，测试结束由调用方清理
    /// </summary>
    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "SimpleFilesExtension_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    /// <summary>
    /// 构建测试 XML：包含一个基本类型测点(int-v)和一个 JSON 测点(json-v)<br/>
    /// json-v 配置了 autoCreateFile=true，验证基类基于 <see cref="SimpleFilesDirectTagBase{T}.FormatValue"/> 自动创建默认文件的能力
    /// </summary>
    private static XElement BuildTestXml(string baseDir)
    {
        return XElement.Parse($@"
<root>
    <Channel name='sf' driver='SimpleFiles'>
        <BaseDir>{baseDir}</BaseDir>
    </Channel>
    <TagGrp name='g' isEntry='true' channel='sf' scanInterval='0'>
        <Tag name='int-v'   address='int.txt'    type='INT32' />
        <Tag name='json-v'  address='point.json' type='JSON' autoCreateFile='true' />
    </TagGrp>
</root>");
    }

    /// <summary>
    /// 注册：通道 + JSON 测点创建委托（通过 configure 钩子 + WithFactory）+ 基本类型 builder
    /// </summary>
    private static ServiceProvider BuildRoot()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTagsProjectServices(b =>
        {
            // 1. 仅注册通道
            b.AddSimpleFilesChannel();

            // 2. 注册 JSON 测点：configure 钩子中通过 WithFactory 注入创建委托（无需自定义 DirectTagBuilder 类）
            b.AddSimpleFilesDirectTagBuilder(
                predicate: bd => bd.TagDescriptor.TagKind == "JSON",
                configure: b => b.WithFactory((descriptor, thisChannel, container) =>
                {
                    // 与内部 SimpleFilesDirectTagFactory 一致：基于通道 BaseDir 归一化文件路径
                    var channel = thisChannel ?? container.SearchRequiredChannel();
                    var sfsChannel = channel as SimpleFilesTagChannel ?? throw new InvalidOperationException($"通道类型不匹配：{channel?.GetType().FullName}");
                    descriptor.NormalizedAddress = sfsChannel.MakePath(descriptor.RawAddress);
                    return new JsonPointDirectTag(descriptor, thisChannel as SimpleFilesTagChannel, container);
                })
            );

            // 3. 注册基本类型测点构建器（后注册，处理其余所有测点，走内部工厂）
            b.AddSimpleFilesDirectTagBuilder();
        });
        return services.BuildServiceProvider();
    }

    #endregion

    #region 验证：基本类型 + JSON 测点共存并能加载

    [Fact]
    public void AddSimpleFilesDirectTagBuilder_WithCustomJsonBuilder_LoadsBothBasicAndJsonTags()
    {
        var tempDir = CreateTempDir();
        try
        {
            using var root = BuildRoot();
            using var scope = root.CreateScope();
            var sp = scope.ServiceProvider;

            using var proj = sp.MakeProject(null, BuildTestXml(tempDir));

            // 通道
            Assert.Single(proj.Channels);
            Assert.IsType<SimpleFilesTagChannel>(proj.Channels[0]);

            // 两个测点都加载成功
            var g = proj.Tags.SelectGrp("g");
            Assert.NotNull(g);

            var intTag = g.SelectTag("int-v");
            Assert.NotNull(intTag);
            Assert.Equal(BuiltinTagKinds.INT32, intTag.TagKind());

            var jsonTag = g.SelectTag("json-v");
            Assert.NotNull(jsonTag);
            Assert.Equal("JSON", jsonTag.TagKind());
            // 外部自定义的 JSON 测点类型被正确构建
            Assert.IsType<JsonPointDirectTag>(jsonTag);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    #endregion

    #region 验证：基本类型读写（走 AddSimpleFilesDirectTagBuilder 注册的内部构建器）

    [Fact]
    public async Task BasicIntTag_WriteThenRead_RoundTrips()
    {
        var tempDir = CreateTempDir();
        try
        {
            using var root = BuildRoot();
            using var scope = root.CreateScope();
            var sp = scope.ServiceProvider;

            using var proj = sp.MakeProject(null, BuildTestXml(tempDir));
            var g = proj.Tags.SelectGrp("g");
            var intTag = g.SelectTag("int-v");

            // 预创建文件（模拟文件已存在的场景；autoCreateFile=false 时文件不存在会直接跳过写入）
            var intPath = Path.Combine(tempDir, "int.txt");
            await File.WriteAllTextAsync(intPath, "0");

            // 写入
            intTag.Value = 42;
            await intTag.WriteAsync(CancellationToken.None);
            Assert.False(intTag.IsDirty);
            Assert.Equal("42", await File.ReadAllTextAsync(intPath));

            // 读取
            intTag.Value = 0;
            await intTag.ReadAsync(CancellationToken.None);
            Assert.Equal(42, intTag.Value);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    #endregion

    #region 验证：JSON 测点读写（外部自定义 builder + 继承 SimpleFilesDirectTagBase<T>）

    [Fact]
    public async Task JsonTag_WriteAsync_SerializesJsonToFile()
    {
        var tempDir = CreateTempDir();
        try
        {
            using var root = BuildRoot();
            using var scope = root.CreateScope();
            var sp = scope.ServiceProvider;

            using var proj = sp.MakeProject(null, BuildTestXml(tempDir));
            var g = proj.Tags.SelectGrp("g");
            var jsonTag = g.SelectTag("json-v");

            // 写入：POCO → JSON 文件
            jsonTag.Value = new JsonPoint(3, 4);
            await jsonTag.WriteAsync(CancellationToken.None);
            Assert.False(jsonTag.IsDirty);

            var content = await File.ReadAllTextAsync(Path.Combine(tempDir, "point.json"));
            Assert.Equal("{\"X\":3,\"Y\":4}", content);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task JsonTag_ReadAsync_DeserializesJsonFromFile()
    {
        var tempDir = CreateTempDir();
        try
        {
            // 预置 JSON 文件内容
            await File.WriteAllTextAsync(Path.Combine(tempDir, "point.json"), "{\"X\":7,\"Y\":9}");

            using var root = BuildRoot();
            using var scope = root.CreateScope();
            var sp = scope.ServiceProvider;

            using var proj = sp.MakeProject(null, BuildTestXml(tempDir));
            var g = proj.Tags.SelectGrp("g");
            var jsonTag = g.SelectTag("json-v");

            // 读取：JSON 文件 → POCO
            await jsonTag.ReadAsync(CancellationToken.None);
            Assert.Equal(new JsonPoint(7, 9), jsonTag.Value);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task JsonTag_AutoCreateFile_WhenFileMissing_CreatesDefaultJson()
    {
        var tempDir = CreateTempDir();
        try
        {
            // 文件不存在（json-v 已配置 autoCreateFile=true）
            var jsonPath = Path.Combine(tempDir, "point.json");
            Assert.False(File.Exists(jsonPath));

            using var root = BuildRoot();
            using var scope = root.CreateScope();
            var sp = scope.ServiceProvider;

            using var proj = sp.MakeProject(null, BuildTestXml(tempDir));
            var g = proj.Tags.SelectGrp("g");
            var jsonTag = g.SelectTag("json-v");

            // ReadAsync：文件缺失 → 基类调用 CreateAndWriteDefaultAsync → FormatValue(default) 生成默认 JSON
            await jsonTag.ReadAsync(CancellationToken.None);

            // 默认文件被创建，内容为 FormatValue(null) 产出的合法默认 JSON
            Assert.True(File.Exists(jsonPath));
            var content = await File.ReadAllTextAsync(jsonPath);
            Assert.Equal("{\"X\":0,\"Y\":0}", content);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    #endregion

    #region 验证：WithFactory 委托返回 null 时回退内部工厂

    [Fact]
    public void WithFactory_DelegateReturnsNull_FallsBackToInternalFactory()
    {
        var tempDir = CreateTempDir();
        try
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddTagsProjectServices(b =>
            {
                b.AddSimpleFilesChannel();

                // configure 中注入委托，但委托返回 null → 应回退内部工厂
                b.AddSimpleFilesDirectTagBuilder(
                    configure: b => b.WithFactory((_, _, _) => null!)
                );
            });

            using var root = services.BuildServiceProvider();
            using var scope = root.CreateScope();

            // 只含 INT32 测点的 XML
            var xml = XElement.Parse($@"
<root>
    <Channel name='sf' driver='SimpleFiles'>
        <BaseDir>{tempDir}</BaseDir>
    </Channel>
    <TagGrp name='g' isEntry='true' channel='sf' scanInterval='0'>
        <Tag name='int-v' address='int.txt' type='INT32' />
    </TagGrp>
</root>");

            using var proj = scope.ServiceProvider.MakeProject(null, xml);

            var g = proj.Tags.SelectGrp("g");
            Assert.NotNull(g);

            var intTag = g.SelectTag("int-v");
            Assert.NotNull(intTag);
            Assert.Equal(BuiltinTagKinds.INT32, intTag.TagKind());
            // 委托返回 null → 回退内部工厂创建内部实现类
            Assert.Equal("StdUnit.Tags.SimpleFiles.IntDirectTag", intTag.GetType().FullName);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    #endregion

    #region 多JSON读写
    record MyJson1(string name, int age);
    record MyJson2(string name, int x, int y);
    [Fact]
    public void WithJsonTagFactory_RegisterMultipleTimes_CreateMultipleJsonTags()
    {
        var tempDir = CreateTempDir();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTagsProjectServices(b =>
        {
            // 1. 仅注册通道
            b.AddSimpleFilesChannel();

            b.AddSimpleFilesDirectTagBuilder(
                predicate: bd => bd.TagDescriptor.TagKind == "myjson1",
                configure: b => b.WithJsonTagFactory<MyJson1>()
            );

            b.AddSimpleFilesDirectTagBuilder(
                predicate: bd => bd.TagDescriptor.TagKind == "myjson2",
                configure: b => b.WithJsonTagFactory<MyJson2>()
            );

            // 3. 注册基本类型测点构建器（后注册，处理其余所有测点，走内部工厂）
            b.AddSimpleFilesDirectTagBuilder();
        });

        var rootsp = services.BuildServiceProvider();
        using var scope = rootsp.CreateScope();
        var sp = scope.ServiceProvider;

        var xml = XElement.Parse($@"
<root>
    <Channel name='sf' driver='SimpleFiles'>
        <BaseDir>{tempDir}</BaseDir>
    </Channel>
    <TagGrp name='g' isEntry='true' channel='sf' scanInterval='0'>
        <Tag name='int-v'   address='int.txt'    type='INT32' />
        <Tag name='json-1'  address='j1.json' type='myjson1' autoCreateFile='true' />
        <Tag name='json-2'  address='j2.json' type='myjson2' autoCreateFile='true' />
    </TagGrp>
</root>");

        using var proj = rootsp.MakeProject(null, xml);
        var tags = proj.Tags.SelectGrp("g");
        Assert.NotNull(tags);
        var intTag = tags.SelectTag("int-v");
        Assert.IsType<IntDirectTag>(intTag);
        var json1Tag = tags.SelectTag("json-1");
        Assert.IsType<JsonDirectTag<MyJson1>>(json1Tag);
        var json2Tag = tags.SelectTag("json-2");
        Assert.IsType<JsonDirectTag<MyJson2>>(json2Tag);

    }
    #endregion

    #region 验证：泛型 WithFactory<TVal> —— 工厂无需自行归一化路径

    [Fact]
    public void GenericWithFactory_FactoryWithoutManualNormalization_StillBuildsWithNormalizedAddress()
    {
        var tempDir = CreateTempDir();
        try
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddTagsProjectServices(b =>
            {
                b.AddSimpleFilesChannel();

                // 与 BuildRoot 中的旧写法对比：这里工厂内不再自行 MakePath / 校验通道类型，
                // 「通道解析 + 地址归一化」由泛型 WithFactory 重载代为完成
                b.AddSimpleFilesDirectTagBuilder(
                    predicate: bd => bd.TagDescriptor.TagKind == "JSON",
                    configure: b => b.WithFactory((descriptor, thisChannel, container) =>
                        new JsonPointDirectTag(descriptor, thisChannel, container))
                );

                // 基本类型测点走内部工厂
                b.AddSimpleFilesDirectTagBuilder();
            });

            using var root = services.BuildServiceProvider();
            using var scope = root.CreateScope();

            using var proj = scope.ServiceProvider.MakeProject(null, BuildTestXml(tempDir));
            var jsonTag = proj.Tags.SelectGrp("g").SelectTag("json-v");
            Assert.NotNull(jsonTag);
            Assert.IsType<JsonPointDirectTag>(jsonTag);
            // 归一化地址 = BaseDir + RawAddress，已由泛型 WithFactory 写入 descriptor
            Assert.Equal(Path.Combine(tempDir, "point.json"), jsonTag.NormalizedAddress());
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    #endregion

}
