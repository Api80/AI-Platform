# Crypto Intelligence System Phase 1 MVP Implementation Plan

> 状态：Ready for implementation  
> 产品与技术范围：[PHASE1_MVP_DESIGN.md](./PHASE1_MVP_DESIGN.md)  
> 配置规范：[PHASE1_MVP_CONFIGURATION.md](./PHASE1_MVP_CONFIGURATION.md)  
> 当前状态：[PROJECT_STATUS.md](./PROJECT_STATUS.md)

## 1. Delivery Goal

交付一个可重复运行的单来源新币研究闭环：

```text
Adapter Spike
→ Reliable Ingestion
→ New Token Radar
→ Minimal Risk Filter
→ Early Momentum Strategy
→ Paper Execution
→ Historical Replay
→ Forward Paper
→ Validation Report
```

实现重点是数据正确、链路可追溯、结果可复现，而不是在 Phase 1 扩展全部平台能力。

## 2. Delivery Rules

1. 每个 Milestone 达到 Exit Criteria 后才能进入下一个；
2. 不允许在 Adapter Spike 完成前实现大量特定 Parser；
3. 不允许在 Raw Event、Checkpoint 和回放完成前优化策略；
4. 不允许使用理想价格代替 Paper Execution；
5. 不允许删除失败订单或无法退出记录；
6. 不允许在 Phase 1 引入私钥、签名或真实交易；
7. 不允许因为 AI 可以生成代码而扩大 MVP 范围；
8. 所有影响结果的参数必须进入 StrategyRun 配置快照。

## 3. Dependency Flow

```text
M0 Adapter Spike
       ↓
M1 Foundation
       ↓
M2 Ingestion and Radar
       ↓
M3 Theme and Risk
       ↓
M4 Strategy and Replay
       ↓
M5 Paper Execution
       ↓
M6 API and Dashboard
       ↓
M7 Forward Paper and Validation
```

M1 中与 Adapter 无关的工程骨架可以和 M0 后半段并行，但 M2 不能在 M0 Adapter Decision 完成前进入正式实现。

## 4. Recommended Repository Structure

```text
CryptoIntelligence.sln

src/
├── CryptoIntelligence.Domain/
│   ├── Common/
│   ├── Ingestion/
│   ├── Radar/
│   ├── Analysis/
│   ├── Strategy/
│   └── PaperTrading/
│
├── CryptoIntelligence.Application/
│   ├── Ingestion/
│   ├── Radar/
│   ├── Analysis/
│   ├── Strategy/
│   ├── PaperTrading/
│   └── Analytics/
│
├── CryptoIntelligence.Infrastructure/
│   ├── Persistence/
│   ├── Solana/
│   ├── Adapters/
│   ├── Jobs/
│   ├── Observability/
│   └── Configuration/
│
├── CryptoIntelligence.Worker/
├── CryptoIntelligence.Api/
├── CryptoIntelligence.Contracts/
└── CryptoIntelligence.Dashboard/

tests/
├── CryptoIntelligence.Domain.Tests/
├── CryptoIntelligence.Application.Tests/
├── CryptoIntelligence.Infrastructure.Tests/
├── CryptoIntelligence.IntegrationTests/
├── CryptoIntelligence.AdapterContractTests/
├── CryptoIntelligence.ReplayTests/
└── CryptoIntelligence.ArchitectureTests/

samples/
└── solana-transactions/
    └── <adapter>/<parser-version>/
```

## 5. Milestone 0: Adapter Selection Spike

> 状态：Completed（2026-07-28）
> 证据：[ADAPTER_SPIKE_REPORT.md](./ADAPTER_SPIKE_REPORT.md)

### Objective

确认一个 Launch Source 和一个 AMM/Pool 能够支撑从新币发现到模拟退出的完整闭环。

### Tasks

#### M0-01 Source Candidate Matrix

收集候选来源并记录：

- ProgramId；
- 新 Token/Pool 发现方式；
- Swap 和 Liquidity 事件；
- 历史交易可用性；
- WebSocket 发现能力；
- RPC 补采能力；
- Pool Reserve 和 Quote 能力；
- Program 版本变化频率；
- 已知限制；
- 测试样本可获得性。

