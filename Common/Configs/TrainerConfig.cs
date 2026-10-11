using System.ComponentModel;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;

namespace TerrariaTrainer.Common.Configs
{
    /// <summary>
    /// 模组文字使用哪种语言。
    /// 枚举成员的显示名走本地化键 Mods.TerrariaTrainer.Configs.TrainerLanguage.{成员}.Label。
    /// </summary>
    public enum TrainerLanguage
    {
        /// <summary>跟随游戏的语言设置（默认）。</summary>
        Auto,

        English,

        Chinese,
    }

    /// <summary>
    /// 对应原版修改器的 12 个功能开关，外加 3 个可调数值，
    /// 以及「剑类合成天顶剑」这一组玩法向开关。
    /// 全部默认关闭，需要时按热键或点面板开启。
    /// </summary>
    public class TrainerConfig : ModConfig
    {
        public static TrainerConfig Instance => ModContent.GetInstance<TrainerConfig>();

        public override ConfigScope Mode => ConfigScope.ClientSide;

        [Header("Cheats")]

        [DefaultValue(false)]
        public bool InfiniteHealth { get; set; }

        [DefaultValue(false)]
        public bool InfiniteMana { get; set; }

        [DefaultValue(false)]
        public bool InfiniteBreath { get; set; }

        [DefaultValue(false)]
        public bool InfiniteFlight { get; set; }

        [DefaultValue(false)]
        public bool Noclip { get; set; }

        [DefaultValue(false)]
        public bool InfiniteItems { get; set; }

        [DefaultValue(false)]
        public bool QuickHeal { get; set; }

        [DefaultValue(false)]
        public bool FullBright { get; set; }

        [DefaultValue(false)]
        public bool FreeCraft { get; set; }

        [DefaultValue(false)]
        public bool IgnoreCraftingStations { get; set; }

        [DefaultValue(false)]
        public bool SuperDamage { get; set; }

        [DefaultValue(false)]
        public bool SpeedHack { get; set; }


        [Header("ZenithCraft")]

        /// <summary>
        /// 剑类合成天顶剑：只要背包里有「天顶剑配方所需的任意一件武器」，
        /// 天顶剑配方就会亮起，可以直接制作——缺的材料自动忽略。
        /// 合成时仍会正常消耗那一件武器，也就是说铜短剑会「变成」天顶剑。
        /// 有几把符合条件的武器，就能合几把。
        /// 兼容原版、灾厄以及其它改过天顶剑配方的模组（配方是运行时动态查找的）。
        /// </summary>
        [DefaultValue(false)]
        public bool SwordToZenith { get; set; }

        /// <summary>
        /// 合成天顶剑时连制作站也不要求（不装秘银砧也能合）。
        /// 只在「剑类合成天顶剑」开启时才有意义。
        /// </summary>
        [DefaultValue(false)]
        public bool ZenithIgnoreStation { get; set; }


        [Header("ShellphoneCraft")]

        /// <summary>
        /// 通讯类合成：只要背包里有「手机」或「贝壳电话」配方所需的任意一件材料，
        /// 对应配方就会亮起，可以直接制作——缺的材料自动忽略。
        /// 一次性覆盖两级：手机（PDA + 镜子）与 贝壳电话（手机 + 两个海螺），
        /// 所以从海螺一路合到贝壳电话是打通的。
        /// 合成时那一件材料照常被消耗，也就是说魔法海螺会「变成」贝壳电话。
        /// 兼容原版、灾厄以及其它改过这两条配方的模组（配方是运行时动态查找的）。
        /// </summary>
        [DefaultValue(false)]
        public bool ShellphoneCraft { get; set; }

        /// <summary>
        /// 合成手机 / 贝壳电话时连制作站也不要求（不装工匠作坊也能合）。
        /// 只在「通讯类合成」开启时才有意义。
        /// </summary>
        [DefaultValue(false)]
        public bool ShellphoneIgnoreStation { get; set; }


        [Header("Values")]

        /// <summary>游戏变速倍率，对应原版修改器的 0.1x ~ 10.0x 滑条（默认 2.5x）。</summary>
        [Range(0.1f, 10f)]
        [Increment(0.1f)]
        [Slider]
        [DefaultValue(2.5f)]
        public float GameSpeed { get; set; }

        /// <summary>超级伤害附加的固定伤害值，对应原版修改器返回的 999999。</summary>
        [Range(100f, 999999f)]
        [Increment(100f)]
        [Slider]
        [DefaultValue(999999f)]
        public float DamageBonus { get; set; }

        /// <summary>全亮强度。1 = 原版，越大越亮。</summary>
        [Range(1f, 5f)]
        [Increment(0.1f)]
        [Slider]
        [DefaultValue(3f)]
        public float Brightness { get; set; }


        [Header("Language")]

        /// <summary>
        /// 模组文字的显示语言。
        /// Auto = 跟随游戏语言；选了具体语言后会强制覆盖（含配置界面本身）。
        /// </summary>
        [DefaultValue(TrainerLanguage.Auto)]
        public TrainerLanguage Language { get; set; } = TrainerLanguage.Auto;
    }
}
