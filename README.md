# Cheat Trainer (14 in 1) — tModLoader 版

[English](README.en.md) | **简体中文**

这是把 **FLiNG《Terraria v1.4-v1.4.4.9 Plus 12 Trainer》** 的 12 项功能
完整复刻成的 tModLoader 模组，另外**额外加送第 13、14 项**自研功能：

- **第 13 项 · 剑类合成天顶剑**（任意天顶剑配方武器 → 直接合成天顶剑，原版 / 灾厄通用）
- **第 14 项 · 通讯类合成**（任意手机 / 贝壳电话配方材料 → 直接合成，两级打通）

原版修改器是「64 位原生 C++ 外壳 + 向 32 位 .NET 进程注入 x86 汇编」，
而 tModLoader 是 **.NET 8 / x64** 进程，那套注入架构无法照搬；
本模组改用 HookGen/MonoMod 在 **IL 层**实现，功能一一对应。

---

## 一、功能对照表

| # | 功能 | 默认热键 | 原版实现 | 本模组实现 |
|---|------|---------|---------|-----------|
| 1 | 无限生命 | 小键盘 1 | `statLife = 9999` | `PostUpdateMiscEffects` 里回满；`PreKill` 兜底防秒杀 |
| 2 | 无限魔法 | 小键盘 2 | `statMana = 9999` | `statMana = statManaMax2` |
| 3 | 无限氧气 | 小键盘 3 | `breath = breathMax` | 同左，并清空 `breathCD` |
| 4 | 无限飞行 | 小键盘 4 | `wingTime=10000, rocketTime=9999` | 同左，另外没翅膀时自动补 `wingsLogic` |
| 5 | 幽灵穿墙 | 小键盘 5 | `ghost = true` | 同左 |
| 6 | 无限物品 | 小键盘 6 | `stack = maxStack` | 每帧把背包物品补满堆叠 |
| 7 | 快速治疗 | 小键盘 7 | 把 `QuickHeal_GetItemToUse` 的 0.91/0.56 阈值改成 1.0 | 等价效果：没满血就自动喝恢复量最大的那瓶 |
| 8 | 全 亮 | 小键盘 8 | Hook `LightingEngine::UpdateLightDecay` | `Lighting.GlobalBrightness` 调高（强度可调） |
| 9 | 免材料合成 | 小键盘 9 | Hook `Recipe::CollectedEnoughItemsToCraftRecipeNew` | Hook `Terraria.On_Recipe.CollectedEnoughItemsToCraftRecipeNew` |
| 10 | 无视制作站 | 小键盘 0 | Hook `Recipe::PlayerMeetsTileRequirements` | Hook 同名方法 + `PlayerMeetsEnvironmentConditions` |
| 11 | 超级伤害 | 小键盘 * | `GetWeaponDamage` 返回 999999 | `ModifyWeaponDamage` 里 `damage.Flat += 999999` |
| 12 | 游戏变速 | 小键盘 / | 独立的 `TrSpeedHack_x86.dll`（变速齿轮） | Hook `Main.DoUpdate`，按倍率重复/跳过逻辑帧 |
| **13** | **剑类合成天顶剑** ★自研 | **`` ` ``（反引号）** | ——（原修改器无此项） | 运行时反查天顶剑配方，只放行判定、材料消耗交给游戏原流程 |
| **14** | **通讯类合成** ★自研 | **F5** | ——（原修改器无此项） | 运行时反查手机 + 贝壳电话两条配方（前缀匹配），同样只放行判定 |

另外按 **小键盘 .** 打开/关闭悬浮控制面板，可以直接点开关、拖滑条。

> 关于第 9、10 项：本机 tModLoader 里这三个方法的真实签名是
> `static bool CollectedEnoughItemsToCraftRecipeNew(Recipe)`、
> `static bool PlayerMeetsTileRequirements(Player, Recipe)`、
> `static bool PlayerMeetsEnvironmentConditions(Player, Recipe)`，
> 直接返回 `true` 就等于原版「把校验跳过去」的效果。

---

## 一·补、第 13 项详解：剑类合成天顶剑

### 需求

背包里只要有「天顶剑配方所需的**任意一件武器**」，天顶剑配方就亮起、直接可做，
缺的材料自动忽略；合成时那一件武器**照常被消耗** —— 铜短剑会「变成」天顶剑。
有几把符合条件的剑，就能合几把（铜短剑 + 星怒 = 能合 2 把）。

### 为什么必须动态查配方，不能写死 ID

天顶剑配方在不同环境下不一样：

| 环境 | 配方 |
|---|---|
| 原版 | 泰拉刃 + 喵喵刃 + 星光之怒 + 涌流波动剑 + 无头骑士之剑 + 种子弯刀 + 星怒 + 养蜂人 + 附魔剑 + 铜短剑（各 ×1） |
| 灾厄 | 上面这 10 件**全部保留**，**再加**圣金源锭 ×5 |
| 其它模组 | 「更好的天顶剑」之类还会再改一轮 |

所以 `TrainerZenithCraft` 一律**运行时反查**「产出物是天顶剑的那条配方到底要哪些材料」，
环境怎么改都不用动代码，也不会因为某个 ID 不存在而崩。

### 关键实现：只放行判定，不碰消耗

```csharp
// TrainerHooks.cs —— 复用第 9 项的那条 hook（它是"材料够不够"的总闸）
private bool HookFreeCraft(On_Recipe.orig_CollectedEnoughItemsToCraftRecipeNew orig, Recipe recipe)
{
    TrainerConfig cfg = TrainerConfig.Instance;
    if (cfg != null && cfg.FreeCraft) return true;          // 第 9 项：全都不要
    if (TrainerZenithCraft.ShouldUnlock(recipe)) return true; // 第 13 项：只放这一条
    return orig(recipe);
}
```

| 阶段 | 归谁管 | 效果 |
|---|---|---|
| **放行判定**（配方亮不亮） | 本模组 | 缺的材料在这里被「忽略」 |
| **材料消耗** | 游戏原本的流程 | 只吃掉玩家**真正拥有**的那几件 |

这就是「不会无限刷」的原因：我们只负责让按钮可点，不碰扣什么材料。
凑不出的材料游戏不会凭空扣，所以手上那把铜短剑照常被吃掉。

### 只认「武器」型材料

灾厄往配方里塞了圣金源锭（非武器）。如果按「手上任意一件材料」放行，
那手里只有锭也能合，逻辑就不对了。所以缓存时只收 `damage > 0` 的武器材料，
正好对应需求里的「任意剑类」。

### 兼容性结论

| 环境 | 结论 |
|---|---|
| 原版 | ✅ 铜短剑开局即可合 |
| 灾厄 | ✅ 功能生效；但**圣金源锭需打完叶罗尼才能挖到矿**，这是灾厄的终局门槛，本功能跳不过 |
| 其它改配方的模组 | ✅ 动态查配方，自动适配 |

热键 `` ` ``（OemTilde）：小键盘已被 12 项占满，而 `` ` `` 在 Terraria 默认键位里是空闲的。

---

## 一·补二、第 14 项详解：通讯类合成

### 需求

一次性打通**手机 → 贝壳电话**两级合成。只要背包里有**该配方自己**所需的
任意一件材料，那条配方就亮起、直接可做，缺的材料自动忽略。

### 为什么必须两级一起覆盖

```
手机     = PDA + 魔法镜子
贝壳电话 = 手机 + 魔法海螺 + 恶魔海螺
```

贝壳电话的配方里有「手机」，而手机本身又是一条复杂配方。
如果只放行贝壳电话这一条，玩家拿着魔法海螺也合不出贝壳电话
—— 因为缺手机，而手机同样合不出来。所以两条配方必须一起接管。

| 手里只有… | 能直接合出 |
|---|---|
| 魔法镜子 / PDA | 手机 |
| 手机 / 魔法海螺 / 恶魔海螺 | 贝壳电话（那一件会被消耗掉） |

### ⚠️ 材料集合必须按配方分开（这里踩过一个坑）

两条配方的引子材料**不能合并成一个总集合**，否则会出现「跨级误放行」：

| 做法 | 手上只有魔法镜子时 | 结果 |
|---|---|---|
| ❌ 合并成并集 | 手机配方亮 + **贝壳电话配方也亮** | 合一下白嫖一个贝壳电话 |
| ✅ 按配方分组 | 只有手机配方亮 | 符合预期 |

所以缓存结构是 `Dictionary<产物类型, HashSet<材料类型>>`，
判定时只查**这条配方自己**的那一组。

### 和天顶剑版的实现差异

| | 第 13 项（天顶剑） | 第 14 项（通讯类） |
|---|---|---|
| 目标配方 | 只有 `Zenith` 一条 | `CellPhone` + `Shellphone` 两条 |
| 材料过滤 | 只收 `damage > 0`（排除圣金源锭） | **不过滤**（材料全是工具，`dmg = -1`） |
| 材料作用域 | 单配方，无需分组 | **按产物分组**，防止跨级误放行 |
| 名称匹配 | 精确匹配产物的内部名 | `StartsWith` 前缀匹配，一次收齐贝壳电话 4 个变体 |

贝壳电话共有 **5 个物品 ID**（5358 家 / 5359 出生点 / 5360 海洋 / 5361 地狱，
外加 5437 占位符），逐个写 ID 太脆；改用「内部名以 `Shellphone` 开头」一次全收。

### 关键实现

```csharp
// TrainerHooks.cs —— 还是复用第 9 项那条「材料够不够」的总闸
private bool HookFreeCraft(On_Recipe.orig_CollectedEnoughItemsToCraftRecipeNew orig, Recipe recipe)
{
    TrainerConfig cfg = TrainerConfig.Instance;
    if (cfg != null && cfg.FreeCraft) return true;              // 第 9 项：全都不要
    if (TrainerZenithCraft.ShouldUnlock(recipe)) return true;   // 第 13 项
    if (TrainerShellphoneCraft.ShouldUnlock(recipe)) return true; // 第 14 项
    return orig(recipe);
}
```

同样的「只放行判定、材料消耗交给游戏原流程」原则 —— 所以拿 1 个魔法海螺，
它就只会「变成」1 个贝壳电话，不会无限刷。

热键 **F5**：F1~F4 被原版占用（`Loadout1/2/3`、`ToggleCameraMode`），
F5 在 Terraria 默认键位里是空闲的。

---

## 二、安装

`.tmod` 已经自动装好了，**不需要自己编译**：

```
C:\Users\<用户名>\Documents\My Games\Terraria\tModLoader-preview\Mods\CheatTrainer.tmod
```

启动 tModLoader → **模组管理** → 找到 `Cheat Trainer (14 in 1)` → 启用 → 重载模组。
（首次需要手动启用；`build.txt` 里没写 `side`，默认双端可加载。）

> 注意：这台机器用的是 `tModLoader-preview` 存档目录（Steam `tModLoader` 安装目录是
> `D:\SteamLibrary\steamapps\common\tModLoader`）。如果你的游戏实际读的是
> `My Games\Terraria\tModLoader\Mods`，把 `.tmod` 复制过去即可。

---

## 三、用法

1. 进游戏后按 **小键盘 .** 呼出面板，或者直接按小键盘 `1`~`9`、`0`、`*`、`/` 开功能
   （对应第 1~12 项，编号一一对应）；第 13 项「剑类合成天顶剑」在 `` ` `` 键（反引号），
   第 14 项「通讯类合成」在 **F5**。