输出：`ADAPTER_SPIKE_REPORT.md` 初稿。

#### M0-02 Capture Fixed Fixtures

为每个候选至少保存脱敏或公开的固定交易样本：

- Mint Created；
- Pool Created；
- Buy Swap；
- Sell Swap；
- Liquidity Added；
- Liquidity Removed；
- 同交易多事件；
- 失败交易；
- 未知或不支持版本。

Fixtures 必须保存 Signature、Slot、ProgramId、原始 Payload 和预期标准事件。

#### M0-03 Parser Prototype

验证能否稳定产生：

```text
MintCreated
PoolCreated
SwapObserved
LiquidityChanged
TokenTransferred
```

验证 EventOrdinal 和 DomainEventIndex 在重复解析时保持一致。

#### M0-04 Quote Prototype

实现最小 `QuoteExactInput` 原型，验证：

- Buy Quote；
- Sell Quote；
- Pool Fee；
- Price Impact；
- Reserve State；
- 无法 Quote 的失败原因。

#### M0-05 Discovery and Backfill Prototype

验证：

- WebSocket 能发现相关 Signature；
- RPC 能获得完整交易；
- 可以按 Slot/Signature 补采；
- 暂时返回空时能够重试；
- 能刷新 commitment/finality。

#### M0-06 Adapter Decision

选定：

- Launch Adapter；
- Pool Adapter；
- ProgramIds；
- ParserVersion；
- StartSlot；
- RPC Source；
- 已知限制；
- MVP 数据覆盖声明。

更新 `PHASE1_MVP_CONFIGURATION.md` 的必填项。

### Tests

- Fixtures 可重复解析；
- 同一输入输出完全一致；
- 同交易相同类型多事件不冲突；
- 买卖 Quote 方向和单位正确；
- 未知版本明确失败；
- RPC 暂时空和永久不可用可区分。

### Exit Criteria

- 一个 Launch Source 和一个 Pool Adapter 被明确选定；
- 至少一条完整 Mint → Pool → Buy → Sell → Liquidity Change 样本链路；
- Parser 和 Quote 原型可运行；
- 历史补采路径已证明；
- 已知限制已记录；
- 不存在阻止模拟退出的结构性数据缺失。

## 6. Milestone 1: Foundation

> 状态：In progress
> 实施记录：[M1_FOUNDATION.md](./M1_FOUNDATION.md)

### Objective

建立可编译、可测试、可迁移和可观测的模块化单体工程。

### Tasks

#### M1-01 Solution Scaffold

- 创建 .NET 8 Solution；
- 创建 Domain、Application、Infrastructure、Worker、API、Contracts；
- 创建测试项目；
- 配置统一 nullable、analyzer 和 warning 策略；
- 固定 SDK 和依赖版本。

#### M1-02 Architecture Rules

建立自动化依赖检查：

```text
Domain → no Infrastructure
Application → Domain
Infrastructure → Application/Domain
API/Worker → Composition Root
```

禁止模块直接访问其他模块内部实现。

#### M1-03 Core Value Objects

实现并测试：

- Chain；
- Network；
- Slot；
- TransactionSignature；
- ProgramId；
- TokenAddress；
- WalletAddress；
- RawAmount；
- BasisPoints；
- UTC Time。

#### M1-04 Configuration System

- 实现配置加载和 Schema 校验；
- 实现 ConfigurationVersion 和 Hash；
- 区分普通配置和 Secret Reference；
- 未填必需策略参数时拒绝正式 Run；
- 保存 Canonical JSON 配置快照。

#### M1-05 PostgreSQL Foundation

- 配置 EF Core/PostgreSQL；
- 建立迁移工具；
- 建立命名、精度、UTC 和并发规则；
- 建立本地开发数据库；
- 建立空库迁移测试。

#### M1-06 Observability Foundation

- 结构化日志；
- CorrelationId；
- Metrics；
- Health Checks；
- Error 分类；
- Secret 日志过滤。

#### M1-07 CI

- Restore、Build、Test；
- Format/Analyzer；
- Architecture Tests；
- Migration Test；
- 禁止敏感配置提交检查。

### Exit Criteria

