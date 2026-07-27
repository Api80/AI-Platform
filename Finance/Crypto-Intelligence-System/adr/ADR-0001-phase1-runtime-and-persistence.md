# ADR-0001: Phase 1 Runtime and Persistence Architecture

- Status: Accepted
- Date: 2026-07-28
- Scope: Crypto Intelligence System Phase 1

## Context

Phase 1 需要可靠采集 Solana 事件、生成分析结果并进行 Paper Trading，但当前规模和团队边界尚不足以证明需要拆分独立微服务。

系统需要明确唯一事实来源，避免 PostgreSQL、Redis、进程内 Channel 和未来消息队列之间出现数据归属不清。

## Decision

### Runtime

Phase 1 采用模块化单体：

```text
CryptoIntelligence.Worker
CryptoIntelligence.Api
CryptoIntelligence.Dashboard
PostgreSQL
Redis（可选）
```

Worker 内部模块：

```text
Ingestion
EventStore
EventDispatching
Projection
Features
Risk
Signals
Decisions
PaperTrading
Analytics
```

模块通过明确接口和事件契约交互，不直接访问其他模块的内部实现。

### Persistence

PostgreSQL 是唯一事实来源，保存：

- Raw Blockchain Events；
- Checkpoints 和处理状态；
- Domain Projections；
- Features、Risk、Signals、Decisions；
- Paper Orders、Fills、Positions；
- Strategy Runs 和 Performance Results。

Redis 只允许用于：

- 查询缓存；
- 短期实时状态；
- 可丢失的性能优化。

任何 Redis 数据都必须能够从 PostgreSQL 重建。

`.NET Channel` 只用于进程内并发、背压和工作分发。只有已经持久化的数据才允许进入 Channel。

### Deployment Boundary

Phase 1 不按逻辑模块拆分独立服务。只有出现以下证据时才考虑拆分：

- 某模块具有独立吞吐或扩缩容需求；
- 故障隔离收益明确；
- 独立发布频率产生实际价值；
- 团队所有权需要独立部署；
- 单体资源或部署窗口已经成为瓶颈。

## Consequences

优点：

- 降低分布式事务和运维复杂度；
- 保留清晰领域边界；
- 易于完成事务性持久化和本地回放；
- 未来可以按模块边界拆分。

代价：

- Worker 内部需要严格依赖规则；
- 单进程故障会影响多个模块；
- 需要通过租约和幂等支持多实例运行。

## Guardrails

- Domain 不依赖 Infrastructure；
- 模块不得直接修改其他模块拥有的表；
- 跨模块写入通过应用服务或持久化事件完成；
- 禁止把 Redis 或 Channel 当作唯一数据来源；
- 禁止在 Phase 1 引入真实交易或签名依赖。

## Review Trigger

当吞吐、部署、故障隔离或团队协作出现可量化瓶颈时重新评审。