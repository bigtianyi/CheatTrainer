# -*- coding: utf-8 -*-
"""
生成 CheatTrainer 的双语本地化文件。

用法（在仓库任意位置）：
    python _tools/gen_localization.py

输出到本脚本上一级的 Localization/ 目录，即仓库根下的：
    Localization/en-US_Mods.CheatTrainer.hjson
    Localization/zh-Hans_Mods.CheatTrainer.hjson
    Localization/en-US_Mods.CheatTrainer.Configs.hjson
    Localization/zh-Hans_Mods.CheatTrainer.Configs.hjson

每个文件内部同时包含三份内容：
    1. 原生键（跟随游戏语言时使用）
    2. LangEn.* 英文镜像
    3. LangZh.* 中文镜像
这样运行时无论当前是哪种文化，都能同时读到中英两套文字，
模组内的语言开关才能自由切换（见 Common/Systems/TrainerLang.cs）。
"""
import io
import os
import sys

MOD = "CheatTrainer"
# 脚本位于 <repo>/_tools/ 下，本地化文件位于 <repo>/Localization/。
OUT_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Localization")

# ---------------------------------------------------------------- 数据源
# (属性名, 中文, 英文, 中文提示, 英文提示)
CHEATS = [
    ("InfiniteHealth", "无限生命", "Infinite Health",
     "血量始终保持满值，受到任何伤害都不会死亡。", "Keeps your health full so you cannot die."),
    ("InfiniteMana", "无限魔法", "Infinite Mana",
     "魔法值始终保持满值。", "Keeps your mana full."),
    ("InfiniteBreath", "无限氧气", "Infinite Breath",
     "在水下不会窒息。", "Prevents drowning underwater."),
    ("InfiniteFlight", "无限飞行", "Infinite Flight",
     "可以一直飞行，没有翅膀也能飞。", "Fly indefinitely, even without wings equipped."),
    ("Noclip", "幽灵穿墙", "Noclip",
     "变成幽灵状态，可以穿过任何方块（穿墙期间不能攻击）。", "Turn into a ghost and pass through blocks (you cannot attack while active)."),
    ("InfiniteItems", "无限物品", "Infinite Items",
     "背包里所有可堆叠物品始终保持满堆叠。", "Keeps every stackable item in your inventory at max stack."),
    ("QuickHeal", "快速治疗", "Quick Heal",
     "血量不满时自动喝下背包里恢复量最大的药水。", "Automatically drinks your strongest healing potion when hurt."),
    ("FullBright", "全亮", "Full Bright",
     "提高全局亮度，地下和夜晚都能看清。", "Raises global brightness so caves and night are visible."),
    ("FreeCraft", "免材料合成", "Free Craft",
     "合成物品不需要任何材料。", "Crafting consumes no materials."),
    ("IgnoreCraftingStations", "无视制作站", "Ignore Crafting Stations",
     "不需要靠近工作台等制作站就能合成。", "Craft anywhere without being near a crafting station."),
    ("SuperDamage", "超级伤害", "Super Damage",
     "大幅提高武器伤害，可以秒杀大部分敌人。", "Greatly increases weapon damage."),
    ("SpeedHack", "游戏变速", "Game Speed Hack",
     "调节游戏运行速度（配合「游戏速度倍率」滑条）。", "Changes how fast the game runs (see the Game Speed Multiplier slider)."),
    ("SwordToZenith", "剑类合成天顶剑", "Sword to Zenith",
     "背包里有天顶剑配方所需的任意一件武器时，天顶剑就会解锁，缺的材料自动忽略；合成时那一件武器照常消耗，相当于它「变成」了天顶剑。有几把就能合几把。原版与灾厄通用。", "If you carry any weapon used in the Zenith recipe, Zenith becomes craftable and missing materials are ignored. That weapon is still consumed, so it effectively becomes the Zenith. One craft per weapon you hold. Works in vanilla and Calamity."),
    ("ShellphoneCraft", "通讯类合成", "Shellphone Crafting",
     "背包里有「手机」或「贝壳电话」配方所需的任意一件材料时，对应配方就会解锁，缺的材料自动忽略；合成时那一件材料照常消耗，相当于它「变成」了成品。一次覆盖手机与贝壳电话两级，从海螺可以一路合到贝壳电话。原版与灾厄通用。", "If you carry any ingredient used in the Cell Phone or Shellphone recipe, that recipe becomes craftable and missing materials are ignored. That ingredient is still consumed, so it effectively becomes the result. Covers both tiers, so you can craft all the way from a conch to a Shellphone. Works in vanilla and Calamity."),
]

