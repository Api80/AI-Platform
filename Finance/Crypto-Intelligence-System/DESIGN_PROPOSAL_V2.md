# Crypto Intelligence System Design Proposal V2

> 状态：Phase 1 Design Baseline  
> 适用范围：Phase 1，并为 Phase 2 Wallet Intelligence、Phase 3 AI Intelligence 预留稳定扩展点  
> 关键设计已经通过 ADR 和专项规范固化；历史设计文档保留但不再作为实现依据。

## 1. 设计目标

Crypto Intelligence System 的目标不是构建一个简单交易机器人，而是建立一套可以长期积累链上数据、复现历史状态、评估风险、验证策略并逐步扩展到钱包智能和 AI 智能的系统。

Phase 1 的核心目标：

1. 稳定采集 Solana 新 Token、Pool、Swap 和流动性事件；
2. 保存可回放、可审计的链上原始事实；
3. 基于明确版本的特征和规则进行风险评估；
4. 使用尽可能贴近真实成交条件的 Paper Trading 验证策略；
5. 形成可量化、可复现的策略评估结果；
6. 为 Phase 2 钱包画像和 Phase 3 AI 模型保留数据基础。

Phase 1 不包含：

- 自动真实交易；
- 私钥保存和链上签名；
- 多链接入；
- 复杂 AI 模型训练；
- 复杂微服务部署；
- 大型运营型 Dashboard。

## 2. 核心设计原则

### 2.1 原始事实优先

先保存原始链上事件，再进行解析、特征计算和策略决策。分析结果可以重算，丢失的原始事实通常无法低成本恢复。

### 2.2 Event Time 优先

特征、风险评分、信号和决策只能使用决策时刻已经可见的数据，禁止未来数据泄漏。

系统同时记录：

- `EventTime`：事件在链上发生的时间；
- `ObservedTime`：系统首次观察到事件的时间；
- `ProcessedTime`：系统完成处理的时间。

### 2.3 全链路可追溯

系统保存完整决策链路：

```text
Raw Event
    ↓
Normalized Event
    ↓
Feature Snapshot
    ↓
Risk Assessment
    ↓
Signal
    ↓
Decision
    ↓
Order / Fill / Position
    ↓
Performance Result
```

任何交易结果都应能够追溯到当时的数据、规则、参数和代码版本。

### 2.4 风险控制独立

风险规则不应隐藏在策略代码中。硬性拒绝条件、风险评分、策略信号和最终决策必须分离。

### 2.5 模块化单体优先

Phase 1 采用模块化单体，逻辑上保持清晰边界，但暂不拆成大量独立微服务。等吞吐量、部署隔离或团队协作产生真实需求后再拆分。

### 2.6 版本化

以下内容必须带版本：

- 事件 Schema；
- Feature Set；
- 风险规则；
- 策略；
- Paper Execution Model；
- AI 模型和 Prompt（Phase 3）。

## 3. Phase 1 推荐架构

```text
                 Solana RPC / WebSocket
                          |
                          v
                  Ingestion Adapter
                          |
             +------------+-------------+
             |                          |
             v                          v
       Raw Event Store          Ingestion Checkpoint
             |
             v
      Durable Event Dispatcher
             |
       +-----+------+------+
       |            |      |
       v            v      v
     Token        Market  Wallet
    Analyzer      Analyzer Analyzer
       |            |      |
       +------------+------+
                    |
                    v
              Feature Engine
                    |
                    v
               Risk Engine
                    |
                    v
               Signal Engine
                    |
                    v
              Decision Engine
                    |
                    v
          Paper Execution Engine
                    |
                    v
          Performance Analytics
                    |
                    v
                 API / UI
```

### 3.1 运行时组成

```text
CryptoIntelligence.Worker
├── Ingestion
├── EventStore
├── EventDispatching
├── TokenAnalysis
├── MarketAnalysis
├── WalletAnalysis
├── Features
├── Risk
├── Signals
├── Decisions
├── PaperTrading
└── Analytics

CryptoIntelligence.Api
CryptoIntelligence.Dashboard
PostgreSQL
Redis（可选，仅作缓存和短期实时状态）
```

PostgreSQL 是事实来源。Redis 中的数据必须能够从 PostgreSQL 或链上原始数据重新生成。

## 4. 链上数据采集与可靠性

### 4.1 Event Envelope

