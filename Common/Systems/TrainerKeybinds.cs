using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameInput;
using Terraria.ModLoader;
using CheatTrainer.Common.Configs;

namespace CheatTrainer.Common.Systems
{
    /// <summary>
    /// 热键注册与开关处理。
    ///
    /// 默认全部落在**小键盘**上，因为原版 Terraria 把小键盘数字区完全留空
    /// （经查 input profiles.json：只有 Add / Subtract 被地图缩放占用，
    /// 而 NumPad0~9、Multiply、Divide、Decimal 都是空闲的）。
    /// 好处是不用改动游戏自带键位，装完即用、零冲突。
    ///
    ///   小键盘 1~9 → 功能 1~9     小键盘 0 → 功能 10
    ///   *（乘）    → 功能 11       /（除）  → 功能 12
    ///   .（小数点）→ 开关悬浮面板
    ///
    ///   功能 13「剑类合成天顶剑」默认落在 ` 键（反引号，OemTilde）——
    ///   小键盘已经被 12 个功能占满，而 ` 在 Terraria 默认键位里是空闲的。
    ///   功能 14「通讯类合成」默认落在 F5 —— F1~F4 被原版占用（装备套装 / 相机），
    ///   而 F5~F12 是空闲的。
    ///
    /// 全部可以在「设置 → 控制 → Cheat Trainer」里重新绑定。
    ///
    /// ⚠️ 小键盘数字键需要 **NumLock 处于开启状态**，否则会变成方向键。
    ///
    /// 功能的显示名不写死在这里，而是记一个本地化键，
    /// 运行时由 TrainerLang 按当前语言取出——这样面板、聊天提示、
    /// 配置界面三处的叫法永远一致，且能中英切换。
    /// </summary>
    public class TrainerKeybinds : ModSystem
    {
        public const int CheatCount = 14;

        /// <summary>本地化键 / 配置读写 / 默认热键。</summary>
        public static readonly (string LocaleKey, Func<TrainerConfig, bool> Get, Action<TrainerConfig, bool> Set, Keys DefaultKey)[] Cheats = {
            ("Configs.TrainerConfig.InfiniteHealth.Label", c => c.InfiniteHealth, (c, v) => c.InfiniteHealth = v, Keys.NumPad1),
            ("Configs.TrainerConfig.InfiniteMana.Label", c => c.InfiniteMana, (c, v) => c.InfiniteMana = v, Keys.NumPad2),
            ("Configs.TrainerConfig.InfiniteBreath.Label", c => c.InfiniteBreath, (c, v) => c.InfiniteBreath = v, Keys.NumPad3),
            ("Configs.TrainerConfig.InfiniteFlight.Label", c => c.InfiniteFlight, (c, v) => c.InfiniteFlight = v, Keys.NumPad4),
            ("Configs.TrainerConfig.Noclip.Label", c => c.Noclip, (c, v) => c.Noclip = v, Keys.NumPad5),
            ("Configs.TrainerConfig.InfiniteItems.Label", c => c.InfiniteItems, (c, v) => c.InfiniteItems = v, Keys.NumPad6),
            ("Configs.TrainerConfig.QuickHeal.Label", c => c.QuickHeal, (c, v) => c.QuickHeal = v, Keys.NumPad7),
            ("Configs.TrainerConfig.FullBright.Label", c => c.FullBright, (c, v) => c.FullBright = v, Keys.NumPad8),
            ("Configs.TrainerConfig.FreeCraft.Label", c => c.FreeCraft, (c, v) => c.FreeCraft = v, Keys.NumPad9),
            ("Configs.TrainerConfig.IgnoreCraftingStations.Label", c => c.IgnoreCraftingStations, (c, v) => c.IgnoreCraftingStations = v, Keys.NumPad0),
            ("Configs.TrainerConfig.SuperDamage.Label", c => c.SuperDamage, (c, v) => c.SuperDamage = v, Keys.Multiply),
            ("Configs.TrainerConfig.SpeedHack.Label", c => c.SpeedHack, (c, v) => c.SpeedHack = v, Keys.Divide),

            // 13. 剑类合成天顶剑。小键盘已满，` 键在游戏里是空闲的。
            ("Configs.TrainerConfig.SwordToZenith.Label", c => c.SwordToZenith, (c, v) => c.SwordToZenith = v, Keys.OemTilde),

            // 14. 通讯类合成（手机 + 贝壳电话）。F5 空闲（F1~F4 被原版占用）。
            ("Configs.TrainerConfig.ShellphoneCraft.Label", c => c.ShellphoneCraft, (c, v) => c.ShellphoneCraft = v, Keys.F5),
        };

        public static ModKeybind[] CheatKeys;
        public static ModKeybind TogglePanelKey;

        public override void Load()
        {
            if (Main.dedServ) {
                return;
            }

            CheatKeys = new ModKeybind[CheatCount];
            for (int i = 0; i < CheatCount; i++) {
                CheatKeys[i] = KeybindLoader.RegisterKeybind(Mod, "Cheat" + i, Cheats[i].DefaultKey);
            }

            TogglePanelKey = KeybindLoader.RegisterKeybind(Mod, "TogglePanel", Keys.Decimal);
        }

