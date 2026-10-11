using System;
using Terraria.Localization;
using Terraria.ModLoader;
using CheatTrainer.Common.Configs;

namespace CheatTrainer.Common.Systems
{
    /// <summary>
    /// 模组文字的取词入口，支持中英双语切换。
    ///
    /// 字符串全部放在 Localization/*.hjson 里，每个文件内同时含三份：
    ///   ① 原生键              —— 跟随游戏语言（默认行为）
    ///   ② LangEn.*  英文镜像
    ///   ③ LangZh.*  中文镜像
    ///
    /// 「界面语言」选「跟随游戏」时直接用原生键；
    /// 选了具体语言时，把 key 里的 "Mods.CheatTrainer." 后面插一段
    /// LangEn. / LangZh. 再取一次。
    ///
    /// 为什么要 Hook Language.GetText，而不是只在自己代码里判断？
    /// 因为模组配置界面（游戏内那个设置页）是 tModLoader 自己渲染的，
    /// 它走 ConfigManager → Language.GetText。只有拦在 Language 这一层，
    /// 配置页的文字才会跟着语言开关一起变。
    /// 非本模组的 key 一律原样放行，不影响游戏本身。
    /// </summary>
    public static class TrainerLang
    {
        public const string ModPrefix = "Mods.CheatTrainer.";
        private const string TagEnglish = "LangEn";
        private const string TagChinese = "LangZh";

        /// <summary>配置实例缓存。Language.GetText 调用极频繁，不能每次都去查一遍。</summary>
        private static TrainerConfig cachedConfig;

        public static void Load()
        {
            Terraria.Localization.On_Language.GetText += HookGetText;
        }

        public static void Unload()
        {
            Terraria.Localization.On_Language.GetText -= HookGetText;
            cachedConfig = null;
        }

        /// <summary>
        /// 取配置实例。模组加载早期配置可能还没注册好，
        /// 这里吞掉异常并返回 null，等注册完成后再缓存。
        /// </summary>
        private static TrainerConfig Config {
            get {
                if (cachedConfig == null) {
                    try {
                        cachedConfig = ModContent.GetInstance<TrainerConfig>();
                    }
                    catch {
                        return null;
                    }
                }

                return cachedConfig;
            }
        }

        /// <summary>当前强制使用的语言标签；跟随游戏时返回 null。</summary>
        private static string ForcedTag {
            get {
                TrainerConfig cfg = Config;
                if (cfg == null) {
                    return null;
                }

                return cfg.Language switch {
                    TrainerLanguage.English => TagEnglish,
                    TrainerLanguage.Chinese => TagChinese,
                    _ => null,
                };
            }
        }

        private static LocalizedText HookGetText(Terraria.Localization.On_Language.orig_GetText orig, string key)
        {
            // 这层 Hook 挂在 Language 上，是全游戏最热的路径之一。
            // 所以先做前缀判断：非本模组的 key 只走一次字符串比较就放行，
            // 连配置都不去读。
            if (key == null || !key.StartsWith(ModPrefix, StringComparison.Ordinal)) {
                return orig(key);
            }

            string tag = ForcedTag;
            if (tag != null) {
                string altKey = ModPrefix + tag + "." + key.Substring(ModPrefix.Length);
                if (Language.Exists(altKey)) {
                    return orig(altKey);
                }
            }

            return orig(key);
        }

        /// <summary>取本模组的一条文字。subKey 形如 "UI.PanelTitle"。</summary>
        public static string Get(string subKey)
        {
            return Language.GetTextValue(ModPrefix + subKey);
        }

        /// <summary>取一条带占位符的文字。</summary>
        public static string Get(string subKey, params object[] args)
        {
            return Language.GetTextValue(ModPrefix + subKey, args);
        }
    }
}
