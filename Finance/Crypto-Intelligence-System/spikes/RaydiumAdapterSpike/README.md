# Raydium Adapter Spike

最小可运行原型，用于在正式工程实现前验证两项 M0 假设：

1. CPMM Exact Input 可以使用整数 raw amount 确定性计算；
2. Solana transaction logs 中属于目标 Program 的 instruction/event 可以按稳定日志位置排序。

运行：

```text
dotnet run --project spikes/RaydiumAdapterSpike -- --self-test
```

手工 Quote：

```text
dotnet run --project spikes/RaydiumAdapterSpike -- \
  quote <reserveInRaw> <reserveOutRaw> <amountInRaw> <feeBps>
```

当前边界：

- 这是 M0 原型，不是生产 Adapter；
- Quote 未包含 Token-2022 transfer fee、动态费或路由；
- Event locator 只建立稳定位置，不负责 Anchor discriminator 解码；
- 下一步需读取固定 raw transaction payload，并依据固定 IDL 解码账户和事件；
- 未知 Program/IDL 版本必须拒绝，不允许猜测。
