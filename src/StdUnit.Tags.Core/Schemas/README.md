# TagsProject XML Schema（XSD）

## 架构：schema 随驱动项目归属

XSD 源文件**按驱动归属**存放在各项目自己的 `Schemas/` 目录，而非集中存放：

| 文件 | 所在项目 |
|---|---|
| `Schemas/tagsproject.xsd`（核心：Project/TagGrp/TagCbnt/Tag + Channel） | `StdUnit.Tags.Core` |
| `Schemas/s7.xsd` | `StdUnit.Tags.S7` |
| `Schemas/modbustcp.xsd` | `StdUnit.Tags.ModbusTcp` |
| `Schemas/zlan.xsd` | `StdUnit.Tags.ZLan` |
| `Schemas/hjzk.xsd` | `StdUnit.Tags.Hjzk` |
| `Schemas/com.xsd` | `StdUnit.Tags.ComScanner` |
| `Schemas/opcua-client.xsd` | `StdUnit.Tags.OpcUaClient` |
| `Schemas/simplefiles.xsd` | `StdUnit.Tags.SimpleFiles` |

每个内置项目把自己的 XSD 交给源生成器（`src/StdUnit.Tags.SchemaGenerator`）编译为静态字符串常量
（类 `SchemaContent_{文件名}`，命名空间 `StdUnit.Tags.Generated`）——**不依赖资源反射，对 NativeAOT/trim 友好**。
这是内置驱动的内部实现选择；**第三方驱动不要求使用源生成器**（见下文"第三方驱动库"）。

## 三种存在形式

| 形式 | 用途 |
|---|---|
| 各项目 `Schemas/`（源） | 本仓库开发时编辑器校验、维护 |
| **DLL 内（源生成器编译为常量）** | **运行期可选校验**（`EnableXmlSchemaValidation()`），随包自动部署 |
| **NuGet `buildTransitive` 注入** | **应用方编辑器智能提示**：还原包后自动以链接项出现 |

## 应用方：编辑器智能提示（零配置）

还原 `StdUnit.Tags.Core` + 用到的驱动包后，项目树自动出现（由各包 `buildTransitive` targets 注入）：

```
<项目根>/Schemas/
├── tagsproject.xsd        # Core 注入（核心 schema）
└── Drivers/               # 各驱动包注入
    ├── s7.xsd  
    ├── com.xsd  
    ├── modbustcp.xsd 
    ├── ...
```

`index.xml` 根元素上写：

```xml
<Project xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
      xsi:noNamespaceSchemaLocation="Schemas/tagsproject.xsd"
      xmlns:s7="tags:s7">
  <Channel name="S7-1" driver="S7">
    <s7:IpAddr>localhost</s7:IpAddr>
    ...
  </Channel>
</Project>
```
Core 不承载驱动映射，驱动专属子元素（`<s7:IpAddr>` 等）的编辑器校验/补全，需把驱动命名空间映射到驱动 schema
（vscode-xml 用 `xml.schemas` 设置，Oxygen/XMLSpy 用 XML Catalog 指向各驱动 `Schemas/*.xsd`）。
仓库内示例见 `.vscode/settings.json` 的 `xml.schemas`。


关闭自动注入：`<TagsProjectSchemaDisabled>true</TagsProjectSchemaDisabled>`。

## 运行期校验（可选功能）

加载期校验统一抽象为 `ITagsProjectValidator`（可注册多个，按注册顺序执行；第三方可实现自己的校验器）：

```csharp
services.AddTagsProjectServices(b =>
{
    b.AddValidation<TagsProjectSchemaValidator>();       // 内置：XSD 校验（或便捷方法 EnableXmlSchemaValidation()）
    b.AddValidation<ChannelCrossReferenceValidator>();   // 内置：channel 引用校验（或便捷方法 
    b.AddValidation<MyValidator>();                      // 自定义校验器（实现 ITagsProjectValidator）
    b.AddS7Support();                                    // 各 AddXxxSupport 会自动注册各自的 schema provider
    ...
});
```

不注册任何校验器时，加载期不校验——老的无命名空间前缀 XML 照常工作。

## 第三方驱动库：嵌入自己的 schema 校验

第三方驱动**只需要做两件事**，不要求依赖 `StdUnit.Tags.SchemaGenerator`：

### 1. 运行时校验：实现 `ITagsProjectSchemaProvider`（DI 注册）

契约只有一条：该方法返回 `IEnumerable<(string LogicalName, string Content)>`（逻辑名 → XSD 文本）。
内容来源完全自由——EmbeddedResource、磁盘文件、硬编码字符串、源生成器均可：

```csharp
// 方式一：EmbeddedResource（xsd 作为嵌入资源）
public sealed class MyDriverSchemaProvider : ITagsProjectSchemaProvider
{
    public IEnumerable<(string LogicalName, string Content)> GetSchemaContents()
    {
        var asm = typeof(MyDriverSchemaProvider).Assembly;
        var names = asm.GetManifestResourceNames().Where(n => n.EndsWith(".xsd"));
        foreach (var n in names)
        {
            using var s = asm.GetManifestResourceStream(n)!;
            using var r = new StreamReader(s);
            yield return (n, r.ReadToEnd());
        }
    }
}
```

```csharp
// 方式二：源生成器（可选，与内置驱动一致，AOT/trim 友好）
//   引用 StdUnit.Tags.SchemaGenerator，AdditionalFiles 传入自己的 xsd，
//   生成器自动生成 SchemaContent_{文件名} 类：
public sealed class MyDriverSchemaProvider : ITagsProjectSchemaProvider
{
    public IEnumerable<(string LogicalName, string Content)> GetSchemaContents() =>
        SchemaContent_mydriver.GetSchemaContents();
}
```

注册：

