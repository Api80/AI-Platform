# Crypto Intelligence System

## 项目定位

Crypto Intelligence System 是 AI Platform 在金融领域的一个链上智能分析方向。

目标不是简单开发交易机器人，而是建立一个基于区块链公开数据、风险分析、AI 分析和策略验证的长期系统。

## 当前阶段

Phase 1: Solana New Token Radar & Paper Trading

第一阶段重点：

- 监听 Solana 新创建和新开放交易代币；
- 分析代币风险；
- 建立模拟交易系统；
- 验证短线策略是否具有统计优势。

## 当前设计基线

以下文档共同构成 Phase 1 设计基线，阅读顺序如下：

1. [DESIGN_PROPOSAL_V2.md](./DESIGN_PROPOSAL_V2.md)  
   总体架构、核心原则，以及 Phase 2/3 扩展方向。

2. [DOMAIN_MODEL.md](./DOMAIN_MODEL.md)  
   领域概念、边界、不变量，以及 Risk、Signal、Decision、Execution 的职责划分。

3. [DATA_MODEL_DESIGN.md](./DATA_MODEL_DESIGN.md)  
   Phase 1 逻辑数据模型、关系、唯一约束、版本化、幂等和 Paper Trading 模型。

4. [DEVELOPMENT_PLAN_PHASE1.md](./DEVELOPMENT_PLAN_PHASE1.md)  
   开发顺序、测试要求、里程碑交付物和验收门槛。

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

当前状态：总体架构和上述 ADR/专项规范已形成 Phase 1 基线；具体 Launch Source、AMM ProgramId 和部署凭据由 Adapter Spike 与环境配置确定。

## 历史设计文档

以下文档保留用于记录方案演进，但不应作为当前实现依据：

- [SYSTEM_DESIGN.md](./SYSTEM_DESIGN.md) — Historical / Superseded；
- [SERVICE_ARCHITECTURE.md](./SERVICE_ARCHITECTURE.md) — Historical / Superseded；
- [DATABASE_DESIGN.md](./DATABASE_DESIGN.md) — Historical / Superseded；
- [TECH_DESIGN_PHASE1.md](./TECH_DESIGN_PHASE1.md) — Historical / Superseded；
- [ARCHITECTURE_REVIEW.md](./ARCHITECTURE_REVIEW.md) — 历史评审记录；
- [ROADMAP.md](./ROADMAP.md) — 产品阶段路线，具体实施以当前设计基线为准。

如果历史文档与当前设计基线冲突，以当前设计基线为准。

## 长期路线

```text
Phase 1
新币雷达 + 纸面交易

Phase 2
钱包画像 + 聪明钱追踪

Phase 3
AI 风险分析与策略评分

Phase 4
多链支持 + 自动执行接口

Phase 5
完整 Crypto Intelligence Platform
```

## 设计原则

1. 数据优先：持续积累链上数据资产。
2. 风控优先：先判断风险，再考虑交易。
3. 模拟优先：验证策略后再考虑真实执行。
4. 模块化：数据、分析、策略、执行相互独立。
5. 可追溯：所有决策都能追溯到特征、规则和原始事件。
6. 可复现：相同数据和版本化配置产生相同结果。
7. 可扩展：Phase 1 数据底座能够支持 Wallet Intelligence 和 AI Intelligence。

