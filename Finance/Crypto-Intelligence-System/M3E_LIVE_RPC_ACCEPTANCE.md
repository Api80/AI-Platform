# M3E Live RPC Acceptance Runbook

## 目标

使用真实 PostgreSQL、付费主 Solana RPC 和独立备用 RPC，连续验证自动风险链路。运行只读取公开链上数据，不读取私钥、不签名、不发送交易。

M3E 分为两部分：

1. 机器检查：由 `/api/v1/operations/m3-acceptance` 生成；
2. 人工证据核对：Authority、Holder、Vault 和 Quote 抽样复核。

只有两部分都通过，才可以关闭 M3 Exit Gate。接口中的 `automatedChecksPassed` 只代表机器检查通过，不代表整项 M3E 已完成。

## 启动前准备

不得把 RPC URL、访问令牌或数据库密码写入仓库。Worker 使用以下环境变量：

```text
CRYPTO_DB_CONNECTION
SOLANA_RPC_WS_URL
SOLANA_RPC_HTTP_URL
SOLANA_RPC_FALLBACK_HTTP_URL
CryptoIntelligence__formalRun=true
CryptoIntelligence__source__historicalRunStartSlot=<验收起始 finalized slot>
CryptoIntelligence__source__rpcSourceName=<主 RPC 的非敏感名称>
CryptoIntelligence__source__fallbackRpcSourceName=<备用 RPC 的非敏感名称>
```

API 至少需要相同的数据库连接、`formalRun`、主备 Source Name 和验收阈值配置。

正式模式会拒绝以下启动方式：

- 未配置 WebSocket、主 HTTP RPC 或备用 HTTP RPC；
- 主 HTTP RPC 与备用 HTTP RPC 地址相同；
- 主 Source Name 与备用 Source Name 相同；
- 未设置明确的 Historical Run Start Slot；
- 关闭 Reconciled Data 要求。

“地址不同”只能防止误配为同一 URL。主备 RPC 是否来自独立供应商仍需人工确认，验收记录只保存非敏感供应商名称。

## 数据库

Docker PostgreSQL 启动后执行：

```text
dotnet ef database update --project src/CryptoIntelligence.Infrastructure
```

Migration 007 新增 `automated_assessment_attempts`，按 Raw Event 唯一记录：

- Attempt Count；
- Deferred Count；
- 当前 Outcome：Attempted、Deferred、Unsupported 或 Completed；
- 首次/最近尝试时间；
- Terminal 完成时间和原因。

重试成功后 Outcome 会变为 Completed，但历史 Deferred Count 会保留。

## 固定验收窗口

验收开始时记录一个固定 UTC 时间 `T0`。整个验收期始终使用同一个 `from=T0`，不能使用不断滚动的七天窗口掩盖早期故障。

```text
GET /api/v1/operations/m3-acceptance?from=<T0>
```

默认机器门槛：

- 连续可观测时间不少于 168 小时；
- 自动评估样本不少于 1；
- Completed + Unsupported 的 Terminal Coverage 不低于 9500 bps；
- 未解决 Gap 为 0；
- Dead Letter 为 0；
- 每个配置 Program 都有 Checkpoint；
- Checkpoint 水位满足 Observed ≥ Persisted ≥ Processed ≥ Finalized ≥ Reconciled；
- 验收窗口内至少观察到一次备用 RPC 产生的 Raw Event。

阈值位于 `CryptoIntelligence.acceptance`，修改必须经过版本化配置评审，不能为了通过验收临时放宽。

## 备用 RPC 演练

至少执行一次受控演练：

1. 保存演练前的验收报告；
2. 暂时阻断 Worker 对主 HTTP RPC 的访问；
3. 保持备用 HTTP RPC 可用；
4. 确认补采/Transaction/Token Evidence 请求可以由备用源完成；
5. 恢复主 RPC；
6. 确认 Worker 继续推进 Checkpoint；
7. 确认报告中的 `fallbackRawEvents` 大于 0。

WebSocket 断开与恢复仍需结合 Worker 结构化日志记录。不得通过手工修改数据库 Source 字段制造备用演练通过结果。

## 人工证据抽样

从验收窗口抽取至少 20 条 Completed，以及全部 Unsupported 记录：

- 用独立区块浏览器或第二 RPC 核对 Mint/Freeze Authority；
- 核对 Token Supply、Top10 和 Creator Holding；
- 核对交易 Slot、Input/Output Vault Before Balance；
- 核对 Trade Fee、Creator Fee 和费率反推；
- 使用保存的整数储备与手续费重算 Sell Quote；
- 确认 Hard Reject 原因与缺失/不支持证据一致。

每条抽样记录保存 Transaction Signature、Slot、非敏感数据摘要、核对结论和核对时间。不得保存 RPC Token。

## 每日留档

每天保存以下 JSON 和日志摘要：

```text
GET /api/v1/operations/m3-acceptance?from=<T0>
GET /api/v1/ingestion/checkpoints
GET /api/v1/ingestion/gaps?limit=1000
GET /api/v1/ingestion/capacity
GET /health/ready
```

同时记录 Worker 重启、WebSocket 重连、主备切换、Retry、Dead Letter 和人工抽样结果。

## Exit Gate

满足以下全部条件后才能将 M3 标记为完成：

- `automatedChecksPassed=true`；
- 连续运行达到配置时长；
- 备用 RPC 演练通过；
- Completed 抽样和全部 Unsupported 人工核对通过；
- 没有未解释的 Gap、Dead Letter、错误 Hard Reject 或错误放行；
- 验收报告和每日证据已归档。

未满足任一条件时，继续保持 “M3 live RPC acceptance pending”，不得进入真实交易。
