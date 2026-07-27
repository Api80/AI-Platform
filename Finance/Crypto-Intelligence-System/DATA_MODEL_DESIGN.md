# Crypto Intelligence System Phase 1 Data Model Design

> 状态：Phase 1 逻辑数据模型基线  
> 上位架构：[DESIGN_PROPOSAL_V2.md](./DESIGN_PROPOSAL_V2.md)  
> 领域语义：[DOMAIN_MODEL.md](./DOMAIN_MODEL.md)  
> 关键决策：[adr/](./adr/)；执行与验证规范见 README 当前设计基线

## 1. Purpose

定义 Phase 1 核心数据模型，为 Solana 新币雷达、风险分析、Paper Trading、历史回放以及未来 Wallet Intelligence 和 AI Intelligence 提供统一数据基础。

核心原则：

1. PostgreSQL 是系统事实来源；
2. 原始事实与分析结果分层保存；
3. 不只保存结果，保存从链上事件到交易结果的完整链路；
4. 所有分析和策略结果带数据时点与算法版本；
5. 重要历史记录只追加新版本，不覆盖旧结果；
6. Redis 仅作缓存，任何关键数据都必须能够从 PostgreSQL 重建。

```text
Raw Events
    ↓
Normalized Events / Domain Projections
    ↓
Feature Snapshots
    ↓
Risk Assessments
    ↓
Signals
    ↓
Decisions
    ↓
Orders / Fills / Positions
    ↓
Equity / Performance Results
```

## 2. Common Conventions

### 2.1 Identifier

内部关系使用稳定 ID；链上对象同时保存自然标识：

- Token：`Chain + Network + MintAddress`；
- Pool：`Chain + Network + PoolAddress`；
- Wallet：`Chain + Network + Address`；
- Transaction：`Chain + Network + TransactionSignature`。

地址保存前进行统一格式校验，不通过显示名称建立关系。

### 2.2 Time

时间统一使用 UTC，区分：

- `EventTime`：链上事件发生时间；
- `ObservedTime`：系统首次观察时间；
- `ProcessedTime`：处理完成时间；
- `AsOfTime`：分析结果所代表的数据时点；
- `CreatedTime`：记录写入时间。

### 2.3 Numeric Precision

链上原始数量优先保存：

- RawAmount：整数或高精度数值；
- Decimals：精度；
- NormalizedAmount：仅用于查询和展示。

资金、Token 数量和价格禁止使用浮点类型。

### 2.4 Versioning

以下字段按场景保存：

```text
SchemaVersion
ParserVersion
FeatureSetVersion
RiskModelVersion
StrategyVersion
ExecutionModelVersion
```

## 3. Ingestion and Raw Event Layer

### 3.1 RawBlockchainEvent

保存不可变的链上原始事实。

字段：

```text
Id
EventId
Chain
Network
Slot
BlockHash
TransactionSignature
InstructionIndex
InnerInstructionIndex
ProgramId
EventType
EventOrdinal
EventTime
ObservedTime
FinalizedTime
CommitmentLevel
CanonicalStatus
FinalityUpdatedTime
RevertedTime
RevertReason
Source
RawPayload
SchemaVersion
ProcessingStatus
RetryCount
LastError
CorrelationId
CreatedTime
UpdatedTime
```

推荐唯一约束：

```text
Chain
Network
TransactionSignature
InstructionIndex
InnerInstructionIndex
EventType
EventOrdinal
SchemaVersion
```

如果某类事件不能通过上述字段唯一定位，解析器必须生成确定性的 EventId，并建立唯一约束。

推荐索引：

- `Network + Slot`；
- `TransactionSignature`；
- `EventType + EventTime`；
- `ProcessingStatus + ObservedTime`；
- `ProgramId + EventTime`。

处理状态：

```text
Pending
Processing
Completed
RetryableFailure
DeadLetter
```

原始 Payload 不覆盖；解析器升级时新增标准化版本。

### 3.2 IngestionCheckpoint

保存采集进度和断线恢复位置。

字段：

```text
Id
Chain
Network
Source
SubscriptionType
ObservedThroughSlot
PersistedThroughSlot
ProcessedThroughSlot
FinalizedThroughSlot
ReconciledThroughSlot
LastCompletedSignature
Status
LeaseOwner
LeaseUntil
UpdatedTime
```

