# ADR-0004: Phase 1 Solana Adapter Scope

- Status: Accepted
- Date: 2026-07-28
- Scope: Phase 1 ingestion adapters

## Context

“监听 Solana 新币”范围过大。不同 Program、Launchpad 和 Pool 的事件格式不同，如果 Phase 1 同时支持大量来源，Parser、测试和运维范围会失控。

系统需要先完成一个从 Mint 到 Pool、Swap、Feature、Decision 和 Paper Fill 的完整闭环，同时保持后续新增 Adapter 的能力。

## Decision

### Supported Capability Set

Phase 1 V1 只保证以下能力：

1. Token Mint Discovery；
2. 一个 Launch Source Adapter；
3. 一个 Liquidity Pool / AMM Adapter；
4. Pool Created；
5. Swap Observed；
6. Liquidity Added / Removed；
7. 支撑报价、滑点和价格冲击的 Pool State；
8. 创建者和早期持有人所需的 Transfer/Balance 投影。

### Program Configuration

ProgramId 不硬编码到 Domain。每个部署环境通过配置声明：

```text
AdapterName
Network
ProgramIds
EnabledEventTypes
ParserVersion
Commitment
BackfillMode
StartSlot
Enabled
```

正式实施前由一次短期 Spike 选择 Launch Source 和 AMM Adapter，并把具体 ProgramId、版本和已知限制写入运行配置及 Adapter README。

选择标准：

- 能够稳定获取历史交易详情；
- 事件结构可测试；
- 可以识别 Pool、Swap 和 Liquidity；
- 能计算保守的成交价格影响；
- 数据源允许补采和对账；
- 维护成本可控。

### Adapter Contract

每个 Adapter 必须实现：

```text
CanHandle(RawBlockchainEvent)
Parse(RawBlockchainEvent, ParserVersion)
GetDeterministicDomainEvents()
GetSupportedProgramIds()
GetSupportedEventTypes()
GetKnownLimitations()
```

输出统一的 Normalized Domain Events：

```text
MintCreated
PoolCreated
SwapObserved
LiquidityChanged
TokenTransferred
```

### Discovery and Backfill

- WebSocket 用于发现可能相关的 Signature；
- RPC 用于获取完整交易详情；
- Adapter 不直接把 WebSocket Payload 当作最终事实；
- 每个 Adapter 都必须声明历史补采方法；
- 不支持补采的 Adapter 不允许成为正式 StrategyRun 的唯一来源。

### Explicitly Out of Scope

Phase 1 V1 不承诺：

- 覆盖全部 Solana DEX 或 Launchpad；
- 自动发现未知 Program；
- 解析任意自定义 Token 程序；
- 跨 DEX 聚合最优价格；
- MEV 或超低延迟执行；
- 多链统一 Parser。

## Consequences

- Phase 1 数据覆盖面有限，但能够形成可信闭环；
- 新增来源通过 Adapter 扩展，不修改 Domain 主链路；
- Dashboard 必须显示数据覆盖范围，不能把“已支持来源”描述为“全市场”。

## Adapter Acceptance

每个 Adapter 上线前必须通过：

- 固定交易样本解析测试；
- 同交易多事件测试；
- Parser 重放一致性测试；
- 历史补采测试；
- 未知版本或格式失败测试；
- Pool reserve 与 Swap 计算校验；
- 已知限制文档审核。