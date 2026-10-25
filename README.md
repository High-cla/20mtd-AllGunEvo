# AllGunEvo — 20 Minutes Till Dawn (v0.6.6) BepInEx 插件

## 功能
1. 开局拥有所选枪的全部武器进化（GunEvolution）。
2. 每 30 秒把全图 XPPickup 交给玩家。
3. 帧率 / 垂直同步 / 渲染队列深度三件套（低延迟）。

## 环境事实（2026-10-26 实测）
- 游戏：D:/Steam/steamapps/common/20MinuteTillDawn，buildid 12679152，Unity 2019.4.40f1，Mono。
- 加载器：Gale 1.23.2 管理的 BepInEx 5.4.19（core/BepInEx.dll 5.4.19.0）+ 0Harmony 2.9.0.0。
- profile：C:/Users/11/AppData/Roaming/com.kesomannen.gale/20-minutes-till-dawn/profiles/Default/BepInEx
- 反编译产物：ref/game（ilspycmd -p，618 个 .cs）。

## 关键调用链（ref/game 内路径:行号）
- 武器进化：flanne.Core/CombatState.cs:11 —— 等级 20 切 GunEvoMenuState。
- 进化数据：flanne/GunData.cs:49 gunEvolutions。
- 应用入口：flanne.PerkSystem/PlayerPerks.cs:35 Equip(GunEvolution) => Copy().Apply(player)。
- 进化内容：flanne/GunEvolution.cs:53 Apply() = statChanges + effects.Equip + 可选换枪。
- 爆炸免疫：flanne.PerkSystem.Actions/ExplosionImmunityAction.cs:9 RemoveVulnerability("HarmfulToPlayer")。
- 伤害判定：flanne/PlayerHealth.cs:127 Start() 重建 vulnerableTags；:213 CheckIfVulnerable(tag) 用 tag.Contains 匹配。
- 经验球：flanne.Pickups/XPPickup.cs:11 UsePickup -> PlayerXP.GainXP + XPPickupEvent。
- 拾取流程：flanne.Pickups/Pickup.cs:17 OnTriggerEnter2D -> PickupCR（0.3s tween -> UsePickup -> 回池）。

## 架构
    Plugin.cs                     装配：配置绑定、GameApi 校验、patch 注册、组件挂载
    Support/GameApi.cs            唯一的反射句柄解析点（缺失即报，不散落字符串）
    Features/GunEvolutionFeature.cs   进化发放（时机 + 幂等 + 逐条容错）
    Features/XpVacuumFeature.cs       定时吸经验（原生 PickupCR，保通知链）
    Features/DisplayFeature.cs        帧率/VSync/队列深度（配置热重载）

设计约束（按实际踩坑确定，不是装饰）：
- **时机必须晚于 PlayerHealth.Start**：PlayerHealth.Start 会重建 vulnerableTags 列表，早期应用（LoadoutInitializer.Start Postfix）会被覆盖 → Blast Shield 免疫失效。改挂 CombatState.Enter（所有 Start 已完成）。
- **幂等按 PlayerController.InstanceID**：CombatState.Enter 每次升级返回都会触发；GunEvolution 是跨局共享的 ScriptableObject，不能按它去重。
- **逐条 try/catch**：单条进化抛异常不得中断其余两条。
- **故障隔离**：patch 注册失败只记日志，真空与显示功能继续工作。
- 驱动级 Reflex / Anti-Lag 2 / XeLL 需要游戏内埋延迟标记点，BepInEx 插件无法实现；
  Unity 原生等效 = 帧率上限 + vSync + QualitySettings.maxQueuedFrames（渲染队列深度）。

## 构建 / 部署
    cd src/AllGunEvo && dotnet build -c Release
    cp bin/Release/AllGunEvo.dll <profile>/BepInEx/plugins/Local-AllGunEvo/

引用 profile core 的 BepInEx.dll / 0Harmony.dll + Managed 下的 Assembly-CSharp、UnityEngine 门面与模块、Unity.InputSystem、Unity.TextMeshPro、com.flanne.extensions（全部 Private=false）。
目标框架 netstandard2.0。

## 配置（BepInEx/config/local.20mtd.allgunevo.cfg，改后热生效）
- Evolutions.GiveAllOnRunStart = true
- XPVacuum.Enabled = true
- XPVacuum.IntervalSeconds = 30
- Display.Enabled = true
- Display.VSyncCount = 1        # 0=关（最低延迟，会撕裂）1=跟随刷新率 2-4=每 N 次刷新
- Display.TargetFrameRate = -1  # -1=不限；VSyncCount 非 0 时 Unity 忽略此项
- Display.MaxQueuedFrames = 3   # 1=最低延迟 3=Unity 默认

## 启动自检（LogOutput.log）
    ok CombatState.Enter
    ok PlayerPerks.Equip(GunEvolution)
    ok Pickup.PickupCR
    ok Pickup.pickUpCoroutine
    ok StateMachine.CurrentState
    ok GunData.gunEvolutions
    display game default: vSync=.. targetFrameRate=.. maxQueuedFrames=.. refresh=..Hz
    display applied: ...
    Applied 3/3 evolutions for gun <name>. Vulnerable tags: Enemy,EProjectile
    Vacuum collected N xp pickups (found M).

Blast Shield 生效的判据：Vulnerable tags 里不再包含 HarmfulToPlayer。

## 已知限制
- 20 级进化菜单仍会出现（再叠加同一条）。
- 一次吸上千球会同时起同等数量 0.3s tween，短时开销；若卡顿需改分批。
- 游戏更新改类名时，自检日志给出 missing game member。
