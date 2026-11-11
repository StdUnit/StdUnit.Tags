# Backlog / 开发计划

> 本文件只保留**还要做什么**；"当时为什么这么选"在 [docs/设计决策与边界/](docs/设计决策与边界/)。
> **已评估、决定不做**的条目（公开面基线、`WriteIntent` 旧重载、热重载、S7 连接/断开真异步等）也记在那边——
> 想推翻它们请改 `docs`，不要在本文件里重新开一条。

## 现状快照（2026-11-11）

- 版本：`2.0.0-beta8` 已发布；`2.0.0-beta9` 是 2.0.0 的最后一个 beta（计划 2026-12），随后进 RC。
- **beta9 的独占窗口已经空了**：动公开面、动驱动内部、定包清单这三件事都已完成
  （删除 `BlazorLib`、驱动释放路径收敛、S7 通道简化、包清单定稿并写明 `BlazorLib.Core` 维护模式）。
  剩下的是 RC 期收尾与 2.1+ 的增量，**都不必挤在 beta9**。
- 构建：整解 `dotnet build StdUnit.Tags.sln -c Release` = 0 警告 0 错误（`src/` 已开 `TreatWarningsAsErrors`）；
  `StdUnit.Tags.Tests`（两个 TFM）与 `Tests.NetCoreOnly`（net8.0）都绿。
- PR/push CI：整解构建（net8.0 + net472 编译）→ net8.0 主测试 → net8.0-only 测试 →
  net472-on-mono → AOT 三件套 + WebMonitor publish。
- 发布物与包清单以 [src/publish-packages.ps1](src/publish-packages.ps1) 为唯一准绳。
- 运行期观测：`StdUnit.Tags` 已暴露 4 个 `System.Diagnostics.Metrics` 指标（`stdunit.tags.*`，维度只到 `entry`）。
- 性能基准：[benchmarks/StdUnit.Tags.Benchmarks](benchmarks/StdUnit.Tags.Benchmarks)（在解决方案内，CI 只**编译**不运行）。

---

## 一、RC 前（只收尾，不动公开面）

- [ ] **2.0 迁移指南**：把各 beta 的破坏性变更汇总成"1.x → 2.0 要改什么"。现在它们散落在 CHANGELOG 的十几条
      beta 条目里，且同一处 API 常被多轮迭代（`ITagGrpRunner` 的 event→属性、`TagsProjectEventArgs.IsStarted`
      →扩展方法、S7 的 `CreateClientAndConnectAsync`→`CreateClientAndConnect`），按 beta 顺序读拼不出"我现在该怎么写"。
- [ ] **发布前复核一次**：Windows 真机全 TFM、NativeAOT 产物、WebUi 端到端（release 工作流已覆盖，要有人看结果）。
- [ ] **首次 push 后盯一次 CI**：新增的 net8.0-only 测试步骤还没在 GitHub 上跑过——确认 `--no-build` + 两步顺序
      在 4 vCPU runner 上真能跑通，而不只是本地通过。
- [ ] **定覆盖率算不算硬门槛**：codecov 已有 `patch 91%` + `project: auto ±5%`，但不确定它是不是 required check。
      要么确认它已经在拦，要么明确"不拦"——别留成"看起来有、其实靠自觉"。
- [ ] **XML 注释抽查**：README 承诺"每个公开成员都有注释"，抽查一轮，重点是本次新增与改签名的成员。

---

## 二、2.1+（增量，不阻塞 2.0）

- [ ] 日志 / 报错 / 注释文档的多语言支持——**不做预留性重构**，真要做时按资源内嵌整体改。
- [ ] 分析器加码（`AnalysisMode=All`）；`net472-on-mono` 从"硬性把关"转正或降级。