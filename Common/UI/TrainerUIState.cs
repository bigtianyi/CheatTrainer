using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using Terraria.UI;
using TerrariaTrainer.Common.Configs;
using TerrariaTrainer.Common.Systems;

namespace TerrariaTrainer.Common.UI
{
    /// <summary>面板上的一根滑条，直接读写配置项。</summary>
    public class TrainerSlider : UIElement
    {
        private readonly Func<float> getter;
        private readonly Action<float> setter;
        private readonly float min;
        private readonly float max;
        private readonly float step;

        private bool dragging;

        public TrainerSlider(Func<float> getter, Action<float> setter, float min, float max, float step)
        {
            this.getter = getter;
            this.setter = setter;
            this.min = min;
            this.max = max;
            this.step = step;

            Width.Set(0f, 1f);
            Height.Set(16f, 0f);
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            if (!Main.mouseLeft) {
                dragging = false;
            }
            else if (!dragging && ContainsPoint(Main.MouseScreen)) {
                dragging = true;
            }

            if (!dragging) {
                return;
            }

            CalculatedStyle dim = GetDimensions();
            if (dim.Width <= 0f) {
                return;
            }

            float ratio = MathHelper.Clamp((Main.MouseScreen.X - dim.X) / dim.Width, 0f, 1f);
            float raw = min + ratio * (max - min);

            if (step > 0f) {
                raw = (float)Math.Round(raw / step) * step;
            }

            setter(MathHelper.Clamp(raw, min, max));
        }

        protected override void DrawSelf(SpriteBatch spriteBatch)
        {
            CalculatedStyle dim = GetDimensions();
            Texture2D pixel = TextureAssets.MagicPixel.Value;

            int x = (int)dim.X;
            int y = (int)dim.Y;
            int w = (int)dim.Width;
            int h = (int)dim.Height;

            // 底槽
            spriteBatch.Draw(pixel, new Rectangle(x, y, w, h), new Color(38, 40, 54) * 0.95f);

            // 已填充部分
            float ratio = max - min <= 0f ? 0f : MathHelper.Clamp((getter() - min) / (max - min), 0f, 1f);
            int filled = (int)(w * ratio);
            if (filled > 0) {
                spriteBatch.Draw(pixel, new Rectangle(x, y, filled, h), new Color(86, 156, 232));
            }

            // 滑块
            int knobX = Math.Clamp(x + filled - 2, x, x + w - 4);
            spriteBatch.Draw(pixel, new Rectangle(knobX, y - 2, 4, h + 4), new Color(226, 234, 245));
        }
    }

    /// <summary>
    /// 悬浮控制面板：14 个可点击开关 + 3 根滑条。
    /// 面板打开时不会误触游戏内的物品使用。
    /// </summary>
    public class TrainerUIState : UIState
    {
        // ---- 布局常量 ----
        // 行高 22，第一行从 56 开始；滑条区紧跟在开关列表下面。
        // 加了第 13、14 项之后行数变多，这些值必须跟着调，
        // 否则滑条会盖住最后几行开关（曾经的 bug：第 12~14 行被滑条压住）。
        private const float RowStart = 56f;
        private const float RowHeight = 22f;
        private const float SliderSectionHeight = 48f;
        private const float SliderTextOffset = 0f;
        private const float SliderTrackOffset = 20f;
        private const float FooterGap = 14f;

        private UIPanel panel;
        private UIText titleText;
        private UIText hintText;
        private UIText footerText;
        private UIText[] rowTexts;
        private UIText[] sliderLabels;

        private bool dragging;
        private Vector2 dragOffset;
        private int refreshTimer;

        public override void OnInitialize()
        {
            rowTexts = new UIText[TrainerKeybinds.CheatCount];
            sliderLabels = new UIText[3];

            // 滑条区起点 = 所有开关行之后；面板高度 = 滑条区之后 + 页脚。
            float sliderTop = RowStart + TrainerKeybinds.CheatCount * RowHeight + 8f;
            float footerTop = sliderTop + 3 * SliderSectionHeight + FooterGap;
            float panelHeight = footerTop + 30f;

            panel = new UIPanel {
                BackgroundColor = new Color(22, 24, 36) * 0.94f,
                BorderColor = new Color(86, 156, 232),
            };
            panel.SetPadding(10f);
            panel.Left.Set(560f, 0f);
            panel.Top.Set(140f, 0f);
            // 宽度留够英文长标签（如 Ignore Crafting Stations）
            panel.Width.Set(384f, 0f);
            panel.Height.Set(panelHeight, 0f);
            panel.OnLeftMouseDown += (evt, el) => {
                dragging = true;
                dragOffset = Main.MouseScreen - new Vector2(el.GetDimensions().X, el.GetDimensions().Y);
            };
            panel.OnLeftMouseUp += (evt, el) => dragging = false;
            Append(panel);

            titleText = new UIText("", 1f, true);
            titleText.Left.Set(6f, 0f);
            titleText.Top.Set(2f, 0f);
            titleText.TextColor = new Color(226, 234, 245);
            panel.Append(titleText);

            var close = new UIText("✕", 1f, true);
            close.Left.Set(-26f, 1f);
            close.Top.Set(2f, 0f);
            close.TextColor = new Color(220, 130, 130);
            close.OnLeftClick += (evt, el) => TrainerUISystem.HidePanel();
            panel.Append(close);

            hintText = new UIText("", 0.7f);
            hintText.Left.Set(8f, 0f);
            hintText.Top.Set(30f, 0f);
            hintText.TextColor = new Color(150, 158, 176);
            panel.Append(hintText);

            for (int i = 0; i < rowTexts.Length; i++) {
                int index = i;
                var row = new UIText("", 0.8f);
                row.Left.Set(8f, 0f);
                row.Top.Set(RowStart + i * RowHeight, 0f);
                row.OnLeftClick += (evt, el) => TrainerKeybinds.Toggle(index);
                panel.Append(row);
                rowTexts[i] = row;
            }

            // ---- 3 根滑条，紧跟在开关列表之后，不再写死坐标 ----
            AddSliderSection(0, sliderTop);
            AddSliderSection(1, sliderTop + SliderSectionHeight);
            AddSliderSection(2, sliderTop + 2 * SliderSectionHeight);

            footerText = new UIText("", 0.66f);
            footerText.Left.Set(8f, 0f);
            footerText.Top.Set(footerTop, 0f);
            footerText.TextColor = new Color(150, 158, 176);
            panel.Append(footerText);

            RefreshTexts();
        }

