# AllGunEvo — 20 Minutes Till Dawn (v0.6.6) BepInEx 插件

## 功能
1. 开局拥有所选枪的全部武器进化（GunEvolution）。
2. 每 30 秒把全图 XPPickup 交给玩家。
3. 帧率 / 垂直同步 / 渲染队列深度三件套（低延迟）。

## 环境事实（2026-10-26 实测）
- 游戏：D:/Steam/steamapps/common/20MinuteTillDawn，buildid 12679152，Unity 2019.4.40f1，Mono。
- 加载器：Gale 1.23.2 管理的 BepInEx 5.4.19（core/BepInEx.dll 5.4.19.0）+ 0Harmony 2.9.0.0。
- profile：C:/Users/11/AppData/Roaming/com.kesomannen.gale/20-minutes-till-dawn/profiles/Default/BepInEx
- 反编译产物：ref/game（ilspycmd -p，618 个 .cs，不入库）。
- **部署目录固定为 `plugins/AllGunEvo/`；同 GUID 只能存在一份，否则 BepInEx 只加载先扫到的那个。**

## 关键调用链（ref/game 内路径:行号）
- 武器进化：flanne.Core/CombatState.cs:11 —— 等级 20 切 GunEvoMenuState。
- 进化数据：flanne/GunData.cs:49 gunEvolutions。
- 应用入口：flanne.PerkSystem/PlayerPerks.cs:35 Equip(GunEvolution) => Copy().Apply(player)。
- 进化内容：flanne/GunEvolution.cs:53 Apply() = statChanges + effects.Equip + 可选换枪。
- 爆炸免疫：flanne.PerkSystem.Actions/ExplosionImmunityAction.cs:9 RemoveVulnerability("HarmfulToPlayer")。
- 伤害判定：flanne/PlayerHealth.cs:127 Start() 重建 vulnerableTags；:213 CheckIfVulnerable 用 tag.Contains 匹配。
- 经验球：flanne.Pickups/XPPickup.cs:11 UsePickup -> PlayerXP.GainXP + XPPickupEvent。
- 拾取流程：flanne.Pickups/Pickup.cs:17 OnTriggerEnter2D -> PickupCR（0.3s tween -> UsePickup -> 回池）。
- 交换链重建：flanne.UI/OptionsSetter.cs:166 SetFullscreen / :179 SetResolution。

## 架构：六边形（端口 / 适配器）

    src/AllGunEvo.Core/            核心，零 UnityEngine 引用（纯类型 + 纯决策）
      Settings.cs                  纯 Settings DTO（Evolution / Vacuum / Display）
      Ports.cs                     端口：IEvolutionGateway / IVacuumGateway / IDisplayGateway /
                                   ISettingsProvider / ILogPort / EvolutionHandle / DisplayState
      Policies.cs                  纯决策：EvolutionGrantPolicy / VacuumSchedule / DisplayPolicy
      FeatureContracts.cs          IAllGunEvoFeature + FeatureRegistry（异常隔离的分发器）
      Features.cs                  三个内置功能（EvolutionFeature / VacuumFeature / DisplayFeature）
      AllGunEvoApi.cs              对外静态入口 RegisterFeature（第三方插件用）

    src/AllGunEvo/                 适配器，全部 Unity / BepInEx 依赖都在这一侧
      Adapters/GameApi.cs          唯一反射点 + 实现 IEvolutionGateway / IVacuumGateway + 目标校验
      Adapters/Gateways.cs         UnityLogPort / QualitySettingsGateway / ConfigSettingsProvider
      Adapters/HarmonyLinks.cs     RunStartedPatch / SwapChainPatch / FrameRunner（只转发，不决策）
      AdapterHost.cs               组合根：装配端口、注册 features、转发引擎事件
      Plugin.cs                    BepInEx 入口：ConfigEntry 绑定 + Harmony 注册

    tests/AllGunEvo.Core.Tests/    xunit，35 个用例

依赖方向：适配器 → 核心，永不反向。核心不认识 Unity、BepInEx、Harmony。
纯净性由编译器强制：测试项目以 `<Compile Include="../../src/AllGunEvo.Core/**/*.cs">` 编同一份源码，
且不引用任何 Unity 程序集——核心一旦 using UnityEngine，测试项目立刻编译失败。

### 决策在核心，副作用在适配器

| 决策 | 核心 | 适配器 |
|---|---|---|
| 这一局还要不要发进化 | EvolutionGrantPolicy（run token 幂等） | 提供 run token（PlayerController.InstanceID） |
| 单条进化失败怎么办 | 逐条 try/catch + 计数 | 真正调用 PlayerPerks.Equip |
| 什么时候吸经验 | VacuumSchedule（间隔 + 战斗门控 + 下限钳制 + 丢弃积压） | 提供 deltaTime / inCombat，执行 PickupCR |
| 显示设置要不要重写 | DisplayPolicy.ShouldReapply（三项比对） | 读写 QualitySettings |
| 谁在什么时候被调用 | FeatureRegistry（异常隔离 + 快照遍历） | Harmony 补丁与 MonoBehaviour 转发 |

### 扩展点
第三方 BepInEx 插件照常被 BepInEx 发现（不需要 `BepInDependency`，与生态里的 UtillI 同形态），
在其 Awake 里调用：

    AllGunEvoApi.RegisterFeature(new MyFeature());   // 实现 IAllGunEvoFeature { Id, OnRunStarted, OnTick }

内置三个功能走同一个注册表，没有特权路径。重复 Id 会被拒绝并记警告。

## 构建 / 测试 / 部署
    cd src/AllGunEvo && dotnet build -c Release        # 插件（含 Core 源码）
    cd tests/AllGunEvo.Core.Tests && dotnet test       # 核心单测（35 个）
    cp src/AllGunEvo/bin/Release/AllGunEvo.dll <profile>/BepInEx/plugins/AllGunEvo/

## 配置（BepInEx/config/local.20mtd.allgunevo.cfg，改后热生效）
- Evolutions.GiveAllOnRunStart = true
- XPVacuum.Enabled = true
- XPVacuum.IntervalSeconds = 30
- Display.Enabled = true
- Display.VSyncCount = 1        # 0=关（最低延迟，会撕裂）1=跟随刷新率 2-4=每 N 次刷新
- Display.TargetFrameRate = -1  # -1=不限；VSyncCount 非 0 时 Unity 忽略此项
- Display.MaxQueuedFrames = 3   # 1=最低延迟 3=Unity 默认
- Display.Verbose = false       # 预留诊断开关

## 启动自检（LogOutput.log）
    ok CombatState.Enter
    ok PlayerPerks.Equip(GunEvolution)
    ok Pickup.PickupCR
    ok Pickup.pickUpCoroutine
    ok StateMachine.CurrentState
    ok GunData.gunEvolutions
    ok OptionsSetter.SetFullscreen
    ok OptionsSetter.SetResolution
    feature registered: core.evolutions / core.xp-vacuum / core.display
    display applied (startup): vSync=1 targetFrameRate=-1 maxQueuedFrames=3
    Applied 3/3 evolutions for gun <name>.
    Vacuum collected N xp pickups.

判据：
- Blast Shield 生效 = 应用日志之后玩家的 vulnerableTags 不含 HarmfulToPlayer。
- vSync 生效 = 没有反复出现 "display drift detected"；出现 "display applied (options fullscreen)" 说明游戏重建了交换链，属预期。

## 已知限制
- 20 级进化菜单仍会出现（再叠加同一条）。
- 一次吸上千球会同时起同等数量 0.3s tween，短时开销；若卡顿需改分批。
- 游戏更新改类名时，启动自检会报 missing game member。