唯一约束：

```text
Chain + Network + Source + SubscriptionType
```

### 3.3 DeadLetterEvent

可以作为 RawBlockchainEvent 状态的查询视图，也可以独立保存处理失败历史。

字段：

```text
Id
RawEventId
Processor
ParserVersion
FailureType
FailureMessage
AttemptCount
FirstFailedTime
LastFailedTime
ResolvedTime
Resolution
```

### 3.4 NormalizedDomainEvent

保存从原始 Payload 解析出的标准化领域事件。

字段：

```text
Id
RawEventId
DomainEventType
EntityType
EntityNaturalKey
DomainEventIndex
Payload
EventTime
CanonicalStatus
ParserVersion
SchemaVersion
CreatedTime
```

唯一约束：

```text
RawEventId + DomainEventType + DomainEventIndex + ParserVersion
```

## 4. Domain Fact Layer

### 4.1 Token

字段：

```text
Id
Chain
Network
MintAddress
Name
Symbol
RawSupply
Decimals
CreatorWalletId
MintAuthority
FreezeAuthority
LifecycleStatus
CreatedSlot
CreatedTime
FirstObservedTime
UpdatedTime
```

生命周期：

```text
Discovered
PoolAvailable
Trading
Inactive
Closed
```

唯一约束：

```text
Chain + Network + MintAddress
```

风险结论不写入 LifecycleStatus。

### 4.2 LiquidityPool

字段：

```text
Id
Chain
Network
PoolAddress
Dex
ProgramId
BaseTokenId
QuoteTokenId
CreatedSlot
CreatedTime
InitialBaseReserve
InitialQuoteReserve
LifecycleStatus
FirstObservedTime
UpdatedTime
```

唯一约束：

```text
Chain + Network + PoolAddress
```

一个 Token 可以关联多个 Pool。

### 4.3 SwapEvent

字段：

```text
Id
RawEventId
PoolId
TraderWalletId
BaseTokenId
QuoteTokenId
Side
BaseRawAmount
QuoteRawAmount
EffectivePrice
FeeAmount
Slot
EventTime
ObservedTime
```

唯一约束：

```text
RawEventId + PoolId
```

如果一条原始事件包含多次 Swap，则增加确定性的 `SwapIndex`。

### 4.4 LiquidityEvent

字段：

```text
Id
RawEventId
PoolId
ProviderWalletId
ChangeType
BaseRawAmount
QuoteRawAmount
BaseReserveAfter
QuoteReserveAfter
Slot
EventTime
ObservedTime
```

ChangeType：

```text
Added
Removed
Initialized
Closed
```

### 4.5 Wallet

字段：

```text
Id
Chain
Network
Address
FirstSeenSlot
FirstSeenTime
LastSeenTime
CreatedTime
UpdatedTime
```

唯一约束：

```text
Chain + Network + Address
```

Wallet 不直接保存单一 Balance 或固定 BehaviorScore。

### 4.6 WalletTokenPosition

表示钱包当前 Token 投影状态。

字段：

```text
Id
WalletId
TokenId
RawBalance
LastEventSlot
AsOfTime
ProjectionVersion
UpdatedTime
```

唯一约束：

```text
WalletId + TokenId
```

### 4.7 WalletHoldingSnapshot

字段：

```text
Id
WalletId
TokenId
RawBalance
OwnershipPercentage
AsOfSlot
AsOfTime
CreatedTime
```

推荐索引：

```text
TokenId + AsOfTime
WalletId + AsOfTime
```

### 4.8 TokenHolderSnapshot

字段：

```text
Id
TokenId
HolderCount
Top1Percentage
Top5Percentage
Top10Percentage
CreatorPercentage
AsOfSlot
AsOfTime
CreatedTime
```

唯一约束：

```text
TokenId + AsOfSlot
```

### 4.9 MarketSnapshot

字段：

```text
Id
TokenId
PoolId
QuoteTokenId
Price
BaseVolume
QuoteVolume
BuyCount
SellCount
BaseReserve
QuoteReserve
LiquidityValue
HolderCount
AsOfSlot
AsOfTime
CreatedTime
```

必须明确价格的 Quote Token，避免不同 Pool 的价格混合。

推荐唯一约束：

```text
PoolId + AsOfSlot
```

## 5. Analysis Layer