- Solution 全量构建通过；
- 单元和架构测试通过；
- 空库可以迁移；
- 配置验证能阻止缺失参数；
- Health Check 和日志可用；
- CI 在干净环境通过；
- 不存在私钥和真实交易接口。

## 7. Milestone 2: Reliable Ingestion and New Token Radar

### Objective

可靠保存支持来源的链上事件，并构建新 Token/Pool 候选和滚动市场窗口。

### Tasks

#### M2-01 Raw Event Schema

实现：

- RawBlockchainEvent；
- EventId/EventOrdinal；
- CanonicalStatus；
- ProcessingStatus；
- 唯一约束；
- 时间分区和关键索引。

#### M2-02 Checkpoint and Slot State

实现：

```text
ObservedThroughSlot
PersistedThroughSlot
ProcessedThroughSlot
FinalizedThroughSlot
ReconciledThroughSlot
```

维护 Slot 完成状态，水位不能越过缺口。

#### M2-03 WebSocket Discovery

- 连接管理；
- 自动重连；
- Subscription 配置；
- Signature 去重；
- 低延迟发现指标；
- 断线记录。

#### M2-04 RPC Retrieval and Backfill

- 获取交易详情；
- commitment 刷新；
- Slot/Signature 补采；
- Source 超时和限流；
- 指数退避；
- 备用 Source 接口；
- Gap 记录。

#### M2-05 Durable Dispatch

- At-least-once；
- Worker Lease；
- Pending/Processing/Completed；
- RetryableFailure；
- DeadLetter；
- 幂等 Consumer；
- Lease 超时回收。

#### M2-06 Adapter Integration

将 Spike Parser 迁移为正式 Adapter：

- Adapter Contract；
- ParserVersion；
- 固定 Fixtures；
- Known Limitations；
- Unknown Version Failure。

#### M2-07 Domain Projection

实现：

- Token；
- LiquidityPool；
- SwapEvent；
- LiquidityEvent；
- MarketSnapshot；
- 最小 Wallet/Holder 投影。

#### M2-08 Candidate Builder

实现 TokenCandidate 状态：

```text
Discovered
Observing
Eligible
Rejected
Expired
```

#### M2-09 Rolling Window

实现配置化滚动窗口和 FeatureSnapshot：

- Price Change；
- Buy/Sell；
- Unique Buyers；
- Transaction Velocity；
- Liquidity Change；
- No Trade Duration；
- Price Impact。

#### M2-10 Replay Infrastructure

- 按 Event Time 重放；
- 可控制时间推进；
- 相同数据产生相同投影；
- 支持 Parser/Projection 重建；
- Replay 与实时 Worker 使用相同 Application Handler。

### Tests

- 重复事件；
- 多事件序号；
- 乱序；
- WebSocket 断线；
- RPC 暂时空；
- Worker 崩溃；
- Lease 超时；
- Dead Letter；
- canonical 回退；
- Checkpoint 遇到 Gap 停止；
- Replay 一致性；
- 一个 Token 多 Pool；
- Quote Token 价格语义。

### Exit Criteria

- 支持来源的新 Token/Pool 可以被发现；
- Raw Event 永久落库后才进入处理；
- 断线可以补采；
- 重复事件不产生重复投影；
- Gap 阻止 Reconciled 水位推进；
- 相同事件重放结果一致；
- Radar 可以输出候选和滚动指标；
- 连续采集期间指标符合 Phase 1 SLO 基线。

## 8. Milestone 3: Theme and Minimal Risk

### Objective

使用简单主题规则和最小风险过滤生成可解释候选状态。

### Tasks

#### M3-01 Theme Configuration

- Hot Keywords；
- Blocked Keywords；
- Theme Valid Time；
- Name/Symbol 规范化；
- ThemeMatch 和原因；
- Theme ConfigurationVersion。

#### M3-02 Sell Quote Check

- 使用 Adapter Quote 验证 Sell Path；
- 保存 Quote 输入和结果；
- 区分暂时失败与结构性不支持；
- 数据过期时拒绝使用。

#### M3-03 Authority Risk

- Mint Authority；
- Freeze Authority；
- Adapter 已知权限风险；
- Hard Reject 原因。