        private void AddSliderSection(int slot, float top)
        {
            // 文字内容在 RefreshTexts 里按当前语言填充
            var text = new UIText("", 0.78f);
            text.Left.Set(8f, 0f);
            text.Top.Set(top + SliderTextOffset, 0f);
            text.TextColor = new Color(200, 208, 224);
            panel.Append(text);
            sliderLabels[slot] = text;

            TrainerSlider slider = slot switch {
                0 => new TrainerSlider(() => TrainerConfig.Instance.GameSpeed,
                    v => TrainerConfig.Instance.GameSpeed = v, 0.1f, 10f, 0.1f),
                1 => new TrainerSlider(() => TrainerConfig.Instance.DamageBonus,
                    v => TrainerConfig.Instance.DamageBonus = v, 1000f, 999999f, 1000f),
                _ => new TrainerSlider(() => TrainerConfig.Instance.Brightness,
                    v => TrainerConfig.Instance.Brightness = v, 1f, 5f, 0.1f),
            };

            slider.Left.Set(6f, 0f);
            slider.Top.Set(top + SliderTrackOffset, 0f);
            slider.Width.Set(-12f, 1f);
            panel.Append(slider);
        }

        /// <summary>
        /// 鼠标是否真的落在**面板本体**上（不是整个屏幕）。
        /// 注意不能直接对 UIState 调 ContainsPoint——UIState 铺满全屏，
        /// 那样判断永远为 true，等于没判断。这里只测 panel 自己。
        /// </summary>
        public bool IsMouseOverPanel(Vector2 mouseScreen)
        {
            return panel != null && panel.ContainsPoint(mouseScreen);
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            if (dragging) {
                if (!Main.mouseLeft) {
                    dragging = false;
                }
                else {
                    // 把面板位置夹在屏幕内，避免被拖出去之后再也拖不回来
                    // （超出屏幕的部分无从下手，只能重开面板）。
                    CalculatedStyle dim = panel.GetDimensions();
                    float maxLeft = Math.Max(0f, Main.screenWidth - dim.Width);
                    float maxTop = Math.Max(0f, Main.screenHeight - dim.Height);

                    float left = MathHelper.Clamp(Main.MouseScreen.X - dragOffset.X, 0f, maxLeft);
                    float top = MathHelper.Clamp(Main.MouseScreen.Y - dragOffset.Y, 0f, maxTop);

                    panel.Left.Set(left, 0f);
                    panel.Top.Set(top, 0f);
                    panel.Recalculate();
                }
            }

            if (++refreshTimer >= 6) {
                refreshTimer = 0;
                RefreshTexts();
            }
        }

        /// <summary>每 6 帧刷一次；语言切换后这里会自动跟上。</summary>
        private void RefreshTexts()
        {
            TrainerConfig cfg = TrainerConfig.Instance;
            if (cfg == null) {
                return;
            }

            titleText.SetText(TrainerLang.Get("UI.PanelTitle"));
            hintText.SetText(TrainerLang.Get("UI.Hint"));
            footerText.SetText(TrainerLang.Get("UI.Footer"));

            for (int i = 0; i < rowTexts.Length; i++) {
                bool on = TrainerKeybinds.Cheats[i].Get(cfg);

                rowTexts[i].SetText(string.Format(
                    "{0,2}. {1}  [{2}]  {3}",
                    i + 1,
                    TrainerKeybinds.NameOf(i),
                    TrainerKeybinds.KeyLabel(i),
                    TrainerLang.Get(on ? "UI.StateOn" : "UI.StateOff")));

                rowTexts[i].TextColor = on ? new Color(126, 224, 126) : new Color(158, 164, 180);
            }

            sliderLabels[0].SetText(TrainerLang.Get("UI.SpeedLabel") + "  x" + cfg.GameSpeed.ToString("0.0"));
            sliderLabels[1].SetText(TrainerLang.Get("UI.DamageLabel") + "  +" + ((int)cfg.DamageBonus));
            sliderLabels[2].SetText(TrainerLang.Get("UI.BrightLabel") + "  " + cfg.Brightness.ToString("0.0"));
        }
    }
}
