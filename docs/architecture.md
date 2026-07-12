# AI Platform Architecture v0.1

## 1. 架构目标

AI Platform 采用分层和可插拔架构，避免把新闻、量化、模型、Agent 等能力堆成一个难以维护的大系统。

架构目标：

- 通用能力与行业能力分离
- 系统之间通过标准接口协作
- 数据和因子可以跨系统复用
- 模型可以替换
- 每个决策可以追溯和解释
- 先支持 TruePulse，后续能够扩展到其他领域

## 2. 四层结构

```text
Platform
  -> Domain
    -> System
      -> Module
```

### 2.1 Platform Layer

提供跨领域通用基础能力：

- Data Pipeline
- Unified Storage
- Metadata and Lineage
- Knowledge Graph Engine
- Feature Engine / Feature Store
- Model Training and Inference
- Agent Orchestration
- Decision Engine
- Explainability Engine
- Backtest and Evaluation
- Monitoring and Feedback

### 2.2 Domain Layer

封装某个领域的业务语义、规则、知识和数据模型。

首个领域：

- TruePulse / Finance

未来可能扩展：

- Healthcare
- Legal
- Research
- Business Intelligence

### 2.3 System Layer

领域内部由多个相对独立的系统协作。

TruePulse 第一阶段包括：

- News System
- Market Data System
- Event System
- Knowledge Graph System
- Factor System
- Quant System
- Backtest System
- Risk System
- Decision System
- Explainability System

### 2.4 Module Layer

每个系统内部继续拆分为可替换的模块。

例如 News System：

- Source Connector
- Crawler / API Client
- Deduplication
- Normalization
- Entity Extraction
- Event Classification
- Sentiment Analysis
- Credibility Scoring
- Time-decay Calculation

## 3. TruePulse 参考数据流

```text
News / Market / Capital Flow / Fundamentals
                  |
                  v
          Data Ingestion Layer
                  |
                  v
       Normalize + Validate + Lineage
                  |
                  v
       Event Extraction / Knowledge Graph
                  |
                  v
        Feature Engine / Feature Store
                  |
                  v
        Multiple Prediction Models
                  |
                  v
             Decision Engine
                  |
                  v
        Explainability + Risk Control
                  |
                  v
       Backtest / Paper Trade / Execution
                  |
                  v
            Feedback and Evolution
```

## 4. 微服务边界原则

不是所有模块都必须一开始拆成独立微服务。

只有满足以下条件时才适合独立成服务：

- 生命周期相对独立
- 数据职责清晰
- 有独立扩缩容需求
- 需要被多个系统复用
- 技术栈或部署方式明显不同

第一阶段可以采用模块化单体或少量服务，先验证数据闭环和业务价值，再逐步拆分。

## 5. 数据共享原则

系统之间不直接依赖对方的内部数据库结构。

推荐使用：

- 统一事件模型
- 统一实体标识
- 统一 Feature Schema
- API 或消息事件
- 数据血缘与版本信息

核心共享对象：

- Raw Data
- Normalized Event
- Entity and Relationship
- Feature Vector
- Label
- Prediction
- Decision
- Explanation
- Feedback

## 6. 模型架构原则

平台不绑定某一种模型。

第一阶段优先使用适合表格型因子的模型：

- Linear / Logistic Regression
- Random Forest
- XGBoost
- LightGBM

后续根据数据和任务再引入：

- Time-series models
- Transformer
- Graph Neural Network
- Multimodal models
- Ensemble and Agent-based decision systems

模型必须通过统一接口读取 Feature，并输出标准化预测结果。

## 7. 可解释性与数据血缘

每一个 Feature、Prediction 和 Decision 都应保存：

- 数据来源
- 生成时间
- 处理版本
- 模型版本
- 贡献因子
- 置信度
- 已知风险

可解释性不是展示层功能，而是底层数据模型的一部分。

## 8. 当前阶段边界

当前阶段重点不是建设一个完整通用平台，而是：

1. 通过 TruePulse 验证方法论
2. 跑通新闻和行情到 Feature 的流程
3. 建立最小可用回测闭环
4. 使用中小型机器学习模型验证 Feature 有效性
5. 保存完整的数据血缘与解释信息

平台化是长期方向，不应成为前期过度设计的理由。