#### M3-04 Holder Risk

- Creator Holding；
- Top Holder Concentration；
- Snapshot AsOfTime；
- 缺失数据处理；
- 版本化阈值。

#### M3-05 Liquidity and Impact Risk

- Minimum Quote Reserve；
- Maximum Entry Impact；
- Liquidity Drop；
- Stale State；
- Unsupported Pool Version。

#### M3-06 Risk Assessment

输出：

```text
OverallScore
RiskLevel
HardReject
RuleResults
Reasons
InputAsOfTime
RiskModelVersion
```

#### M3-07 Candidate State Rules

- Hard Reject → Rejected；
- 超过 Entry Age → Expired；
- 最小观察窗口未完成 → Observing；
- 风险和基础条件通过 → Eligible。

### Tests

- Theme 大小写和规范化；
- Blocked Keyword 优先；
- Sell Quote 失败；
- stale market state；
- authority Hard Reject；
- Holder 边界值；
- Liquidity 和 Price Impact 边界；
- 缺失数据不默认为低风险；
- Risk Version 不覆盖历史。

### Exit Criteria

- 每个候选具有 Theme 和 Risk 解释；
- Hard Reject 不会进入策略；
- Sell Quote 无效的候选不能 Eligible；
- 所有判断带 AsOfTime 和版本；
- 缺失或过期数据以保守方式处理。

## 9. Milestone 4: Strategy and Historical Replay

### Objective

实现一套版本化 Early Momentum 进入/退出策略，并完成不使用未来数据的历史回放。

### Tasks

#### M4-01 Strategy Definition and Run

- StrategyDefinition；
- StrategyRun；
- Configuration Snapshot；
- CodeVersion；
- RunType；
- 数据范围；
- 状态和失败原因。

#### M4-02 Entry Signals

实现配置化条件：

- Token Age；
- Momentum；
- Buy/Sell Ratio；
- Unique Buyers；
- Transaction Velocity；
- Liquidity Growth；
- Price Impact；
- Theme；
- Risk。

#### M4-03 Decision Engine

- Enter；
- Hold；
- Reject；
- Ignore；
- DecisionSignal 关系；
- Account/Position Constraints；
- 解释和版本。

#### M4-04 Exit Signals

- Take Profit；
- Stop Loss；
- Max Holding；
- Liquidity Drop；
- Momentum Decay；
- Sell Pressure；
- No Trade Timeout；
- Emergency Exit。

#### M4-05 Event-Time Replay

- Strategy 只读取 AsOfTime 之前的数据；
- 模拟时钟；
- 时间顺序；
- Snapshot 选择；
- 相同 Run 可复现。

#### M4-06 Experiment Registry

保存：

- Hypothesis；
- 参数；
- 数据范围；
- 结果；
- 决策；
- 失败实验。

#### M4-07 Dataset Split

- Development 50%；
- Validation 20%；
- OOS 30%；
- 不随机打乱；
- OOS 在参数冻结前不可读取。

### Tests

- Future Data Leakage；
- AsOfTime 边界；
- Signal 不创建 Order；
- Hard Reject 阻止 Enter；
- 超过 Entry Age 不进入；
- Exit 优先级；
- 相同配置结果一致；
- 配置变化生成新 Run；
- OOS 隔离。

### Exit Criteria

- 一套进入和退出策略完成；
- 所有条件可配置；
- 任意 Decision 可追溯；
- Historical Replay 不使用未来数据；
- Development/Validation/OOS 明确隔离；
- 失败实验被保留。

## 10. Milestone 5: Paper Execution and Portfolio

### Objective

实现保守、确定性和可复现的模拟成交、账户和持仓。

### Tasks

#### M5-01 Paper Account

- Initial Capital；
- Available/Reserved Cash；
- Position Limits；
- Daily Loss Limit；
- Run Status。

#### M5-02 Paper Order

- Decision → Order；
- Side、Quantity、Notional；
- Submitted、Filled、Failed、Expired；
- 不允许 Signal 直接创建 Order。

#### M5-03 Execution Attempt

按照 `paper-execution-v1`：

