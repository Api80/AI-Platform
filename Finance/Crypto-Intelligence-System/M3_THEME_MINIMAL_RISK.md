# M3 Theme and Minimal Risk

> 状态：M3A Rule Engine complete; evidence adapters and persistence pending

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

## 当前安全边界

系统尚未从 Solana/Raydium 自动取得以下正式证据：

- 可执行的实际 Sell Quote；
- Mint/Freeze Authority；
- Adapter 已知权限风险；
- Creator Holding；
- Top10 Holder Concentration。

因此在启用相应规则时，证据缺失会得到 Missing + Hard Reject。调用方不能伪造证据把候选送入策略。

## 下一增量

M3B 将实现：

- Raydium Sell Quote Adapter；
- Mint/Freeze Authority 读取；
- 最小 Holder Snapshot；
- ThemeMatch/RiskAssessment 追加式持久化；
- Candidate 投影与历史评估关联；
- 相同 Token、版本和 AsOfTime 的幂等约束；
- Radar 查询返回最新 Theme/Risk 解释。

M3B 可以继续生成代码和数据库迁移，但真实证据联调仍需要可用的 Solana RPC；数据库集成验证可以在后续 Docker、云数据库或服务器 PostgreSQL 环境完成。
