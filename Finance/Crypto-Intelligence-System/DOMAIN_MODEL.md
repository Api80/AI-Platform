# Crypto Intelligence System Domain Model

## Domain Overview

Phase 1 目标：建立 Solana 新币雷达和纸面交易系统。

系统围绕链上事件、市场状态、风险判断和交易决策构建。

## Domain Flow

```text
Blockchain Event
        ↓
Token / Pool / Wallet
        ↓
Feature Generation
        ↓
Risk Analysis
        ↓
Signal
        ↓
Decision
        ↓
Paper Order
        ↓
Trade Result
```

## Important Domain Concepts

### Token

链上资产实体。

负责：

- 身份
- 生命周期
- 创建者关系

---

### Blockchain Event

系统事实来源。

不直接解释，只记录链上发生了什么。

---

### Feature

对原始数据进行加工后的可分析指标。

例如：

- 交易速度
- 流动性变化
- 钱包质量
- 持仓集中度

---

### Signal

策略产生的观察结果。

例如：

- EarlyMomentum
- HighRisk
- Ignore

Signal 不直接交易。

---

### Decision

综合规则、风险和未来 AI 模型后的最终决策。

例如：

- EnterPaperTrade
- Reject
- Exit

---

### Execution

负责模拟成交。

未来可以扩展：

- Paper Executor
- Live Executor

---

## Design Principle

策略、风险、执行必须分离。

避免未来 AI 模型加入时重构整个系统。
