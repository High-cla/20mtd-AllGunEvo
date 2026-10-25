# AllGunEvo — 20 Minutes Till Dawn (v0.6.6) BepInEx 插件

## 功能
1. 开局拥有所选枪的全部武器进化（GunEvolution）。
2. 每 30 秒把全图 XPPickup 交给玩家。
3. 帧率控制：软限帧（默认 刷新率-3）／vSync／渲染队列深度。

## 环境事实（2026-10-26 实测）
- 游戏：D:/Steam/steamapps/common/20MinuteTillDawn，buildid 12679152，Unity 2019.4.40f1，Mono。
- 加载器：Gale 1.23.2 + BepInEx 5.4.19（core/BepInEx.dll 5.4.19.0）+ 0Harmony 2.9.0.0。
- Gale 启动方式：Steam `-applaunch 1966900 --doorstop-enable true --doorstop-target <profile>/BepInEx/core/BepInEx.Preloader.dll`，
  **不复制 plugins 到游戏目录**，BepInEx 根目录即 profile 的 BepInEx。
- **部署目录固定 `plugins/AllGunEvo/`；同 GUID 只能一份**，否则 BepInEx 只加载先扫到的那个。
- **BepInEx 出厂 `LogLevels = Error, Warning`，Info 不落盘** —— 插件正常日志会全部消失（本仓库已改成含 Info，备份 BepInEx.cfg.bak）。

## 关键调用链（ref/game 内路径:行号）
- 武器进化：flanne.Core/CombatState.cs:11（等级 20 切 GunEvoMenuState）。
- 进化数据：flanne/GunData.cs:49 gunEvolutions；应用：flanne.PerkSystem/PlayerPerks.cs:35。
- 爆炸免疫：flanne.PerkSystem.Actions/ExplosionImmunityAction.cs:9 RemoveVulnerability("HarmfulToPlayer")；
  伤害判定 flanne/PlayerHealth.cs:127（Start 重建 vulnerableTags）、:213 CheckIfVulnerable。
- 经验球：flanne.Pickups/XPPickup.cs:11；拾取 flanne.Pickups/Pickup.cs:17（OnTriggerEnter2D -> PickupCR）。
- 状态机：StateMachine.cs:7 public virtual CurrentState（因此无需反射）。
- 交换链重建：flanne.UI/OptionsSetter.cs:166 SetFullscreen / :179 SetResolution（游戏唯一重建点）。

## 架构：六边形（端口 / 适配器）

    src/AllGunEvo.Core/     核心，零 UnityEngine 引用
      Settings.cs           纯 DTO（Evolution / Vacuum / Display，含软限帧两项）
      Ports.cs              IEvolutionGateway / IVacuumGateway / IDisplayGateway(+RefreshRate) /
                            ISettingsProvider / ILogPort / EvolutionHandle / DisplayState
      Policies.cs           EvolutionGrantPolicy / VacuumSchedule / DisplayPolicy
      FeatureContracts.cs   IAllGunEvoFeature + FeatureRegistry（快照缓存 + 逐 feature 隔离）
      Features.cs           EvolutionFeature / VacuumFeature / DisplayFeature
      AllGunEvoApi.cs       RegisterFeature（第三方入口）
    src/AllGunEvo/          适配器，Unity / BepInEx 依赖只在这一侧
      Adapters/GameApi.cs       唯一反射点 + 两个 driven port + 目标校验
      Adapters/Gateways.cs      UnityLogPort / QualitySettingsGateway(+交换链重建) / ConfigSettingsProvider
      Adapters/HarmonyLinks.cs  RunStartedPatch / SwapChainPatch / FrameRunner
      AdapterHost.cs            组合根
      Plugin.cs                 ConfigEntry → DTO
    tests/AllGunEvo.Core.Tests/ xunit，49 用例

核心纯净性由编译器强制：测试项目编同一份 Core 源码且不引用任何 Unity 程序集。

## 显示与帧率：三条路径与它们的前提

