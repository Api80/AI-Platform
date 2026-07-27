# Crypto Intelligence System Phase 1 MVP Design

> 状态：Implementation Baseline  
> 目标：单来源新币雷达、基础风险预警、早期动量策略和 Paper Trading 验证  
> 长期架构：[DESIGN_PROPOSAL_V2.md](./DESIGN_PROPOSAL_V2.md)

## 1. MVP Objective

在一个指定 Solana 新币来源中：

1. 及时发现刚创建或刚开放交易的 Token/Pool；
2. 观察前几分钟的价格、流动性、成交和买卖行为；
3. 使用可解释的基础风险规则过滤明显高风险候选；
4. 使用一套可配置的早期动量规则生成买入和退出 Decision；
5. 通过保守 Paper Execution 模拟真实可成交结果；
6. 使用 Historical Replay 和 Forward Paper 判断策略是否具有继续研究的价值。

MVP 不承诺盈利，也不承诺在流动性消失前成功卖出。

## 2. Primary User

Phase 1 主要用户是策略研究者或内部运营人员。

核心任务：

- 查看刚发现的新币；
- 查看其来源、Pool、年龄、流动性和交易热度；
- 查看风险拒绝原因；
- 查看策略为什么买入、退出或忽略；
- 查看模拟订单、成交、失败和持仓；
- 比较不同参数运行结果；
- 判断策略是否值得继续研究。

## 3. MVP Success Definition

MVP 成功不等于策略盈利。必须同时满足：

### System Success

- 新币数据可稳定采集；
- 断线后可补采；
- 同一事件不会重复生成候选或交易；
- 任意 Decision 可追溯到原始事件和当时特征；
- 任意 Position 可追溯到 Order 和 Fill；
- 相同数据和配置能够复现相同结果。

### Research Success

- 能获得足够候选样本；
- 能真实记录失败、滑点和无法退出；
- 能完成历史 OOS 和 Forward Paper；
- 能判断策略状态为 Validated、NeedsMoreEvidence 或 Rejected。

## 4. In Scope

### Data Source

- 一个 Launch Source Adapter；
- 一个 AMM/Pool Adapter；
- 一个 Solana Network 环境；
- WebSocket 发现；
- RPC 交易详情、补采和对账。

### Events

- MintCreated；
- PoolCreated；
- SwapObserved；
- LiquidityChanged；
- 支撑创建者和 Holder 集中度的最小 Transfer/Balance 数据。

### Product Features

- 新币候选列表；
- 热点主题关键词；
- 基础风险预警；
- 前几分钟滚动指标；
- 规则进入和退出；
- Paper Trading；
- Historical Replay；
- Forward Paper；
- 最小 Dashboard；
- 数据和运行状态监控。

## 5. Out of Scope

- 全 Solana 新币覆盖；
- 多 Launch Source 和多 DEX 路由；
- 完整 Wallet Intelligence；
- Smart Money 评分；
- 钱包关系图谱；
- 社交媒体全量采集；
- AI 直接产生交易动作；
- 多链；
- 真实交易、私钥和签名；
- 微服务；
- 移动端；
- 复杂运营后台。

## 6. MVP Architecture

```text
Solana WebSocket / RPC
          |
          v
Source Adapter
          |
          v
Raw Event Store + Checkpoint
          |
          v
Token / Pool / Swap Projection
          |
          v
New Token Candidate Builder
          |
          v
Rolling Market Window
          |
     +----+----+
     |         |
     v         v
Theme Filter  Risk Filter
     |         |
     +----+----+
          |
          v
Early Momentum Strategy
          |
          v
Decision Engine
          |
          v
Paper Execution
          |
          v
Account / Order / Fill / Position
          |
          v
Performance Report + Dashboard
```

### Runtime

```text
CryptoIntelligence.Worker
CryptoIntelligence.Api
CryptoIntelligence.Dashboard
PostgreSQL
Redis（可选）
```

Phase 1 不拆独立微服务。

## 7. Main Modules

### 7.1 SourceAdapter

职责：

- 识别支持的 Program；
- 从交易中解析标准事件；
- 生成确定性事件序号；
- 提供历史补采；
- 提供 Pool Quote 和已知限制。

输出：

```text
MintCreated
PoolCreated
SwapObserved
LiquidityChanged
TokenTransferred
```

### 7.2 RawEventStore

职责：

- 原始事件先落库；
- 幂等写入；
- canonical 状态；
- Retry、Dead Letter；
- 五类水位；
- RPC 对账。

### 7.3 CandidateBuilder

根据 Token 和 Pool 状态生成 `TokenCandidate`。

候选条件：

- Token 已发现；
- 支持的 Pool 已创建；
- Pool 已有可用储备；
- Token Age 位于观察范围；
- 数据来源属于 MVP 支持范围。

### 7.4 RollingMarketWindow

按配置窗口计算：

- PriceChange；
- Liquidity；
- LiquidityChange；
- BuyCount；
- SellCount；
- BuyVolume；
- SellVolume；
- UniqueBuyers；
- TransactionVelocity；
- NoTradeDuration；
- EstimatedPriceImpact。

