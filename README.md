# AllGunEvo — 20 Minutes Till Dawn (v0.6.6) BepInEx 插件

## 功能
1. 开局拥有所选枪的全部武器进化（GunEvolution）。
2. 每 30 秒把全图 XPPickup 交给玩家。
3. 帧率 / 垂直同步 / 渲染队列深度三件套（低延迟）。

## 环境事实（2026-10-26 实测）
- 游戏：D:/Steam/steamapps/common/20MinuteTillDawn，buildid 12679152，Unity 2019.4.40f1，Mono。
- 加载器：Gale 1.23.2 管理的 BepInEx 5.4.19（core/BepInEx.dll 5.4.19.0）+ 0Harmony 2.9.0.0。
- 反编译产物：ref/game（ilspycmd -p，618 个 .cs，不入库）。
- **部署目录固定为 `plugins/AllGunEvo/`；同 GUID 只能存在一份，否则 BepInEx 只加载先扫到的那个**（v2.0 前踩过：旧版目录残留导致新功能全部不生效）。

## 关键调用链（ref/game 内路径:行号）
- 武器进化：flanne.Core/CombatState.cs:11 —— 等级 20 切 GunEvoMenuState。
- 进化数据：flanne/GunData.cs:49 gunEvolutions。
- 应用入口：flanne.PerkSystem/PlayerPerks.cs:35 Equip(GunEvolution) => Copy().Apply(player)。
- 爆炸免疫：flanne.PerkSystem.Actions/ExplosionImmunityAction.cs:9 RemoveVulnerability("HarmfulToPlayer")。
- 伤害判定：flanne/PlayerHealth.cs:127 Start() 重建 vulnerableTags；:213 CheckIfVulnerable 用 tag.Contains。
- 经验球：flanne.Pickups/XPPickup.cs:11 UsePickup -> PlayerXP.GainXP + XPPickupEvent。
- 拾取流程：flanne.Pickups/Pickup.cs:17 OnTriggerEnter2D -> PickupCR（0.3s tween -> UsePickup -> 回池）。
- 状态机属性：StateMachine.cs:7 **public virtual State CurrentState**（因此无需反射）。
- 交换链重建：flanne.UI/OptionsSetter.cs:166 SetFullscreen / :179 SetResolution。

## 架构：六边形（端口 / 适配器）

    src/AllGunEvo.Core/            核心，零 UnityEngine 引用（纯类型 + 纯决策）
      Settings.cs                  纯 Settings DTO
      Ports.cs                     IEvolutionGateway / IVacuumGateway / IDisplayGateway /
                                   ISettingsProvider / ILogPort / EvolutionHandle / DisplayState
      Policies.cs                  EvolutionGrantPolicy / VacuumSchedule / DisplayPolicy
      FeatureContracts.cs          IAllGunEvoFeature + FeatureRegistry（快照缓存 + 异常隔离）
      Features.cs                  EvolutionFeature / VacuumFeature / DisplayFeature
      AllGunEvoApi.cs              RegisterFeature（第三方入口）

    src/AllGunEvo/                 适配器，Unity / BepInEx 依赖只在这一侧
      Adapters/GameApi.cs          唯一反射点 + 两个 driven port + 目标校验
      Adapters/Gateways.cs         UnityLogPort / QualitySettingsGateway / ConfigSettingsProvider
      Adapters/HarmonyLinks.cs     RunStartedPatch / SwapChainPatch / FrameRunner（只转发）
      AdapterHost.cs               组合根
      Plugin.cs                    BepInEx 入口

    tests/AllGunEvo.Core.Tests/    xunit，38 个用例

依赖方向：适配器 → 核心，永不反向。核心纯净性由编译器强制：测试项目编同一份 Core 源码且
不引用任何 Unity 程序集，核心一旦 using UnityEngine 立刻编译失败。

### 决策在核心，副作用在适配器

| 决策 | 核心 | 适配器 |
|---|---|---|
| 这一局还要不要发进化 | EvolutionGrantPolicy（run token 幂等） | 提供 token（PlayerController.InstanceID） |
| 单条进化失败怎么办 | 逐条 try/catch + 计数 | 调 PlayerPerks.Equip |
| 什么时候吸经验 | VacuumSchedule（间隔／战斗门控／下限钳制／丢弃积压） | 提供 deltaTime／inCombat，执行 PickupCR |
| 显示要不要重写 | DisplayPolicy.ShouldReapply（int 级比较，零分配） | 读写 QualitySettings |
| 谁被调用、谁失败不影响谁 | FeatureRegistry（快照数组 + 逐 feature 隔离） | Harmony 补丁 / MonoBehaviour 转发 |

### 扩展点
第三方 BepInEx 插件照常被 BepInEx 发现（不需要 BepInDependency，与生态里的 UtillI 同形态）：

    AllGunEvoApi.RegisterFeature(new MyFeature());   // IAllGunEvoFeature { Id, OnRunStarted, OnTick }

