using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TerrariaTrainer.Common.Configs;

namespace TerrariaTrainer.Common.Systems
{
    /// <summary>
    /// 「通讯类合成」——一次覆盖手机与贝壳电话两级。
    ///
    /// 需求：背包里只要有「要合成的那件东西的配方里、任意一件材料」，
    ///       该配方就亮起、可直接合成，缺的材料自动忽略；
    ///       合成时手上那一件照常被消耗——也就是魔法海螺会「变成」贝壳电话。
    ///       并且**不需要工匠作坊**（配置项可关）。
    ///
    /// ── 为什么一次管两条配方 ──
    ///
    /// 贝壳电话 = 手机 + 魔法海螺 + 恶魔海螺。它自己就要用到「手机」，
    /// 而手机本身（PDA + 镜子）也是一条复杂配方。
    /// 如果只放行贝壳电话这一条，玩家拿到海螺也合不出来——因为缺手机，
    /// 而手机同样合不出来。所以两条一起放开，链路才真正打通：
    ///
    ///   魔法海螺 ──▶ 贝壳电话（手机与恶魔海螺自动忽略）
    ///   PDA      ──▶ 手机     （镜子自动忽略）──▶ 再合贝壳电话
    ///
    /// ── 和「剑类合成天顶剑」的关键区别 ──
    ///
    /// 天顶剑那版靠 `damage > 0` 筛出「武器类」材料，因为灾厄往配方里
    /// 塞了圣金源锭（非武器），必须排除掉。
    /// 但手机 / 贝壳电话的材料**全是工具类**（手机、海螺，damage 都是 -1），
    /// 按伤害筛会把所有材料全排除掉，所以这里**不筛**——配方里的物品全都算引子。
    ///
    /// ── 怎么做到「缺什么忽略什么、但手上那件照样消耗」 ──
    ///
    /// 和天顶剑一致：只放行「判定」，不碰「消耗」。
    ///   1. 判定阶段返回 true → 配方亮起（= 缺的材料被忽略）
    ///   2. 消耗阶段交给游戏原流程 → 只吃掉你真正拥有的那几件
    /// 因此不会出现「合一次白得一件、材料还在」的无限刷。
    /// </summary>
    public class TrainerShellphoneCraft : ModSystem
    {
        /// <summary>
        /// 要覆盖的「目标产物」内部名。
        /// 走内部名而不是写死 ID：内部名不随语言变，中文环境下也一致；
        /// 而且 Shellphone 有 5 个 ID（四个目标地点变体 + 一个虚拟版本），
        /// 按名字匹配能一次全收进来，不用维护 ID 清单。
        /// </summary>
        private static readonly string[] TargetNames = {
            "CellPhone",
            "Shellphone",
        };

        /// <summary>目标产物的物品类型集合（手机 + 贝壳电话的全部变体）。</summary>
        private static readonly HashSet<int> targetTypes = new HashSet<int>();

        /// <summary>
        /// 「每条配方各自能用哪些材料当引子」。
        /// 键 = 产物物品类型（CreateItem.type），值 = 这条配方的材料集合。
        ///
        /// ⚠️ 必须**按配方分开存**，不能并成一个总集合：
        ///   · 手机配方     = PDA / 魔法镜子 / 冰雪镜子
        ///   · 贝壳电话配方 = 手机 / 魔法海螺 / 恶魔海螺
        /// 如果合并成一个并集，手上只有「魔法镜子」时贝壳电话配方也会被放行，
        /// 合一下就能白嫖一个贝壳电话（跨级误放行）——那是 bug。
        /// </summary>
        private static readonly Dictionary<int, HashSet<int>> recipeSeeds = new Dictionary<int, HashSet<int>>();

        /// <summary>所有引子材料的并集，仅供 PlayerHasSeedItem 的快速预筛与调试用。</summary>
        private static readonly HashSet<int> allSeedTypes = new HashSet<int>();

        private static bool cacheReady;

        /// <summary>是否已经定位到至少一条目标配方。</summary>
        public static bool RecipeFound {
            get {
                EnsureCache();
                return targetTypes.Count > 0;
            }
        }

        public override void Unload()
        {
            targetTypes.Clear();
            recipeSeeds.Clear();
            allSeedTypes.Clear();
            cacheReady = false;
        }

