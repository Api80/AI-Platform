# Phase 1 Adapter Selection Spike Report

> 更新时间：2026-07-28  
> Milestone：M0 Adapter Selection Spike  
> 状态：M0 Completed / Go

## 1. Executive Decision

Phase 1 首个闭环有条件选择：

- Launch Adapter：Raydium LaunchLab v1；
- Pool Adapter：Raydium CPMM v1；
- Network：Solana mainnet-beta；
- Quote Token（首选）：WSOL；
- Parser 基线：固定 Raydium 官方 Anchor IDL，自主实现只读 .NET Parser；
- Quote 基线：CPMM Exact Input，使用链上池状态计算，不依赖前端价格；
- 发现与补采：WebSocket 只负责低延迟发现，RPC `getSignaturesForAddress` + `getTransaction` 负责持久化与补采。

该组合的主要优势是 LaunchLab 完成 bonding curve 后可以直接迁移到同一生态的 CPMM，官方提供版本化 IDL、SDK、ProgramId 和池查询接口，最适合 Phase 1 的“发现 → 观察 → 模拟买入 → 迁移后继续观察/模拟卖出”单来源闭环。

本结论不是盈利结论。M0 技术退出条件已经完成，可以进入 M1 工程骨架；正式 Forward Paper 前仍必须配置独立主/备 RPC。

## 2. Candidate Matrix

| Candidate | Launch discovery | Graduation/exit pool | Official artifacts | Quote complexity | Historical recovery | MVP assessment |
|---|---|---|---|---|---|---|
| Raydium LaunchLab + CPMM | LaunchLab Program logs/accounts | Native migration to CPMM | Official docs, versioned IDLs, SDK, ProgramIds | Low; constant-product Exact Input | Finalized signatures and transactions verified | Selected |
| Meteora DBC + DAMM v2 | DBC Program logs/accounts | Native migration to DAMM | Open-source program and SDK | Medium/high; configurable curves and dynamic fees | Technically feasible | Phase 2 candidate |
| Other launch sources | Source-specific | Source-specific/multiple pools | Quality varies by source | Varies | Must be separately proven | Deferred |

Meteora DBC 是有效备选，但其多段曲线、动态费用和 DAMM 迁移组合会扩大 Phase 1 Parser 与 Quote 面积。当前目标是验证策略假设，因此先选择边界更窄的 Raydium 组合。

## 3. Pinned Artifacts

### Programs

| Component | Mainnet ProgramId | Devnet ProgramId |
|---|---|---|
| Raydium LaunchLab | `LanMV9sAd7wArD4vJFi2qDdfnVhFxYSUg6eADduJ3uj` | `DRay6fNdQ5J82H7xV6uq2aV3mNrUZ1J4PgSKsWgptcm6` |
| Raydium CPMM | `CPMMoo8L3F4NbTegBCKVNunggL7H1ZpdTHKxQB5qKP1C` | `DRaycpLY18LhpbydsBWbVJtxpNv9oXPgjRSfpF2bWpYb` |

### Version Pins

- Raydium IDL repository commit: `e7e0c96fe77bcf6a020b84a44c47a722aac8e359`;
- LaunchLab IDL blob: `70186e3f02c2bead7e5a9a92453104df853683ed`;
- CPMM IDL blob: `923d272a4e56643ef7fece05ca137c036b0295de`;
- Raydium SDK v2 reference commit: `fb2d829a559f9b6ca95922e4e6c69e3b5bddc95c`;
- SDK reference version at validation time: `0.2.59-alpha`.

Production ParserVersion 建议命名为：

- `raydium-launchlab-e7e0c96-v1`；
- `raydium-cpmm-e7e0c96-v1`。

SDK v2 当前采用 GPL-3.0。Phase 1 不应直接复制或嵌入 SDK 代码；生产实现使用官方 IDL、公开账户布局和独立实现的只读解析/报价逻辑。若未来需要引入 SDK，必须先单独完成许可证评审。