内置三个功能走同一注册表，无特权路径；重复 Id 拒绝并记警告。

## 性能：热路径零反射、零分配

| 项 | 修前 | 修后 |
|---|---|---|
| 取战斗状态 | 每帧 1 次 `PropertyInfo.GetValue` | 直接访问 public 属性 `CurrentState` |
| 查找 GameController | 标题场景每帧全场景 `FindObjectOfType` | 1 Hz 节流（`ControllerProbeSeconds`） |
| 吸球 | 每颗球 1 次 `MethodInfo.Invoke` + `new object[]` | 缓存 `Func<Pickup, GameObject, IEnumerator>` 委托 |
| 读取 pickUpCoroutine | 每颗球 2 次反射 `GetValue/SetValue` | `AccessTools.FieldRef` 强类型引用 |
| feature 分发 | 每帧 `features.ToArray()` | 注册时重建快照，遍历零分配 |
| 设置读取 | 每帧 3 个 DTO 分配 | 预分配实例 + 字段刷新（热重载照常生效） |
| 漂移比较 | 内部 `Normalize` 再分配 1 个 DTO | 纯 int 归一化函数 |
| 每帧托管分配合计 | 4 处 / ~136 B（≈8 KB/s @60fps） | 0 |

反模式修复（ast-grep 规则 `probe/rules/antipatterns.yml` 复扫确认）：
`catch (e) { log(e.Message) }` → `e.ToString()`（保留堆栈）；反射目标字符串 → `nameof`（public 成员，编译期校验）
与 `const`（private 成员，单点声明，同时供 `AccessTools` 与 `[HarmonyPatch]` 使用）；`Verbose` 死配置复活为 1 Hz 诊断。

**netstandard2.0 / Unity Mono 2019 拿不到的现代 API**（属硬约束，非偷懒）：`Math.Clamp`、
C# 10 插值字符串处理器、`record`/`init`/`Span<T>`/`System.Text.Json`/`FrozenDictionary`/`Random.Shared`、
`FindObjectsOfType<T>(List<T>)`（Unity 2020.1+）。故手写 `Clamp` 与每次吸球的数组分配是当前 TFM 下的正解。

## 调研方法论（下次改这块代码先看这段）
- **codebase_memory**：`index_repository` → `get_architecture`（hotspots/clusters）→ `trace_path`。
  hotspots 里 `DisplayState..ctor` fan_in 排第一，正是分配点的信号。
- **ast-grep**：规则文件见 `probe/rules/antipatterns.yml`（`alloc-inside-update`、`caught-exception-message-only`、
  `per-frame-static-lookup`、`static-mutable-field`）。注意 `alloc-inside-update` 对当前代码命中 **0** ——
  每帧分配不在 `Update` 字面里，而在调用链末端，纯 pattern 看形状、跨方法必须靠调用链。

## 构建 / 测试 / 部署
    cd src/AllGunEvo && dotnet build -c Release
    cd tests/AllGunEvo.Core.Tests && dotnet test        # 38 用例
    cp src/AllGunEvo/bin/Release/AllGunEvo.dll <profile>/BepInEx/plugins/AllGunEvo/

## 配置（BepInEx/config/local.20mtd.allgunevo.cfg，改后热生效）
- Evolutions.GiveAllOnRunStart = true
- XPVacuum.Enabled = true
- XPVacuum.IntervalSeconds = 30
- Display.Enabled = true
- Display.VSyncCount = 1        # 0=关（最低延迟，会撕裂）1=跟随刷新率 2-4=每 N 次刷新
- Display.TargetFrameRate = -1  # -1=不限；VSyncCount 非 0 时 Unity 忽略此项
- Display.MaxQueuedFrames = 3   # 1=最低延迟 3=Unity 默认
- Display.Verbose = false       # true 时每秒打印实测值：display ok: vSync=.. targetFrameRate=.. maxQueuedFrames=..

## 启动自检（LogOutput.log）
    ok CombatState.Enter / PlayerPerks.Equip / PickupCR / pickUpCoroutine / gunEvolutions /
       SetFullscreen / SetResolution
    feature registered: core.evolutions / core.xp-vacuum / core.display
    display applied (startup): vSync=1 targetFrameRate=-1 maxQueuedFrames=3
    Applied 3/3 evolutions for gun <name>.
    Vacuum collected N xp pickups.

判据：
- Blast Shield 生效 = 玩家 `vulnerableTags` 不含 `HarmfulToPlayer`（当前日志未输出该项，需要时再加诊断端口）。
- vSync 生效 = `Display.Verbose` 打出的值与配置一致；出现 `display applied (options fullscreen)` 说明游戏重建了交换链（预期），
  反复出现 `display drift detected` 才是异常。

## 已知限制
- 20 级进化菜单仍会出现（再叠加同一条）。
- 一次吸上千球会同时起同等数量 0.3s tween，短时开销；若卡顿需改分批。
- 游戏更新改类名时，启动自检会报 missing game members。
