# Crypto Intelligence System Architecture

## 总体架构

```text
Solana Blockchain
        |
        v
Blockchain Listener
        |
        v
Token Discovery Service
        |
        v
Market Data Service
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

## Phase 1 模块

### Blockchain Listener

负责监听链上事件：

- 新 Mint
- 新交易池
- 首次交易
- 流动性变化

### Token Discovery Service

负责识别和保存新代币信息：

- Mint 地址
- 名称
- 创建时间
- 创建者
- 总供应量

### Market Data Service

保存实时市场信息：

- 价格
- 流动性
- 成交量
- 买卖次数
- 持有人数量

### Risk Engine

分析：

- 流动性风险
- 创建者风险
- 持仓集中风险
- 无法卖出风险
- 异常交易风险

### Strategy Engine

第一阶段采用规则策略，不直接依赖 AI。

负责产生：

- 买入信号
- 卖出信号
- 放弃信号

### Paper Trading Engine

模拟真实交易流程：

- 模拟资金
- 模拟成交
- 手续费
- 滑点
- 收益统计

## 后续扩展

- 钱包画像
- 聪明钱追踪
- AI 分析
- 多链支持
- 自动执行接口