## 4. Mainnet Evidence

固定样本清单位于 [samples/adapter-spike/raydium-launchlab-cpmm/fixture-manifest.json](./samples/adapter-spike/raydium-launchlab-cpmm/fixture-manifest.json)。当前已验证：

- LaunchLab `Initialize` + 初始 `BuyExactIn`；
- LaunchLab 独立 `BuyExactIn`；
- LaunchLab `SellExactIn`；
- 同交易多 Program 指令和多个 `Program data` 事件；
- 失败交易可由 signature 状态保留，而不是被过滤；
- CPMM `SwapBaseInput`；
- Raydium 官方池接口返回 `launchMigratePool=true` 的 CPMM 主网池；
- 两个 ProgramId 均可通过公共 RPC 查询 finalized signatures 和完整历史 transaction。

### Confirmed Migration Pool

- CPMM Pool：`Q2sPHPdUWFMg7M7wwrQKLrn619cAucfRsmhVJffodSp`；
- ProgramId：`CPMMoo8L3F4NbTegBCKVNunggL7H1ZpdTHKxQB5qKP1C`；
- Pair：WSOL / `Dz9mQ9NzkBcCsuGPFJ3r1bS4wgqKMHBPiVuniW8Mbonk`；
- Raydium API 标记：`launchMigratePool=true`。

这证明 LaunchLab → CPMM 的结构性退出路径存在；它不保证任意 Token 都会迁移，也不保证策略能在流动性恶化时成交。

## 5. Quote Prototype Result

使用上述 CPMM 池在验证时的公开池快照：

- WSOL reserve：`12404.532310903`；
- Token reserve：`16137545.623432`；
- trading fee rate：`0.0025`；
- creator fee rate：`0.0005`，该样本池为输入侧收取。

以恒定乘积 Exact Input 原型计算：

| Direction | Input | Estimated output | Trading + creator fee | Total impact |
|---|---:|---:|---:|---:|
| Buy | 0.1 WSOL | 129.702622 Token | 0.00030 WSOL | 30 bps |
| Sell | 1000 Token | 0.766321904 WSOL | 3 Token | 30 bps |

整数除法、fee 向上取整和恒定乘积输出向下取整已与固定 commit `fb2d829a` 的 Raydium SDK v2 `CurveCalculator.swapBaseInput`、`CpmmFee.tradingFee` 和 `ConstantProductCurve.swapBaseInputWithoutFees` 逐项交叉核对。

本结果只证明双向单位、费用和价格冲击计算路径可行，不是生产报价。正式实现必须：

1. 从同一 `AsOfSlot` 的链上 PoolState/Vault 读取储备；
2. 使用整数 raw amount，禁止用浮点数记账；
3. 明确 Token A/B 方向和 decimals；
4. 保存 fee、price impact、state slot 和失败原因；
5. 对 stale、unsupported、insufficient liquidity 明确失败。

## 6. Discovery and Backfill Result

### Verified

- `getSignaturesForAddress` 能按 ProgramId 和 Pool account 返回 finalized signatures；
- `getTransaction` 能恢复 instruction、inner instruction、logs、error 和 loaded addresses；
- LaunchLab 与 CPMM 均已取得 finalized 历史样本；
- 失败 signature 可以保留 `InstructionError`；
- 可使用 `before` 分页补采历史记录。
- LaunchLab WebSocket `logsSubscribe` 捕获 signature 后可立即通过 RPC 取得 confirmed transaction；
- CPMM WebSocket `logsSubscribe` 捕获 signature 后可立即通过 RPC 取得 confirmed transaction；
- 原型包含 RPC 暂时空结果重试，并可读取 confirmation status。

### Operational follow-up

- Worker 自动重连、长期延迟指标和主/备 RPC 切换进入 M2 可靠采集实现；
- 正式 Forward Paper 前必须选择付费主 RPC 和独立备用 RPC。

