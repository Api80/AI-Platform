# ADR-0002: Event Delivery, Idempotency and Checkpoints

- Status: Accepted
- Date: 2026-07-28
- Scope: Phase 1 ingestion and processing

## Context

WebSocket、RPC 补采、进程重启和重试都会导致事件重复、乱序或延迟到达。系统不能依赖 Exactly Once，也不能在数据落库前把事件只放入内存队列。

## Decision

### Delivery Semantics

系统采用：

```text
At-least-once delivery
+ Durable raw event store
+ Idempotent consumers
```

不声明 Exactly Once。

### Event Identity

Raw Event 使用确定性身份：

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

`EventOrdinal` 用于同一 Instruction 中出现多个相同 EventType 的情况。

Normalized Domain Event 使用：

```text
RawEventId
DomainEventType
DomainEventIndex
ParserVersion
```

`DomainEventIndex` 必须由解析顺序确定，并在重复解析时保持稳定。

### Durable Dispatch

Phase 1 使用 PostgreSQL 状态队列：

```text
Raw Event INSERT
    ↓ same transaction
Processing State = Pending
    ↓
Worker claims lease
    ↓
Handler executes idempotently
    ↓
Projection/result commit
    ↓ same transaction
Processing State = Completed
```

Worker Claim 字段：

```text
LeaseOwner
LeaseUntil
AttemptCount
NextAttemptTime
```

租约到期后允许其他 Worker 重新处理。

### Retry Policy

错误分类：

```text
Transient
DataUnavailable
ParserUnsupported
PermanentInvalidData
SystemInvariantViolation
```

- Transient、DataUnavailable：指数退避并加入抖动；
- ParserUnsupported：等待 Parser 升级或人工处理；
- PermanentInvalidData：进入 Dead Letter；
- SystemInvariantViolation：立即告警并停止相关实体的后续处理。

重试次数、首次失败、最后失败和错误摘要必须保存。

### Checkpoint Watermarks

每个 Source/Subscription 保存独立水位：

```text
ObservedThroughSlot
PersistedThroughSlot
ProcessedThroughSlot
FinalizedThroughSlot
ReconciledThroughSlot
```

定义：

- Observed：数据源已报告到该 Slot；
- Persisted：该范围已发现事件完成原始落库；
- Processed：该范围已完成要求的解析和投影；
- Finalized：该范围达到策略要求的最终性；
- Reconciled：已通过 RPC 对账确认没有已知缺口。

水位只能连续推进，不能越过未完成 Slot。

### Ordering

系统不假设全局严格顺序。

- 不同 Token/Pool 可以并行处理；
- 同一实体使用 Slot、TransactionIndex、InstructionIndex、EventOrdinal 排序；
- 当前状态投影拒绝旧版本覆盖新版本；
- 历史事实按事件身份追加保存。

## Consequences

- 重复处理是正常情况；
- 所有 Handler 必须可重复执行；
- Checkpoint 推进需要维护 Slot 完成状态；
- 数据库唯一约束是最后一道幂等保护。

## Required Tests

- 重复投递；
- 同交易相同类型多事件；
- 乱序到达；
- Worker 在持久化前后崩溃；
- Lease 超时；
- Retry 与 Dead Letter；
- Checkpoint 不越过缺口；
- 重放结果一致性。