# Crypto Intelligence System Phase 1 Development Plan

> 状态：Phase 1 实施计划基线  
> 上位架构：[DESIGN_PROPOSAL_V2.md](./DESIGN_PROPOSAL_V2.md)  
> 领域模型：[DOMAIN_MODEL.md](./DOMAIN_MODEL.md)  
> 数据模型：[DATA_MODEL_DESIGN.md](./DATA_MODEL_DESIGN.md)  
> 关键决策：[adr/](./adr/)  
> 专项规范：[PAPER_EXECUTION_MODEL_V1.md](./PAPER_EXECUTION_MODEL_V1.md)、[STRATEGY_VALIDATION_PROTOCOL_V1.md](./STRATEGY_VALIDATION_PROTOCOL_V1.md)、[OBSERVABILITY_SLO_PHASE1.md](./OBSERVABILITY_SLO_PHASE1.md)

## 1. Goal

完成 Solana 新币雷达、风险分析和 Paper Trading 基础系统，验证事件驱动的新币策略是否具有统计价值，并建立可以扩展到 Wallet Intelligence 和 AI Intelligence 的数据底座。

Phase 1 不进行真实交易，不保存私钥，不执行链上签名。

## 2. Delivery Principles

1. 先建立可靠数据链路，再开发策略；
2. 每个 Milestone 必须通过验收门槛后进入下一阶段；
3. 原始数据、分析结果、决策和交易结果全部可追溯；
4. 每个模块同时交付实现、测试、指标和运行说明；
5. 规则、策略、Feature 和 Execution Model 必须版本化；
6. 先采用模块化单体，不在 Phase 1 过早拆分微服务；
7. 不以自动盈利作为开发目标，以数据正确性和策略可验证性作为目标。

## 3. Recommended Solution Structure

```text
CryptoIntelligence.sln

src/
├── CryptoIntelligence.Domain
├── CryptoIntelligence.Application
├── CryptoIntelligence.Infrastructure
├── CryptoIntelligence.Worker
├── CryptoIntelligence.Api
└── CryptoIntelligence.Contracts

tests/
├── CryptoIntelligence.Domain.Tests
├── CryptoIntelligence.Application.Tests
├── CryptoIntelligence.Infrastructure.Tests
├── CryptoIntelligence.IntegrationTests
└── CryptoIntelligence.ReplayTests
```

模块边界：

```text
Ingestion
EventStore
EventDispatching
TokenAnalysis
MarketAnalysis
WalletAnalysis
Features
Risk
Signals
Decisions
PaperTrading
Analytics
```

## 4. Milestone 1: Foundation and Contracts

### Goal

建立工程基础、领域契约、数据库和最低可观测性。

### Tasks

- 创建 .NET 8 Solution 和项目结构；
- 建立模块依赖规则；
- 定义 Chain、Network、Address、Signature、Slot 等值对象；
- 定义 Event Envelope 和 Domain Event 契约；
- 建立 PostgreSQL、EF Core 和迁移流程；
- 建立结构化日志、CorrelationId 和健康检查；
- 建立配置管理和 RPC 凭据安全加载；
- 建立单元测试、集成测试和基础 CI；
- 固化 UTC、金额精度和版本字段规范。

### Deliverables

- 可编译、可测试的 Solution；
- 初始数据库迁移；
- Event Envelope 契约；
- 健康检查和结构化日志；
- 工程运行说明。

### Exit Criteria

- 架构依赖测试通过；
- 数据库可以从空库完整迁移；
- 关键值对象校验测试通过；
- 日志中不存在凭据；
- Phase 1 工程中不存在私钥和签名接口。

## 5. Milestone 2: Reliable Blockchain Ingestion

### Goal

建立可断线恢复、可补采、可去重、可重试的链上原始数据采集能力。

### Tasks

- 接入 Solana WebSocket，用于低延迟发现；
- 接入 Solana RPC，用于交易详情、状态确认和补采；
- 实现 RawBlockchainEvent Store；
- 实现 IngestionCheckpoint；
- 实现包含 EventOrdinal 的确定性 EventId 和唯一约束；
- 实现 At-least-once 投递、Worker Lease 和幂等 Consumer；
- 实现 Transaction Parser 和 ParserVersion；
- 实现 Pending、Processing、Completed、RetryableFailure、DeadLetter 状态；
- 实现 Observed、Persisted、Processed、Finalized、Reconciled 五类连续水位；
- 实现 Observed、Confirmed、Finalized、Reverted canonical 状态；
- 实现 provisional 派生结果失效和投影重建；
- 实现进程重启恢复；
- 实现 WebSocket 断线后的 Slot 范围补采；
- 实现 RPC 超时、限流、重试和退避；
- 实现数据缺口检测；
- 暴露最新 Slot、延迟、积压、重复率和失败率指标。

### Tests

- 重复事件测试；
- 同一交易多 Instruction 测试；
- 乱序事件测试；
- WebSocket 断线与恢复测试；
- 进程崩溃与重启测试；
- RPC 限流和超时测试；
- Parser 失败与 Dead Letter 测试；
- Checkpoint 不越过未完成数据测试。

