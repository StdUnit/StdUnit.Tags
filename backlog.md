# Backlog / 开发计划

> **v1.0 之前的 todo 已全部完成。** 其中的决策与理由（"当时为什么这么选"）已按专题整理到
> [docs/设计决策与边界/](docs/设计决策与边界/)（范围与契约、CI、兼容与命名、打包与发布、错误处理），
> 本文件此后**只保留"还要做什么"**。

## v1.0 之后的 todo

- [ ] 公开面基线工具（`Microsoft.CodeAnalysis.PublicApiAnalyzers` + `PublicAPI.Shipped.txt`）：留到 v1.1 之后。
- [ ] 日志、报错提示、注释文档的多语言支持。
- [ ] 代码评审后续项（背景与理由见 `docs/设计决策与边界/`）：
  - [ ] **通道的资源释放完整性**：`ModbusTcpChannel.Dispose()` 只 `Close()` 掉 `TcpClient`，没有释放 `_connSignal`（`SemaphoreSlim`）；且 `DisconnectAsync()` 不参与 `_connSignal` 的互斥，`EnsureConnectedAsync` 与它并发时可能"断开后被重连"。`OpcUaClientTagChannel.Dispose()` 只 `Close()` 会话。（根因"StopAsync 与循环并发"已修，这里只剩"Dispose 不完整"的洁癖级问题。）
  - [x] ~~**跨驱动一致性**：`TagChannelDescriptor_S7Extensions` 这个类名在 **Hjzk 与 ZLan** 里都是 S7 的复制粘贴残留~~ **已改**：改为 `TagChannelDescriptor_HjzkExtensions` / `TagChannelDescriptor_ZLanTcpExtensions`（1.0 未发布，此刻改名不构成破坏性变更）。
  - [ ] **重复代码收敛**（各驱动 `AddXxxSupport()` 的注册样板、`ToXxxTagChannelDescriptor` 的骨架、`FileAsyncCompat`、`FloatBitsCompat`、DI/DO 的 `Area` 谓词）：建议收敛到 `StdUnit.Tags.Core` 或各驱动的公共位置。注意 `FileAsyncCompat`/`FloatBitsCompat` 是跨框架垫片，收敛时要保持"调用点无 `#if`"的既有约定。
  - [ ] **运行期观测**：用 `System.Diagnostics.Metrics` 暴露每轮耗时、失败次数、重连次数、意图被拒次数（日志对"长期趋势"无能为力）。
  - [ ] **热重载 API**（`ReloadAsync(root)`）与"写后读回校验"选项。
- [ ] 工程化：
  - [ ] 覆盖率门槛：`coverlet.collector` 已引入，但尚未设门槛；
  - [ ] 启用 NetAnalyzers / `TreatWarningsAsErrors`。
- [ ] Cache 性能优化：Modbus 读路径 cache 复用——`TagCbnt<T>` 各驱动的 `ReadAsync` 每轮轮询换新 `Cache` 引用（如 `ModbusRegisterTagCbnt` 的 `this.Cache = regs`、位空间 `this.Cache = bits`），`CacheSize` 不变时可复用同一 `T[]` 消除每轮分配；需接口改动（`IModbusRegisterChannel.ReadRegistersAsync`/`IModbusBitsChannel.ReadBitsAsync` 改为写入预分配 buffer 返回元素数）
- [ ] S7 优化：底层基于 Sharp7 一个古老的实现，有两个优化的点：
    - 底层通道基于 Sharp7 一个古老的同步实现，用了 `Thread` 伪装成异步接口。将来可以改成真异步（上层异步接口不需要变更）。说明：PLC的并发吞吐能力远小于上位机，而且目前已经在单通道做了串行轮询机制，所以这个改进的收益不大。
    - 底层有很多无谓的字节拷贝和内存分配操作，借助 C# 的 `Span<T>` 和 `Memory<T>` 可以大幅优化实现。
