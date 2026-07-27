# M3 Theme and Minimal Risk

> 状态：M3A Rule Engine complete；M3B Evidence Components complete（离线验证）；持久化和真实环境验收待完成

## 目标

使用版本化、可解释、可回放的规则，对新币候选生成 Theme、Risk 和 Candidate Eligibility 结果。M3 不生成交易信号，不创建订单。

本阶段核心规则不依赖 PostgreSQL，可以通过纯内存输入和离线测试验证。

## M3A 已完成

### Theme Rules

- Token Name/Symbol Unicode 兼容规范化；
- 大小写、标点和空白规范化；
- Hot Keywords；
- Blocked Keywords 优先；
- Theme Valid Until；
- Theme Score、匹配关键词、原因和 ConfigurationVersion。

AI 主题分类不参与 Hard Reject，也不能覆盖关键词规则。

### Minimal Risk Rules

- Sell Quote 存在性、状态、正输出、时间、输入数量和 AdapterVersion；
- Pool/Program 版本支持；
- 正式 Run 的 finalized/reconciled 要求；
- Market State 新鲜度；
- Minimum Quote Reserve；
- Maximum Liquidity Drop；
- Maximum Entry Price Impact；
- Mint/Freeze/Adapter Authority 风险；
- Creator/Top10 Holder 集中度；
- 缺失证据保守处理；
- OverallScore、RiskLevel、HardReject、RuleResults、Reasons、InputAsOfTime 和 RiskModelVersion。

所有启用规则的 Missing 结果都不能默认为低风险。Hard Reject 优先于候选进入。

### Candidate Eligibility

```text
Entry Age exceeded
→ Expired

Theme blocked/invalid/required but unmatched
or Risk Hard Reject
or Risk Score exceeds maximum
→ Rejected

Observation or liquidity not ready
→ Observing

All required conditions pass
→ Eligible
```

### 无数据库评估接口

```text
POST /api/v1/intelligence/evaluate
```

接口接收一个带 `InputAsOfTime` 的证据快照，返回完整 Theme、Risk 和 Candidate 解释。它用于离线验证和后续证据 Adapter 联调，不保存历史结果。

## M3B 已完成

### Raydium CPMM Sell Quote

- 将 Spike 中已与固定 Raydium SDK 向量交叉验证的整数 CPMM Exact Input 算法迁入正式 Domain；
- 严格校验固定 CPMM ProgramId、AdapterVersion 和经典 SPL Token Program；
- 使用原始整数储备量和手续费向上取整计算 Quote；
- 拒绝过期 Pool Snapshot、Token-2022、未知 Program/Adapter 和超出存储范围的数量；
- 输出 SellQuote 状态、输入/输出数量、价格冲击、AsOfTime、AdapterVersion 和失败原因。

### Token Risk Evidence

- 使用 finalized `getAccountInfo` 读取 Mint/Freeze Authority；
- 使用 finalized `getTokenSupply` 与 `getTokenLargestAccounts` 计算 Top10 集中度；
- 已知 Creator 地址存在时，使用 `getTokenAccountsByOwner` 计算 Creator 持仓；
- 主/备用 RPC 只在临时不可用时切换，结构性不支持不会被备用源掩盖；
- 所有缺失、暂时不可用和结构性不支持状态均保留，不降级成“低风险”。

### Evidence Composition

`RiskEvidenceCollector` 并行读取 Authority 和 Holder 证据，结合 Sell Quote 与采集状态生成 M3A `RiskEvidenceSnapshot`。缺失字段保持 `null`，由规则引擎执行 Missing + Hard Reject。

## 当前安全边界

系统已经具备证据组件，但尚未完成以下生产闭环：

- 从正式采集链路生成同 Slot 的 Pool State/Vault Reserve Snapshot；
- 将证据采集器接入 Candidate 投影；
- ThemeMatch/RiskAssessment 追加式持久化与查询；
- 使用真实 Solana RPC 对 Authority/Holder 结果进行现场比对；
- 使用 PostgreSQL 验证迁移、幂等和历史查询。

因此当前 Sell Quote 组件只接受可信的 Pool Snapshot 输入，不能宣称已形成可执行的真实链上报价。缺失证据仍会得到 Missing + Hard Reject，调用方不能伪造证据把候选送入策略。

## 下一增量

M3C 将实现：

- ThemeMatch/RiskAssessment 追加式持久化；
- Candidate 投影与历史评估关联；
- 相同 Token、版本和 AsOfTime 的幂等约束；
- Radar 查询返回最新 Theme/Risk 解释。

M3C 可以继续生成代码和数据库迁移，但数据库集成验证需要后续 Docker、云数据库或服务器 PostgreSQL 环境。Pool Snapshot 正式接入和真实证据联调将在可用 Solana RPC 环境单独验收。
