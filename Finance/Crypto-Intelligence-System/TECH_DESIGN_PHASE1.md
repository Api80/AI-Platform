# Crypto Intelligence System Phase 1 Technical Design

## 目标

Phase 1 实现 Solana 新代币雷达 + 纸面交易系统。

核心问题：

> 发现早期新币机会，并通过数据和模拟交易验证策略是否具有长期价值。

第一阶段不进行真实交易，不保存私钥，不执行链上签名。

---

## 系统流程

```text
Solana Blockchain
        |
        v
Blockchain Listener
        |
        v
Token Discovery
        |
        v
Market Data Collection
        |
        v
Risk Engine
        |
        v
Strategy Engine
        |
        v
Paper Trading Engine
        |
        v
Performance Analysis
```

---

## 核心模块

### 1. Blockchain Listener

职责：

- 监听 Solana 链上事件；
- 捕获新 Mint、新交易池、首次交易；
- 保存原始链上事件。

输入：

- Solana RPC
- WebSocket

输出：

- Token 创建事件；
- Pool 创建事件；
- Transaction 信息。

---

### 2. Token Discovery

负责识别新代币。

记录：

- Mint 地址；
- 名称；
- Symbol；
- 创建时间；
- Creator；
- Supply；
- Decimals。

---

### 3. Market Data Service

采集：

- 价格；
- 流动性；
- 成交量；
- 买卖次数；
- Holder 数量；
- 价格变化。

---

### 4. Risk Engine

第一阶段重点模块。

风险因素：

- 是否可以正常卖出；
- 流动性是否足够；
- 创建者历史；
- 持仓集中度；
- 权限风险；
- 价格冲击。

输出：

```json
{
 "score": 80,
 "level": "HIGH",
 "reasons": []
}
```

---

### 5. Strategy Engine

第一阶段采用规则策略。

示例：

进入条件：

- 创建时间小于指定时间；
- 风险评分低于阈值；
- 流动性达到要求；
- 买入增长明显。

退出条件：

- 达到止盈；
- 达到止损；
- 超过最大持有时间；
- 流动性异常。

---

### 6. Paper Trading Engine

模拟真实交易流程。

记录：

- 模拟账户；
- 买入价格；
- 卖出价格；
- 手续费；
- 滑点；
- 盈亏；
- 退出原因。

---

## 技术栈

后端：

- .NET 8
- Worker Service
- ASP.NET Core
- EF Core

数据：

- PostgreSQL
- Redis（实时数据）

前端：

- React
- Dashboard

---

## 第一阶段验收

- 连续运行并采集新币数据；
- 建立新币数据库；
- 完成风险评分；
- 完成模拟交易；
- 输出策略统计。

真实交易接口暂不启用。