| 手段 | 生效前提 | 备注 |
|---|---|---|
| `QualitySettings.vSyncCount=1` | **必须重建交换链**（Unity 只在交换链创建时读 PresentInterval） | 我们在值变化时调用 `Screen.SetResolution` 重建一次（带守卫）。实测该游戏运行时单改变量 → 600-700fps 无效 |
| `Application.targetFrameRate` | `vSyncCount == 0`（否则 Unity 直接忽略） | 软限帧走这条 |
| **软限帧 刷新率-3** | 需要 `Screen.currentResolution.refreshRate > 0` | 默认开启：`SoftLimitToRefresh=true`、`SoftLimitOffset=3`，并强制 `vSyncCount=0` |

为什么 -3：帧率恰好等于刷新率时，每帧正好压在 vblank 边界，帧时间抖动最差；低 3fps 让每帧稳定落在一个刷新窗口内（G-Sync/VRR 的经典裁剪做法）。
`RefreshRate` 报 0（某些窗口模式）时自动回退到 `TargetFrameRate` 配置值。

## 性能：热路径零反射、零分配
| 项 | 修前 | 修后 |
|---|---|---|
| 取战斗状态 | 每帧 `PropertyInfo.GetValue` | 直接访问 public `CurrentState` |
| 查找 GameController | 标题场景每帧全场景搜索 | 1 Hz 节流 |
| 吸球 | 每球 `MethodInfo.Invoke` + `new object[]` | 缓存委托 + `AccessTools.FieldRef` |
| feature 分发 | 每帧 `ToArray()` | 注册时重建快照 |
| 设置读取 | 每帧 3 个 DTO 分配 | 预分配实例 + 字段刷新 |
| 漂移比较 | 内部再分配 DTO | 纯 int 归一化 |
| 每帧托管分配 | ~136 B | 0 |

不做的：`FindObjectsOfType<XPPickup>` 每次吸球的数组分配（Unity 2019 无 List 重载）；静态可变字段（BepInEx 补丁与 ConfigEntry 的框架约束）。
netstandard2.0 / Mono 2019 拿不到：`Math.Clamp`、插值字符串处理器、`record`/`init`/`Span<T>`/`System.Text.Json`。

## 构建 / 测试 / 部署
    cd src/AllGunEvo && dotnet build -c Release
    cd tests/AllGunEvo.Core.Tests && dotnet test        # 49 用例
    cp src/AllGunEvo/bin/Release/AllGunEvo.dll <profile>/BepInEx/plugins/AllGunEvo/

## 配置（BepInEx/config/local.20mtd.allgunevo.cfg，改后热生效）
- Evolutions.GiveAllOnRunStart = true
- XPVacuum.Enabled = true
- XPVacuum.IntervalSeconds = 30
- Display.Enabled = true
- **Display.SoftLimitToRefresh = true**  # 帧率 = 刷新率 - SoftLimitOffset，并强制 vSyncCount=0
- **Display.SoftLimitOffset = 3**        # 低于刷新率的幅度
- Display.VSyncCount = 1                # 0=关 1=跟随刷新 2-4=每 N 次刷新；软限帧开启时被忽略
- Display.TargetFrameRate = -1          # 软限帧或 vSync 开启时被忽略
- Display.MaxQueuedFrames = 3           # 1=最低延迟 3=Unity 默认
- Display.Verbose = false               # true：每秒一行实测 fps（Warning 级，不会被日志级别过滤）

## 启动自检（LogOutput.log）
    ok CombatState.Enter / PlayerPerks.Equip / PickupCR / pickUpCoroutine / gunEvolutions /
       SetFullscreen / SetResolution
    feature registered: core.evolutions / core.xp-vacuum / core.display
    display applied (startup): vSync=0 targetFrameRate=57 maxQueuedFrames=3 refresh=60Hz [soft limit 60-3 = 57fps]
    display live: fps=57.0 frames=57 vSync=0 targetFrameRate=57 maxQueuedFrames=3 refresh=60Hz   (Verbose=true)
    Applied 3/3 evolutions for gun <name>.
    Vacuum collected N xp pickups.

判据：`display live: fps=` 接近 `刷新率-3` = 限帧生效；反复出现 `display drift detected` = 有外部在改。

## 已知限制
- vSync 需要交换链重建，个别视频模式（窗口/无边框）Unity 2019 仍可能不等待 vblank —— 此时用软限帧。
- 20 级进化菜单仍会出现（再叠加同一条）。
- 一次吸上千球会同时起同等数量 0.3s tween；若卡顿需分批。
- 游戏更新改类名时启动自检会报 missing game members。
