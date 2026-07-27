# Crypto Intelligence System Domain Model

> 状态：Phase 1 领域模型基线  
> 上位架构：[DESIGN_PROPOSAL_V2.md](./DESIGN_PROPOSAL_V2.md)

## 1. Domain Goal

Phase 1 建立 Solana 新币雷达和 Paper Trading 系统，并形成可回放、可审计、可扩展的数据与决策链路。

系统围绕以下领域能力构建：

- 链上事实采集；
- Token、Pool、Market、Wallet 状态投影；
- 特征计算；
- 风险评估；
- 策略信号；
- 最终决策；
- 模拟执行；
- 绩效分析。

## 2. Domain Flow

```text
Raw Blockchain Event
        ↓
Normalized Domain Event
        ↓
Token / Pool / Market / Wallet Projection
        ↓
Feature Snapshot
        ↓
Risk Assessment
        ↓
Strategy Signal
        ↓
Decision
        ↓
Paper Order / Fill / Position
        ↓
Performance Result
```

所有下游结果必须能够追溯到输入数据时点、规则版本、策略版本和运行批次。

## 3. Domain Boundaries

```text
Ingestion
  └── 发现和保存链上事实，不做业务判断

Projection
  └── 将链上事实投影为 Token、Pool、Market、Wallet 状态

Analysis
  └── 生成 Feature 和 Risk Assessment

Strategy
  └── 基于可见信息生成 Signal

Decision
  └── 综合风险、信号和账户约束产生最终动作

Execution
  └── 将动作转换为 Order、Fill、Position

Analytics
  └── 计算权益、收益、回撤和策略统计
```

## 4. Core Concepts

### 4.1 RawBlockchainEvent

系统事实来源，记录链上发生了什么，不直接包含风险或策略解释。

主要属性：

- 唯一链上定位信息；
- 原始 Payload；
- Event Time、Observed Time、Finalized Time；
- Schema Version；
- 处理状态。

相同事件重复进入系统不得产生重复领域数据。

### 4.2 NormalizedDomainEvent

由解析器从原始事件中提取的标准化领域事件，例如：

- MintCreated；
- PoolCreated；
- SwapObserved；
- LiquidityChanged；
- TokenTransferred。

原始事件不可变；解析器升级后可以产生新版本的标准化事件。

### 4.3 Token

链上资产身份和生命周期实体。

Token 生命周期只表达客观市场状态：

```text
Discovered
PoolAvailable
Trading
Inactive
Closed
```

`Suspicious`、`HighRisk` 等分析结论不属于 Token 生命周期，应由 RiskAssessment 表达。

### 4.4 LiquidityPool

表示一组 Base Asset、Quote Asset 和 DEX Program 形成的交易池。

一个 Token 可以存在多个 Pool；系统不能假设 Token 与 Pool 一对一。

### 4.5 Wallet

表示链上地址身份。Wallet 本身不直接保存单一 Balance 或固定 BehaviorScore。

钱包资产和分析结果分别由以下概念表达：

- WalletTokenPosition；
- WalletHoldingSnapshot；
- WalletFeatureSnapshot；
- WalletScore。

Phase 1 只实现支撑创建者风险、早期买入者和持仓集中度所需的最小 Wallet 能力。

### 4.6 FeatureSnapshot

表示某个实体在 `AsOfTime` 时刻可用的分析特征。

例如：

- Momentum；
- LiquidityChangeRate；
- HolderConcentration；
- CreatorHoldingRatio；
- EstimatedPriceImpact；
- EarlyBuyerQuality。

FeatureSnapshot 必须带 `FeatureSetVersion`，并记录来源事件范围。特征算法更新时追加新版本，不覆盖历史结果。

### 4.7 RiskAssessment

表达风险分析结果，不产生交易动作。

输出包括：

- OverallScore：0 最低风险，100 最高风险；
- RiskLevel；
- HardReject；
- RuleResults；
- RiskModelVersion；
- InputAsOfTime。

无法卖出、关键权限风险或流动性低于最低要求等条件应使用 `HardReject`，不能被其他低风险特征抵消。

### 4.8 Signal