所有链上事件使用统一信封：

```text
EventId
Chain
Network
EventType
Signature
Slot
InstructionIndex
InnerInstructionIndex
ProgramId
EventTime
ObservedTime
FinalizedTime
SchemaVersion
Source
Payload
```

`EventId` 必须能够支持幂等写入。数据库应建立能够阻止同一链上事件被重复保存的唯一约束。

### 4.2 采集流程

1. WebSocket 用于低延迟发现；
2. RPC 用于拉取交易详情、确认状态和断线补采；
3. 原始事件先写入数据库；
4. 成功落库后再进入下游分析；
5. 定期校验检查点和数据缺口；
6. 解析失败的事件进入可重试状态，不直接丢弃。

### 4.3 Checkpoint

检查点至少记录：

```text
Source
Network
LastObservedSlot
LastCompletedSlot
LastCompletedSignature
UpdatedTime
Status
```

系统重启或 WebSocket 断开后，从已完成检查点继续补采。

### 4.4 .NET Channel 的定位

`.NET Channel` 可以用于进程内并发和背压控制，但不能作为唯一事件队列。只有已经写入 Raw Event Store 的事件才能进入 Channel。

Phase 1 可以使用 PostgreSQL 状态字段或 Outbox 形式完成持久化分发，后续再根据吞吐量切换到独立消息队列。

## 5. 分析与决策分层

### 5.1 Analyzer

Analyzer 将原始事件转换为可查询的领域事实：

- Token Analyzer：Token、Mint 权限、供应量等；
- Market Analyzer：Pool、Swap、流动性、成交和价格；
- Wallet Analyzer：创建者、早期买入者、持仓变化。

一期 Wallet Analyzer 只实现支撑基础风险评估所需的最小能力，不实现完整钱包画像。

### 5.2 Feature Engine

Feature 是某个实体在某一时刻的可计算事实，例如：

- Pool 流动性；
- 买卖数量变化；
- Holder 集中度；
- 创建者持仓比例；
- 价格冲击估算；
- Token 创建后经过时间。

Feature Snapshot 必须记录：

```text
EntityType
EntityId
FeatureSetVersion
AsOfTime
ComputedTime
Values
SourceEventRange
```

### 5.3 Risk Engine

风险输出分成两类：

1. `HardReject`：无法卖出、关键权限风险、流动性低于最低要求等不可覆盖条件；
2. `RiskScore`：用于比较和排序的连续风险分数。

统一约定：

- `0` 表示最低风险；
- `100` 表示最高风险；
- 分数越高风险越大。

Risk Assessment 保存：

```text
TokenId
AssessmentId
OverallScore
RiskLevel
HardReject
RuleResults
InputAsOfTime
RiskModelVersion
CreatedTime
```

### 5.4 Signal Engine

Signal 只表达策略观察结果，不决定是否执行：

```text
SignalType
Direction
Strength
Reason
StrategyVersion
InputAsOfTime
ExpiresTime
```

### 5.5 Decision Engine

Decision Engine 综合：

```text
Token Risk
+ Market Signal
+ Wallet Signal
+ Portfolio Constraints
+ AI Prediction（Phase 3）
= Final Decision
```

Decision 可能是：

- Enter；
- Exit；
- Hold；
- Reject；
- Ignore。

每个 Decision 保存所有引用输入以及拒绝或采纳原因。

## 6. 数据库设计建议

### 6.1 原始与采集层

- `RawBlockchainEvent`
- `IngestionCheckpoint`
- `EventProcessingState`
- `DeadLetterEvent`

### 6.2 领域事实层

- `Token`
- `Pool`：包含 BaseMint、QuoteMint、Dex 和 PoolAddress；
- `SwapEvent`
- `LiquidityEvent`
- `Wallet`
- `WalletTokenPosition`
- `TokenHolderSnapshot`
- `MarketSnapshot`

### 6.3 分析与决策层

- `FeatureSnapshot`
- `RiskAssessment`
- `Signal`
- `Decision`
- `StrategyDefinition`
- `StrategyRun`

### 6.4 Paper Trading 层

- `PaperAccount`
- `PaperOrder`
- `PaperFill`
- `PaperPosition`
- `EquitySnapshot`
- `ExecutionAttempt`

不要用单一 `PaperTrade` 同时表达信号、订单、成交、持仓和盈亏。

