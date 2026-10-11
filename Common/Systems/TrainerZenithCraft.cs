using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using CheatTrainer.Common.Configs;

namespace CheatTrainer.Common.Systems
{
    /// <summary>
    /// 「剑类合成天顶剑」。
    ///
    /// 需求：背包里只要有「天顶剑配方所需的任意一件武器」，就能直接合成天顶剑，
    ///       缺的材料自动忽略；合成时那一件武器照常被消耗——
    ///       也就是铜短剑「变成」了天顶剑。有几把符合条件的就合几把。
    ///
    /// ── 为什么必须动态查配方，不能写死物品 ID ──
    ///
    /// 天顶剑的配方在不同环境下是不一样的：
    ///   原版     ：泰拉刃 + 喵喵刃 + 星光之怒 + 涌流波动剑 + 无头骑士之剑
    ///              + 种子弯刀 + 星怒 + 养蜂人 + 附魔剑 + 铜短剑
    ///   灾厄     ：上面这 10 件 **再加** 圣金源锭 ×5
    ///   其它模组 ：「更好的灾厄天顶剑」之类还会再改一轮
    ///
    /// 所以这里一律在运行时反查：「产出物是天顶剑的那条配方，它到底要哪些材料」。
    /// 环境怎么改都不用动代码，也不会因为某个 ID 不存在而崩。
    ///
    /// ── 怎么做到「缺什么忽略什么、但手上那件照样消耗」 ──
    ///
    /// 关键在于「放行判定」和「材料消耗」是两件事：
    ///   1. 判定阶段（RecipeAvailable / CollectedEnoughItemsToCraftRecipeNew）返回 true，
    ///      配方就会亮起——这就是「缺的材料自动忽略」。
    ///   2. 消耗阶段交给游戏原本的流程。游戏只吃掉玩家**真正拥有**的那几件，
    ///      凑不出的材料它不会凭空扣——所以铜短剑会被正常消耗掉，不会出现
    ///      「做一次白得一把、材料还在」的无限刷。
    ///
    /// 换句话说：我们只负责「让按钮可点」，不碰「扣什么材料」。
    /// </summary>
    public class TrainerZenithCraft : ModSystem
    {
        /// <summary>天顶剑的物品类型缓存。查不到（比如环境里根本没有）就是 -1。</summary>
        private static int zenithType = -1;

        /// <summary>天顶剑配方的索引缓存；换世界 / 重载模组时会失效重查。</summary>
        private static int zenithRecipeIndex = -1;

        /// <summary>
        /// 「能用来当引子的武器」集合 = 天顶剑配方里所有**武器类**材料。
        /// 之所以不直接拿全部材料去判，是因为灾厄还往配方里塞了圣金源锭这种
        /// 非武器材料——如果按「手上任意一件材料」放行，那手里只有锭也能合，逻辑就不对了。
        /// 这里只认 damage > 0 的武器，正好对应需求里的「任意剑类」。
        /// </summary>
        private static readonly HashSet<int> seedWeaponTypes = new HashSet<int>();

        private static bool cacheReady;

        /// <summary>是否已经定位到天顶剑配方。</summary>
        public static bool ZenithFound {
            get {
                EnsureCache();
                return zenithRecipeIndex >= 0;
            }
        }

        public override void Unload()
        {
            zenithType = -1;
            zenithRecipeIndex = -1;
            seedWeaponTypes.Clear();
            cacheReady = false;
        }

        /// <summary>
        /// 配方全部注册完成后才能查——早于这个时点，灾厄等模组的配方还没加进来。
        /// </summary>
        public override void PostSetupRecipes()
        {
            BuildCache();
        }

        /// <summary>开新世界 / 重载模组时清掉缓存，下一帧自动重建。</summary>
        public override void OnWorldLoad()
        {
            cacheReady = false;
        }

        public override void OnWorldUnload()
        {
            cacheReady = false;
        }

        private static void EnsureCache()
        {
            if (cacheReady) {
                return;
            }

            BuildCache();
        }

