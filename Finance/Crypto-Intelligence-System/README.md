# Crypto Intelligence System

## 项目定位

Crypto Intelligence System 是 AI Platform 在金融领域的一个链上智能分析方向。

长期目标不是简单开发交易机器人，而是建立一个基于区块链公开数据、风险分析、钱包分析、AI Intelligence 和策略验证的系统。

```text
新币雷达
→ 风险预警
→ 钱包分析
→ AI Intelligence
→ 策略验证
```

## 当前进度

当前状态：`Phase 1 M0/M1 已完成，M2 Reliable Ingestion 开发中`。

- [PROJECT_STATUS.md](./PROJECT_STATUS.md)  
  当前项目情况、已完成设计、实施状态、风险和下一步。

- [ADAPTER_SPIKE_REPORT.md](./ADAPTER_SPIKE_REPORT.md)  
  首个 Launch/Pool Adapter 候选矩阵、主网证据、ProgramId、版本固定、Quote 验证、限制和 M0 Exit Gate。

已完成 Raydium LaunchLab + CPMM 选型、10 个主网离线 Fixtures、IDL 解析、整数 Quote，以及正式 .NET Solution、配置、数据库、可观测性和 CI 骨架。当前开始实现 M2 可靠采集。

- [M1_FOUNDATION.md](./M1_FOUNDATION.md)
  正式工程结构、配置、数据库迁移、健康检查、测试、CI 和本地运行说明。

- [M2_RELIABLE_INGESTION.md](./M2_RELIABLE_INGESTION.md)
  M2 增量范围、原始事件身份、水位、Gap、Worker Lease、重试和 Dead Letter 说明。

## Phase 1 MVP 实施基线

Phase 1 实际交付范围已经从长期平台方案收敛为：

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

实施文档：

1. [PHASE1_MVP_DESIGN.md](./PHASE1_MVP_DESIGN.md)  
   MVP 产品目标、技术范围、模块、状态机、API、Dashboard、验收和扩展路径。

2. [PHASE1_MVP_CONFIGURATION.md](./PHASE1_MVP_CONFIGURATION.md)  
   数据源、雷达、主题、风险、进入、退出、账户、执行、验证和 AI 配置规范。

3. [PHASE1_MVP_IMPLEMENTATION_PLAN.md](./PHASE1_MVP_IMPLEMENTATION_PLAN.md)  
   从 Adapter Spike 到 Forward Paper 的详细任务、依赖、测试、交付物和退出条件。

Phase 1 不包含完整 Wallet Intelligence、AI 直接买卖、多 DEX、多链、私钥或真实交易。

## 长期设计基线

以下文档作为系统长期扩展架构：

1. [DESIGN_PROPOSAL_V2.md](./DESIGN_PROPOSAL_V2.md)  
   总体架构、核心原则，以及 Phase 2/3 扩展方向。

2. [DOMAIN_MODEL.md](./DOMAIN_MODEL.md)  
   领域概念、边界、不变量，以及 Risk、Signal、Decision、Execution 的职责划分。

3. [DATA_MODEL_DESIGN.md](./DATA_MODEL_DESIGN.md)  
   逻辑数据模型、关系、唯一约束、版本化、幂等和 Paper Trading 模型。

4. [DEVELOPMENT_PLAN_PHASE1.md](./DEVELOPMENT_PLAN_PHASE1.md)  
   长期设计视角下的开发顺序和验收要求；实际 Phase 1 实施以 MVP Implementation Plan 为准。

## 已接受架构决策

- [ADR-0001：Phase 1 Runtime and Persistence](./adr/ADR-0001-phase1-runtime-and-persistence.md)
- [ADR-0002：Event Delivery, Idempotency and Checkpoints](./adr/ADR-0002-event-delivery-idempotency-and-checkpoints.md)
- [ADR-0003：Solana Finality and Reconciliation](./adr/ADR-0003-solana-finality-and-reconciliation.md)
- [ADR-0004：Phase 1 Solana Adapter Scope](./adr/ADR-0004-phase1-solana-adapter-scope.md)
- [ADR-0005：Data Partition, Retention and Backup](./adr/ADR-0005-data-partition-retention-and-backup.md)

## Phase 1 专项规范

- [Paper Execution Model V1](./PAPER_EXECUTION_MODEL_V1.md)  
  定义执行延迟、有效市场状态、Pool Quote、费用、滑点、价格冲击、失败条件、保守估值和压力场景。

- [Strategy Validation Protocol V1](./STRATEGY_VALIDATION_PROTOCOL_V1.md)  
  定义时间切分、最低样本、OOS 门槛、Walk-Forward、敏感性、偏差控制和研究结论状态。

- [Phase 1 Observability and SLO](./OBSERVABILITY_SLO_PHASE1.md)  
  定义数据延迟、积压、缺口、Dead Letter、备份恢复、告警和运行手册要求。

## 历史设计文档

以下文档保留用于记录方案演进，但不应作为当前实现依据：

- [SYSTEM_DESIGN.md](./SYSTEM_DESIGN.md) — Historical / Superseded；
- [SERVICE_ARCHITECTURE.md](./SERVICE_ARCHITECTURE.md) — Historical / Superseded；
- [DATABASE_DESIGN.md](./DATABASE_DESIGN.md) — Historical / Superseded；
- [TECH_DESIGN_PHASE1.md](./TECH_DESIGN_PHASE1.md) — Historical / Superseded；
- [ARCHITECTURE_REVIEW.md](./ARCHITECTURE_REVIEW.md) — 历史评审记录；
- [ROADMAP.md](./ROADMAP.md) — 产品阶段路线，具体实施以当前 MVP 和长期设计基线为准。

如果历史文档与 MVP 实施基线冲突，Phase 1 以 MVP 实施基线为准。

## 长期路线

```text
Phase 1
单来源新币雷达 + 风险过滤 + Paper Trading

Phase 2
钱包画像 + Smart Money + 钱包关系

Phase 3
热点识别 + AI 风险分析 + 策略评分

Phase 4
多来源、多链 + 可控执行接口

Phase 5
完整 Crypto Intelligence Platform
```

## 设计原则

1. 范围要小：Phase 1 只验证一个闭环。
2. 数据要真：保留原始事件、成本、失败和无法退出。
3. 风控优先：Hard Reject 优先于策略信号。
4. 模拟优先：历史回放和 Forward Paper 通过后才讨论真实执行。
5. 可追溯：所有决策能追溯到特征、规则和原始事件。
6. 可复现：相同数据和配置产生相同结果。
7. 可扩展：MVP 模块对应长期 Domain 边界，后续可以增加 Wallet 和 AI。
