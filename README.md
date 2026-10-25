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

## 关键调用链（ref/game 内路径:行号）
- 武器进化：flanne.Core/CombatState.cs:11 —— 等级 20 切 GunEvoMenuState。
- 进化数据：flanne/GunData.cs:49 gunEvolutions。
- 应用入口：flanne.PerkSystem/PlayerPerks.cs:35 Equip(GunEvolution) => Copy().Apply(player)。
- 进化内容：flanne/GunEvolution.cs:53 Apply() = statChanges + effects.Equip + 可选换枪。
- 爆炸免疫：flanne.PerkSystem.Actions/ExplosionImmunityAction.cs:9 RemoveVulnerability("HarmfulToPlayer")。
- 伤害判定：flanne/PlayerHealth.cs:127 Start() 重建 vulnerableTags；:213 CheckIfVulnerable 用 tag.Contains 匹配。
- 经验球：flanne.Pickups/XPPickup.cs:11 UsePickup -> PlayerXP.GainXP + XPPickupEvent。
- 拾取流程：flanne.Pickups/Pickup.cs:17 OnTriggerEnter2D -> PickupCR（0.3s tween -> UsePickup -> 回池）。
- 交换链重建：flanne.UI/OptionsSetter.cs:166 SetFullscreen / :179 SetResolution —— 游戏唯一重建交换链处。
- 选项面板刷新：flanne.UI/OptionsSetter.cs:62 Start -> Refresh（启动时就会重建一次交换链）。

## 架构
    Plugin.cs                          装配：配置绑定、GameApi 校验、patch 注册、组件挂载
    Support/GameApi.cs                 唯一的反射句柄解析点（缺失即报，不散落字符串）
    Features/GunEvolutionFeature.cs    进化发放（生命周期锚点 + 幂等 + 逐条容错）
    Features/XpVacuumFeature.cs        定时吸经验（原生 PickupCR，保通知链）
    Features/DisplayFeature.cs         帧率/VSync/队列深度 + 漂移自愈 + 交换链重建挂钩

设计约束（按实际踩坑确定，不是装饰）：
- **时机必须晚于 PlayerHealth.Start**：PlayerHealth.Start 会重建 vulnerableTags 列表，
  早期应用（LoadoutInitializer.Start Postfix）会被覆盖 → Blast Shield 免疫失效。
  改挂 CombatState.Enter（此时场景内所有 Start 必已完成）。
- **幂等按 PlayerController.InstanceID**：CombatState.Enter 每次升级返回都会触发；
  GunEvolution 是跨局共享的 ScriptableObject，不能按它去重。
- **全局可变状态必须自愈**：QualitySettings 是全局状态，Screen.SetResolution 重建交换链时
  Unity 会重置 vSyncCount。写入型配置一律可重复执行，并在 场景加载 / 选项面板操作 / 每秒漂移检测 时重放。
- **逐条 try/catch + patch 注册隔离**：单条进化或单个 patch 失败不影响其余功能。

框架适配选择的判断：
- 发进化 = 数据驱动（读游戏自己的 gunEvolutions）+ 游戏公开 API（PlayerPerks.Equip）+ Harmony 定位时机。
  唯一非 native 的是"所有 Start 完成后"这个锚点——Unity 没有对应公开事件。
- 吸经验 = 纯 Unity 组件组合（MonoBehaviour + Update 计时），最贴合引擎。
- 显示设置 = 无扩展点（游戏代码从不碰 QualitySettings），只能写入 + 自愈。
- 未采用：伪造 ScriptableObject（反射写私有序列化字段，脆弱）、HookGen On.*（private 覆盖不到、版本耦合）、
  全量 IL 重写、DI 容器/ECS（对 5 个功能点是过度设计）。
- 驱动级 Reflex / Anti-Lag 2 / XeLL 需要游戏内埋延迟标记点，BepInEx 插件无法实现。

## 构建 / 部署
    cd src/AllGunEvo && dotnet build -c Release
    cp bin/Release/AllGunEvo.dll <profile>/BepInEx/plugins/Local-AllGunEvo/

引用 profile core 的 BepInEx.dll / 0Harmony.dll + Managed 下的 Assembly-CSharp、UnityEngine 门面与模块、Unity.InputSystem、Unity.TextMeshPro、com.flanne.extensions（全部 Private=false）。目标框架 netstandard2.0。

## 配置（BepInEx/config/local.20mtd.allgunevo.cfg，改后热生效）
- Evolutions.GiveAllOnRunStart = true
- XPVacuum.Enabled = true
- XPVacuum.IntervalSeconds = 30
- Display.Enabled = true
- Display.VSyncCount = 1        # 0=关（最低延迟，会撕裂）1=跟随刷新率 2-4=每 N 次刷新
- Display.TargetFrameRate = -1  # -1=不限；VSyncCount 非 0 时 Unity 忽略此项
- Display.MaxQueuedFrames = 3   # 1=最低延迟 3=Unity 默认
- Display.Verbose = false       # true 时每秒打印实际值，用于诊断

## 启动自检（LogOutput.log）
    ok CombatState.Enter
    ok PlayerPerks.Equip(GunEvolution)
    ok Pickup.PickupCR
    ok Pickup.pickUpCoroutine
    ok StateMachine.CurrentState
    ok GunData.gunEvolutions
    display game default: vSync=.. targetFrameRate=.. maxQueuedFrames=.. refresh=..Hz fullscreen=..
    display applied (startup): ...
    Applied 3/3 evolutions for gun <name>. Vulnerable tags: Enemy,EProjectile
    Vacuum collected N xp pickups (found M).

诊断要点：
- Blast Shield 生效判据 = Vulnerable tags 不含 HarmfulToPlayer。
- vSync 生效判据 = 没有 "drift corrected" / "options fullscreen" 反复出现，且 Display.Verbose 打出的值稳定为配置值。
- 若日志显示值正确但画面仍撕裂，则是 GPU 驱动强制关闭了垂直同步（AMD/NVIDIA 控制面板的"等待垂直刷新"设置），插件无法覆盖。

## 已知限制
- 20 级进化菜单仍会出现（再叠加同一条）。
- 一次吸上千球会同时起同等数量 0.3s tween，短时开销；若卡顿需改分批。
- 游戏更新改类名时，自检日志给出 missing game member。