### Deliverables

- Raw Event Store；
- Checkpoint 和补采机制；
- Transaction Parser；
- Ingestion Dashboard/指标；
- 故障恢复操作说明。

### Exit Criteria

- 相同事件重复输入不会产生重复记录；
- 重启后可以从持久化 Checkpoint 继续；
- WebSocket 断开期间的数据可以补采；
- 失败事件可查询、可重试且不会静默丢失；
- Redis 或进程内 Channel 丢失不会造成原始事实丢失。

## 6. Milestone 3: Domain Projection and Replay

### Goal

将原始链上事实可靠投影为 Token、Pool、Market 和最小 Wallet 领域状态。

### Tasks

- 实现 NormalizedDomainEvent；
- 实现 Token Projection；
- 实现 LiquidityPool Projection；
- 实现 SwapEvent 和 LiquidityEvent；
- 实现 Wallet、WalletTokenPosition；
- 实现 TokenHolderSnapshot；
- 实现 MarketSnapshot；
- 实现基于 Slot 的投影更新规则；
- 实现历史事件回放；
- 实现投影重建和版本迁移流程。

### Phase 1 Wallet Scope

只实现：

- 创建者钱包关联；
- 早期买入钱包识别；
- 持仓集中度；
- 创建者持仓比例；
- 支撑基础风险判断的历史行为摘要。

暂不实现完整钱包关系图谱和 Smart Money 模型。

### Tests

- 一个 Token 多 Pool 测试；
- Base/Quote 价格语义测试；
- 旧 Slot 不覆盖新投影测试；
- 重复回放结果一致性测试；
- ParserVersion 升级重建测试；
- 不同事件顺序下最终投影一致性测试。

### Exit Criteria

- 原始事件可以重建全部 Phase 1 投影；
- 相同数据重复回放得到相同结果；
- Token、Pool 和 Wallet 关系可追溯到原始事件；
- 市场价格明确 Quote Token 和 Pool；
- 发现投影错误时可以在不修改 Raw Event 的情况下重建。

## 7. Milestone 4: Feature, Risk, Signal and Decision

### Goal

建立可版本化、可解释、无未来数据泄漏的分析和决策链路。

### Tasks

- 实现 FeatureSnapshot 和 FeatureSetVersion；
- 实现基础市场、流动性、持仓和钱包特征；
- 实现 RiskAssessment；
- 实现 HardReject 规则；
- 固化 0 低风险、100 高风险的分数语义；
- 实现 StrategyDefinition 和 StrategyRun；
- 实现 Signal Engine；
- 实现 Decision Engine；
- 实现风险、信号和账户约束的决策解释；
- 实现参数配置哈希和代码版本记录；
- 实现 Event Time 数据访问限制。

### Initial Feature Candidates

- TokenAge；
- CurrentLiquidity；
- LiquidityChangeRate；
- BuySellRatio；
- TransactionVelocity；
- HolderConcentration；
- CreatorHoldingRatio；
- EstimatedPriceImpact；
- EarlyBuyerCount；
- EarlyBuyerQuality。

### Hard Reject Candidates

- 无法模拟卖出；
- 流动性低于最低阈值；
- 关键权限风险；
- 数据缺失或过期；
- 估算价格冲击超过上限。

具体规则和阈值必须通过版本配置管理，不硬编码为不可追溯常量。

### Tests

- AsOfTime 边界测试；
- 未来数据泄漏测试；
- Feature 版本复现测试；
- Risk 规则解释测试；
- HardReject 阻止 Enter 测试；
- Signal 不直接生成 Order 测试；
- Decision 输入追溯测试；
- 相同 Run 配置产生相同决策测试。

### Exit Criteria

- 任意 Decision 可以追溯到 Feature、Risk、Signal 和 Raw Event；
- HardReject 不会产生 Enter；
- Feature 和 Risk 算法更新不会覆盖历史版本；
- 决策时只能读取当时已经可见的数据；
- Decision 不包含 Paper 或 Live 执行语义。

## 8. Milestone 5: Paper Execution and Portfolio

### Goal

使用可版本化的成交模型模拟账户、订单、成交和持仓。

### Tasks

- 实现 PaperAccount；
- 实现 PaperOrder；
- 实现 ExecutionAttempt；
- 实现 PaperFill；
- 实现 PaperPosition；
- 实现 EquitySnapshot；
- 实现交易费用和网络费用；
- 实现信号到执行的延迟；
- 实现基于 Pool 状态的滑点和价格冲击；
- 实现部分成交、失败、过期和无法退出；
- 实现仓位和资金限制；
- 实现 ExecutionModelVersion；
- 按 `PAPER_EXECUTION_MODEL_V1.md` 实现 Baseline 和压力场景。

### Initial Execution Assumptions

第一版模型允许使用保守参数，但必须：

- 参数显式配置；
- 保存模型版本；
- 保存成交时使用的 MarketSnapshot；
- 将失败交易计入结果；
- 不允许直接按 Signal 时刻的理想价格无条件成交。

