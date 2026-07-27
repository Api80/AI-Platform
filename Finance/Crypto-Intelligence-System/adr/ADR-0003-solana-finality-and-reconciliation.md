# ADR-0003: Solana Finality, Canonicality and Reconciliation

- Status: Accepted
- Date: 2026-07-28
- Scope: Solana Phase 1

## Context

Solana RPC 和 PubSub 支持不同 commitment。低延迟通知可能尚未达到最终状态，而策略研究需要明确哪些数据可以进入 Feature、Decision 和 Paper Trading。

单一 WebSocket 连接不能证明数据完整。部分订阅能力也取决于 RPC 节点配置，因此必须结合 RPC 查询完成对账。

官方参考：

- https://solana.com/docs/rpc/websocket/accountsubscribe
- https://solana.com/docs/rpc/websocket/blocksubscribe
- https://solana.com/docs/rpc/http/gettransaction

## Decision

### Commitment Use

```text
Observed / Confirmed data
    → 低延迟雷达和 provisional 分析

Finalized and reconciled data
    → 策略研究、Decision 和 Paper Trading
```

默认安全策略：只有同时满足以下条件的数据才能进入正式 StrategyRun：

- `CanonicalStatus = Finalized`；
- 对应 Slot 不大于 `ReconciledThroughSlot`；
- Raw Event 和必要交易详情已持久化；
- Parser 已成功完成。

需要低延迟实验时，可以建立独立 `StreamingPaperProvisional` RunType，但必须与正式结果分开统计。

### Canonical Status

Raw Event 增加：

```text
CanonicalStatus
├── Observed
├── Confirmed
├── Finalized
└── Reverted

CommitmentLevel
FinalityUpdatedTime
RevertedTime
RevertReason
```

Normalized Event、Projection、Feature、Risk、Signal 和 Decision 必须能够追溯到 Raw Event 的 canonical 状态。

### Reversion Handling

如果已观察或已确认事件未进入最终链：

1. Raw Event 不删除，标记为 Reverted；
2. 对应 Normalized Event 标记非 canonical；
3. 当前状态投影通过重放或补偿重新构建；
4. Provisional Feature、Risk、Signal 和 Decision 标记失效；
5. 正式 StrategyRun 不应受到影响，因为只消费 finalized/reconciled 数据；
6. 如果 provisional Paper Run 已经执行，保留记录并标记来源事件已回退，不覆盖历史。

### Reconciliation

每个 Slot 范围执行：

```text
WebSocket discovery
    ↓
RPC transaction/block retrieval
    ↓
Signature and transaction-detail comparison
    ↓
Finality refresh
    ↓
Gap detection
    ↓
ReconciledThroughSlot advance
```

对账不得依赖单一通知计数。

### Data Availability

RPC 返回暂时为空时，先分类为 `DataUnavailable`，进入有上限的退避重试；不能立即判定交易不存在。

当历史节点无法提供所需范围时：

- 停止推进 ReconciledThroughSlot；
- 触发数据缺口告警；
- 尝试备用 Source；
- 明确记录不可恢复缺口。

## Consequences

优点：

- 正式策略结果不受 provisional 事件回退影响；
- 雷达仍可使用较低延迟数据；
- 数据完整性可以通过独立对账验证。

代价：

- 正式 Paper Trading 有额外延迟；
- 需要维护 canonical 状态和对账水位；
- provisional 与 finalized 结果必须分开。

## Required Tests

- Observed → Confirmed → Finalized；
- Observed/Confirmed → Reverted；
- RPC 暂时返回空；
- WebSocket 漏事件但 RPC 对账发现；
- Reconciled 水位遇到缺口停止；
- Provisional 派生结果失效；
- Finalized StrategyRun 不读取 provisional 数据。