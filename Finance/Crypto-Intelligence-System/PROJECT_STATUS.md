# Crypto Intelligence System Project Status

> 更新时间：2026-07-28  
> 状态：M0/M1 已完成，M2 Reliable Ingestion 开发中
> 代码实施进度：M2A/M2B/M2C 已合并；M2D 代码已完成本地验证，运行验收待执行

## 1. Project Positioning

Crypto Intelligence System 的长期目标是形成一套面向加密资产的智能分析系统，核心能力包括：

```text
新币雷达
→ 风险预警
→ 钱包分析
→ AI Intelligence
→ 策略验证
→ 可控执行接口（后期独立评审）
```

当前客户最直接的需求不是完整平台，而是：

> 发现刚开始交易的新币，观察最初几分钟的价格、流动性和交易热度，在规则允许的时间和涨幅范围内进行模拟买入，并通过止盈、止损、最大持有时间、流动性下降和动量衰减规则模拟卖出，验证扣除费用、滑点和失败交易后是否存在统计优势。

该需求被定义为“新币早期动量策略验证”，不承诺稳定套利或保证在流动性消失前成功卖出。

## 2. Current Decision

长期架构保留，但 Phase 1 实际交付范围缩小为一个纵向 MVP：

```text
单一 Launch Source
+ 单一 AMM/Pool Adapter
+ 新币雷达
+ 基础风险过滤
+ 一套早期动量策略
+ Paper Trading
+ Historical Replay
+ Forward Paper
+ 最小 Dashboard
```

Phase 1 不实现完整 Wallet Intelligence、AI 交易模型、多 DEX、多链或真实自动交易。

## 3. Completed Work

### Product and Architecture

- 完成项目长期定位；
- 完成 Phase 1、Phase 2、Phase 3 演进方向；
- 确认采用事件驱动、可回放、可追溯的数据链路；
- 确认采用模块化单体，暂不拆分微服务；
- 确认 PostgreSQL 为唯一事实来源；
- 确认 Redis 只用于可重建缓存；
- 确认真实交易不属于 Phase 1。

### Domain and Data Design

- 完成 Raw Event → Feature → Risk → Signal → Decision → Execution 分层；
- 完成 Token、Pool、Market、Wallet 最小领域边界；
- 完成 EventId、EventOrdinal、DomainEventIndex 等幂等标识；
- 完成 Observed、Persisted、Processed、Finalized、Reconciled 水位设计；
- 完成 Account、Order、Fill、Position、Equity Paper Trading 模型；
- 完成 Feature、Risk、Strategy 和 Execution Model 版本化设计。

### Reliability Decisions

- 确认 At-least-once + Idempotent Consumer；
- 完成 Worker Lease、Retry 和 Dead Letter 设计；
- 完成 Solana commitment、canonical 状态、回退和 RPC 对账设计；
- 完成数据分区、保留、备份和恢复原则；
- 完成 Phase 1 可观测性和 SLO 基线。

### Research Design

- 完成 Paper Execution Model V1；
- 完成策略时间序列切分和样本外验证协议；
- 完成费用、滑点、价格冲击、无法成交和无法退出建模要求；
- 完成 Walk-Forward、参数敏感性和压力测试门槛。

## 4. Existing Design Baseline

当前长期设计基线：

- [DESIGN_PROPOSAL_V2.md](./DESIGN_PROPOSAL_V2.md)
- [DOMAIN_MODEL.md](./DOMAIN_MODEL.md)
- [DATA_MODEL_DESIGN.md](./DATA_MODEL_DESIGN.md)
- [DEVELOPMENT_PLAN_PHASE1.md](./DEVELOPMENT_PLAN_PHASE1.md)

关键决策：

- [ADR-0001 Runtime and Persistence](./adr/ADR-0001-phase1-runtime-and-persistence.md)
- [ADR-0002 Event Delivery and Idempotency](./adr/ADR-0002-event-delivery-idempotency-and-checkpoints.md)
- [ADR-0003 Solana Finality and Reconciliation](./adr/ADR-0003-solana-finality-and-reconciliation.md)
- [ADR-0004 Adapter Scope](./adr/ADR-0004-phase1-solana-adapter-scope.md)
- [ADR-0005 Data Retention and Backup](./adr/ADR-0005-data-partition-retention-and-backup.md)

专项规范：

- [PAPER_EXECUTION_MODEL_V1.md](./PAPER_EXECUTION_MODEL_V1.md)
- [STRATEGY_VALIDATION_PROTOCOL_V1.md](./STRATEGY_VALIDATION_PROTOCOL_V1.md)
- [OBSERVABILITY_SLO_PHASE1.md](./OBSERVABILITY_SLO_PHASE1.md)

### Phase 1 MVP Implementation Baseline

- [PHASE1_MVP_DESIGN.md](./PHASE1_MVP_DESIGN.md)
- [PHASE1_MVP_CONFIGURATION.md](./PHASE1_MVP_CONFIGURATION.md)
- [PHASE1_MVP_IMPLEMENTATION_PLAN.md](./PHASE1_MVP_IMPLEMENTATION_PLAN.md)

