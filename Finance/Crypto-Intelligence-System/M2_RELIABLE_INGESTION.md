# M2 Reliable Ingestion and New Token Radar

> 状态：Implementation in progress
> 当前增量：M2C New Token Radar Projections

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

## M2B Solana Sources and Raydium Adapter

M2B 增加：

- Solana `logsSubscribe` WebSocket 发现；
- 多 Program Subscription、Subscription ID 映射和 Signature 有界去重；
- 断线记录、自动重连和指数退避；
- `getTransaction` RPC 获取、暂时空值重试、HTTP 限流退避；
- 主 RPC 失败后的独立备用 Source；
- 固定 IDL 内嵌和正式 Raydium LaunchLab/CPMM Adapter；
- Unknown Program Version 明确失败；
- Raw Transaction 持久化后再进行 Adapter 解析；
- `normalized_domain_events` 幂等追加和 Migration 003；
- Worker 采集与持久派发循环。

Worker 只从环境变量读取 RPC Endpoint，Endpoint 不进入配置快照或仓库：

```text
SOLANA_RPC_WS_URL
SOLANA_RPC_HTTP_URL
SOLANA_RPC_FALLBACK_HTTP_URL
```

未提供前两个变量时 Worker 保持安全禁用状态。系统不读取私钥，不签名，不发送交易。

## M2C New Token Radar Projections

M2C 增加：

- Token/Pool/Swap/Liquidity 投影；
- 最小 Wallet 和 MarketSnapshot 投影；
- TokenCandidate 的 Discovered/Observing/Eligible/Rejected/Expired 状态；
- 15/30/60/180 秒配置化滚动窗口；
- Price Change、Buy/Sell、Unique Buyers、交易速度、流动性变化、No Trade Duration 和 Price Impact；
- Quote Token 明确的价格语义；
- 实时处理与 Replay 共用 `IProjectionEventHandler`；
- 按 Event Time、Slot、EventOrdinal 确定性重放；
- Radar Candidate 列表与详情只读 API；
- Migration 004。

```text
GET /api/v1/radar/candidates
GET /api/v1/radar/candidates/{tokenAddress}
```

当前 Adapter 从 Raydium 指令参数生成首版 Radar 投影。对于 Swap，输入数量和最低输出数量属于交易意图，不等同于链上最终成交结果。进入风险、策略和 Paper Trading 前，必须继续解析 Event Payload，并用交易后的账户余额与池储备变化完成对账；未经对账的数量和价格只用于验证采集、候选状态与滚动窗口链路。

## 下一增量

M2D 将完成 M2 Exit Gate：

- Checkpoint 与实时 Worker 的完整联动；
- Slot/Signature Backfill 和 Finality Refresh；
- ReconciledThroughSlot 连续推进验证；
- Raw Event 分区参数和保留策略的 PostgreSQL 容量验证；
- 连续采集试运行和数据缺口报告。

M2 未完成前，不进入风险判断、策略决策或模拟交易。