Signal 是策略对市场状态的观察，不直接决定交易，也不表达风险等级。

示例：

- EarlyMomentum；
- LiquidityGrowth；
- BuyPressureIncrease；
- SmartWalletEntry；
- MomentumDecay。

Signal 记录方向、强度、原因、有效期和策略版本。

以下内容不属于 Signal：

- HighRisk：属于 RiskAssessment；
- Ignore、Reject：属于 Decision。

### 4.9 Decision

Decision Engine 综合以下输入：

```text
Risk Assessment
+ Strategy Signals
+ Wallet Signals
+ Account / Portfolio Constraints
+ AI Prediction（Phase 3）
= Final Decision
```

Decision 动作统一为：

```text
Enter
Exit
Hold
Reject
Ignore
```

Decision 不绑定执行环境。不能使用 `EnterPaperTrade` 或 `EnterLiveTrade` 作为领域动作。

### 4.10 Execution

Execution 将 Decision 转换为实际执行尝试。

Phase 1：

- PaperExecutor。

未来：

- LiveExecutor。

两个 Executor 消费相同 Decision 契约，但分别应用自己的安全、成交和失败规则。

Execution 领域对象包括：

- Account；
- Order；
- Fill；
- Position；
- ExecutionAttempt；
- EquitySnapshot。

### 4.11 StrategyRun

表示一次可复现的策略运行，保存：

- 数据时间范围；
- StrategyVersion；
- FeatureSetVersion；
- RiskModelVersion；
- ExecutionModelVersion；
- 参数和配置哈希；
- 初始资金；
- 代码或构建版本。

相同数据与相同运行配置应产生相同结果。

## 5. Aggregate and Ownership Guidance

Phase 1 推荐的逻辑边界：

```text
Ingestion Aggregate
  RawBlockchainEvent / ProcessingState

Token Intelligence Aggregate
  Token / Pool / MarketSnapshot / FeatureSnapshot / RiskAssessment

Wallet Intelligence Aggregate
  Wallet / WalletTokenPosition / WalletHoldingSnapshot / WalletScore

Strategy Aggregate
  StrategyDefinition / StrategyRun / Signal / Decision

Paper Portfolio Aggregate
  PaperAccount / PaperOrder / PaperFill / PaperPosition / EquitySnapshot
```

跨边界通过稳定标识和领域事件关联，不通过共享可变对象耦合。

## 6. Domain Invariants

1. 原始链上事件不可覆盖，只能追加状态或解析版本；
2. 相同链上事件重复输入不会生成重复投影；
3. Feature、Risk、Signal、Decision 必须保存 `AsOfTime`；
4. Decision 只能使用其决策时刻已经可见的数据；
5. HardReject 为 true 时不得产生 Enter Decision；
6. Fill 总数量不得超过 Order 可成交数量；
7. Position 必须由 Fill 推导，不能由 Signal 直接修改；
8. 已关闭的 StrategyRun 不得继续追加新交易；
9. Phase 1 领域模型中不存在私钥、签名或真实交易命令。

## 7. Phase 2 and Phase 3 Extension

### Phase 2 Wallet Intelligence

通过历史 Raw Event 回放扩展：

```text
WalletTransaction
WalletRelationship
WalletFeatureSnapshot
WalletProfile
WalletScore
SmartWalletSignal
```

WalletScore 作为 Decision Engine 的输入，不直接发起交易。

### Phase 3 AI Intelligence

AI 模型消费版本化 FeatureSnapshot，输出 ModelPrediction：

```text
ModelVersion
InputAsOfTime
FeatureSetVersion
Prediction
Confidence
Explanation
```

ModelPrediction 可以生成 Signal 或作为 Decision 输入，但不能绕过 RiskAssessment、Portfolio Constraints 和 Execution 安全边界。

## 8. Design Principles

- 原始事实、分析结果和交易结果分层保存；
- 风险、策略、决策和执行严格分离；
- 所有可变算法均版本化；
- 所有决策均可解释、可回放、可复现；
- Phase 1 使用模块化单体，保持未来拆分能力；
- 真实交易能力必须经过独立安全评审后才能引入。
