# Crypto Intelligence System Phase 1 Service Architecture

## 服务划分

```text
Crypto Intelligence System

├── Blockchain.Listener
│      链上事件监听
│
├── Token.Discovery
│      新币发现和解析
│
├── Market.Data
│      行情和流动性采集
│
├── Risk.Engine
│      风险评分
│
├── Strategy.Engine
│      策略决策
│
├── Paper.Trading
│      模拟交易执行
│
├── Performance.Analysis
│      收益统计
│
└── Dashboard
       数据展示
```

---

## 开发顺序

1. 数据采集基础
2. Token 数据模型
3. 链监听服务
4. 风险分析
5. 模拟交易
6. Dashboard

---

## 设计原则

- 模块独立；
- 数据驱动；
- 策略与执行分离；
- 真实交易接口默认关闭；
- 为后续 AI 分析预留数据。
