# Changelog

本文件记录**用户可见**的变化。版本号遵循[语义化版本](https://semver.org/lang/zh-CN/) `<major>.<minor>.<patch>`：
`v1.0` 之前，每个 `minor` 跳变都可能引入新特性与破坏性更新；`v1.0` 之后，只在 `major` 跳变时才引入破坏性更新。

## 1.0.0

首个正式版本。此前的所有版本都只发布在测试源（`https://baget.stdunit.com/v3/index.json`），从v1.0.0起，正式版本一律发布到 nuget.org，测试版本可能会先于正式版之前发布在测试源上。

相对上一版本 `0.16.0` 的变化如下。

### 破坏性变更

- **`IsScaned` 重命名为 `IsScanned`**（`ITag` / `ITagCbnt` / `ITagGrp`）。
- **加载期异常族重构**：新增抽象基类 `TagsProjectLoadException`（带 `Location` 位置上下文），加载期错误统一派生自它——
  `TagsProjectXmlException`（XML 值不合法）、`TagsProjectAddressException`（地址无法解析）、
  `TagsProjectConfigurationException`（配置语义不自洽）、`TagsProjectValidationException`（校验器聚合错误）。
  `TagsProjectSchemaException` 不再是直接派生自 `Exception` 的独立类型，改为 `TagsProjectValidationException` 的派生（仅用于 XSD 校验）；
  校验错误始终是聚合形态（`Errors` 逐条列出，`Message` 里也拼进全部条目）。
  **此前按 `TagsProjectSchemaException` 或裸 `Exception` 捕获加载错误的代码需要调整。**
- **运行期不再抛裸 `Exception`**：库内改为语义贴近的 BCL 类型（`InvalidOperationException` / `ArgumentException` /
  `ArgumentNullException` / `ArgumentOutOfRangeException` / `InvalidCastException` / `KeyNotFoundException`），
  并在消息里补上通道名、操作、地址、底层错误等上下文。只写 `catch (Exception)` 的调用方不受影响。
- **S7：组合（`TagCbnt`）自身必须写绝对地址**。写成相对地址（如 `$$100`）会在**加载期**被拒绝 （`TagsProjectAddressException`）——这种配置以前从未真正生效过：`BlockSpecified=false` 只由 `$$` 解析产生，
  而没有任何代码为组合自身的相对地址去找上层解析。
- **SimpleFiles：移除对 废弃XML 属性 `AutoCreateFile`的支持**，现在只认 camelCase 风格的 `autoCreateFile`。仍写旧名会在加载期抛`TagsProjectConfigurationException` 并提示迁移。
- **OpcUa：读取失败 = 抛异常**。设备报的读取错误（Bad 质量）不再"打日志跳过"，改为抛异常，交给 runner 的失败重试与重连； `Uncertain` 仍按原值采集，如果需要自定义如何解释什么是“坏点”，自行在注册时设定“”。

虽然都叫破坏性更新，但是真正影响较大的只有第一条“IsScanned”的 typo fix。

### 修复和改进

- OpcUa：`WriteAsync` 不再丢弃调用方的 `CancellationToken`；
- SimpleFiles：解析文件内容失败时，异常消息补上测点名与文件路径（`ParseValue` 子类只拿得到文本，路径由基类补）。
- 日志: 不再把异常拍平成 `ex.Message`，记录更多上下文。