### 6.5 审计字段

主要记录应包含：

```text
CreatedTime
UpdatedTime
AsOfTime
SchemaVersion
RunId
Source
CorrelationId
```

分析和决策记录原则上只追加新版本，不覆盖历史结果。

## 7. Paper Execution Model

Paper Trading 的目标不是按市场快照价格记账，而是估算在当时条件下是否可能成交以及可能的成交结果。

一期至少考虑：

- 信号到执行之间的延迟；
- Pool 流动性；
- 订单规模产生的价格冲击；
- 滑点；
- 交易和网络费用；
- 部分成交；
- 交易失败；
- 流动性移除；
- 无法退出；
- 最大持仓时间。

Paper Execution Model 必须有独立版本。每次运行保存：

```text
ExecutionModelVersion
LatencyParameters
FeeParameters
SlippageParameters
FailureParameters
InitialCapital
PositionLimits
```

同一批数据和同一配置重复运行，应得到相同结果。

## 8. 策略验证原则

### 8.1 禁止未来数据泄漏

策略在时间 `T` 做决策时，只能读取 `AsOfTime <= T` 且在当时已经可观察的数据。

### 8.2 分离研究区间

至少区分：

- 策略开发数据；
- 参数验证数据；
- 样本外测试数据。

### 8.3 核心指标

策略报告至少包含：

- 交易样本数量；
- 毛收益与净收益；
- 最大回撤；
- 胜率；
- 平均盈利和平均亏损；
- 盈亏比；
- Profit Factor；
- 费用和滑点占比；
- 无法成交及无法退出比例；
- 按流动性、Token 年龄、风险等级的分组表现；
- 参数敏感性；
- 样本外表现。

仅有累计收益为正，不能证明策略具有统计优势。

## 9. 可观测性与运维

系统至少暴露以下指标：

- 最新观察和完成的 Slot；
- 链上数据延迟；
- WebSocket 连接和重连次数；
- RPC 调用错误率和限流次数；
- Raw Event 写入速率；
- 事件积压量；
- 重复事件率；
- 解析失败率；
- Analyzer、Feature、Risk、Decision 的处理延迟；
- 检测到的数据缺口；
- Paper Execution 失败率。

必须提供：

- 结构化日志；
- CorrelationId；
- 健康检查；
- 数据缺口告警；
- 数据库备份和恢复流程。

## 10. 安全边界

Phase 1：

- 不保存私钥；
- 不执行签名；
- 不提供真实交易入口；
- RPC/API 凭据使用安全配置管理；
- Dashboard 和管理 API 需要身份认证；
- 日志不得输出凭据；
- 数据库账号使用最小权限。

Phase 4 引入真实执行前，必须单独进行安全架构和交易风控评审。

## 11. Phase 2：Wallet Intelligence 扩展

Phase 2 在已有 Event Pipeline 上新增或增强 Wallet Analyzer：

```text
Raw Events
    ↓
Wallet Analyzer
    ↓
Wallet Features
    ↓
Wallet Profile / Wallet Score
    ↓
Smart Money Signal
    ↓
Decision Engine
```

新增模型：

- `WalletTransaction`
- `WalletHoldingSnapshot`
- `WalletRelationship`
- `WalletFeatureSnapshot`
- `WalletProfile`
- `WalletScore`

一期保存完整原始事件后，二期可以通过历史回放生成钱包画像，不需要重新设计主链路。

## 12. Phase 3：AI Intelligence 扩展

AI 不直接控制执行，而是作为 Signal 或 Decision 的一个输入：

```text
Feature Snapshots
        ↓
Rules + Wallet Model + AI Model
        ↓
Signal Engine
        ↓
Decision Engine
        ↓
Execution Engine
```

新增模型：

- `DatasetDefinition`
- `LabelDefinition`
- `TrainingRun`
- `ModelVersion`
- `ModelPrediction`
- `EvaluationResult`
- `PromptVersion`（使用大模型时）

每次预测保存：

- 输入数据时点；
- Feature Set 版本；
- 模型版本；
- 预测值与置信度；
- 解释信息；
- 最终 Decision 是否采纳。

模型必须支持离线评估、灰度启用、快速禁用和版本回滚。

## 13. Phase 1 验收门槛

### 13.1 数据正确性

