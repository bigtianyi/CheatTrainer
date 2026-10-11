using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using TerrariaTrainer.Common.Configs;

namespace TerrariaTrainer.Common.Players
{
    /// <summary>
    /// 每帧生效的状态类作弊。
    ///
    /// 对应原版修改器的第 1~7、11 项：
    ///   1 无限生命   2 无限魔法   3 无限氧气   4 无限飞行
    ///   5 幽灵穿墙（见 Systems/TrainerHooks）  6 无限物品   7 快速治疗   11 超级伤害
    /// </summary>
    public class TrainerPlayer : ModPlayer
    {
        private const int InventorySize = 58;

        private int quickHealCooldown;

        public override void PostUpdateMiscEffects()
        {
            if (Player.whoAmI != Main.myPlayer) {
                return;
            }

            TrainerConfig cfg = TrainerConfig.Instance;
            if (cfg == null) {
                return;
            }

            if (quickHealCooldown > 0) {
                quickHealCooldown--;
            }

            ApplyInfiniteHealth(cfg);
            ApplyInfiniteMana(cfg);
            ApplyInfiniteBreath(cfg);
            ApplyInfiniteFlight(cfg);

            if (cfg.QuickHeal) {
                ApplyQuickHeal();
            }
        }

        public override void PostUpdate()
        {
            if (Player.whoAmI != Main.myPlayer) {
                return;
            }

            TrainerConfig cfg = TrainerConfig.Instance;
            if (cfg != null && cfg.InfiniteItems) {
                RefillInventoryStacks();
            }
        }

        /// <summary>1. 无限生命。原版修改器直接写入 statLife = 9999。</summary>
        private void ApplyInfiniteHealth(TrainerConfig cfg)
        {
            if (!cfg.InfiniteHealth) {
                return;
            }

            Player.statLife = Player.statLifeMax2;
        }

        /// <summary>2. 无限魔法。</summary>
        private void ApplyInfiniteMana(TrainerConfig cfg)
        {
            if (!cfg.InfiniteMana) {
                return;
            }

            Player.statMana = Player.statManaMax2;
        }

        /// <summary>3. 无限氧气。</summary>
        private void ApplyInfiniteBreath(TrainerConfig cfg)
        {
            if (!cfg.InfiniteBreath) {
                return;
            }

            Player.breath = Player.breathMax;
            Player.breathCD = 0;
        }

        /// <summary>4. 无限飞行。原版修改器写入 wingTime = 10000f、rocketTime = 9999。</summary>
        private void ApplyInfiniteFlight(TrainerConfig cfg)
        {
            if (!cfg.InfiniteFlight) {
                return;
            }

            // 没带翅膀也能飞：给一份最基础的翅膀逻辑
            if (Player.wingsLogic == 0) {
                Player.wingsLogic = 1;
            }

            Player.wingTime = 10000f;
            Player.wingTimeMax = 10000;
            Player.rocketTime = 9999;
            Player.rocketTimeMax = 9999;
        }

        /// <summary>
        /// 7. 快速治疗。
        ///
        /// 原版修改器把 Player.QuickHeal_GetItemToUse 里的 0.91 / 0.56 两个血量阈值
        /// 改成 1.0，效果是「只要没满血，快速治疗就一定选最好的那瓶药」。
        /// 这里用等价且更直接的做法：血量不满就自动喝下背包里恢复量最大的治疗药水，
        /// 并照常施加药水冷却，所以不会出现无限制狂嗑。
        /// </summary>
        private void ApplyQuickHeal()
        {
            if (quickHealCooldown > 0 || Player.statLife >= Player.statLifeMax2) {
                return;
            }

            if (Player.potionDelay > 0) {
                return;
            }

            int bestSlot = -1;
            int bestHeal = 0;

            for (int i = 0; i < InventorySize; i++) {
                Item item = Player.inventory[i];
                if (item == null || item.IsAir || item.stack <= 0) {
                    continue;
                }

                if (!item.potion || item.healLife <= 0) {
                    continue;
                }

                if (item.healLife > bestHeal) {
                    bestHeal = item.healLife;
                    bestSlot = i;
                }
            }

            if (bestSlot < 0) {
                return;
            }

            Item potion = Player.inventory[bestSlot];
            Player.statLife = Math.Min(Player.statLife + potion.healLife, Player.statLifeMax2);

            if (!Player.HasBuff(BuffID.PotionSickness)) {
                Player.AddBuff(BuffID.PotionSickness, 3600);
            }

            potion.stack--;
            if (potion.stack <= 0) {
                potion.TurnToAir();
            }

            quickHealCooldown = 30;
        }

        /// <summary>
        /// 6. 无限物品。原版修改器把物品堆叠数直接写成 maxStack，这里同样处理，
        /// 背包里所有可堆叠物品始终保持满堆叠，弹药 / 药水 / 建材都不会消耗完。
        /// </summary>
        private void RefillInventoryStacks()
        {
            for (int i = 0; i < InventorySize; i++) {
                Item item = Player.inventory[i];
                if (item == null || item.IsAir || item.stack <= 0) {
                    continue;
                }

                if (item.maxStack > 1 && item.stack < item.maxStack) {
                    item.stack = item.maxStack;
                }
            }
        }

        /// <summary>1. 无限生命的兜底：单次伤害超过血量上限时也不该死。</summary>
        public override bool PreKill(double damage, int hitDirection, bool pvp, ref bool playSound, ref bool genGore,
            ref PlayerDeathReason damageSource)
        {
            TrainerConfig cfg = TrainerConfig.Instance;
            if (cfg == null || !cfg.InfiniteHealth) {
                return true;
            }

            Player.statLife = Player.statLifeMax2;
            Player.dead = false;
            playSound = false;
            genGore = false;
            return false;
        }

        /// <summary>11. 超级伤害。原版修改器让 GetWeaponDamage 返回 999999。</summary>
        public override void ModifyWeaponDamage(Item item, ref StatModifier damage)
        {
            TrainerConfig cfg = TrainerConfig.Instance;
            if (cfg != null && cfg.SuperDamage) {
                damage.Flat += cfg.DamageBonus;
            }
        }
    }
}
