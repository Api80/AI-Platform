# Crypto Intelligence System Architecture Review

## Review Purpose

本文件用于评审 Phase 1 技术方案，目标不是快速实现一个简单交易机器人，而是建立可持续扩展的链上智能分析系统基础。

## 当前方案评价

初始方案方向正确：

```text
Blockchain Listener
        ↓
Token Discovery
        ↓
Market Data
        ↓
Risk Engine
        ↓
Strategy Engine
        ↓
Paper Trading
```

但是如果目标是长期建设 Crypto Intelligence System，需要增加数据资产、事件驱动和决策分层设计。

## 主要架构调整

## 1. 增加 Event Pipeline

区块链不是传统行情系统，核心不是单纯价格，而是链上事件。

调整为：

```text
Blockchain Listener
        ↓
Event Pipeline
        ↓
----------------------
|        |           |
Token   Market    Wallet
Analyzer Analyzer Analyzer
```

第一阶段使用 .NET Channel 实现，未来可扩展消息队列。

## 2. 增加链上事件模型

新增核心数据：

- BlockchainEvent
- Raw Transaction
- Program Event
- Swap Event
- Liquidity Event

原因：

原始事件是未来 AI 分析和模型训练的数据资产。

## 3. 增加 Wallet 维度

新币分析不能只看 Token。

需要关注：

- 创建者钱包
- 早期买入钱包
- 持仓变化
- 钱包历史行为

新增：

- Wallet
- TokenHolderSnapshot

## 4. 策略与决策分离

调整：

```text
Feature Engine
        ↓
Signal Engine
        ↓
Decision Engine
        ↓
Execution Engine
```

原因：

未来规则策略、AI模型、钱包评分可以共同参与决策。

## 5. 数据设计原则

不要只保存结果。

应该保存完整链路：

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

## 推荐 Phase 1 架构

```text
                 Solana
                    |
                    ↓
          Blockchain Listener
                    |
                    ↓
              Event Pipeline
                    |
        ------------------------
        |          |           |
        ↓          ↓           ↓
     Token     Market       Wallet
   Analyzer   Analyzer    Analyzer

                    ↓

              Feature Engine
                    ↓
              Risk Engine
                    ↓
          Decision Engine
                    ↓
        Paper Trading Engine
                    ↓
       Performance Analytics
```

## Phase 1 范围控制

一期不做：

- 自动真实交易
- 多链支持
- 复杂 AI 模型训练
- 复杂 Dashboard

一期完成：

- Solana 数据监听
- 新 Token 发现
- Pool 发现
- 基础风险评分
- 规则策略
- Paper Trading
- 数据积累

## 评审结论

原方案：70/100

调整后方案：90/100

核心提升：

1. 从交易脚本升级为链上数据系统；
2. 为未来 AI 分析建立数据基础；
3. 支持钱包分析和多策略融合；
4. 避免后期重构。

下一阶段：

继续设计 Phase 1 的详细开发计划、项目结构和数据库模型。