VALUES = [
    ("GameSpeed", "游戏速度倍率", "Game Speed Multiplier",
     "0.1x 到 10.0x，默认 2.5x。只在「游戏变速」开启时生效。", "0.1x to 10.0x, default 2.5x. Only applies while Game Speed Hack is on."),
    ("DamageBonus", "超级伤害加成", "Super Damage Bonus",
     "附加到武器上的固定伤害值。只在「超级伤害」开启时生效。", "Flat damage added to your weapons. Only applies while Super Damage is on."),
    ("Brightness", "全亮强度", "Brightness Strength",
     "1.0 为原版亮度，越大越亮。只在「全亮」开启时生效。", "1.0 is vanilla brightness; higher is brighter. Only applies while Full Bright is on."),
    ("ZenithIgnoreStation", "合成天顶剑免除制作站", "Zenith Needs No Station",
     "开启后，合成天顶剑连秘银砧都不需要。只在「剑类合成天顶剑」开启时生效。", "Crafting the Zenith no longer requires an anvil. Only applies while Sword to Zenith is on."),
    ("ShellphoneIgnoreStation", "合成通讯类免除制作站", "Shellphone Needs No Station",
     "开启后，合成手机 / 贝壳电话连工匠作坊都不需要。只在「通讯类合成」开启时生效。", "Crafting the Cell Phone or Shellphone no longer requires a Tinkerer's Workshop. Only applies while Shellphone Crafting is on."),
    ("Language", "界面语言", "UI Language",
     "控制这个模组使用哪种语言文字。", "Which language this mod uses for its own text."),
]

HEADERS = [
    ("Cheats", "功能开关", "Cheats"),
    ("Values", "数值调节", "Values"),
    ("ZenithCraft", "天顶剑合成", "Zenith Crafting"),
    ("ShellphoneCraft", "通讯类合成", "Shellphone Crafting"),
    ("Language", "语言", "Language"),
]

LANGUAGE_ENUM = [
    ("Auto", "跟随游戏", "Follow Game"),
    ("English", "English", "English"),
    ("Chinese", "中文", "中文"),
]

# 注意：ModConfig 的 DisplayName 会**整个替换**配置页标题
# （默认是 "{模组显示名}: {配置类名}"），所以这里要把模组名带回来。
CONFIG_DISPLAY = ("Cheat Trainer (14 in 1) · 修改器设置",
                  "Cheat Trainer (14 in 1) · Trainer Settings")

UI = [
    ("PanelTitle", "Cheat Trainer 14 合 1", "Cheat Trainer (14 in 1)"),
    ("Hint", "按小键盘 . 开关面板 · 热键见 设置 → 控制", "Press NumPad . to toggle · Hotkeys: Settings > Controls"),
    ("Footer", "点击文字即可开关（开关状态会自动保存）", "Click a line to toggle (state is saved automatically)"),
    ("SpeedLabel", "游戏速度", "Game Speed"),
    ("DamageLabel", "超级伤害加成", "Damage Bonus"),
    ("BrightLabel", "全亮强度", "Brightness"),
    ("StateOn", "开", "ON"),
    ("StateOff", "关", "OFF"),
    ("On", "开启", "ON"),
    ("Off", "关闭", "OFF"),
    ("Unbound", "未绑定", "Unbound"),
    ("NumPadPrefix", "小键盘 ", "NumPad "),
    ("Prefix", "[Trainer] ", "[Trainer] "),
]

TOGGLE_PANEL_KEYBIND = ("呼出 / 收起面板", "Toggle Control Panel")


# ---------------------------------------------------------------- 工具
def q(s):
    """hjson 字符串转义（一律加双引号）。"""
    return '"' + s.replace('\\', '\\\\').replace('"', '\\"') + '"'


def ind(level):
    return "\t" * level


