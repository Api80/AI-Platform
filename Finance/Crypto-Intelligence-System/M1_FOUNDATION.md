# M1 Foundation

> 状态：Completed

## Goal

建立 Phase 1 正式工程骨架，使后续链上采集和策略模块具备可编译、可测试、可迁移、可观测的基础。

## Solution

```text
CryptoIntelligence.Domain
  no infrastructure dependencies

CryptoIntelligence.Application
  → Domain

CryptoIntelligence.Infrastructure
  → Application + Domain

CryptoIntelligence.Contracts
  transport-only contracts

CryptoIntelligence.Api / Worker
  composition roots
```

## Implemented Foundation

- .NET 8 Solution 和中央依赖版本；
- nullable、analyzer、warning-as-error 和确定性构建；
- Solana Slot、Signature、ProgramId、TokenAddress、WalletAddress；
- RawAmount、BasisPoints 和 UTC Timestamp；
- Phase 1 配置加载、跨字段校验和正式 Run Gate；
- Canonical JSON 配置快照和 SHA-256 Hash；
- PostgreSQL EF Core DbContext；
- Migration 001：Configuration Foundation；
- API live/ready Health Checks；
- CorrelationId 和 JSON 结构化日志；
- Worker 基础宿主；
- 单元、架构和迁移脚本测试；
- GitHub Actions Build/Test/Migration CI。

## Runtime Secrets

数据库连接不得进入普通配置或 StrategyRun 快照。API/Worker 只从以下位置读取：

```text
ConnectionStrings__Postgres
CRYPTO_DB_CONNECTION
```

仓库不提供真实连接字符串、RPC 密钥、私钥或签名能力。

## Local Commands

```text
dotnet tool restore
dotnet restore CryptoIntelligence.sln
dotnet build CryptoIntelligence.sln --configuration Release
dotnet test CryptoIntelligence.sln --configuration Release
```

生成幂等迁移脚本：

```text
dotnet tool run dotnet-ef migrations script \
  --idempotent \
  --project src/CryptoIntelligence.Infrastructure/CryptoIntelligence.Infrastructure.csproj \
  --startup-project src/CryptoIntelligence.Infrastructure/CryptoIntelligence.Infrastructure.csproj
```

启动 API 或 Worker 前，通过环境变量提供 PostgreSQL 连接。

## M1 Gate Result

- GitHub CI 在干净环境执行通过；
- Migration 001 已在 PostgreSQL 16 空库执行通过；
- PR #3 已评审并合并。
