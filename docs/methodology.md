# AI Platform Methodology v0.1

## 1. 平台愿景

AI Platform 不是一个单独的金融系统，而是一个可复用的通用 AI 决策平台。

TruePulse 是该平台的第一个金融领域应用。未来平台可以扩展到医疗、法律、科研、商业分析等领域，而不需要推翻底层方法论和核心架构。

平台的目标不是简单地输出预测，而是把原始数据持续转化为可解释、可验证、可迭代的决策建议。

## 2. 核心方法论

整个平台遵循以下闭环：

```text
Data
  -> Information
  -> Knowledge Graph
  -> Feature Engineering
  -> AI Models
  -> Decision
  -> Explainability
  -> Backtest
  -> Feedback
  -> Self Evolution
```

## 3. 核心原则

### 3.1 Feature First

人负责定义和构造 Feature（因子），AI 负责学习 Feature 与未来结果之间的关系。

传统量化通常是：

```text
Human -> Strategy -> Program Execution
```

AI Quant 更接近：

```text
Human -> Feature Design -> AI Learning -> Strategy/Decision
```

这里不是完全取消人的作用，而是把人的主要职责从“手工组合策略”转移到：

- 定义问题
- 构建高质量因子
- 设计标签
- 约束风险
- 验证结果
- 解释模型

### 3.2 Predict More Than Direction

AI 不应该只预测“涨”或“跌”。

平台应支持多目标预测，例如：

- 未来 1 日上涨概率
- 未来 5 日上涨概率
- 未来 20 日上涨概率
- 未来收益率
- 未来波动率
- 最大回撤
- 风险等级
- 不同市场环境下的置信度

最终由 Decision Engine 或 Decision Agent 综合多个模型结果，而不是依赖单一二分类信号。

### 3.3 One Event -> Many Features

一条新闻不是一个因子，而是一组可以拆解、保存和组合的 Feature。

一条新闻至少可以拆分为：

- 情绪
- 重要性
- 可信度
- 事件类型
- 影响公司
- 影响行业
- 影响国家或地区
- 影响方向
- 影响持续时间
- 时间衰减
- 是否首次发生
- 是否为政策、财报、新品发布或风险事件
- 涉及实体及其关系

因此，新闻系统的核心价值不是“抓到新闻”，而是把新闻转化为结构化事件和可复用因子。

### 3.4 Data Is Asset

模型可以替换，数据资产不能轻易重建。

平台的长期核心资产包括：

- 原始新闻数据库
- 行情与资金数据库
- 事件数据库
- Feature Store
- Knowledge Graph
- 标签数据库
- 回测结果
- 模型预测历史
- 决策历史
- 解释记录

今天可以使用 LightGBM、XGBoost，未来可以替换为 Transformer、GNN、MoE 或其他模型，但高质量、长期积累的数据仍然可以继续使用。

### 3.5 Explainability by Design

可解释性不能在系统完成后再补，而必须从数据和因子产生阶段开始设计。

平台不仅保存因子值，还要保存因子的来源、生成过程和证据。

例如，系统给出“新闻影响分 92”，必须能够解释：

```text
新品发布影响       35%
行业影响           25%
市场情绪           18%
新闻可信度         12%
历史相似事件        2%
其他因素            8%
```

最终用户看到的不应该只是“买入”或“卖出”，而应该包括：

- 模型预测了什么
- 预测置信度是多少
- 哪些因子贡献最大
- 因子来自哪些数据
- 哪些风险可能导致预测失效

## 4. 平台分层

平台按以下层次组织：

```text
Platform
  -> Domain
    -> System
      -> Module
```

### Platform

提供跨领域可复用的通用能力，例如：

- 数据管道
- 存储
- 知识图谱引擎
- Feature Engine
- 模型训练与推理
- Agent 调度
- Decision Engine
- Explainability Engine
- Backtest Engine

### Domain

某个行业或业务领域，例如金融、医疗、法律。

### System

领域内相对独立的系统。例如在 TruePulse 中：

- 新闻系统
- 行情系统
- 因子系统
- 量化系统
- 回测系统
- 风险系统

### Module

系统内部具体可实现、可替换的功能模块。

## 5. TruePulse 的位置

TruePulse 是 AI Platform 上的第一个金融领域应用。

它不是整个平台本身，而是验证平台方法论和通用能力的第一个落地场景。

TruePulse 可以包含：

- News Service
- Market Data Service
- Event Extraction Service
- Knowledge Graph Service
- Feature Service
- Quant Model Service
- Backtest Service
- Decision Service
- Explainability Service

## 6. 长期目标

最终形成一个持续学习的决策闭环：

```text
Data
  -> Knowledge
  -> Features
  -> Models
  -> Decisions
  -> Explanations
  -> Backtests
  -> Feedback
  -> Model and Feature Evolution
```

平台真正的竞争力不是某一个模型，而是：

1. 高质量数据资产
2. 可复用的 Feature 体系
3. 结构化知识与关系
4. 可验证的决策流程
5. 可解释的输出
6. 持续反馈和自我演进能力

## 7. 文档维护原则

本仓库作为 AI Platform 的知识库和设计基线，优先记录：

- 为什么这样设计
- 系统如何分层
- 哪些原则已经形成共识
- 关键架构决策及其理由
- 仍未解决的问题

重要决策使用 ADR（Architecture Decision Record）单独记录，避免只保存结论而丢失决策背景。