公共 Solana RPC 在批量抓取时出现明显限流，因此它只适合开发验证，不应作为正式 Forward Paper 的唯一数据源。

## 7. Parser Contract

原型和正式 Adapter 必须稳定产生：

```text
PoolCreated
SwapObserved
LiquidityChanged
TokenTransferred
```

`MintCreated` 可以来自 LaunchLab Initialize 交易中的 Token Program 指令，但需要与 LaunchLab PoolCreated 分开建模。

建议稳定键：

```text
EventId = chain + network + signature + eventOrdinal
DomainEventId = EventId + parserVersion + domainEventIndex
```

排序规则必须只依赖交易内位置：outer instruction index、inner instruction group/index、log/event ordinal；重复解析同一 payload 必须得到相同结果。

未知 discriminator、未知账户长度或 ProgramId/IDL 不匹配时，必须产生 `UnsupportedProgramVersion`，不得猜测解析。

## 8. Known Limitations

- 已固化样本足以通过 M0，但尚未覆盖所有 Token-2022 和未来 Program 版本；
- Raydium API 只能辅助发现和交叉验证，不能成为研究事实来源；
- LaunchLab 的正式实现应以链上 payload 和固定 IDL 为准；
- 公共 RPC 会限流，需要配置付费主源和独立备用源；
- Token-2022、transfer fee、动态费或未知池版本必须先拒绝，不在 Phase 1 静默兼容；
- 单一 Raydium 来源不能代表整个 Solana 新币市场；
- `launchMigratePool=true` 只证明迁移关系，不保证任何时点可退出。

## 9. M0 Progress

| Task | Status | Evidence / remaining work |
|---|---|---|
| M0-01 Candidate Matrix | Completed | Raydium selected; Meteora retained as backup |
| M0-02 Fixed Fixtures | Completed | 10 raw payloads: init/buy/sell/failed/multi-event/migration/deposit/withdraw/CPMM swap |
| M0-03 Parser Prototype | Completed | Fixed IDL discriminator, self-CPI event, Token transfer and deterministic replay verified |
| M0-04 Quote Prototype | Completed | Integer exact-input quote, trading fee, creator fee and official reference cross-check verified |
| M0-05 Discovery/Backfill | Completed | LaunchLab/CPMM WebSocket discovery and immediate RPC retrieval verified |
| M0-06 Adapter Decision | Go | ProgramIds, IDL pins, ParserVersion and fixture StartSlot fixed |

## 10. Exit Gate

M0 Exit Gate 已通过：

- 原始 JSON payload 与固定 IDL 已进入仓库；
- 10 个 Fixture 的 manifest 断言和重复解析测试通过；
- 买入、卖出、迁移、CPMM Swap、加/减流动性和失败交易均有样本；
- 未知 IDL discriminator 会显式抛出 `UnsupportedProgramVersionException`；
- WebSocket 发现与 RPC 补采已在 LaunchLab、CPMM 各验证一次；
- Fixture 覆盖起始 Slot 固定为 `339103624`。

下一阶段进入 M1 Foundation。公共 RPC 仅作为开发验证源，付费主源和独立备用源是 Forward Paper 启动门槛，不阻塞 M1。

## 11. Official References

- [Raydium versions and migration](https://docs.raydium.io/protocol-overview/versions-and-migration)
- [Raydium LaunchLab overview](https://docs.raydium.io/user-flows/launchlab-overview)
- [Raydium CPMM accounts and math](https://docs.raydium.io/products/cpmm/accounts)
- [Raydium Anchor IDLs](https://docs.raydium.io/sdk-api/anchor-idl)
- [Raydium program addresses](https://docs.raydium.io/zh/reference/program-addresses)
- [Raydium SDK v2](https://github.com/raydium-io/raydium-sdk-V2)
- [Raydium IDL repository](https://github.com/raydium-io/raydium-idl)
- [Meteora Dynamic Bonding Curve](https://github.com/MeteoraAg/dynamic-bonding-curve)
