# Strategy Validation Protocol V1

> 状态：Accepted for Phase 1 research gate  
> 协议版本：`strategy-validation-v1`

## 1. Purpose

在查看样本外结果之前预先定义数据分割、指标、压力测试和通过门槛，降低未来数据泄漏、参数过拟合、幸存者偏差和选择性报告风险。

本协议用于判断策略是否值得继续研究，不构成真实交易批准。

## 2. Run Classification

```text
Exploratory Run
    用于开发特征和参数，不用于证明策略有效

Validation Run
    使用固定策略版本和验证区间

Out-of-Sample Run
    参数冻结后对未使用数据进行最终评估

Forward Paper Run
    使用实时到达的 finalized/reconciled 数据
```

任何使用过样本外结果调整参数的策略，必须升级 StrategyVersion，并重新选择未使用的数据作为新的样本外区间。

## 3. Data Eligibility

进入正式 Validation 或 Out-of-Sample 的数据必须：

- 来自已声明的 Adapter 范围；
- Raw Event 已持久化；
- CanonicalStatus = Finalized；
- Slot 不超过 ReconciledThroughSlot；
- 不存在未声明的数据缺口；
- Feature 使用 `AsOfTime` 之前可观察的数据；
- 所有标签使用独立 LabelTime，且不会回流到历史特征。

包含缺口的区间只能用于故障测试，不能计入正式结果。

## 4. Time-Based Split

禁止随机打乱时间序列。

Phase 1 默认：

```text
Development: 50%
Validation:  20%
Out-of-Sample: 30%
```

按 Event Time 连续划分。

另外至少执行 3 个滚动 Walk-Forward Window，每个 Window 都保持训练/验证发生在测试之前。

## 5. Minimum Sample

策略只有同时满足以下条件才能进入“Validated Research Candidate”：

- 总关闭交易数不少于 300；
- Out-of-Sample 关闭交易数不少于 100；
- 覆盖时间不少于 30 个自然日；
- 不少于 3 个独立 Walk-Forward 测试窗口；
- 单一 Token、单日或单一来源不能贡献绝大多数交易。

样本不足时报告结果，但状态只能是 `InsufficientEvidence`。

## 6. Required Metrics

每个数据区间和压力场景分别报告：

- Enter Decisions；
- Submitted Orders；
- Filled Orders；
- Failed Orders；
- Closed Trades；
- ExitUnavailable；
- Gross Return；
- Net Return；
- Max Drawdown；
- Win Rate；
- Average Win；
- Average Loss；
- Payoff Ratio；
- Profit Factor；
- Fee Ratio；
- Slippage Ratio；
- Holding Time Distribution；
- Return per Trade；
- 单笔最大盈利贡献；
- 按流动性、Token Age、Risk Level 和来源的分组表现。

## 7. Phase 1 Pass Gates

Out-of-Sample Baseline 必须全部满足：

```text
NetReturn > 0
ProfitFactor >= 1.20
MaxDrawdown <= 25%
ClosedTrades >= 100
ExecutionFailureRate <= 20%
ExitUnavailableRate <= 5%
LargestSingleTradeProfitContribution <= 20%
```

统计要求：

- 使用按交易或按日分组的 Bootstrap 置信区间；
- 平均净收益的 95% 置信区间下界必须大于 0；
- 至少 2/3 Walk-Forward 测试窗口净收益为正；
- 不允许依靠一个极端盈利交易通过整体门槛。

如果 Return 分布使普通 Bootstrap 不稳定，报告多种分组方式，并将策略状态保持为 `NeedsMoreEvidence`。

## 8. Execution Stress Gates

必须使用相同 StrategyVersion 执行：

```text
Baseline
ConservativeLatency
ConservativeSlippage
LiquidityStress
ZeroExitValueStress
```

最低要求：

- ConservativeLatency：NetReturn > 0 且 ProfitFactor >= 1.0；
- ConservativeSlippage：NetReturn > 0 且 ProfitFactor >= 1.0；
- LiquidityStress：不得出现无法解释的账户负余额或模型不变量破坏；
- ZeroExitValueStress：必须报告最坏回撤和资本损失，不要求净收益为正，但不得隐藏结果。

## 9. Parameter Sensitivity

对主要阈值执行局部扰动：

```text
-20%
-10%
Baseline
+10%
+20%
```

通过要求：

- 大多数相邻参数组合的 OOS NetReturn 为正；
- 不能只有单一精确参数点盈利；
- 参数变化不应使交易数、收益或回撤出现无法解释的断崖式变化。

## 10. Bias Controls

必须检查：

- Future Data Leakage；
- Survivorship Bias；
- Selection Bias；
- Look-ahead Label；
- 缺失交易被静默删除；
- 无法退出 Position 被理想价格估值；
- 重复 Token/Event；
- 参数和策略多重试验。

每次 Experiment 保存：

```text
ExperimentId
Hypothesis
StrategyVersion
Parameters
DataRange
Metrics
Decision
CreatedTime
```

失败实验不能删除，以便识别多重试验和选择性报告。

## 11. Decision Status

```text
Exploratory
InsufficientEvidence
NeedsMoreEvidence
ValidatedResearchCandidate
Rejected
```

`ValidatedResearchCandidate` 只表示通过 Phase 1 研究门槛，不代表可以进入真实交易。

真实交易需要独立的：

- 前向 Paper 运行；
- 安全评审；
- 风险预算；
- 小额执行校准；
- 人工批准。

## 12. Forward Paper Requirement

在任何真实执行讨论之前，策略至少完成一个独立 Forward Paper 周期，期间：

- StrategyVersion 和核心参数冻结；
- 只使用实时到达的 finalized/reconciled 数据；
- 所有失败、缺口和人工干预被记录；
- 结果与历史 OOS 的差异被解释。

具体运行时长和最低交易数在 Phase 4 评审时确定，不能由历史回测替代。

## 13. Protocol Change Rule

查看 OOS 结果后修改通过门槛，需要：

1. 升级 ValidationProtocolVersion；
2. 记录修改理由；
3. 将旧 OOS 视为已使用数据；
4. 使用新的未观察数据重新验证。

不得为了让现有策略通过而回溯修改协议。