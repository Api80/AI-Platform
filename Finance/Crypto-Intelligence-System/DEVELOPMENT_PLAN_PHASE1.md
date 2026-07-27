# Crypto Intelligence System Phase 1 Development Plan

## Goal

完成 Solana 新币雷达 + Paper Trading 基础系统。

不进行真实交易，不保存私钥，不执行链上签名。

## Milestone 1: Foundation

目标：建立工程基础。

任务：

- 创建 .NET 8 Solution
- 创建项目结构
- 建立 Domain Models
- 配置数据库
- 建立基础测试

## Milestone 2: Blockchain Data

目标：获取链上数据。

任务：

- Solana RPC 接入
- WebSocket Listener
- BlockchainEvent 保存
- Transaction Parser

## Milestone 3: Analysis

目标：生成分析结果。

任务：

- Token Analyzer
- Pool Analyzer
- Wallet Analyzer
- Feature Engine
- Risk Engine

## Milestone 4: Strategy

目标：验证交易逻辑。

任务：

- Signal Engine
- Decision Engine
- 规则策略
- 参数配置

## Milestone 5: Paper Trading

目标：模拟交易。

任务：

- 虚拟账户
- 模拟成交
- 手续费
- 滑点
- 盈亏统计

## Milestone 6: Analytics

目标：评估策略。

输出：

- 胜率
- 平均收益
- 最大回撤
- 失败原因

## Development Principle

先建立数据资产，再优化策略。

不要直接追求自动盈利。