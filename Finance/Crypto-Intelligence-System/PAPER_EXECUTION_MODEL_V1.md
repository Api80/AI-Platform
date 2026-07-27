# Paper Execution Model V1

> 状态：Accepted for Phase 1 baseline  
> 模型版本：`paper-execution-v1`

## 1. Purpose

定义可复现、保守且能够解释失败原因的模拟成交模型。目标不是精确预测每笔真实交易结果，而是避免按理想快照价格无条件成交，从而高估策略表现。

## 2. Supported Execution

Phase 1 V1 支持 AMM/Pool Swap：

```text
Decision Enter → Buy Swap
Decision Exit  → Sell Swap
```

不支持：

- 限价订单簿排队；
- 拆单路由；
- 多 DEX 最优路由；
- MEV、抢跑或优先打包模拟；
- 随机成交概率；
- 真实链上签名。

AMM Swap 在 V1 中按原子执行建模：全部成交或失败。数据结构保留 Partial Fill 能力，但 V1 默认不产生部分成交。

## 3. Deterministic Inputs

每次 ExecutionAttempt 必须引用：

```text
OrderId
DecisionId
PoolId
MarketSnapshotId
DecisionTime
EligibleExecutionTime
ExecutionModelVersion
ExecutionParameters
```

相同输入和参数必须产生相同结果。V1 不使用随机失败概率。

## 4. Execution Time

```text
EligibleExecutionTime = Decision.CreatedTime + ConfiguredLatency
```

基准研究配置：

```text
ConfiguredLatency = 2 seconds
MaxMarketDataAge = 5 seconds
```

敏感性场景：

```text
Fast:         0.5 seconds
Baseline:     2 seconds
Conservative: 5 seconds
```

成交使用 `EligibleExecutionTime` 之后第一份满足以下条件的市场状态：

- Finalized；
- Reconciled；
- Pool 与 Quote Token 匹配；
- 数据年龄不超过 MaxMarketDataAge；
- Reserve 数据完整。

如果找不到有效状态，订单失败，原因是 `NoEligibleMarketState` 或 `StaleMarketState`。

## 5. Quote and Price Impact

每个 Pool Adapter 必须实现确定性的：

```text
QuoteExactInput(
    PoolState,
    InputToken,
    InputAmount,
    AdapterVersion)
```

返回：

```text
ExpectedOutputAmount
AverageExecutionPrice
PoolTradingFee
PriceImpactBps
QuoteReason
```

如果 Pool 类型符合常数乘积模型，可以使用 Pool Adapter 内部的 reserve 公式；其他类型必须由对应 Adapter 实现，不能错误套用统一公式。

系统保存 AdapterVersion 和输入 PoolState，确保 Quote 可以复现。

## 6. Conservative Constraints

基准配置：

```text
MaxPositionToQuoteReserve = 1%
MaxPriceImpactBps = 1000
MaxAdditionalSlippageBps = 500
```

说明：

- 订单名义金额不得超过执行时 Quote Reserve 的 1%；
- Adapter 估算价格冲击超过 10% 时失败；
- Additional Slippage 是对 Adapter Quote 的额外保守折价，基准为 5%；
- 所有值均属于 StrategyRun 配置，并参加敏感性测试。

买入成交价格向不利方向调整，卖出成交输出向不利方向调整。

## 7. Fees

总费用包括：

```text
PoolTradingFee
ProtocolFee
NetworkFeeAssumption
PriorityFeeAssumption
```

规则：

- Pool/Protocol Fee 优先来自 Adapter Quote；
- Network/Priority Fee 使用版本化配置；
- 无法确认的费用使用保守上限，不使用 0；
- Fee 必须单独记录，不允许只写入最终 Profit。

## 8. Execution Failure Reasons

V1 使用确定性失败条件：

```text
NoEligibleMarketState
StaleMarketState
PoolInactive
InsufficientLiquidity
PositionLimitExceeded
PriceImpactExceeded
SlippageExceeded
QuoteFailed
UnsupportedPoolVersion
TokenRiskHardReject
InsufficientAccountCash
InsufficientPosition
ExitUnavailable
DataNotFinalized
DataNotReconciled
```

所有失败订单计入策略统计，不能删除或从分母中排除。

## 9. Buy Execution

执行步骤：

1. 校验 Decision 为 Enter；
2. 校验 RiskAssessment 未 HardReject；
3. 校验账户可用资金和仓位限制；
4. 选择 Eligible Market State；
5. 调用 Adapter Quote；
6. 校验储备比例、价格冲击和滑点；
7. 扣除费用；
8. 生成 ExecutionAttempt 和 PaperFill；
9. 由 Fill 更新 PaperPosition 和 Account；
10. 生成 EquitySnapshot。

## 10. Sell Execution

执行步骤：

1. 校验 Decision 为 Exit；
2. 校验账户持有足够 Position；
3. 选择 Eligible Market State；
4. 调用反向 Adapter Quote；
5. 校验 Pool Active、储备、价格冲击和滑点；
6. 如果无法获得有效输出，记录 `ExitUnavailable`；
7. 成功时生成 Fill 并更新 Position；
8. 记录 Realized PnL、费用和退出原因。

无法退出的 Position 不能按最后价格假设平仓。

## 11. Valuation

未平仓 Position 的估值使用：

```text
ConservativeExitValue
```

即按当前 Position 全量执行反向 Quote 后的可获得金额扣除费用，而不是简单使用 `Quantity × LastPrice`。

无法取得有效退出 Quote 时：

- 标记 `ValuationUnavailable`；
- 风险报告单独显示；
- 压力场景可以按 0 退出价值计算；
- 不允许使用理想价格掩盖无法退出风险。

## 12. Run Profiles

每个策略至少执行：

```text
Baseline
ConservativeLatency
ConservativeSlippage
LiquidityStress
ZeroExitValueStress
```

只有在 Baseline 和约定压力场景下均满足策略验证门槛，才能认为结果具有进一步研究价值。

## 13. Required Tests

- 相同输入结果完全一致；
- Fast/Baseline/Conservative 延迟选择不同快照；
- stale market state；
- 买卖价格不利调整；
- 费用单独计入；
- 价格冲击超过上限；
- 储备比例超过上限；
- 无法退出；
- Position 只能由 Fill 改变；
- 失败交易进入绩效报告；
- 未平仓保守退出估值；
- AdapterVersion 变化产生独立运行版本。

## 14. Review Trigger

以下情况升级模型版本：

- 新增不同 Pool 类型；
- 引入路由或拆单；
- 引入基于历史链上失败率的模型；
- 引入真实小额执行校准；
- 修改成交、费用、延迟或估值核心规则。