- Eligible Execution Time；
- Market Snapshot；
- Adapter Quote；
- Fees；
- Price Impact；
- Additional Slippage；
- Failure Reason。

#### M5-04 Paper Fill

- 原子 AMM Fill；
- Quantity、Price、Fee、Slippage；
- Fill 总量不超过 Order；
- 相同 Attempt 不重复 Fill。

#### M5-05 Position Projection

- Average Entry；
- Quantity；
- Realized/Unrealized PnL；
- HoldingAtRisk；
- Position 只由 Fill 更新。

#### M5-06 Conservative Valuation

- 使用可执行 Sell Quote；
- 不使用简单 LastPrice；
- ValuationUnavailable；
- Zero Exit Value Stress。

#### M5-07 Equity and Performance

- EquitySnapshot；
- Drawdown；
- Gross/Net Return；
- Fee/Slippage；
- Failure/ExitUnavailable。

### Tests

- 资金不足；
- 仓位限制；
- 延迟选择快照；
- stale market；
- Quote 失败；
- Price Impact；
- Fees；
- ExitUnavailable；
- Position 不变量；
- 失败订单计入报告；
- 保守估值；
- Run 重复性。

### Exit Criteria

- Enter/Exit 能形成完整 Order/Fill/Position；
- 费用、滑点、延迟和失败被保存；
- 无法退出不会按理想价格平仓；
- 相同输入和模型产生相同结果；
- 账户不变量测试通过；
- 压力场景可以运行。

## 11. Milestone 6: API, Dashboard and Operations

### Objective

提供最小只读产品界面和运行监控。

### Tasks

#### M6-01 Read API

实现 MVP Design 中的 Radar、Strategy、Paper 和 Operations API。

要求：

- Pagination；
- UTC；
- 稳定错误契约；
- 查询超时；
- 大型报告异步生成；
- DTO 不直接暴露 EF Entity。

#### M6-02 Radar Dashboard

- Candidate List；
- Theme/Risk；
- Token Age；
- Price/Liquidity；
- Candidate State；
- 数据更新时间。

#### M6-03 Candidate Detail

- Timeline；
- Feature；
- Risk；
- Signal；
- Decision；
- Order/Fill；
- Exit Failure。

#### M6-04 Strategy Dashboard

- Run Config；
- Open Positions；
- Equity Curve；
- Drawdown；
- Profit Factor；
- Failure Metrics；
- 场景对比。

#### M6-05 Operations Dashboard

- 五类水位；
- Lag；
- Gap；
- Retry/Dead Letter；
- WebSocket/RPC；
- Backup Status。

#### M6-06 Authentication

- Dashboard 用户认证；
- 管理接口授权；
- Secret 不返回客户端；
- 审计管理操作。

#### M6-07 Runbooks

- WebSocket 断线；
- RPC Source 故障；
- Gap；
- Checkpoint 停滞；
- Dead Letter；
- Parser Unsupported；
- Backup Restore。

### Exit Criteria

- 用户可以查看完整新币和策略链路；
- Dashboard 默认只读；
- 数据更新时间可见；
- 管理接口受保护；
- 运行状态和数据缺口可识别；
- SLO 告警和 Runbook 可用。

## 12. Milestone 7: Forward Paper and Validation

### Objective

使用冻结策略和实时 finalized/reconciled 数据验证历史结果是否能够延续。

### Tasks

#### M7-01 Freeze Baseline

冻结：

- AdapterVersion；
- ParserVersion；
- FeatureSetVersion；
- RiskModelVersion；
- StrategyVersion；
- ExitStrategyVersion；
- ExecutionModelVersion；
- ConfigurationVersion。

#### M7-02 Historical Validation

执行：

- Development；
- Validation；
- OOS；
- Walk-Forward；
- 参数敏感性；
- 执行压力场景。

#### M7-03 Forward Paper Run

- 实时数据；
- 配置冻结；
- 不使用事后修正数据做实时 Decision；
- canonical 回退保留审计；
- 所有人工干预记录；
- 每日运行报告。

#### M7-04 Capacity Review

稳定采集 7 天后记录：

- Event/Day；
- Raw Bytes/Day；
- Index Growth；
- Snapshot Growth；
- Backup Size；
- Restore Duration；
- RPC Error/Limit；
- SLO 达成率。