        /// <summary>
        /// 配方全部注册完成后才能查——早于这个时点，其它模组的配方还没加进来。
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
        /// 找出「产物是手机或贝壳电话」的全部配方，并记下它们用到的所有材料。
        /// 全程按名字匹配，不依赖写死的 ID，因此在原版 / 灾厄 / 汉化环境下都能工作。
        /// </summary>
        private static void BuildCache()
        {
            cacheReady = true;
            targetTypes.Clear();
            recipeSeeds.Clear();
            allSeedTypes.Clear();

            // ---- 1. 按内部名收集目标产物（Shellphone 的多个变体一次全收）----
            //   内部名形如 ShellphoneHome / ShellphoneSpawn ... 所以用前缀匹配。
            for (int type = 1; type < ItemID.Count; type++) {
                string name;
                try {
                    name = ItemID.Search.GetName(type);
                }
                catch {
                    continue;
                }

                if (string.IsNullOrEmpty(name)) {
                    continue;
                }

                for (int i = 0; i < TargetNames.Length; i++) {
                    if (name.StartsWith(TargetNames[i], StringComparison.OrdinalIgnoreCase)) {
                        targetTypes.Add(type);
                        break;
                    }
                }
            }

            if (targetTypes.Count == 0) {
                return;
            }

            // ---- 2. 扫全部配方，凡是产物命中目标集合的，**按产物分组**记下它的材料 ----
            //   注意「手机」既是产物也是材料：作为贝壳电话的材料时，会把它的类型
            //   放进「贝壳电话」那一组的材料集合里，所以「手上只有手机」能直接合贝壳电话。
            for (int i = 0; i < Recipe.numRecipes; i++) {
                Recipe recipe = Main.recipe[i];
                if (recipe == null || recipe.createItem == null) {
                    continue;
                }

                int productType = recipe.createItem.type;
                if (!targetTypes.Contains(productType)) {
                    continue;
                }

                // requiredItem 在 1.4.4 是 List<Item>（旧版才是定长数组）
                List<Item> required = recipe.requiredItem;
                for (int j = 0; j < required.Count; j++) {
                    Item ingredient = required[j];
                    if (ingredient == null || ingredient.type <= ItemID.None) {
                        continue;
                    }

                    // 不按 damage 筛：手机 / 海螺这类工具 damage 都是 -1，
                    // 筛了会把材料全排除。配方里出现什么，什么就能当引子。
                    int ingredientType = ingredient.type;

                    if (!recipeSeeds.TryGetValue(productType, out HashSet<int> set)) {
                        set = new HashSet<int>();
                        recipeSeeds[productType] = set;
                    }

                    set.Add(ingredientType);
                    allSeedTypes.Add(ingredientType);
                }
            }
        }

        /// <summary>
        /// 背包里有没有「这条配方能用来当引子」的材料。
        /// 只扫背包主体 0~58（含快捷栏），不扫装备栏 / 银行 / 时装栏。
        /// </summary>
        private static bool PlayerHasSeedItem(Player player, HashSet<int> seeds)
        {
            if (player == null || seeds == null || seeds.Count == 0) {
                return false;
            }

            for (int i = 0; i < 58; i++) {
                Item item = player.inventory[i];
                if (item == null || item.IsAir) {
                    continue;
                }

                if (seeds.Contains(item.type)) {
                    return true;
                }
            }

            return false;
        }

        /// <summary>功能是否已开启（且环境里确实存在目标配方）。</summary>
        public static bool Enabled {
            get {
                TrainerConfig cfg = TrainerConfig.Instance;
                if (cfg == null || !cfg.ShellphoneCraft) {
                    return false;
                }

                EnsureCache();
                return targetTypes.Count > 0;
            }
        }

        /// <summary>这条配方是不是「手机或贝壳电话」的那条。</summary>
        public static bool IsTargetRecipe(Recipe recipe)
        {
            EnsureCache();

            if (recipe == null || recipe.createItem == null || targetTypes.Count == 0) {
                return false;
            }

            return targetTypes.Contains(recipe.createItem.type);
        }

        /// <summary>
        /// 当前玩家是否可以用「引子材料」触发这条手机 / 贝壳电话配方。
        /// 供 Hook 调用。
        ///
        /// 关键：用**这条配方自己的**材料集合去判定，而不是全局并集，
        /// 否则「手上有魔法镜子」会连贝壳电话配方一起放行（跨级误放行）。
        /// </summary>
        public static bool ShouldUnlock(Recipe recipe)
        {
            if (!Enabled) {
                return false;
            }

            if (recipe == null || recipe.createItem == null) {
                return false;
            }

            EnsureCache();

            int productType = recipe.createItem.type;
            if (!targetTypes.Contains(productType)) {
                return false;
            }

            if (!recipeSeeds.TryGetValue(productType, out HashSet<int> seeds)) {
                return false;
            }

            return PlayerHasSeedItem(Main.LocalPlayer, seeds);
        }

        /// <summary>是否连制作站（工匠作坊）都不需要。</summary>
        public static bool IgnoresStation {
            get {
                TrainerConfig cfg = TrainerConfig.Instance;
                return cfg != null && cfg.ShellphoneCraft && cfg.ShellphoneIgnoreStation;
            }
        }

        /// <summary>
        /// 当前玩家拥有的、可作为引子的材料总数——用于提示与调试。
        /// 统计的是「所有目标配方材料的并集」里玩家持有的件数。
        /// </summary>
        public static int SeedItemCount(Player player)
        {
            if (player == null || allSeedTypes.Count == 0) {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < 58; i++) {
                Item item = player.inventory[i];
                if (item != null && !item.IsAir && allSeedTypes.Contains(item.type)) {
                    count += item.stack;
                }
            }

            return count;
        }
    }
}