### 5.1 FeatureSnapshot

字段：

```text
Id
EntityType
EntityId
FeatureSetVersion
AsOfSlot
AsOfTime
ComputedTime
Values
SourceFromSlot
SourceToSlot
SourceEventCount
RunId
CorrelationId
```

`Values` 可以在 Phase 1 使用 JSONB，但常用查询特征可投影到独立列或索引表。

唯一约束：

```text
EntityType + EntityId + FeatureSetVersion + AsOfTime + RunId
```

### 5.2 RiskAssessment

字段：

```text
Id
TokenId
FeatureSnapshotId
OverallScore
RiskLevel
HardReject
RuleResults
Reasons
InputAsOfTime
RiskModelVersion
RunId
CreatedTime
```

分数语义：

- 0：最低风险；
- 100：最高风险；
- 分数越高风险越大。

RiskLevel：

```text
Low
Medium
High
Critical
```

唯一约束：

```text
TokenId + RiskModelVersion + InputAsOfTime + RunId
```

### 5.3 WalletFeatureSnapshot and WalletScore

Phase 1 可以只实现最小字段，Phase 2 扩展。

WalletFeatureSnapshot：

```text
Id
WalletId
FeatureSetVersion
AsOfTime
Values
RunId
CreatedTime
```

WalletScore：

```text
Id
WalletId
WalletFeatureSnapshotId
ScoreType
Score
Level
ModelVersion
AsOfTime
RunId
CreatedTime
```

不要把 WalletScore 覆盖写入 Wallet。

## 6. Strategy and Decision Layer

### 6.1 StrategyDefinition

字段：

```text
Id
Name
StrategyVersion
Description
ParameterSchema
IsEnabled
CreatedTime
```

唯一约束：

```text
Name + StrategyVersion
```

### 6.2 StrategyRun

字段：

```text
Id
StrategyDefinitionId
RunType
DataFromTime
DataToTime
Parameters
ConfigurationHash
FeatureSetVersion
RiskModelVersion
ExecutionModelVersion
InitialCapital
CodeVersion
Status
StartedTime
CompletedTime
CreatedTime
```

RunType：

```text
HistoricalReplay
StreamingPaperProvisional
ForwardPaper
Backtest
```

### 6.3 Signal

字段：

```text
Id
RunId
TokenId
StrategyDefinitionId
SignalType
Direction
Strength
Reasons
InputFeatureSnapshotId
InputAsOfTime
ExpiresTime
StrategyVersion
CreatedTime
```

Direction：

```text
Positive
Negative
Neutral
```

Signal 不使用 `HighRisk`、`Ignore` 或 `Reject` 类型。

### 6.4 Decision

字段：

```text
Id
RunId
TokenId
Action
RiskAssessmentId
InputAsOfTime
Reasons
ConstraintResults
DecisionPolicyVersion
CreatedTime
```

Action：

```text
Enter
Exit
Hold
Reject
Ignore
```

Signal 与 Decision 是多对多关系，使用 `DecisionSignal`：

```text
DecisionId
SignalId
Contribution
```

Decision 不区分 Paper 和 Live。

## 7. Paper Trading Layer

### 7.1 PaperAccount

字段：

```text
Id
RunId
Name
BaseCurrencyTokenId
InitialCash
AvailableCash
ReservedCash
Status
CreatedTime
ClosedTime
```

### 7.2 PaperOrder

字段：

```text
Id
AccountId
DecisionId
TokenId
PoolId
Side
OrderType
RequestedQuantity
RequestedNotional
LimitPrice
Status
SubmittedTime
ExpiresTime
CreatedTime
UpdatedTime
```

Status：

```text
Created
Submitted
PartiallyFilled
Filled
Rejected
Cancelled
Expired
Failed
```

### 7.3 ExecutionAttempt

字段：

```text
Id
OrderId
AttemptNumber
ExecutionModelVersion
MarketAsOfTime
SimulatedLatency
EstimatedPriceImpact
EstimatedFee
FailureReason
AttemptTime
Result
```

唯一约束：

```text
OrderId + AttemptNumber
```

### 7.4 PaperFill

字段：

```text
Id
OrderId
ExecutionAttemptId
Quantity
Price
GrossAmount
TradingFee
NetworkFee
SlippageAmount
FilledTime
CreatedTime
```