- 支持断线恢复和缺口补采；
- 重复输入不会生成重复领域数据；
- 原始事件可以重新驱动分析流程；
- 解析失败事件可以查询和重试。

### 13.2 系统可靠性

- 连续运行期间能够监测链上延迟和数据缺口；
- 重启后从持久化检查点继续；
- Redis 或进程内 Channel 丢失不会导致原始数据永久丢失。

### 13.3 策略可复现性

- 相同数据、代码版本和运行配置产生相同结果；
- 任意 Decision 可以追溯到 Feature、Risk、Signal 和原始事件；
- 策略报告包含费用、滑点、失败交易和样本外结果。

### 13.4 安全边界

- 系统不存在私钥和签名能力；
- 所有真实交易相关接口默认不存在或不可用；
- 凭据、日志和数据库权限符合最小权限要求。

## 14. 推荐开发顺序

1. Event Envelope 和核心标识设计；
2. PostgreSQL Raw Event Store 与 Checkpoint；
3. Solana Ingestion、补采、幂等和重试；
4. Token、Pool、Swap、Liquidity 领域模型；
5. Token、Market 和最小 Wallet Analyzer；
6. Feature Engine；
7. Risk Engine；
8. Signal 与 Decision Engine；
9. Paper Account、Order、Fill、Position；
10. Performance Analytics；
11. API、基础 Dashboard 和运行监控；
12. 历史回放、断线恢复和策略复现验收。

## 15. 已确认决策

1. Phase 1 采用模块化单体；
2. PostgreSQL 是唯一事实来源，Redis 仅作可重建缓存；
3. 事件投递采用 At-least-once + 幂等 Consumer；
4. Phase 1 使用 PostgreSQL 持久化状态队列和 Worker Lease；
5. WebSocket 用于低延迟发现，RPC 用于详情获取、最终性刷新、补采和对账；
6. 正式 StrategyRun 只消费 Finalized 且 Reconciled 的数据；
7. Phase 1 V1 只支持一个 Launch Source 和一个 AMM/Pool Adapter 的完整闭环；
8. Risk Score 统一为 0 低风险、100 高风险；
9. Paper Execution 使用确定性、保守、版本化的 V1 模型；
10. 策略有效性按预注册的时间切分、OOS、Walk-Forward 和压力测试门槛判断；
11. 数据采用分区、分类保留、每日备份和定期恢复演练；
12. 运行可靠性按 Phase 1 SLO 监控。

详细决策：

- [ADR-0001 Runtime and Persistence](./adr/ADR-0001-phase1-runtime-and-persistence.md)
- [ADR-0002 Event Delivery and Idempotency](./adr/ADR-0002-event-delivery-idempotency-and-checkpoints.md)
- [ADR-0003 Solana Finality and Reconciliation](./adr/ADR-0003-solana-finality-and-reconciliation.md)
- [ADR-0004 Adapter Scope](./adr/ADR-0004-phase1-solana-adapter-scope.md)
- [ADR-0005 Data Retention and Backup](./adr/ADR-0005-data-partition-retention-and-backup.md)
- [Paper Execution Model V1](./PAPER_EXECUTION_MODEL_V1.md)
- [Strategy Validation Protocol V1](./STRATEGY_VALIDATION_PROTOCOL_V1.md)
- [Phase 1 Observability and SLO](./OBSERVABILITY_SLO_PHASE1.md)

## 16. Implementation Decisions Remaining

以下内容不改变总体架构，由对应 Milestone 通过 Spike、配置或容量报告确定：

1. 首个 Launch Source 和 AMM Adapter 的具体 ProgramId；
2. RPC Source 的部署配置和故障切换顺序；
3. Adapter 的已知版本与解析样本；
4. 基于 7 天真实采集数据调整分区和热数据窗口；
5. Network/Priority Fee 的初始保守配置；
6. 策略具体 Feature 阈值和 Risk Rule 参数。

这些参数必须版本化和可审计，不得以未记录的运行时常量存在。

## 17. Next Actions

1. 按 `DEVELOPMENT_PLAN_PHASE1.md` 启动 Milestone 1；
2. 创建数据库物理 Schema 和索引设计；
3. 完成 Adapter Spike 并固化 Program 配置；
4. 将 ADR 和专项规范转化为自动化测试；
5. Milestone 2 稳定采集 7 天后完成容量与 SLO 复审。