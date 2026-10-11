using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using Terraria.UI;

namespace CheatTrainer.Common.UI
{
    /// <summary>把悬浮面板挂进游戏的界面层。</summary>
    public class TrainerUISystem : ModSystem
    {
        private static UserInterface ui;
        private static TrainerUIState state;

        public static bool Visible { get; private set; }

        public override void Load()
        {
            if (Main.dedServ) {
                return;
            }

            ui = new UserInterface();
        }

        public override void Unload()
        {
            ui = null;
            state = null;
            Visible = false;
        }

        public static void TogglePanel()
        {
            if (Visible) {
                HidePanel();
            }
            else {
                ShowPanel();
            }
        }

        public static void ShowPanel()
        {
            if (ui == null) {
                return;
            }

            state ??= new TrainerUIState();
            ui.SetState(state);
            Visible = true;
        }

        public static void HidePanel()
        {
            ui?.SetState(null);
            Visible = false;
        }

        public override void UpdateUI(GameTime gameTime)
        {
            if (ui == null || !Visible || Main.gameMenu) {
                return;
            }

            ui.Update(gameTime);
        }

        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
        {
            if (Main.dedServ) {
                return;
            }

            int index = layers.FindIndex(layer => layer.Name == "Vanilla: Mouse Text");
            if (index == -1) {
                index = layers.Count;
            }

            layers.Insert(index, new LegacyGameInterfaceLayer(
                "CheatTrainer: Panel",
                DrawPanel,
                InterfaceScaleType.UI));
        }

        private bool DrawPanel()
        {
            if (Visible && !Main.gameMenu && ui?.CurrentState != null) {
                ui.Draw(Main.spriteBatch, new GameTime());
            }

            return true;
        }

        public override void PostUpdateEverything()
        {
            // 只有当**鼠标真的落在面板上**时，才挡住游戏输入（避免误挖/误用物品）。
            // 面板可见 ≠ 鼠标在面板上：如果无条件设 mouseInterface = true，
            // 玩家只要开着面板就完全没法攻击/挖掘/用物品了，那是错误的。
            if (!Visible || Main.gameMenu || Main.dedServ) {
                return;
            }

            if (state == null || !state.IsMouseOverPanel(Main.MouseScreen)) {
                return;
            }

            Player player = Main.LocalPlayer;
            if (player != null) {
                player.mouseInterface = true;
            }
        }
    }
}