#### M7-05 Final Research Report

报告：

- 数据覆盖；
- 候选数量；
- 风险过滤；
- 策略交易；
- OOS 指标；
- Forward Paper；
- 费用、滑点和失败；
- ExitUnavailable；
- 参数敏感性；
- 结论和下一步。

### Exit Criteria

- 数据和策略版本冻结；
- Historical OOS 完成；
- Forward Paper 完成一个独立研究周期；
- 所有失败和人工操作被记录；
- 按验证协议给出状态：

```text
InsufficientEvidence
NeedsMoreEvidence
ValidatedResearchCandidate
Rejected
```

- 不因为未通过而修改旧 OOS 结果；
- 只有通过独立评审后才考虑 Phase 2 或真实执行讨论。

## 13. Cross-Cutting Test Matrix

### Unit Tests

- 值对象；
- 状态机；
- Risk Rule；
- Strategy Rule；
- Fee/Slippage；
- Position/Account 不变量；
- Configuration Validation。

### Contract Tests

- Adapter Fixtures；
- Parser Output；
- Quote；
- API DTO；
- Error Contract。

### Integration Tests

- PostgreSQL；
- Migration；
- Lease；
- Retry/Dead Letter；
- Checkpoint；
- Replay；
- API 查询。

### Failure Tests

- WebSocket 断线；
- RPC 超时和限流；
- 进程崩溃；
- 数据库短时不可用；
- Parser Unsupported；
- Gap；
- stale market；
- 无法退出。

### Research Tests

- Future Leakage；
- OOS 隔离；
- Run Reproducibility；
- Parameter Sensitivity；
- Execution Stress；
- Failure Inclusion。

## 14. Database Migration Sequence

```text
Migration 001: Common and Run Configuration
Migration 002: Raw Event and Checkpoint
Migration 003: Token, Pool, Swap, Liquidity
Migration 004: Candidate, Theme, Feature, Risk
Migration 005: Strategy, Signal, Decision
Migration 006: Account, Order, Fill, Position, Equity
Migration 007: Performance and Experiment Registry
Migration 008: Partition and Operational Indexes
```

每个迁移必须支持空库测试，并记录是否需要数据回填。

## 15. Definition of Done

任务只有满足以下条件才算完成：

- 实现已提交；
- 单元/集成测试通过；
- 错误和失败路径覆盖；
- 指标和日志存在；
- 配置和版本被保存；
- 文档更新；
- 没有引入 Phase 1 外能力；
- CI 通过；
- 对应 Exit Criteria 可验证。

## 16. Scope Control

任何以下需求进入 Phase 1 前必须单独评审：

- 第二个 Launch Source；
- 第二个 Pool Adapter；
- 完整 Wallet Profile；
- Smart Money；
- 社交媒体采集；
- AI 直接 Signal；
- Live Trading；
- 多链；
- 微服务拆分。

默认决策是延期，不因实现看起来简单而自动加入。

## 17. Key Risks and Responses

| Risk | Response |
|---|---|
| Adapter 数据不足 | M0 先验证完整买卖闭环 |
| WebSocket 漏数据 | RPC 补采与 Reconciled 水位 |
| Parser 版本变化 | Raw Payload、ParserVersion、Fixtures |
| Paper 收益虚高 | 保守 Quote、延迟、费用、失败和压力测试 |
| 无法退出 | ExitUnavailable、保守估值、Zero Exit Stress |
| 策略过拟合 | 时间切分、OOS、Walk-Forward、实验注册 |
| AI 扩大范围 | AI 只做辅助，不控制核心交易链路 |
| 数据快速增长 | 分区、7 天容量报告、备份和归档 |
| 项目范围失控 | Milestone Gate 和默认延期规则 |

## 18. First Implementation Action

开发从 `M0-01 Source Candidate Matrix` 开始，而不是直接创建大量业务代码。

Adapter Spike 完成后，立即固化：

- ProgramIds；
- Fixtures；
- ParserVersion；
- Quote 方法；
- Backfill 方法；
- Known Limitations。

随后进入 M1 工程骨架和 M2 可靠采集。
