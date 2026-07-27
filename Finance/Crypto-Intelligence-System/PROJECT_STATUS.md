# Crypto Intelligence System Project Status

> 更新时间：2026-07-28  
> 状态：Phase 1 MVP 设计与实施准备  
> 代码实施进度：尚未开始

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

## 5. Implementation Status

| Area | Status | Notes |
|---|---|---|
| Customer requirement | Completed | 已收敛为新币早期动量 MVP |
| Long-term architecture | Completed | 作为未来扩展蓝图 |
| Domain model | Completed | 已明确领域边界和不变量 |
| Logical data model | Completed | 物理 Schema 尚未实现 |
| Reliability ADRs | Completed | 尚未转化为代码和测试 |
| Paper execution specification | Completed | 尚未实现 Adapter Quote |
| Strategy validation protocol | Completed | 尚无真实样本 |
| Phase 1 MVP detailed design | In progress | 需要从长期方案中裁剪实施范围 |
| Adapter selection spike | Not started | Launch Source、AMM、ProgramId 尚未选择 |
| Solution/code | Not started | 当前仓库只有设计文档 |
| Database migrations | Not started | 等待 MVP 物理模型 |
| Historical replay | Not started | 依赖 Adapter 和 Raw Event Store |
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

1. 首个 Launch Source；
2. 首个 AMM/Pool；
3. ProgramId 和 Parser 样本；
4. RPC Source 和故障切换配置；
5. 新币观察窗口；
6. 动量和风险参数初始范围；
7. Network/Priority Fee 假设；
8. 采集 7 天后的容量和分区参数。

这些参数必须配置化、版本化并保存到 StrategyRun。

## 10. Next Actions

1. 完成 `PHASE1_MVP_DESIGN.md`；
2. 完成 `PHASE1_MVP_IMPLEMENTATION_PLAN.md`；
3. 完成 `PHASE1_MVP_CONFIGURATION.md`；
4. 执行 Adapter Selection Spike；
5. 固化第一批 ProgramId 和交易样本；
6. 创建 .NET Solution 和数据库迁移；
7. 按 MVP Milestone 开始开发。

## 11. Current Assessment

项目长期方向有意义，但价值取决于能否通过真实数据证明：

- 新币能够被及时发现；
- 基础风险可以提前过滤一部分高风险 Token；
- 规则策略扣除全部执行成本后仍有优势；
- 系统能够诚实记录无法成交和无法退出。

当前设计基础已完成，下一阶段重点不是继续扩大架构，而是完成 Phase 1 MVP 详细方案并开始单来源闭环实施。