MVP 不建设通用 Feature Store，只保存策略和审计需要的版本化 FeatureSnapshot。

### 7.5 ThemeFilter

输入：

- Token Name；
- Symbol；
- 人工维护 Hot Keywords；
- Blocked Keywords；
- Theme Valid Time。

输出：

```text
MatchedThemes
ThemeScore
MatchReasons
ConfigurationVersion
```

AI 主题分类只能作为附加标签，规则关键词结果必须保留，AI 失败不能阻塞主链路。

### 7.6 RiskFilter

MVP Hard Reject：

- 无法获取有效卖出 Quote；
- 数据未达到要求的 commitment/reconciliation；
- Pool 流动性低于阈值；
- 价格冲击高于阈值；
- 关键 Mint/Freeze 权限风险；
- 创建者持仓超过阈值；
- Top Holder 集中度超过阈值；
- Market State 过期；
- Adapter 不支持当前 Pool/Program 版本。

风险分数：0 最低风险，100 最高风险。

Hard Reject 优先于策略信号。

### 7.7 EarlyMomentumStrategy

策略只消费：

- TokenCandidate；
- ThemeMatch；
- FeatureSnapshot；
- RiskAssessment；
- 当前账户约束。

进入条件全部配置化：

```text
TokenAge <= MaxEntryAge
Liquidity >= MinLiquidity
Momentum >= MinMomentum
BuySellRatio >= MinBuySellRatio
UniqueBuyers >= MinUniqueBuyers
PriceImpact <= MaxPriceImpact
RiskScore <= MaxRiskScore
HardReject = false
ThemeRule satisfied
```

输出 Signal，不直接生成 Order。

### 7.8 DecisionEngine

动作：

```text
Enter
Exit
Hold
Reject
Ignore
```

综合：

- RiskAssessment；
- Strategy Signals；
- Position 状态；
- Account 可用资金；
- 单 Token 和总账户限制。

### 7.9 ExitStrategy

退出条件：

```text
TakeProfit
StopLoss
MaxHoldingTime
LiquidityDrop
MomentumDecay
SellPressureIncrease
NoTradeTimeout
PriceImpactLimit
EmergencyExit
```

退出优先级：

```text
Hard Risk / Emergency
→ Stop Loss
→ Liquidity Risk
→ Max Holding Time
→ Take Profit
→ Momentum Decay
```

当 Pool 已无法提供有效 Quote 时记录 `ExitUnavailable`，不能假设按最后价格退出。

### 7.10 PaperExecution

按照 [PAPER_EXECUTION_MODEL_V1.md](./PAPER_EXECUTION_MODEL_V1.md) 实现：

- 延迟；
- Finalized/Reconciled 或显式 Provisional Run；
- Pool Adapter Quote；
- 手续费和优先费假设；
- 价格冲击；
- 滑点；
- 失败原因；
- 无法退出；
- 保守 Position 估值。

### 7.11 PerformanceAnalytics

按照 [STRATEGY_VALIDATION_PROTOCOL_V1.md](./STRATEGY_VALIDATION_PROTOCOL_V1.md) 输出：

- 候选数量；
- Enter/Reject/Ignore 数量；
- 提交、成交、失败订单；
- 无法退出；
- 毛收益和净收益；
- 最大回撤；
- Profit Factor；
- 费用和滑点；
- 按 Theme、流动性、Token Age 和 Risk 分组；
- OOS、Walk-Forward、敏感性和压力测试结果。

## 8. Candidate State Machine

```text
Discovered
    ↓
Observing
    ├── Rejected
    ├── Expired
    └── Eligible
            ↓
         Entered
            ↓
         Exiting
            ├── Exited
            └── ExitUnavailable
```

规则：

- Discovered：Token/Pool 刚被识别；
- Observing：积累最小观察窗口；
- Rejected：Hard Reject 或基础条件不满足；
- Expired：超过最大进入年龄仍未满足；
- Eligible：满足进入候选条件；
- Entered：Paper Fill 已创建；
- Exiting：产生退出 Decision；
- Exited：退出 Fill 完成；
- ExitUnavailable：无法获得有效卖出 Quote 或执行失败超过策略允许范围。

状态转换必须保存原因和配置版本。

## 9. Strategy State Machine

```text
NoPosition
    |
    | Enter Decision
    v
EntryPending
    ├── Failed → NoPosition
    └── Filled → Holding
                   |
                   | Exit Decision
                   v
                ExitPending
                   ├── Failed/Unavailable → HoldingAtRisk
                   └── Filled → Closed
```

Signal 不能直接改变 Position；Position 只能由 Fill 更新。

## 10. Minimal Data Model

MVP 使用长期数据模型的子集：

### Ingestion

- RawBlockchainEvent；
- IngestionCheckpoint；
- NormalizedDomainEvent；
- ProcessingState / DeadLetter。

### Radar

- Token；
- LiquidityPool；
- SwapEvent；
- LiquidityEvent；
- MarketSnapshot；
- TokenCandidate；
- ThemeMatch。

