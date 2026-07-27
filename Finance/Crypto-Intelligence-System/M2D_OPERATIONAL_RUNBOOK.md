# M2D Operational Validation Runbook

## 目标

在真实 PostgreSQL、主 Solana RPC 和独立备用 RPC 环境中验证 M2 可靠采集出口条件。该运行只采集公开链上数据，不读取私钥、不签名、不发送交易。

## 启动前

必须通过环境变量提供：

```text
CRYPTO_DB_CONNECTION
SOLANA_RPC_WS_URL
SOLANA_RPC_HTTP_URL
SOLANA_RPC_FALLBACK_HTTP_URL
```

正式验收前还必须在版本化配置中明确：

- `historicalRunStartSlot`；
- `backfillMaximumSlotsPerCycle`；
- `backfillMaximumSignaturesPerCycle`；
- `reconciliationIntervalSeconds`；
- `partitionAheadMonths`；
- `rebuildableHotRetentionDays`；
- `operationalRetentionDays`。

RPC URL 和数据库密码不得写入仓库、配置快照或验收报告。

## 每日检查

保存以下接口的 JSON 输出并记录采样时间：

```text
GET /api/v1/ingestion/checkpoints
GET /api/v1/ingestion/gaps?limit=1000
GET /api/v1/ingestion/capacity
GET /health/ready
```

同时记录：

- Worker 启停、WebSocket 断线和自动重连时间；
- 主 RPC/备用 RPC 切换；
- Retry、Dead Letter 和不可恢复 Gap；
- 数据库备份大小；
- 最长采集延迟和积压恢复时间。

## 故障演练

至少执行一次：

1. 停止 Worker，等待产生可观测 Slot 差距；
2. 重新启动 Worker；
3. 确认从 Checkpoint 分段补采；
4. 确认重复 Signature 不生成重复 Raw Event 或投影；
5. 确认 Reconciled 水位只在处理完成、finalized 且无 Gap 时推进；
6. 暂时使主 RPC 不可用，确认备用 RPC 接管；
7. 恢复主 RPC 并确认系统继续运行。

不得通过删除 Checkpoint、手工跳过 Slot 或清除 Gap 来制造通过结果。

## 七天容量复审

使用每日容量快照计算：

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

物理分区状态由容量接口的 `isPartitioned` 字段报告。当前表尚未转换为物理分区表；必须根据七天真实增长和维护窗口单独评审迁移方案，不能在缺少容量证据时直接重建高增长表。

## 通过条件

- 连续运行不少于 7 天；
- 正式验证 Slot 区间未解决 Gap 为 0；
- 断线补采和备用 Source 演练通过；
- Checkpoint 五级水位关系始终成立；
- Reconciled 水位从未跨越 Gap；
- 备份可恢复且恢复耗时已记录；
- 容量报告足以决定分区大小、提前创建月份和热数据保留期。

任一条件未满足，M2 状态保持“Operational validation pending”。
