using System;
namespace EternalSteam
{
    public enum MapStage { Gathering, WaitingForNight, BossBattle, AwaitingCraft, Cleared, Failed }

    // Integer milli-energy units: UI rounding never controls progression.
    [Serializable]
    public sealed class MapEnergyState
    {
        public string MapId;
        public long Target, Extracted, KillReward, BossReward, Balance;
        public long[] Remaining;
        public MapStage Stage;
        public bool IncompleteOrb, PerfectOrb;
        public long Earned => Extracted + KillReward + BossReward;
        public long PreBossLimit => Target / 10 * 9;
        public MapEnergyState(string mapId, long target, int cells, long perCell)
        {
            if(string.IsNullOrWhiteSpace(mapId)||target<=0||target%10!=0||cells<=0||perCell<0)throw new ArgumentException("Invalid map energy configuration");
            MapId=mapId;Target=target;Remaining=new long[cells];Array.Fill(Remaining,perCell);
        }
        long Credit(long requested)
        {
            if(Stage!=MapStage.Gathering||requested<=0)return 0;
            long paid=Math.Min(requested,PreBossLimit-Earned);Balance+=paid;return paid;
        }
        public long Extract(int cell,long requested)
        {
            if(cell<0||cell>=Remaining.Length)throw new ArgumentOutOfRangeException(nameof(cell));
            long paid=Credit(Math.Min(Remaining[cell],requested));Remaining[cell]-=paid;Extracted+=paid;return paid;
        }
        public long RewardKill(long requested){long paid=Credit(requested);KillReward+=paid;return paid;}
        public void Advance(bool night)
        {
            if(Stage==MapStage.Gathering&&Earned==PreBossLimit)Stage=MapStage.WaitingForNight;
            if(Stage==MapStage.WaitingForNight&&night)Stage=MapStage.BossBattle;
        }
        public bool DefeatBoss()
        {
            if(Stage!=MapStage.BossBattle||BossReward!=0)return false;
            BossReward=Target/10;Balance+=BossReward;IncompleteOrb=true;Stage=MapStage.AwaitingCraft;return true;
        }
        public bool Craft(string mapId)
        {
            if(mapId!=MapId||Stage!=MapStage.AwaitingCraft||!IncompleteOrb||Balance<Target)return false;
            Balance-=Target;IncompleteOrb=false;PerfectOrb=true;Stage=MapStage.Cleared;return true;
        }
        public void Fail(){if(Stage!=MapStage.Cleared)Stage=MapStage.Failed;}
    }
}