2. 每个开关的状态会**自动保存**到模组配置里，下次进游戏保持上次的状态。
3. 热键想改的话：**设置 → 控制 → Cheat Trainer**，15 项（`Cheat0`~`Cheat13` + `TogglePanel`）都列在里面。
   悬浮面板会**实时显示实际绑定的键**（读 `ModKeybind.GetAssignedKeys`，不是写死的默认值），
   没绑定时显示「未绑定 / Unbound」。
4. 面板开着的时候不会误触游戏内的挖掘/使用物品，关掉面板就能正常操作。
5. 面板位置可以直接用鼠标拖动标题栏。

> ✅ **零键位冲突**：默认热键全部落在小键盘。查过本机 `input profiles.json`，
> 原版只占用了小键盘的 `Add`(+)=地图放大、`Subtract`(-)=地图缩小，
> 而 `NumPad0`~`NumPad9`、`Multiply`(*)、`Divide`(/)、`Decimal`(.) **全部空闲**，
> 模组刚好只用后者。第 13 项因为小键盘已满，改用 `` ` ``（OemTilde），
> 它在原版默认键位里同样是空的。第 14 项用 **F5** —— F1~F4 被原版占用
> （套装 1/2/3、切换相机），F5~F12 全部空闲，取最靠前的 F5。
>
> ⚠️ **唯一前提：NumLock 必须开启**，否则小键盘会退化成方向键区。
>
> 历史说明：早期版本默认用 `F1`~`F12`，但 F1/F2/F3 被原版绑给了装备套装 1/2/3、
> F4 是切换相机模式，会同时触发。现已全部移出 F 键区。

### 三个滑条

| 滑条 | 范围 | 默认 | 说明 |
|------|------|------|------|
| 游戏速度 | 0.1x ~ 10.0x | 2.5x | 对应原版的变速齿轮 |
| 超级伤害加成 | +1000 ~ +999999 | +999999 | 对应原版返回的 999999 |
| 全亮强度 | 1.0 ~ 5.0 | 3.0 | 1.0 = 原版亮度 |

---

## 四、自己重新编译

源码就在仓库根目录（即 `CheatTrainer/`）。tModLoader 自带 Roslyn，**不需要装 .NET SDK**：

```bash
cd /d/SteamLibrary/steamapps/common/tModLoader
./dotnet/dotnet.exe 'D:\SteamLibrary\steamapps\common\tModLoader\tModLoader.dll' \
    -server -build '<仓库根目录>' -unsafe -define 'JETBRAINS_ANNOTATIONS'
