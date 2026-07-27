# M3 Theme and Minimal Risk

> 状态：M3A-M3D code complete；M3E 验收能力已完成本地验证；真实 Solana RPC 连续运行验收待完成

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

## M3C 已完成

### 追加式评估历史

- 新增 `theme_matches` 与 `risk_assessments`；
- 相同 Token、Configuration/Model Version 和 InputAsOfTime 具有数据库唯一约束；
- 重复写入内容一致时复用历史记录；
- 相同身份但内容不一致时拒绝写入，暴露非确定性结果；
- Theme/Risk 结果、规则明细和原因均保留历史，不覆盖旧版本。

### Candidate 与 Radar

- Candidate 关联最新 ThemeMatch、RiskAssessment 和 EvaluationAsOfTime；
- 只有不早于当前最新时间的结果才能更新 Candidate 状态；
- Radar Candidate 列表和详情返回最新 Theme/Risk 完整解释；
- `IntelligenceAssessmentService` 提供可信采集链路的“评估并保存”入口，不向外开放伪造链上证据的持久化接口。

### 数据库验证

- Migration 005 已在本地 Docker PostgreSQL 16 执行；
- 重复写、冲突写、Candidate 关联和 Radar 查询已通过真实 PostgreSQL 集成测试；
- GitHub CI 在迁移后单独执行 PostgreSQL 持久化测试。

## M3D 已完成

### 同 Slot CPMM 证据

- 解码固定 IDL 的 Raydium CPMM `SwapEvent`；
- 从事件读取交易前 Input/Output Vault 储备，储备与 Swap 位于同一交易 Slot；
- 从实际 InputAmount、TradeFee 和 CreatorFee 唯一反推整数 Basis Points；
- 只有 `base_input=true`、Creator Fee 位于 Input、费率可唯一反推时才标记手续费证据可用；
- Token-2022、Transfer Fee、非唯一费率和未知方向不会被猜测为可用报价；
- Pool 投影保存 Creator、AMM Config 和 Token Program 元数据。

### 自动风险链路

```text
Finalized CPMM Swap
→ Same-slot Vault/Fee Evidence
→ Authority/Holder Evidence
→ Sell Quote + Entry Impact Probe
→ Theme/Risk Evaluation
→ Append-only Persistence
→ Candidate Latest Assessment
→ Radar Explanation
```

- Sell/Entry Probe 默认使用对应储备的 `100 bps`（1%），可配置但必须在 1–10000 bps；
- Confirmed 事件不触发正式评估，升级 Finalized 后重新入队；
- Formal Run 要求 Slot 已 Reconciled，否则进入可重试失败；
- Authority/Holder RPC 临时不可用沿用 Raw Event Retry/Dead Letter；
- Attempted、Completed、Deferred、Unsupported 已提供运行计数器；
- RiskAssessment 保存完整 RiskEvidence JSON，支持审计和回放。

### 验证

- 固定主网 Fixture 验证 Vault 储备、Token Program 和 25 bps 手续费证据；
- 自动链路测试覆盖反向 Mint 方向、1% Probe、Finalized Gate 和持久化；
- Migration 006 已在 Docker PostgreSQL 16 执行；
- PostgreSQL 集成测试覆盖 Finality 重入队、Reconciled Context、Evidence JSON 和 Radar 关联。

## 当前安全边界

代码链路已经闭合，但尚未完成真实运行验收：

- 使用真实 Solana RPC 对 Authority/Holder 结果进行现场比对；
- 在连续主网数据中核对 SwapEvent 储备、费率和实际 Vault Balance；
- 对连续采集期间的评估延迟、失败重试和覆盖率进行验收。

因此当前 Sell Quote 组件只接受可信的 Pool Snapshot 输入，不能宣称已形成可执行的真实链上报价。缺失证据仍会得到 Missing + Hard Reject，调用方不能伪造证据把候选送入策略。

## M3E 验收能力已实现

代码已增加：

- Formal Run 缺少主/备 RPC、主备 URL 相同或 Source Name 相同时拒绝启动；
- `automated_assessment_attempts` 持久化 Attempted、Deferred、Unsupported、Completed 和重试历史；
- `GET /api/v1/operations/m3-acceptance?from=<T0>` 生成固定窗口机器验收报告；
- 自动检查连续时长、Terminal Coverage、Gap、Dead Letter、Checkpoint 水位和备用 RPC 演练；
- Migration 007 已在 Docker PostgreSQL 16 验证。

实际 Exit Gate 仍待执行：

- 配置付费主 RPC 和独立备用 RPC；
- 固定 T0 连续运行七天；
- 对 Authority、Holder、Vault 和 Quote 完成人工抽样核对；
- 机器检查与人工检查均通过后进入 M4 Early Momentum Strategy 和 Historical Replay。

详细步骤见 `M3E_LIVE_RPC_ACCEPTANCE.md`。

在 M3E 通过前，系统不能宣称已完成生产级真实链上风险闭环，也不能进入真实交易。
