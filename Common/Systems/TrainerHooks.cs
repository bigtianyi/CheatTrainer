using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using CheatTrainer.Common.Configs;

namespace CheatTrainer.Common.Systems
{
    /// <summary>
    /// 需要挂到游戏底层的方法上的几项功能：
    ///
    ///   5  幽灵穿墙        —— Player.ghost
    ///   8  全亮            —— Lighting.GlobalBrightness
    ///   9  免材料合成      —— Recipe.CollectedEnoughItemsToCraftRecipeNew
    ///   10 无视制作站      —— Recipe.PlayerMeetsTileRequirements / PlayerMeetsEnvironmentConditions
    ///   12 游戏变速        —— Main.DoUpdate
    ///   13 剑类合成天顶剑  —— Recipe.CollectedEnoughItemsToCraftRecipeNew / PlayerMeetsTileRequirements
    ///   14 通讯类合成      —— Recipe.CollectedEnoughItemsToCraftRecipeNew / PlayerMeetsTileRequirements
    ///
    /// 这三处 Recipe 方法在 1.4.4 里是静态的：
    ///   static bool CollectedEnoughItemsToCraftRecipeNew(Recipe)
    ///   static bool PlayerMeetsTileRequirements(Player, Recipe)
    ///   static bool PlayerMeetsEnvironmentConditions(Player, Recipe)
    /// 直接返回 true 就等于原版修改器里「把校验跳过去」的效果，但走 IL 层，与位数和运行时无关。
    ///
    /// 注意第 13、14 项复用同一个 CollectedEnoughItemsToCraftRecipeNew：
    /// 它是「材料够不够」的总闸。第 9 项是「全都不要」，第 13/14 项是
    /// 「只有各自那几条配方、且手上确实有引子时才放行」——三者互不干扰。
    /// </summary>
    public class TrainerHooks : ModSystem
    {
        /// <summary>变速时一帧最多跑几次逻辑，防止滑条拉满把机器拖死。</summary>
        private const int MaxLogicStepsPerFrame = 10;

        private static bool forcedGhost;
        private double speedAccumulator;

        public override void Load()
        {
            if (Main.dedServ) {
                return;
            }

            Terraria.On_Recipe.CollectedEnoughItemsToCraftRecipeNew += HookFreeCraft;
            Terraria.On_Recipe.PlayerMeetsTileRequirements += HookTileRequirements;
            Terraria.On_Recipe.PlayerMeetsEnvironmentConditions += HookEnvironmentConditions;
            Terraria.On_Main.DoUpdate += HookDoUpdate;
            Terraria.On_Lighting.UpdateGlobalBrightness += HookUpdateGlobalBrightness;
        }

        public override void Unload()
        {
            Terraria.On_Recipe.CollectedEnoughItemsToCraftRecipeNew -= HookFreeCraft;
            Terraria.On_Recipe.PlayerMeetsTileRequirements -= HookTileRequirements;
            Terraria.On_Recipe.PlayerMeetsEnvironmentConditions -= HookEnvironmentConditions;
            Terraria.On_Main.DoUpdate -= HookDoUpdate;
            Terraria.On_Lighting.UpdateGlobalBrightness -= HookUpdateGlobalBrightness;
        }

        public override void PostUpdateEverything()
        {
            TrainerConfig cfg = TrainerConfig.Instance;
            if (cfg == null) {
                return;
            }

            ApplyNoclip(cfg.Noclip);
            ApplyFullBright(cfg);
        }

        private static void ApplyNoclip(bool enabled)
        {
            if (Main.gameMenu) {
                return;
            }

            Player player = Main.LocalPlayer;
            if (player == null) {
                return;
            }

            if (enabled) {
                player.ghost = true;
                forcedGhost = true;
            }
            else if (forcedGhost) {
                // 只清理我们自己点亮的幽灵状态，避免影响真正死亡后的幽灵
                player.ghost = false;
                forcedGhost = false;
            }
        }

        private static void ApplyFullBright(TrainerConfig cfg)
        {
            Lighting.GlobalBrightness = cfg.FullBright ? cfg.Brightness : 1f;
        }

