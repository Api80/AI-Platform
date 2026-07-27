# Phase 1 MVP Configuration Specification

> 状态：Implementation Baseline  
> 目标：保证所有数据源、风险、策略、执行和验证参数都可配置、可版本化、可复现

## 1. Configuration Principles

1. 策略和风险阈值不能散落在代码中；
2. 每次 StrategyRun 保存完整配置快照；
3. 配置使用不可变 `ConfigurationVersion`；
4. 修改任何影响结果的参数都必须生成新版本；
5. Secret 不进入普通配置快照；
6. 未完成 Adapter Spike 的参数使用 `null`，启动对应 Run 前必须校验；
7. 配置单位必须明确，不允许仅使用含义模糊的数字。

## 2. Configuration Groups

```text
System
Source
Radar
Theme
Risk
EntryStrategy
ExitStrategy
Portfolio
Execution
Validation
AI
```

## 3. Example Configuration

以下为结构示例，不代表已经验证的盈利参数：

```yaml
configurationVersion: phase1-mvp-research-v1
network: mainnet-beta
runType: HistoricalReplay

source:
  launchAdapter: null              # Adapter Spike 后填写
  poolAdapter: null                # Adapter Spike 后填写
  programIds: []
  startSlot: null
  discoveryCommitment: confirmed
  strategyCommitment: finalized
  requireReconciledData: true
  rpcSourceName: null
  fallbackRpcSourceName: null

radar:
  minimumObservationSeconds: 30
  maximumCandidateAgeSeconds: 600
  maximumEntryAgeSeconds: 300
  featureWindowsSeconds: [15, 30, 60, 180]
  marketSnapshotIntervalSeconds: 5
  noTradeTimeoutSeconds: 30
  maximumMarketDataAgeSeconds: 5

  quoteTokenRules:
    # 每个 Quote Token 独立设置，不使用模糊的全局流动性数字
    # <quoteMint>:
    #   minimumQuoteReserveRaw: null
    #   decimals: null
    #   referenceCurrency: null

theme:
  mode: KeywordRules
  hotKeywords: []
  blockedKeywords: []
  requiredThemeMatch: false
  caseInsensitive: true
  normalizeWhitespace: true
  themeValidUntil: null
  configurationVersion: theme-rules-v1

risk:
  modelVersion: risk-rules-v1
  scoreMinimum: 0
  scoreMaximum: 100
  maximumAllowedRiskScore: null

  hardReject:
    requireSellQuote: true
    rejectUnsupportedPoolVersion: true
    rejectStaleMarketState: true
    rejectNonFinalizedForFormalRun: true
    rejectNonReconciledForFormalRun: true
    rejectMintAuthorityRisk: true
    rejectFreezeAuthorityRisk: true

    minimumQuoteReserveRaw: null
    maximumCreatorHoldingPercentage: null
    maximumTop10HoldingPercentage: null
    maximumEntryPriceImpactBps: 1000
    maximumMarketDataAgeSeconds: 5

entryStrategy:
  strategyName: EarlyMomentum
  strategyVersion: early-momentum-v1

  minimumTokenAgeSeconds: 30
  maximumTokenAgeSeconds: 300
  minimumMomentumBps: null
  momentumWindowSeconds: 60
  minimumBuySellRatio: null
  minimumUniqueBuyers: null
  minimumTransactionVelocityPerMinute: null
  minimumLiquidityGrowthBps: null
  maximumEntryPriceImpactBps: 1000
  requireThemeMatch: false
  maximumOpenPositions: 1
  cooldownAfterRejectedSeconds: 30

exitStrategy:
  strategyVersion: early-exit-v1

  takeProfitBps: null
  stopLossBps: null
  maximumHoldingSeconds: null
  liquidityDropTriggerBps: null
  momentumDecayTriggerBps: null
  sellPressureRatioTrigger: null
  noTradeTimeoutSeconds: 30
  maximumExitPriceImpactBps: 1000
  emergencyExitOnHardRisk: true
  exitRetryIntervalSeconds: 2
  maximumExitRetryCount: 3
  zeroExitValueAfterUnavailable: false

portfolio:
  baseCurrencyTokenMint: null
  initialCapitalRaw: null
  maximumPositionNotionalRaw: null
  maximumPositionToQuoteReserveBps: 100
  maximumConcurrentPositions: 1
  maximumDailyLossBps: null
  stopRunOnDailyLoss: true
  allowReentrySameToken: false

execution:
  executionModelVersion: paper-execution-v1
  configuredLatencyMilliseconds: 2000
  maximumMarketDataAgeSeconds: 5
  maximumPositionToQuoteReserveBps: 100
  maximumPriceImpactBps: 1000
  additionalSlippageBps: 500
  networkFeeRaw: null
  priorityFeeRaw: null
  allowPartialFill: false
  deterministicFailureOnly: true

validation:
  protocolVersion: strategy-validation-v1
  developmentPercentage: 50
  validationPercentage: 20
  outOfSamplePercentage: 30
  minimumTotalClosedTrades: 300
  minimumOutOfSampleClosedTrades: 100
  minimumCalendarDays: 30
  minimumWalkForwardWindows: 3
  minimumOutOfSampleProfitFactor: 1.20
  maximumOutOfSampleDrawdownBps: 2500
  maximumExecutionFailureRateBps: 2000
  maximumExitUnavailableRateBps: 500
  maximumSingleTradeProfitContributionBps: 2000

  sensitivityPercentages: [-20, -10, 0, 10, 20]
  executionProfiles:
    - Baseline
    - ConservativeLatency
    - ConservativeSlippage
    - LiquidityStress
    - ZeroExitValueStress

ai:
  enabled: false
  themeClassificationEnabled: false
  riskExplanationEnabled: false
  reportSummaryEnabled: false
  modelVersion: null
  promptVersion: null
  timeoutMilliseconds: 5000
  failureMode: ContinueWithoutAI
```

