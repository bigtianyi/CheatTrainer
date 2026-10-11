using Terraria.ModLoader;
using CheatTrainer.Common.Systems;

namespace CheatTrainer
{
    /// <summary>
    /// 模组入口。所有实际逻辑都在 Common/ 下面：
    ///   Configs/TrainerConfig   —— 12 个开关 + 3 个数值 + 语言选项，会被持久化
    ///   Players/TrainerPlayer   —— 每帧生效的状态类作弊（1~7、11）
    ///   Systems/TrainerHooks    —— 全亮 / 免材料合成 / 无视制作站 / 变速（8~10、12）
    ///   Systems/TrainerKeybinds —— 热键注册与处理
    ///   Systems/TrainerLang     —— 中英双语取词（本地化在 Localization/*.hjson）
    ///   UI/                     —— 悬浮控制面板
    /// </summary>
    public class CheatTrainer : Mod
    {
        public override void Load()
        {
            // 尽早挂上，模组配置界面的文字也要走它
            TrainerLang.Load();
        }

        public override void Unload()
        {
            TrainerLang.Unload();
        }
    }
}