        /// <summary>
        /// 游戏每帧都会重算一次全局亮度，这里在开启全亮时把重算直接短路掉，
        /// 否则我们写进去的亮度会被覆盖回去。
        /// </summary>
        private void HookUpdateGlobalBrightness(Terraria.On_Lighting.orig_UpdateGlobalBrightness orig)
        {
            TrainerConfig cfg = TrainerConfig.Instance;
            if (cfg != null && cfg.FullBright) {
                Lighting.GlobalBrightness = cfg.Brightness;
                return;
            }

            orig();
        }

        /// <summary>9. 免材料合成（全配方生效）。</summary>
        private bool HookFreeCraft(Terraria.On_Recipe.orig_CollectedEnoughItemsToCraftRecipeNew orig, Recipe recipe)
        {
            TrainerConfig cfg = TrainerConfig.Instance;
            if (cfg != null && cfg.FreeCraft) {
                return true;
            }

            // 13. 剑类合成天顶剑：只对这一条配方、且手上真有引子武器时放行。
            // 缺的材料在这里被「忽略」掉，但消耗阶段仍归游戏管——
            // 所以手上那把铜短剑照常会被吃掉，不会无限刷。
            if (TrainerZenithCraft.ShouldUnlock(recipe)) {
                return true;
            }

            // 14. 通讯类合成：手机 / 贝壳电话两条配方，同理。
            if (TrainerShellphoneCraft.ShouldUnlock(recipe)) {
                return true;
            }

            return orig(recipe);
        }

        /// <summary>10. / 13. / 14. 无视制作站。</summary>
        private bool HookTileRequirements(Terraria.On_Recipe.orig_PlayerMeetsTileRequirements orig, Player player, Recipe recipe)
        {
            TrainerConfig cfg = TrainerConfig.Instance;
            if (cfg != null && cfg.IgnoreCraftingStations) {
                return true;
            }

            // 13. 只针对天顶剑这一条配方放宽制作站要求（不装秘银砧也能合）
            if (TrainerZenithCraft.IgnoresStation && TrainerZenithCraft.ShouldUnlock(recipe)) {
                return true;
            }

            // 14. 手机 / 贝壳电话：不装工匠作坊也能合
            if (TrainerShellphoneCraft.IgnoresStation && TrainerShellphoneCraft.ShouldUnlock(recipe)) {
                return true;
            }

            return orig(player, recipe);
        }

        /// <summary>10. / 13. / 14. 无视环境要求（水/蜂蜜/岩浆等）。</summary>
        private bool HookEnvironmentConditions(Terraria.On_Recipe.orig_PlayerMeetsEnvironmentConditions orig, Player player, Recipe recipe)
        {
            TrainerConfig cfg = TrainerConfig.Instance;
            if (cfg != null && cfg.IgnoreCraftingStations) {
                return true;
            }

            if (TrainerZenithCraft.IgnoresStation && TrainerZenithCraft.ShouldUnlock(recipe)) {
                return true;
            }

            if (TrainerShellphoneCraft.IgnoresStation && TrainerShellphoneCraft.ShouldUnlock(recipe)) {
                return true;
            }

            return orig(player, recipe);
        }

        /// <summary>
        /// 12. 游戏变速。
        ///
        /// 原版修改器挂的是独立的 TrSpeedHack_x86.dll（变速齿轮），靠劫持游戏进程的计时器
        /// 让游戏每秒多跑若干次逻辑帧。这里等价的做法是把每帧的 Main.DoUpdate 重复调用，
        /// 小于 1 倍时则跳过部分帧，从而实现 0.1x ~ 10.0x 的变速。
        /// </summary>
        private void HookDoUpdate(Terraria.On_Main.orig_DoUpdate orig, Main self, ref GameTime gameTime)
        {
            TrainerConfig cfg = TrainerConfig.Instance;

            if (cfg == null || !cfg.SpeedHack || Main.gameMenu) {
                speedAccumulator = 0d;
                orig(self, ref gameTime);
                return;
            }

            speedAccumulator += cfg.GameSpeed;

            int steps = (int)speedAccumulator;
            if (steps > MaxLogicStepsPerFrame) {
                steps = MaxLogicStepsPerFrame;
            }

            if (steps < 0) {
                steps = 0;
            }

            speedAccumulator -= steps;

            for (int i = 0; i < steps; i++) {
                orig(self, ref gameTime);
            }
        }
    }
}