## 4. Required Parameters Before Historical Replay

Adapter Spike 后必须填写：

- Launch Adapter；
- Pool Adapter；
- ProgramIds；
- RPC Source；
- Quote Token 规则；
- 起始 Slot 或数据范围；
- AdapterVersion；
- ParserVersion。

历史样本初步分析后必须填写：

- Minimum Momentum；
- Minimum Buy/Sell Ratio；
- Minimum Unique Buyers；
- Risk Score 阈值；
- Holder Concentration 阈值；
- Take Profit；
- Stop Loss；
- Maximum Holding Time；
- Liquidity Drop Trigger；
- Network/Priority Fee 假设；
- Initial Capital 和 Position Size。

未填写时，不允许启动正式 Validation 或 Out-of-Sample Run。

## 5. Parameter Units

| Suffix | Unit |
|---|---|
| `Seconds` | 秒 |
| `Milliseconds` | 毫秒 |
| `Bps` | 基点，10000 = 100% |
| `Raw` | 链上原始整数数量 |
| `Percentage` | 0–100 的百分比数值 |
| `Slot` | Solana Slot |
| `Version` | 不可变版本标识 |

禁止在同一字段中混用小数比例和百分比。

## 6. Configuration Validation

启动 Run 前校验：

- 所有 Required 参数非空；
- Development + Validation + OOS = 100；
- Stop Loss、Take Profit 和 Holding Time 大于 0；
- Position Limit 不超过 Quote Reserve Limit；
- Entry Age 不超过 Candidate Age；
- Market Data Age 与 Snapshot Interval 一致；
- 正式 Run 要求 finalized/reconciled；
- Adapter、Parser、Feature、Risk、Strategy、Execution 版本全部存在；
- Base Currency 与 Pool Quote Token 兼容；
- 账户资金和数量精度有效；
- AI FailureMode 不允许阻塞核心交易链路。

校验失败时不得创建 Active StrategyRun。

## 7. Configuration Snapshot

StrategyRun 保存：

```text
ConfigurationVersion
ConfigurationHash
CanonicalJson
AdapterVersion
ParserVersion
FeatureSetVersion
RiskModelVersion
StrategyVersion
ExitStrategyVersion
ExecutionModelVersion
ValidationProtocolVersion
CodeVersion
CreatedTime
```

Secret 字段只保存 Source 名称或 Secret Reference，不保存 Secret 值。

## 8. Configuration Change Rules

### Requires New StrategyVersion

- 进入或退出逻辑变化；
- 新增或删除策略 Feature；
- Signal 计算变化；
- 参数语义变化。

### Requires New RiskModelVersion

- Hard Reject 变化；
- Risk Score 权重或规则变化；
- Holder/Authority 风险解释变化。

### Requires New ExecutionModelVersion

- 延迟、Quote、滑点、费用或估值算法变化；
- 失败条件变化；
- 引入部分成交或路由。

### Requires New ConfigurationVersion Only

- 在同一算法版本下修改参数值；
- 修改热点关键词；
- 修改资本或仓位限制。

## 9. Environment Overrides

允许环境覆盖：

- RPC Endpoint Secret Reference；
- 数据库连接；
- 日志级别；
- API/Dashboard 域名；
- Feature Flag；
- Worker 并发度。

不允许环境静默覆盖：

- 策略阈值；
- 风险阈值；
- Paper Execution 参数；
- Validation 门槛。

影响研究结果的参数必须出现在 StrategyRun 快照中。

## 10. Initial Parameter Discovery

初始策略参数不是通过主观判断直接定稿，而是按以下顺序产生：

1. Adapter Spike 获取真实样本；
2. 描述性统计得到合理数量级；
3. 在 Development 区间探索参数；
4. 在 Validation 区间选择稳定范围；
5. 冻结 StrategyVersion 和配置；
6. 运行未观察过的 OOS；
7. 完成 Forward Paper。

查看 OOS 后修改参数，必须升级版本并使用新的未观察数据。