Phase 1 实际开发以 MVP Implementation Baseline 为准；长期设计用于约束稳定边界和未来扩展。

## 5. Implementation Status

| Area | Status | Notes |
|---|---|---|
| Customer requirement | Completed | 已收敛为新币早期动量 MVP |
| Long-term architecture | Completed | 作为未来扩展蓝图 |
| Domain model | Completed | 已明确领域边界和不变量 |
| Logical data model | Completed | Configuration 与可靠采集物理 Schema 已开始实现 |
| Reliability ADRs | In progress | 补采、Finality、连续水位和 Gap 已转化为代码；等待真实连续运行验收 |
| Paper execution specification | Completed | 尚未实现 Adapter Quote |
| Strategy validation protocol | Completed | 尚无真实样本 |
| Phase 1 MVP detailed design | Completed | MVP Design、Configuration 和 Implementation Plan 已完成 |
| Adapter selection spike | Completed | Raydium LaunchLab + CPMM，10 个离线 Fixtures 和实时发现/补采验证通过 |
| Solution/code | In progress | M2D Backfill、Finality Refresh、Checkpoint 联动和运维查询已完成本地验证 |
| Database migrations | In progress | Migration 001-004 已生成并验证；Migration 004 增加 Radar 读模型 |
| Historical replay | In progress | 确定性 Replay 基础已完成；正式数据集依赖 M2 运行验收 |
| Forward paper | Not started | 依赖前序 Milestone |
| Dashboard | Not started | 只计划最小查询和监控能力 |

## 6. Phase 1 MVP Scope

### Included

- 一个 Launch Source Adapter；
- 一个 AMM/Pool Adapter；
- 新 Token、Pool、Swap、Liquidity 事件；
- 人工维护的热点关键词；
- 新币候选状态机；
- 最小 Risk Hard Reject；
- 一套版本化早期动量策略；
- 一套版本化退出策略；
- Paper Account、Order、Fill、Position；
- Historical Replay；
- Forward Paper；
- 最小 API 和 Dashboard；
- 运行监控和数据缺口告警。

### Deferred

- 完整钱包画像；
- Smart Money 排名；
- 钱包关系图谱；
- 社交媒体全量采集；
- AI 直接买卖；
- 多 Launchpad、多 DEX；
- 多链；
- 真实交易和私钥；
- 微服务拆分；
- 复杂运营后台。

## 7. AI Usage in Phase 1

AI 可以用于：

- Token Name/Symbol 热点主题分类；
- 风险原因自然语言解释；
- 策略报告摘要；
- 失败案例分析；
- Parser 和测试开发辅助。

AI 不直接绕过规则生成交易，不绕过 Hard Reject，不直接修改 Order 或 Position。

Phase 1 的买卖判断以可复现、可回放的规则策略为基线。

## 8. Known Risks

### Strategy Risk

新币最初几分钟上涨只是待验证假设，不是已证明规律。

### Exit Risk

流动性可以快速消失，系统无法保证成功退出。必须保留 ExitUnavailable 和保守估值。

### Data Coverage Risk

单一 Adapter 只能代表其支持来源，不能宣称覆盖整个 Solana 新币市场。

### Execution Bias

如果忽略延迟、费用、优先费、滑点和价格冲击，Paper Trading 会高估收益。

### Scope Risk

过早增加 Wallet、AI、多 DEX 或真实交易会拖慢 MVP 验证。

## 9. Open Implementation Decisions

以下决策需要 Adapter Spike 或真实数据后确定：

1. 正式主 RPC Source 和独立备用 Source；
2. Historical Run 起始 Slot 和数据范围；
3. 新币观察窗口；
4. 动量和风险参数初始范围；
5. Network/Priority Fee 假设；
6. 采集 7 天后的容量和分区参数。

这些参数必须配置化、版本化并保存到 StrategyRun。

## 10. Next Actions

1. M2A Raw Event、Checkpoint、Gap、Lease 与 Dead Letter 基础已完成；
2. WebSocket 自动重连、RPC 详情和 Slot/Signature 补采已完成；
3. Raydium 正式 Adapter 已完成；
4. Token、Pool、Swap、Liquidity 和 Candidate 投影已完成；
5. 滚动 FeatureSnapshot 与确定性 Replay 已完成；
6. 下一步是在真实环境稳定采集 7 天，完成容量、分区、备份恢复和 SLO 复审。

## 11. Current Assessment

项目长期方向有意义，但价值取决于能否通过真实数据证明：

- 新币能够被及时发现；
- 基础风险可以提前过滤一部分高风险 Token；
- 规则策略扣除全部执行成本后仍有优势；
- 系统能够诚实记录无法成交和无法退出。

当前长期架构、Phase 1 MVP 设计、M0 Adapter Selection Spike 和 M1 Foundation 均已完成。M2 代码能力已进入出口验收阶段；通过真实连续采集、断线补采、缺口和容量复审后再进入 M3。
