# Phase 1 Observability and SLO

> 状态：Accepted baseline  
> 适用范围：Phase 1 research system

## 1. Purpose

保证系统能够区分“程序仍在运行”和“数据仍然完整”。Phase 1 的首要可靠性目标是发现数据停止、延迟、缺口、重复和不可处理事件。

## 2. Service Indicators

### Ingestion

```text
LatestObservedSlot
LatestPersistedSlot
LatestProcessedSlot
LatestFinalizedSlot
LatestReconciledSlot
ObservedLagSeconds
ReconciledLagSeconds
WebSocketConnected
WebSocketReconnectCount
RpcRequestRate
RpcErrorRate
RpcRateLimitCount
RawEventsPerMinute
```

### Processing

```text
PendingEventCount
OldestPendingAgeSeconds
ProcessingEventCount
RetryableFailureCount
DeadLetterCount
DuplicateEventCount
ParserUnsupportedCount
ProjectionLagSeconds
FeatureLagSeconds
DecisionLagSeconds
```

### Data Quality

```text
DetectedGapCount
UnresolvedGapCount
RevertedEventCount
MissingTransactionDetailCount
StaleMarketSnapshotCount
UnknownProgramVersionCount
ReconciliationMismatchCount
```

### Paper Trading

```text
SubmittedOrderCount
FilledOrderCount
FailedOrderCount
ExitUnavailableCount
ValuationUnavailableCount
ExecutionLatencySeconds
PriceImpactBps
FeeRatio
SlippageRatio
```

### Infrastructure

```text
DatabaseConnectionUsage
DatabaseStorageBytes
PartitionGrowthBytes
QueryLatency
BackupAge
LastRestoreTestTime
WorkerRestartCount
MemoryUsage
CpuUsage
```

## 3. Phase 1 SLO Targets

### Data Durability

- 已确认写入 PostgreSQL 的 Raw Event 允许永久丢失数量：0；
- Checkpoint 不得推进到尚未完成持久化的范围；
- Redis 或 Worker 重启造成不可恢复数据丢失数量：0。

### Ingestion Availability

- 月度可用性目标：99.0%；
- WebSocket 断线后 60 秒内开始重连；
- WebSocket 恢复后 5 分钟内开始缺口补采；
- RPC Source 故障无法补采时 5 分钟内告警。

### Data Freshness

在 Source 正常且无大规模积压时：

```text
ObservedLagSeconds P95 <= 30 seconds
ProcessedLagSeconds P95 <= 120 seconds
ReconciledLagSeconds P95 <= 300 seconds
```

正式 StrategyRun 只消费 finalized/reconciled 数据，因此低延迟雷达和正式策略的延迟指标分开统计。

### Backlog

正常运行目标：

```text
OldestPendingAge <= 5 minutes
OldestRetryableFailureAge <= 15 minutes
```

持续 10 分钟超过目标触发告警。

### Data Gap

- 检测到连续 Slot 或 Adapter 数据异常后 5 分钟内创建 Gap 记录；
- 未解决 Gap 存在时不得推进 ReconciledThroughSlot；
- 正式 Validation/OOS 区间允许未声明缺口数量：0。

### Dead Letter

- 新增 Dead Letter 立即产生事件；
- 5 分钟内产生告警；
- 24 小时内完成分类；
- Dead Letter 不允许通过静默跳过来推进正式研究水位。

### API and Dashboard

- 月度可用性目标：99.0%；
- 常用查询 P95 <= 1 second；
- 大型历史分析查询应使用异步任务，不占用在线请求；
- Dashboard 状态最长缓存 60 秒，并显示数据更新时间。

### Backup and Recovery

```text
RPO <= 5 minutes
RTO <= 4 hours
Full Backup Frequency = daily
Restore Drill Frequency = monthly
```

恢复演练必须验证：

- Raw Event 数量和时间范围；
- 最新 Checkpoint；
- 关键表关系；
- 最近 StrategyRun 可查询；
- 重建和回放能力。

## 4. Alert Severity

### Critical

- Raw Event 持久化失败持续 5 分钟；
- 数据库不可用；
- Checkpoint 越过未持久化数据；
- 检测到数据永久丢失；
- 账户或 Position 不变量被破坏；
- Backup 超过 36 小时未成功。

### High

- Reconciled Lag 超过 15 分钟；
- 存在未解决 Gap；
- 新增 Dead Letter；
- RPC Source 大面积失败；
- ExitUnavailable 异常增长；
- 恢复演练失败。

### Warning

- P95 Freshness 超过目标；
- Pending Backlog 持续增长；
- 数据库或分区增长异常；
- ParserUnsupported 增长；
- MarketSnapshot 过期率增长。

## 5. Structured Logging

每条关键日志包含：

```text
Timestamp
Level
Service
Module
CorrelationId
RunId
RawEventId
EntityType
EntityId
Slot
TransactionSignature
ParserVersion
ErrorType
```

禁止记录：

- RPC/API Secret；
- 数据库密码；
- 未来可能引入的私钥或签名材料；
- 未经过滤的敏感配置。

## 6. Tracing

关键 Trace：

```text
RPC/WebSocket Receive
→ Raw Event Persist
→ Parse
→ Projection
→ Feature
→ Risk
→ Signal
→ Decision
→ Paper Order
→ Fill / Failure
```

跨异步步骤通过 CorrelationId、RawEventId 和 RunId 关联。

## 7. Runbooks

至少提供：

- WebSocket 断线与补采；
- RPC Source 故障切换；
- Checkpoint 停滞；
- 数据缺口处理；
- Dead Letter 重试；
- Parser 不支持；
- 数据库容量告警；
- Backup 恢复；
- Paper Trading 不变量错误。

## 8. SLO Review

Milestone 2 稳定采集满 7 天后，根据实际事件量、RPC 性能和数据库负载复审 SLO。任何放宽必须记录原因，不能为了隐藏持续故障而降低目标。