        public override void Unload()
        {
            CheatKeys = null;
            TogglePanelKey = null;
        }

        public override void PostUpdateInput()
        {
            if (Main.dedServ || Main.gameMenu) {
                return;
            }

            // 打字 / 编辑告示牌 / 开箱子的时候不要误触
            if (Main.drawingPlayerChat || Main.editSign || Main.editChest) {
                return;
            }

            if (CheatKeys != null) {
                for (int i = 0; i < CheatKeys.Length; i++) {
                    ModKeybind key = CheatKeys[i];
                    if (key != null && key.JustPressed) {
                        Toggle(i);
                    }
                }
            }

            if (TogglePanelKey != null && TogglePanelKey.JustPressed) {
                UI.TrainerUISystem.TogglePanel();
            }
        }

        /// <summary>翻转第 index 项功能，并在左下角提示当前状态。</summary>
        public static void Toggle(int index)
        {
            if (index < 0 || index >= Cheats.Length) {
                return;
            }

            TrainerConfig cfg = TrainerConfig.Instance;
            if (cfg == null) {
                return;
            }

            (string localeKey, Func<TrainerConfig, bool> get, Action<TrainerConfig, bool> set, _) = Cheats[index];

            bool value = !get(cfg);
            set(cfg, value);
            Notify(localeKey, value);
        }

        public static bool IsOn(int index)
        {
            TrainerConfig cfg = TrainerConfig.Instance;
            return cfg != null && Cheats[index].Get(cfg);
        }

        /// <summary>功能名（按当前语言）。</summary>
        public static string NameOf(int index)
        {
            if (index < 0 || index >= Cheats.Length) {
                return string.Empty;
            }

            return TrainerLang.Get(Cheats[index].LocaleKey);
        }

        /// <summary>第 index 项功能**实际绑定**的按键文字；没绑定则提示未绑定。</summary>
        public static string KeyLabel(int index)
        {
            if (index < 0 || index >= Cheats.Length) {
                return string.Empty;
            }

            ModKeybind bind = CheatKeys != null && index < CheatKeys.Length ? CheatKeys[index] : null;
            if (bind != null) {
                return KeyLabelOf(bind);
            }

            // 还没注册（理论上不会走到）时退回默认键的友好名
            return PrettyKey(Cheats[index].DefaultKey.ToString());
        }

        /// <summary>面板开关按键的文字。</summary>
        public static string TogglePanelLabel()
        {
            return TogglePanelKey == null ? PrettyKey(Keys.Decimal.ToString()) : KeyLabelOf(TogglePanelKey);
        }

        /// <summary>取某个热键当前的绑定文字，多个绑定用 / 连接；没绑定时给本地化的「未绑定」。</summary>
        public static string KeyLabelOf(ModKeybind bind)
        {
            List<string> keys = bind.GetAssignedKeys(InputMode.Keyboard);
            if (keys == null || keys.Count == 0) {
                return TrainerLang.Get("UI.Unbound");
            }

            string[] pretty = new string[keys.Count];
            for (int i = 0; i < keys.Count; i++) {
                pretty[i] = PrettyKey(keys[i]);
            }

            return string.Join(" / ", pretty);
        }

        /// <summary>
        /// 把 XNA 的枚举名换成玩家看得懂的样子。
        /// 原样显示的话，小键盘会变成 "NumPad1"、"Multiply"、"Decimal" 这种，
        /// 对中文玩家很不友好。
        /// </summary>
        public static string PrettyKey(string raw)
        {
            if (string.IsNullOrEmpty(raw)) {
                return raw;
            }

            // NumPad0~9 → 小键盘 0~9
            if (raw.Length == 7 && raw.StartsWith("NumPad", StringComparison.Ordinal)) {
                return TrainerLang.Get("UI.NumPadPrefix") + raw[6];
            }

            switch (raw) {
                case "Multiply":
                    return TrainerLang.Get("UI.NumPadPrefix") + "*";
                case "Divide":
                    return TrainerLang.Get("UI.NumPadPrefix") + "/";
                case "Decimal":
                    return TrainerLang.Get("UI.NumPadPrefix") + ".";
                case "Subtract":
                    return TrainerLang.Get("UI.NumPadPrefix") + "-";
                case "Add":
                    return TrainerLang.Get("UI.NumPadPrefix") + "+";
                case "OemTilde":
                    return "`";
                default:
                    return raw;
            }
        }

        public static void Notify(string localeKey, bool on)
        {
            Main.NewText(
                TrainerLang.Get("UI.Prefix") + TrainerLang.Get(localeKey) + "  "
                    + TrainerLang.Get(on ? "UI.On" : "UI.Off"),
                on ? new Color(120, 220, 120) : new Color(225, 130, 130));
        }
    }
}
