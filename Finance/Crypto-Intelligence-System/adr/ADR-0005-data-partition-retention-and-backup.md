# ADR-0005: Data Partitioning, Retention and Backup

- Status: Accepted with measurement review
- Date: 2026-07-28
- Scope: Phase 1 PostgreSQL data

## Context

Raw Event、Swap、MarketSnapshot 和 Feature 数据会持续增长。Phase 1 尚无真实生产数据量，因此需要先定义保守默认值，并在采集到真实数据后通过容量报告调整。

系统的数据资产目标要求：未经验证的清理不得删除无法重建的原始事实。

## Decision

### Partitioning

按月对以下高增长表按 `EventTime` 或 `AsOfTime` 分区：

- RawBlockchainEvent；
- NormalizedDomainEvent；
- SwapEvent；
- LiquidityEvent；
- MarketSnapshot；
- WalletHoldingSnapshot；
- FeatureSnapshot；
- EquitySnapshot。

小型配置、定义和当前状态表不分区。

所有分区必须提前创建并有缺失分区告警。

### Retention Classes

#### Class A: Irreplaceable facts

- RawBlockchainEvent；
- IngestionCheckpoint；
- Parser/processing history。

策略：未经成功归档和校验不得删除。Phase 1 默认长期保存。

#### Class B: Rebuildable facts and projections

- NormalizedDomainEvent；
- SwapEvent；
- LiquidityEvent；
- MarketSnapshot；
- Wallet snapshots。

策略：PostgreSQL 热数据默认保留 180 天；更早数据只有在已归档且可通过抽样恢复验证后才允许清理。

#### Class C: Research and audit results

- FeatureSnapshot；
- RiskAssessment；
- Signal；
- Decision；
- StrategyRun；
- Paper Order/Fill/Position；
- PerformanceReport。

策略：长期保存。它们是研究复现和方案比较依据。

#### Class D: Operational data

- 应用日志；
- 临时缓存；
- 非审计型指标明细。

策略：默认 30 天，聚合指标可以保留更久。

### Market Snapshot Sampling

不允许无控制地按固定超高频率保存所有 Token 快照。

优先保存：

- 状态变化事件；
- 策略决策使用的快照；
- Paper Execution 使用的快照；
- 配置频率的研究快照。

降采样必须生成新数据集，不覆盖策略运行曾引用的原始快照。

### Capacity Review

Milestone 2 稳定采集满 7 天后生成容量报告：

```text
EventsPerDay
RawBytesPerDay
IndexBytesPerDay
SwapRowsPerDay
SnapshotRowsPerDay
LargestTableGrowth
BackupSize
RestoreDuration
```

报告用于调整分区大小、热数据窗口和归档方案。

### Backup

最低要求：

- 每日完整备份；
- 支持时间点恢复的日志/WAL 归档；
- 备份加密；
- 备份与运行数据库隔离；
- 每月至少一次恢复演练；
- 恢复演练必须记录耗时、数据校验和失败原因。

### Deletion Safety

自动清理必须满足：

1. 数据属于允许清理的 Retention Class；
2. 已达到保留期限；
3. 所需归档已完成；
4. 归档校验通过；
5. 没有 StrategyRun 或审计记录仍引用该数据；
6. 删除以分区为单位执行并生成审计记录。

## Consequences

- Phase 1 初期存储成本偏保守；
- 实际保留期由 7 天容量报告校准；
- 原始事实和研究结果不会被常规清理破坏；
- 需要实现引用检查和恢复验证。

## Review Trigger

以下任一条件触发复审：

- 7 天容量报告完成；
- 单表或单分区达到运维阈值；
- 备份或恢复超过 SLO；
- Phase 3 训练数据需求明确；
- 引入对象存储或独立分析仓库。