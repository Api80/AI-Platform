# Crypto Intelligence System Phase 1 Database Design

## 设计原则

数据库用于保存链上事实、分析结果和模拟交易记录。

区分：

- 原始数据；
- 分析数据；
- 策略结果；
- 交易结果。

---

## Token 表

保存代币基础信息。

字段：

- Id
- MintAddress
- Name
- Symbol
- CreatorAddress
- CreateTime
- Supply
- Decimals
- Status

---

## Pool 表

保存交易池信息。

字段：

- Id
- TokenId
- PoolAddress
- Dex
- InitialLiquidity
- CurrentLiquidity
- CreateTime

---

## MarketSnapshot 表

保存价格变化。

字段：

- TokenId
- Price
- Volume
- BuyCount
- SellCount
- HolderCount
- Liquidity
- Timestamp

---

## RiskAssessment 表

保存风险分析结果。

字段：

- TokenId
- Score
- Level
- Reasons
- CreatedTime

---

## PaperTrade 表

保存模拟交易。

字段：

- Id
- TokenId
- EntryPrice
- ExitPrice
- Amount
- Profit
- ExitReason
- OpenTime
- CloseTime

---

后续 Phase 2 增加 Wallet Intelligence 相关表。