### Tests

- 费用与滑点计算测试；
- 部分成交测试；
- 多 Fill 测试；
- 流动性不足测试；
- 无法退出测试；
- Order 数量和 Fill 数量不变量测试；
- Position 只能由 Fill 更新测试；
- 账户资金不足测试；
- 执行模型版本复现测试。

### Exit Criteria

- 任意 Position 可以追溯到 Fill、Order 和 Decision；
- 失败和无法退出交易不会从统计中消失；
- Paper Trading 结果包含费用、滑点和延迟；
- 相同数据和 Execution Model 配置产生相同结果；
- 系统仍不存在真实交易或签名能力。

## 9. Milestone 6: Analytics and Strategy Validation

### Goal

建立能够判断策略是否具有统计价值的绩效分析和验证流程。

### Tasks

- 实现权益曲线；
- 实现收益、回撤和交易统计；
- 实现开发、验证和样本外数据区间；
- 实现按流动性、Token 年龄和风险等级分组；
- 实现参数敏感性分析；
- 实现费用和滑点压力测试；
- 实现运行结果对比；
- 按 `STRATEGY_VALIDATION_PROTOCOL_V1.md` 固化时间切分、OOS 门槛和实验记录；
- 输出机器可读和人类可读报告。

### Required Metrics

- 交易样本数量；
- 毛收益与净收益；
- 最大回撤；
- 胜率；
- 平均盈利和平均亏损；
- 盈亏比；
- Profit Factor；
- 费用占比；
- 滑点占比；
- 失败成交比例；
- 无法退出比例；
- 持仓时间分布；
- 样本外表现；
- 参数敏感性。

### Exit Criteria

- 报告明确数据范围和所有版本；
- 净收益包含费用、滑点和失败交易；
- 策略结果包含样本外验证；
- 仅累计收益为正不能被判定为策略有效；
- 任意结果能够通过 StrategyRun 配置重新生成。

## 10. Milestone 7: API, Minimal Dashboard and Operations

### Goal

提供最小查询、运行监控和人工评审界面。

### Tasks

- 查询 Token、Pool、Market 和 Risk；
- 查询 Signal、Decision 和 Paper Trading；
- 查询数据延迟、积压和失败事件；
- 展示权益曲线和策略报告；
- 增加身份认证和最小权限；
- 建立数据库备份与恢复；
- 建立运行手册和故障处理流程；
- 建立数据缺口和关键错误告警；
- 按 `OBSERVABILITY_SLO_PHASE1.md` 实现指标、告警、Runbook、RPO 和 RTO。

### Exit Criteria

- Dashboard 不直接修改领域事实；
- 管理接口需要身份认证；
- 数据库备份可以恢复；
- 运行人员可以识别数据停止、延迟或缺口；
- Dead Letter 和失败任务可以安全重试。

## 11. Final Phase 1 Acceptance

### Data Correctness

- 支持断线恢复、补采、去重、重试和回放；
- 原始事件不因 Redis、Channel 或进程失败而丢失；
- 原始事件可以重建领域投影和分析结果。

### Traceability

- Decision → Signal/Risk → Feature → Raw Event 可追溯；
- Position → Fill → Order → Decision 可追溯；
- 所有运行结果带完整版本和配置哈希。

### Reproducibility

- 相同数据、代码版本和配置产生相同结果；
- Feature、Risk、Strategy、Execution Model 都可版本化回放。

### Research Validity

- 不使用未来数据；
- 包含样本外验证；
- 包含费用、滑点、失败和无法退出交易；
- 输出完整绩效和敏感性指标。

### Security Boundary

- 不保存私钥；
- 不执行签名；
- 不提供真实交易入口；
- 凭据、日志和数据库权限符合最小权限。

## 12. Work Not Included in Phase 1

- 完整 Smart Money 识别；
- 钱包关系图谱；
- AI 模型训练和在线推理；
- 多链支持；
- 真实交易执行；
- 高频或超低延迟优化；
- 复杂运营 Dashboard；
- 过早的微服务拆分。

## 13. Delivery Risks

### RPC Data Quality

风险：限流、延迟或历史补采能力不足。  
应对：检查点、补采、数据缺口检测、可替换 Source Adapter。

### Parser Evolution

风险：Program 或事件格式变化。  
应对：保留 Raw Payload、ParserVersion 和重放能力。

### Paper Trading Bias

风险：理想价格和忽略失败导致收益高估。  
应对：版本化延迟、费用、滑点、价格冲击和失败模型。

### Scope Expansion

风险：过早进入钱包图谱、AI 或真实交易。  
应对：严格执行 Milestone Exit Criteria 和 Phase 1 边界。

## 14. Development Principle

先建立数据资产和可验证研究流程，再优化策略。

Phase 1 的成功标准不是“自动盈利”，而是：

> 数据可靠、链路可追溯、实验可复现、结果可信，并能够低成本扩展到 Phase 2 和 Phase 3。