### Analysis

- FeatureSnapshot；
- RiskAssessment；
- StrategyDefinition；
- StrategyRun；
- Signal；
- Decision。

### Paper Trading

- PaperAccount；
- PaperOrder；
- ExecutionAttempt；
- PaperFill；
- PaperPosition；
- EquitySnapshot；
- PerformanceReport。

Phase 1 暂不实现：

- 完整 WalletProfile；
- WalletRelationship；
- WalletCluster；
- AI Dataset/Model Registry。

## 11. Minimal API

### Radar

```text
GET /api/radar/candidates
GET /api/radar/candidates/{mintAddress}
GET /api/radar/candidates/{mintAddress}/events
GET /api/radar/candidates/{mintAddress}/risk
```

### Strategy

```text
GET /api/strategy/runs
GET /api/strategy/runs/{runId}
GET /api/strategy/runs/{runId}/signals
GET /api/strategy/runs/{runId}/decisions
GET /api/strategy/runs/{runId}/performance
```

### Paper Trading

```text
GET /api/paper/accounts/{accountId}
GET /api/paper/orders
GET /api/paper/positions
GET /api/paper/trades
```

### Operations

```text
GET /health/live
GET /health/ready
GET /api/operations/ingestion
GET /api/operations/gaps
GET /api/operations/dead-letters
```

Phase 1 Dashboard 默认只读。管理操作使用独立受保护接口。

## 12. Minimal Dashboard

### Radar Page

- Token、Symbol、Theme；
- 创建/发现时间；
- Pool、Quote Token；
- 当前价格和流动性；
- Token Age；
- 买卖数和 Unique Buyers；
- Risk Score、Hard Reject 和原因；
- Candidate 状态。

### Candidate Detail

- 价格和流动性时间线；
- Swap 活动；
- Theme Match；
- Feature、Risk、Signal 和 Decision；
- 相关 Paper Order/Fill；
- 退出状态和失败原因。

### Strategy Page

- StrategyRun 配置；
- 当前账户和持仓；
- 收益、回撤、Profit Factor；
- 失败订单和 ExitUnavailable；
- 参数和场景对比。

### Operations Page

- 五类水位；
- 数据延迟；
- WebSocket/RPC 状态；
- Pending、Retry 和 Dead Letter；
- Gap；
- 最近备份状态。

## 13. AI Boundary

Phase 1 AI 允许：

- 热点主题辅助分类；
- 风险原因解释；
- 报告摘要；
- 失败案例聚类；
- 开发和测试辅助。

Phase 1 AI 禁止：

- 绕过规则产生 Enter/Exit；
- 绕过 Hard Reject；
- 直接修改账户、订单、成交或持仓；
- 使用未版本化 Prompt 影响研究结果；
- AI 调用失败时阻塞核心雷达和策略链路。

如果 AI 输出进入分析结果，必须保存 Model/Prompt Version、输入时点和响应摘要。

## 14. Security Boundary

- 不保存私钥；
- 不签名；
- 不发送真实交易；
- RPC Secret 使用安全配置；
- Dashboard 和管理 API 鉴权；
- 日志不记录 Secret；
- 数据库最小权限；
- Paper Executor 与未来 Live Executor 不共享实现。

## 15. MVP Acceptance

### Functional

- 能发现支持来源的新 Token/Pool；
- 能生成滚动市场特征；
- 能输出风险拒绝原因；
- 能完成规则进入和退出；
- 能记录订单、成交、失败和持仓；
- 能运行 Historical Replay 和 Forward Paper；
- 能通过 API/Dashboard 查询完整链路。

### Reliability

- 重复事件幂等；
- 断线可补采；
- Gap 阻止正式研究水位推进；
- 失败事件可重试；
- 相同 Run 可复现；
- 数据满足 [OBSERVABILITY_SLO_PHASE1.md](./OBSERVABILITY_SLO_PHASE1.md)。

### Research

- 不使用未来数据；
- 所有成本和失败计入结果；
- 无法退出不按理想价格平仓；
- 完成 OOS、压力和敏感性测试；
- 按验证协议给出明确研究状态。

## 16. Extension Path

### Phase 2 Wallet Intelligence

复用 Raw Event、Token、Pool、Swap 和 Feature：

```text
WalletTransaction
WalletProfile
WalletRelationship
WalletScore
SmartWalletSignal
```

### Phase 3 AI Intelligence

复用版本化 Feature、Risk、Signal 和 Decision：

```text
DatasetDefinition
ModelVersion
ModelPrediction
AI Risk Explanation
Theme Intelligence
```

### Phase 4 Execution

复用 Decision、Order 和 Position 契约，但必须新增：

- Live Executor；
- 密钥隔离；
- 资金和损失上限；
- 人工审批；
- Kill Switch；
- 独立安全评审。

## 17. Final MVP Principle

```text
范围要小
数据要真
失败要记
结果要能复现
扩展点要稳定
```

Phase 1 的目标是证明单来源新币雷达和早期动量策略是否值得继续，而不是提前建设完整平台。