一笔 Order 可以有多笔 Fill。Fill 总数量不得超过 Order 可成交数量。

### 7.5 PaperPosition

字段：

```text
Id
AccountId
TokenId
Quantity
AverageEntryPrice
RealizedPnl
UnrealizedPnl
Status
OpenedTime
ClosedTime
AsOfTime
UpdatedTime
```

唯一约束：

```text
AccountId + TokenId + Status（仅一个 Open Position）
```

Position 必须由 Fill 投影生成，不能由 Signal 或 Decision 直接修改。

### 7.6 EquitySnapshot

字段：

```text
Id
AccountId
Cash
ReservedCash
PositionMarketValue
RealizedPnl
UnrealizedPnl
TotalEquity
Drawdown
AsOfTime
CreatedTime
```

### 7.7 TradeResult

TradeResult 是用于分析的聚合结果或查询视图，不是订单和成交事实的替代品。

字段可以包括：

```text
RunId
AccountId
TokenId
EntryTime
ExitTime
EntryValue
ExitValue
TotalFees
TotalSlippage
RealizedPnl
ReturnRate
ExitReason
```

## 8. Analytics Layer

### 8.1 PerformanceReport

字段：

```text
Id
RunId
TradeCount
FilledOrderCount
FailedOrderCount
UnexitAbleCount
GrossReturn
NetReturn
MaxDrawdown
WinRate
AverageWin
AverageLoss
ProfitFactor
FeeRatio
SlippageRatio
Parameters
CreatedTime
```

报告必须明确数据范围、策略版本和 Execution Model 版本。

## 9. Relationships

```text
RawBlockchainEvent
  └── NormalizedDomainEvent
        ├── Token / Pool / Wallet Projection
        ├── SwapEvent
        └── LiquidityEvent

Token / Pool / Wallet
  └── FeatureSnapshot
        └── RiskAssessment / WalletScore
              └── Signal
                    └── Decision
                          └── PaperOrder
                                └── ExecutionAttempt
                                      └── PaperFill
                                            └── PaperPosition
                                                  └── EquitySnapshot
```

## 10. Idempotency and Concurrency

1. Raw event 写入使用链上自然标识唯一约束；
2. Analyzer 以 `RawEventId + ParserVersion` 保证幂等；
3. Projection 更新使用 Slot 或版本检查，旧事件不得覆盖新状态；
4. Signal、Decision 和交易记录绑定 RunId；
5. Order 和 Position 更新使用并发版本字段；
6. Checkpoint 只在对应范围数据完成持久化后推进；
7. 失败事件保留错误和重试历史，不静默跳过。

## 11. Data Retention and Partition Guidance

评审阶段建议：

- Raw Event：长期保存，压缩或归档策略后续确定；
- Swap、Liquidity：按时间分区；
- MarketSnapshot：按时间分区并设置采样策略；
- Feature、Risk、Signal、Decision：长期保存，用于复现；
- Paper Trading：长期保存，用于策略对比；
- 日志和临时缓存：独立生命周期。

最终保留周期需结合 RPC 补采能力、存储成本和 Phase 3 训练需求单独确认。

## 12. Phase 2 and Phase 3 Extension

Phase 2 在现有 Wallet 和 Raw Event 基础上增加：

```text
WalletTransaction
WalletRelationship
WalletProfile
WalletCluster
WalletLabel
```

Phase 3 增加：

```text
DatasetDefinition
LabelDefinition
TrainingRun
ModelVersion
ModelPrediction
EvaluationResult
PromptVersion
```

ModelPrediction 必须引用 FeatureSnapshot、ModelVersion 和 InputAsOfTime，并作为 Signal 或 Decision 输入，不能直接修改 Order 或 Position。

## 13. Phase 1 Data Acceptance

数据层验收至少满足：

1. 重复输入相同链上事件不会生成重复数据；
2. WebSocket 中断后可以从 Checkpoint 补采；
3. 解析失败事件可查询、可重试、不会丢失；
4. 原始事件可以重新生成领域投影和分析结果；
5. 任意 Decision 可以追溯到 Signal、Risk、Feature 和 Raw Event；
6. 任意 Position 可以追溯到 Fill、Order 和 Decision；
7. 相同数据和相同 StrategyRun 配置产生相同结果；
8. Redis 或进程重启不会造成不可恢复的数据丢失。