```

（`<仓库根目录>` 换成你克隆下来的 `CheatTrainer` 文件夹的绝对路径。）

编译成功后会自动打包并安装到 Mods 目录。

或者把 `CheatTrainer` 整个文件夹拷进
`My Games\Terraria\tModLoader-preview\ModSources\`，在游戏里点「构建 + 重载」。
（csproj 里的 `TModLoaderPath` 改成你自己的安装路径即可。）

---

## 五、中英双语

模组自带中文 / English 两套文字，覆盖**配置界面、悬浮面板、聊天提示、热键名**。

### 跟随游戏语言（默认）

文字默认跟随游戏语言。游戏是简体中文，全部显示中文。

### 模组内单独切换

**设置 → 模组配置 → Cheat Trainer (14 in 1) → 语言 → 界面语言**：

| 选项 | 说明 |
|---|---|
| `跟随游戏`（默认） | 用游戏的语言 |
| `English` | 强制英文 |
| `中文` | 强制中文 |

选中后**连配置界面自身**也会跟着变（改完要关掉配置页再打开一次）。

### 实现方式

文字放在 `Localization/` 下的 4 个 hjson 里。每个文件内同时含三份内容：

```
UI: { PanelTitle: ... }        ← 原生键，跟随游戏语言
LangEn: { UI: { PanelTitle: ... } }   ← 英文镜像
LangZh: { UI: { PanelTitle: ... } }   ← 中文镜像
```

这样**无论当前是哪种文化，中英两套文字都同时可读**，语言开关才能自由切换。

切换靠 Hook `Language.GetText`：当「界面语言」选了具体语言时，把 key 里的
`Mods.CheatTrainer.` 后面插入一段 `LangEn.` / `LangZh.` 再取一次。
之所以要拦在 `Language` 这一层，是因为**模组配置界面是 tModLoader 自己渲染的**
（走 `ConfigManager` → `Language.GetText`），不拦这里配置页就不会跟着变。

> 该 Hook 只对 `Mods.CheatTrainer.` 开头的 key 做处理，其余一律原样放行。
> 前缀判断放在最前面，所以对游戏本身的取词开销只是一次字符串比较。

四个 hjson 由 `_tools/gen_localization.py` 生成，改文案请改脚本再重新生成，
避免中英两边写岔。用法：

```bash
python _tools/gen_localization.py
```

---

## 六、已知限制

- **幽灵穿墙（小键盘 5）**：走的是原版 `Player.ghost` 机制，和原版修改器完全一致，
  穿墙期间不能攻击/使用物品，关掉就恢复。
- **游戏变速（小键盘 /）**：靠重复调用 `Main.DoUpdate` 实现，为了不卡死做了
  「一帧最多 10 步」的上限。低速（<1x）时画面会有明显顿感，这是慢动作的必然结果。
- **全亮（小键盘 8）**：调的是 `Lighting.GlobalBrightness`，如果觉得不够亮把滑条往右拉。
- **多人游戏**：这些是本地修改，会被服务器的反作弊/同步机制覆盖或踢出，
  建议只在**单人模式**使用。
- 第 6 项「无限物品」在多人环境下可能导致背包不同步，同样是单人用最佳。
- **第 13 项「剑类合成天顶剑」**：
  - 只认原版天顶剑配方里的 10 把武器（**锡短剑不行**，只有铜短剑行）
  - 默认仍需站在秘银砧 / 山铜砧旁；可在模组配置里关掉这个要求
  - **不跳过灾厄的 Boss 进度**：圣金源锭的材料能忽略，
    但灾厄「打完叶罗尼才能挖到圣金源矿」这个终局门槛跳不过
  - 记录到的配方缓存会在**换世界 / 重载模组**时自动失效重建
- **第 14 项「通讯类合成」**：
  - 两级一起覆盖：手机（PDA + 镜子）与贝壳电话（手机 + 两个海螺）
  - 默认仍需工匠作坊；可在模组配置里关掉这个要求
  - 贝壳电话的 4 个变体（家 / 出生点 / 海洋 / 地狱）一并覆盖
  - 拿一个魔法海螺按 F5，它会「变成」一个贝壳电话（手机、恶魔海螺自动忽略）
  - 同样只放行判定，材料消耗归游戏原流程 —— 不会无限刷
  - 配方缓存在**换世界 / 重载模组**时自动失效重建

---

## 七、许可与免责

### 许可

本项目采用 **MIT License**，详见 [LICENSE](LICENSE)。你可以自由使用、修改、分发。

### 与 FLiNG 修改器的关系

本模组**是独立重写的实现，不包含任何第三方修改器的代码或二进制文件**：

| | FLiNG 修改器 | 本模组 |
|---|---|---|
| 形态 | 独立 `.exe` + 往游戏进程注入 32 位 DLL | 纯 tModLoader 模组（.NET 8 / x64） |
| 技术 | 命名管道 + x86 汇编注入 | Harmony/HookGen IL 层 Hook |
| 授权 | 第三方版权作品 | 本仓库原创，MIT |

功能列表参考了公开的修改器功能说明，但**代码、资源、二进制全部为本仓库原创**，
未打包或再分发任何第三方文件。

> **关于名字**：本项目**刻意不叫** "Terraria Trainer" —— 那是 FLiNG 的产品名。
> 取名 **Cheat Trainer** 有三个考虑：① 与 FLiNG 的产品明确区分，避免被误认为官方发布或转载；
> ② 符合 tModLoader 作弊模组的命名惯例（Cheat Sheet、Cheat Hotkeys…）；
> ③ "Trainer" 原本是**外部 exe 修改器**的用词，而这是个原生 tModLoader 模组。

### 免责

- 这是**作弊模组**，仅供**单人模式**自娱自乐。多人环境使用可能导致与服务器不同步或被踢出。
- 使用作弊功能会**显著降低游戏体验**，并可能让你的存档「过于轻松」。
- 请自行评估风险。作者不对因使用本模组造成的任何存档损坏或损失负责。

### 致谢

- [tModLoader](https://github.com/tModLoader/tModLoader) —— 模组加载器与本模组的运行环境
- [Re-Logic](https://terraria.org/) —— 《泰拉瑞亚》的开发者