def emit_configs_block(lang, lvl=1):
    """生成一份 Configs 内容（lang = 'zh' / 'en'）。lvl = 顶层键的缩进层级。"""
    L = []
    zh = lang == "zh"
    t, t1, t2 = lvl, lvl + 1, lvl + 2

    L.append(ind(t) + "TrainerConfig: {")
    L.append(ind(t1) + f"DisplayName: {q(CONFIG_DISPLAY[0] if zh else CONFIG_DISPLAY[1])}")
    L.append("")
    L.append(ind(t1) + "Headers: {")
    for key, z, e in HEADERS:
        L.append(ind(t2) + f"{key}: {q(z if zh else e)}")
    L.append(ind(t1) + "}")
    for name, z, e, zt, et in CHEATS:
        L.append("")
        L.append(ind(t1) + f"{name}: {{")
        L.append(ind(t2) + f"Label: {q(z if zh else e)}")
        L.append(ind(t2) + f"Tooltip: {q(zt if zh else et)}")
        L.append(ind(t1) + "}")

    # 「数值调节」里的滑条（Language 与两个 IgnoreStation 各自归到别的分区）
    for name, z, e, zt, et in VALUES:
        if name in ("Language", "ZenithIgnoreStation", "ShellphoneIgnoreStation"):
            continue
        L.append("")
        L.append(ind(t1) + f"{name}: {{")
        L.append(ind(t2) + f"Label: {q(z if zh else e)}")
        L.append(ind(t2) + f"Tooltip: {q(zt if zh else et)}")
        L.append(ind(t1) + "}")

    # 天顶剑合成分区：合成天顶剑免除制作站
    zen = next(v for v in VALUES if v[0] == "ZenithIgnoreStation")
    L.append("")
    L.append(ind(t1) + "ZenithIgnoreStation: {")
    L.append(ind(t2) + f"Label: {q(zen[1] if zh else zen[2])}")
    L.append(ind(t2) + f"Tooltip: {q(zen[3] if zh else zen[4])}")
    L.append(ind(t1) + "}")

    # 通讯类合成分区：合成手机 / 贝壳电话免除制作站
    sh = next(v for v in VALUES if v[0] == "ShellphoneIgnoreStation")
    L.append("")
    L.append(ind(t1) + "ShellphoneIgnoreStation: {")
    L.append(ind(t2) + f"Label: {q(sh[1] if zh else sh[2])}")
    L.append(ind(t2) + f"Tooltip: {q(sh[3] if zh else sh[4])}")
    L.append(ind(t1) + "}")

    lang = next(v for v in VALUES if v[0] == "Language")
    L.append("")
    L.append(ind(t1) + "Language: {")
    L.append(ind(t2) + f"Label: {q(lang[1] if zh else lang[2])}")
    L.append(ind(t2) + f"Tooltip: {q(lang[3] if zh else lang[4])}")
    L.append(ind(t1) + "}")
    L.append(ind(t) + "}")
    L.append("")

    # 枚举成员
    L.append(ind(t) + "TrainerLanguage: {")
    for name, z, e in LANGUAGE_ENUM:
        L.append(ind(t1) + f"{name}: {{")
        L.append(ind(t2) + f"Label: {q(z if zh else e)}")
        L.append(ind(t2) + 'Tooltip: ""')
        L.append(ind(t1) + "}")
    L.append(ind(t) + "}")
    return "\n".join(L)


def emit_main_block(lang, lvl=0):
    """生成一份主文件内容（UI + Keybinds）。lvl = 顶层键的缩进层级。"""
    zh = lang == "zh"
    t, t1, t2 = lvl, lvl + 1, lvl + 2
    L = []
    L.append(ind(t) + "UI: {")
    for key, z, e in UI:
        L.append(ind(t1) + f"{key}: {q(z if zh else e)}")
    L.append(ind(t) + "}")
    L.append("")
    L.append(ind(t) + "Keybinds: {")
    for i, (name, z, e, _, _) in enumerate(CHEATS):
        L.append(ind(t1) + f"Cheat{i}: {{")
        L.append(ind(t2) + f"DisplayName: {q(z if zh else e)}")
        L.append(ind(t1) + "}")
    L.append(ind(t1) + "TogglePanel: {")
    L.append(ind(t2) + f"DisplayName: {q(TOGGLE_PANEL_KEYBIND[0] if zh else TOGGLE_PANEL_KEYBIND[1])}")
    L.append(ind(t1) + "}")
    L.append(ind(t) + "}")
    return "\n".join(L)


def write(path, text):
    io.open(path, "w", encoding="utf-8", newline="\n").write(text)
    print(f"  wrote {os.path.relpath(path)}  ({len(text)} bytes)")


def main():
    os.makedirs(OUT_DIR, exist_ok=True)
    print(f"输出目录: {os.path.normpath(OUT_DIR)}")

    for culture, native in (("zh-Hans", "zh"), ("en-US", "en")):
        # ---- Configs 文件（键前缀 Mods.CheatTrainer.Configs.）----
        body = []
        body.append("# 本文件由 _tools/gen_localization.py 生成，请勿手改。")
        body.append("# 键前缀：Mods.CheatTrainer.Configs.")
        body.append(f"# 语言：{culture}")
        body.append("")
        body.append(emit_configs_block(native, lvl=0))
        body.append("")
        body.append("# ---- 双语镜像：供模组内的语言开关切换使用 ----")
        body.append("LangZh: {")
        body.append(emit_configs_block("zh", lvl=1))
        body.append("}")
        body.append("")
        body.append("LangEn: {")
        body.append(emit_configs_block("en", lvl=1))
        body.append("}")
        body.append("")
        write(os.path.join(OUT_DIR, f"{culture}_Mods.{MOD}.Configs.hjson"), "\n".join(body))

        # ---- 主文件（键前缀 Mods.CheatTrainer.）----
        body = []
        body.append("# 本文件由 _tools/gen_localization.py 生成，请勿手改。")
        body.append(f"# 键前缀：Mods.{MOD}.")
        body.append(f"# 语言：{culture}")
        body.append("")
        body.append(emit_main_block(native, lvl=0))
        body.append("")
        body.append("# ---- 双语镜像：供模组内的语言开关切换使用 ----")
        body.append("LangZh: {")
        body.append(emit_main_block("zh", lvl=1))
        body.append("}")
        body.append("")
        body.append("LangEn: {")
        body.append(emit_main_block("en", lvl=1))
        body.append("}")
        body.append("")
        write(os.path.join(OUT_DIR, f"{culture}_Mods.{MOD}.hjson"), "\n".join(body))

    print("完成。")


if __name__ == "__main__":
    main()