        /// <summary>
        /// 定位天顶剑配方，并记下它需要的所有武器材料。
        /// 全程按名字匹配，不依赖任何写死的 ID，因此在原版 / 灾厄 / 汉化环境下都能工作。
        /// </summary>
        private static void BuildCache()
        {
            cacheReady = true;
            zenithType = -1;
            zenithRecipeIndex = -1;
            seedWeaponTypes.Clear();

            // ---- 1. 找到天顶剑这个物品 ----
            // 用内部名匹配，而不是硬编码 4956：这样装了其它模组也不会错。
            zenithType = ResolveZenithType();
            if (zenithType <= 0) {
                return;
            }

            // ---- 2. 找到产出物是天顶剑的那条配方 ----
            // 一条配方可能有多个产物（原版的 1.4 配方都是单产物，但保留多产物处理更稳），
            // 从后往前扫，优先取最后注册的那条——通常是改动模组覆盖后的版本。
            for (int i = Recipe.numRecipes - 1; i >= 0; i--) {
                Recipe recipe = Main.recipe[i];
                if (recipe == null || recipe.createItem == null) {
                    continue;
                }

                if (recipe.createItem.type != zenithType) {
                    continue;
                }

                zenithRecipeIndex = i;
                break;
            }

            if (zenithRecipeIndex < 0) {
                return;
            }

            // ---- 3. 记下这条配方里所有武器类材料 ----
            // 1.4.4 起 requiredItem 是 List<Item>（旧版是定长数组）。
            Recipe target = Main.recipe[zenithRecipeIndex];
            List<Item> required = target.requiredItem;

            for (int i = 0; i < required.Count; i++) {
                Item ingredient = required[i];
                if (ingredient == null || ingredient.type <= ItemID.None) {
                    continue;
                }

                // 只收武器（有伤害的），排除圣金源锭之类的纯材料
                if (ingredient.damage > 0) {
                    seedWeaponTypes.Add(ingredient.type);
                }
            }
        }

        /// <summary>
        /// 找出「天顶剑」对应的物品类型。
        /// 走内部名比对：Terraria 原版内部名是 "Zenith"，中文版也一致
        /// （内部名不随语言变）。找不到就返回 -1，功能自动静默失效，不会报错。
        /// </summary>
        private static int ResolveZenithType()
        {
            for (int type = 1; type < ItemID.Count; type++) {
                string name;
                try {
                    name = ItemID.Search.GetName(type);
                }
                catch {
                    continue;
                }

                if (string.Equals(name, "Zenith", StringComparison.OrdinalIgnoreCase)) {
                    return type;
                }
            }

            return -1;
        }

        /// <summary>
        /// 背包里有没有「能当引子」的武器。
        /// 这就是「任意剑类都能合成天顶剑」的判定依据。
        /// </summary>
        private static bool PlayerHasSeedWeapon(Player player)
        {
            if (player == null || seedWeaponTypes.Count == 0) {
                return false;
            }

            // 只扫背包主体 0~58（含快捷栏），不扫装备栏/银行/时装栏
            for (int i = 0; i < 58; i++) {
                Item item = player.inventory[i];
                if (item == null || item.IsAir) {
                    continue;
                }

                if (seedWeaponTypes.Contains(item.type)) {
                    return true;
                }
            }

            return false;
        }

        /// <summary>这条配方是不是天顶剑的那条。</summary>
        private static bool IsZenithRecipe(Recipe recipe)
        {
            EnsureCache();

            if (recipe == null || zenithRecipeIndex < 0) {
                return false;
            }

            return ReferenceEquals(recipe, Main.recipe[zenithRecipeIndex]);
        }

        /// <summary>
        /// 功能是否已开启（且环境里确实存在天顶剑配方）。
        /// </summary>
        public static bool Enabled {
            get {
                TrainerConfig cfg = TrainerConfig.Instance;
                if (cfg == null || !cfg.SwordToZenith) {
                    return false;
                }

                EnsureCache();
                return zenithRecipeIndex >= 0;
            }
        }

        /// <summary>
        /// 当前玩家是否可以用「引子武器」触发天顶剑配方。
        /// 供钩子调用。
        /// </summary>
        public static bool ShouldUnlock(Recipe recipe)
        {
            if (!Enabled) {
                return false;
            }

            if (!IsZenithRecipe(recipe)) {
                return false;
            }

            return PlayerHasSeedWeapon(Main.LocalPlayer);
        }

        /// <summary>
        /// 是否连制作站都不需要。
        /// </summary>
        public static bool IgnoresStation {
            get {
                TrainerConfig cfg = TrainerConfig.Instance;
                return cfg != null && cfg.SwordToZenith && cfg.ZenithIgnoreStation;
            }
        }

        /// <summary>
        /// 当前玩家拥有的、可作为引子的武器数量——用于提示与调试。
        /// </summary>
        public static int SeedWeaponCount(Player player)
        {
            if (player == null || seedWeaponTypes.Count == 0) {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < 58; i++) {
                Item item = player.inventory[i];
                if (item != null && !item.IsAir && seedWeaponTypes.Contains(item.type)) {
                    count += item.stack;
                }
            }

            return count;
        }
    }
}
