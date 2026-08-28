using PregnantLordsExpanded.Campaign;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace PregnantLordsExpanded
{
    public sealed class SubModule : MBSubModuleBase
    {
        protected override void OnGameStart(Game game, IGameStarter gameStarter)
        {
            base.OnGameStart(game, gameStarter);

            CampaignGameStarter campaignStarter = gameStarter as CampaignGameStarter;
            if (campaignStarter != null)
            {
                campaignStarter.AddBehavior(new PregnancyProgressBehavior());
            }
        }
    }
}