```csharp
services.AddTagsProjectServices(b =>
{
    b.EnableXmlSchemaValidation();
    b.Services.AddSingleton<ITagsProjectSchemaProvider, MyDriverSchemaProvider>();
    ...
});
```

启用校验后，校验器把核心 + 所有 provider 的 schema 合并进 `XmlSchemaSet`。

### 2. 编辑器智能提示：buildTransitive targets 注入

自己的 `buildTransitive` targets 把 xsd 以链接项注入消费项目（与内置驱动一致）：

```xml
<Project>
  <ItemGroup Condition="'$(TagsProjectSchemaDisabled)' != 'true'">
    <None Include="$(MSBuildThisFileDirectory)..\Schemas\mydriver.xsd"
          Link="Schemas\Drivers\mydriver.xsd" />
  </ItemGroup>
</Project>
```

> 注：运行时校验（步骤 1）与编辑器注入（步骤 2）相互独立。没有硬性要求全部完成。

### 3. 设计器元数据：让 schema 自己声明驱动名（可选，但推荐）

`StdUnit.Tags.WebUi` 的 `GET {prefix}/catalog` 会把**所有注册了 `ITagsProjectSchemaProvider` 的 schema**
内省成设计器元数据（通道/测点设计器据此渲染表单、导出 XML）。它需要知道"哪个 schema 属于哪个 `driver`"——
这件事 **XSD 1.0 表达不了**（不能按属性值切换内容模型），而且靠命名约定也不行
（`HjzkIoBox` ↔ `tags:hjzk`、`ZLanTcp` ↔ `tags:zlan` 就对不上）。所以由 schema 自己声明：

```xml
<xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema"
           targetNamespace="tags:mydriver" elementFormDefault="qualified">
  <xs:annotation><xs:appinfo>
    <driver name="MyDriver" displayName="我的驱动" prefix="md"/>

    <!-- 驱动额外认的“测点属性”：核心 schema 里没有，XSD 1.0 也加不进去 -->
    <tagAttribute name="maxlen" kind="number" type="positiveInteger" min="1" max="255"
                  label="字符串最大长度" order="10" group="STR" required="true"
                  appliesToTypes="STR" help="仅 STR 测点需要"/>
    <!-- 组合属性（TagCbnt）用 cbntAttribute，参数与 tagAttribute 相同 -->
    <cbntAttribute name="slot" kind="number" label="槽位" order="1"/>
  </xs:appinfo></xs:annotation>

  <xs:element name="Host" type="xs:string">
    <xs:annotation><xs:appinfo>
      <ui label="主机" order="1" group="连接" placeholder="localhost" help="省略时用 localhost"/>
    </xs:appinfo></xs:annotation>
  </xs:element>
</xs:schema>
```

- `<driver name>` **必须与运行时 `XxxNames.DriverName`（即 XML 里的 `driver="…"`）完全一致**；
  `prefix` 缺省取命名空间后缀（`tags:mydriver` → `mydriver`）。
- `<ui>` 只影响 UI：`label` / `order` / `group` / `placeholder` / `help` / `appliesToDrivers`。
  其中 `appliesToDrivers`（逗号分隔的驱动名）用来给**核心 schema 里“只有部分驱动认”的属性**划作用域——
  典型例子是 `TagCbnt` 的 `area`/`slave`（只有 ModbusTcp / HjzkIoBox / ZLanTcp 读）。XSD 1.0 只能给类型
  *加* 属性、不能按驱动 *删* 属性，所以用这个提示让设计器按有效驱动过滤（已写了值的属性仍会显示，避免
  “看不见却被导出”）。XSD 会忽略 `appinfo` 里的自定义元素，对校验零影响；**不写也能用**（设计器退化为按属性名显示）。
- 这两种元素都按**本地名**匹配，跟 `xs:schema` 上有没有默认 `xmlns` 无关。
- 没声明 `<driver>` 的 schema 仍然参与校验，只是不出现在设计器的驱动列表里。

#### 为什么需要 `<tagAttribute>` / `<cbntAttribute>`

`Tag` / `TagCbnt` / `TagGrp` 在核心 schema 里都带 `<xs:anyAttribute processContents="lax"/>`：
**任何属性都是合法的**。可运行期真正认哪些键，取决于驱动——它们从
`TagDescriptor.Extras` / `TagCbntDescriptor.Extras` 里读（如 S7 的 `maxlen`、SimpleFiles 的
`autoCreateFile`、ModbusTcp 的 `interpret`）。XSD 1.0 **不允许**跨命名空间给既有类型追加属性，
所以这批属性永远进不了核心 schema 的类型定义；只能由驱动"声明"一次，供设计器渲染与提示：

| 属性 | 含义 |
|---|---|
| `name` | 属性名（必须与运行期 `Extras` 的键一致） |
| `kind` | 控件类型：`string` / `bool` / `number` / `enum`（缺省按 `type` 推断） |
| `type` | 仅作文档/推断用（如 `positiveInteger`） |
| `label` / `order` / `group` / `placeholder` / `help` | 与 `<ui>` 同义 |
| `required` | 仅提示（设计器不会因此拒绝导出） |
| `min` / `max` / `minLength` / `enumValues` | 数值/长度/枚举提示（`enumValues` 逗号分隔） |
| `appliesToTypes` | 逗号分隔的测点 `type` 列表；只有命中时才显示（如 S7 的 `maxlen` 只对 `STR`） |

**这些声明不参与校验**：写错的属性名依旧是"合法 XML"，只是设计器不会给出提示。若需要"属性拼写查错"，
请在加载期做 **advisory** 诊断（既不拒绝，也不静默），当前库没有内置。

未声明的属性不会丢：设计器把它们归为"自定义属性"，导入/导出都原样保留。

