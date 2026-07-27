# Raydium Adapter Spike

最小可运行原型，用于在正式工程实现前验证两项 M0 假设：

1. CPMM Exact Input 可以使用整数 raw amount 确定性计算；
2. Solana transaction logs 中属于目标 Program 的 instruction/event 可以按稳定日志位置排序；
3. 固定 Anchor IDL discriminator 可以离线解码 instruction/event；
4. 主网交易可以固化后脱离 RPC 重复回放。

运行：

```text
dotnet run --project spikes/RaydiumAdapterSpike -- --self-test
```

手工 Quote：

```text
dotnet run --project spikes/RaydiumAdapterSpike -- \
  quote <reserveInRaw> <reserveOutRaw> <amountInRaw> \
  <tradingFeeBps> [creatorFeeBps]
```

捕获公开主网交易：

```text
dotnet run --project spikes/RaydiumAdapterSpike -- \
  capture <fixture-manifest.json> <raw-output-directory> [rpc-url]
```

验证离线 Fixtures：

```text
dotnet run --project spikes/RaydiumAdapterSpike -- \
  verify-fixtures <fixture-manifest.json> <raw-directory> \
  <launchlab-idl.json> <cpmm-idl.json>
```

验证 WebSocket 发现后可通过 RPC 补采：

```text
dotnet run --project spikes/RaydiumAdapterSpike -- \
  probe-discovery <websocket-url> <rpc-url> <program-id> [timeout-seconds]
```

当前边界：

- 这是 M0 原型，不是生产 Adapter；
- Quote 未包含 Token-2022 transfer fee、动态费或路由；
- Parser 已解码固定 IDL 的 instruction/event discriminator，但尚未解码所有事件字段；
- TokenTransferred 当前由 Token Program 日志定位，正式实现需结合 inner instruction 和 balance delta；
- 未知 Program/IDL 版本必须拒绝，不允许猜测。
