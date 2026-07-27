# M2 Reliable Ingestion and New Token Radar

> 状态：Implementation in progress
> 当前增量：M2A Reliable Ingestion Foundation

## 本增量目标

先保证链上事件“不丢、不重、可恢复”，再接入实时数据源和 Radar 投影。

本增量覆盖：

- M2-01 Raw Event Schema 的核心表、确定性 EventId、EventOrdinal、唯一约束和关键索引；
- M2-02 五级 Checkpoint 水位、Slot 完成状态和 Gap 阻断规则；
- M2-05 At-least-once 持久派发、Worker Lease、过期回收、重试和 Dead Letter；
- Migration 002；
- 重复身份、连续水位、Gap、租约回收、失败重试和派发测试。

## 已建立的数据表

```text
raw_blockchain_events
ingestion_checkpoints
ingestion_slot_states
```

`raw_blockchain_events` 保存不可变 Payload。事件通过以下字段形成确定性身份：

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

数据库同时对该身份和 SHA-256 `EventId` 建立唯一约束。重复输入返回已有事件，不产生第二条投影。

## 水位与 Gap

```text
ObservedThroughSlot
>= PersistedThroughSlot
>= ProcessedThroughSlot
>= FinalizedThroughSlot
>= ReconciledThroughSlot
```

所有水位只允许连续推进。`ReconciledThroughSlot` 遇到已知 Gap 必须停止，不能用后续成功 Slot 掩盖缺失数据。

## 持久派发

Worker 使用数据库行锁和 `SKIP LOCKED` 领取批次：

```text
Pending / RetryableFailure
→ Processing with Lease
→ Completed
   or RetryableFailure
   or DeadLetter
```

Worker 崩溃后，过期 Lease 可以由其他实例回收。成功处理和失败状态都写回数据库，原始 Payload 不删除。

## 下一增量

M2B 将继续实现：

- WebSocket 发现、自动重连和断线记录；
- RPC 交易详情、限流退避、备用 Source 与补采；
- 将 Raydium Spike Parser 迁移为正式 Adapter；
- Token/Pool/Swap/Liquidity 投影；
- TokenCandidate、滚动 FeatureSnapshot 和确定性 Replay；
- Raw Event 时间分区与保留策略的 PostgreSQL 验证。

M2 未完成前，不进入风险判断、策略决策或模拟交易。
