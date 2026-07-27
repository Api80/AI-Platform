# Crypto Intelligence System Phase 1 Data Model Design

## Purpose

定义 Phase 1 的核心数据模型，为 Solana 新币雷达、风险分析、纸面交易和未来 AI 分析提供统一数据基础。

核心原则：

> 不只保存结果，要保存从链上事件到决策结果的完整链路。

```text
Raw Events
    ↓
Features
    ↓
Risk Score
    ↓
Signal
    ↓
Decision
    ↓
Trade Result
```

## Core Entities

## BlockchainEvent

保存链上原始事件。

字段：

- EventId
- Chain
- Slot
- TransactionSignature
- Program
- EventType
- RawData
- CreatedAt

事件类型：

- MintCreated
- PoolCreated
- Swap
- LiquidityChange
- Transfer

---

## Token

代表链上代币。

字段：

- MintAddress
- Name
- Symbol
- Supply
- Decimals
- CreatorAddress
- CreatedAt
- Status

Token 状态：

- Created
- PoolReady
- Trading
- Suspicious
- Dead

---

## LiquidityPool

保存交易池信息。

字段：

- PoolAddress
- Dex
- TokenAddress
- QuoteToken
- InitialLiquidity
- CurrentLiquidity

---

## Wallet

保存钱包行为信息。

字段：

- Address
- FirstSeen
- Balance
- BehaviorScore

用于未来：

- 聪明钱包分析
- 创建者画像
- 关联地址分析

---

## TokenHolderSnapshot

保存持仓变化。

字段：

- Token
- HolderCount
- Top10Percentage
- SnapshotTime

---

## MarketSnapshot

保存市场状态。

字段：

- Price
- Volume
- BuyCount
- SellCount
- Liquidity
- Timestamp

---

## Feature

保存计算后的特征。

例如：

- Momentum
- LiquidityScore
- HolderConcentration
- WalletScore

---

## RiskAssessment

保存风险结果。

字段：

- Score
- Level
- Reasons
- CreatedAt

---

## PaperTrade

保存模拟交易。

字段：

- Token
- EntryPrice
- ExitPrice
- Amount
- Fee
- Slippage
- Profit
- ExitReason

---

## Future Extension

后续支持：

- AI Feature Store
- 多链数据
- 模型训练数据
- 自动策略优化
