using System;
namespace EternalSteam
{
    // Inject one campaign into multiple map contexts. No scene or static ownership.
    [Serializable]
    public sealed class CampaignProgression:ICampaignMainLevel
    {
        [Saved(1,10)] public int MainLevel {get;private set;}=1;
        public const int MaximumLevel=10;
        public bool TryUpgrade(int expectedLevel)
        {if(expectedLevel!=MainLevel||MainLevel>=MaximumLevel)return false;MainLevel++;return true;}
        public void MergeMainLevel(int level)
        {if(level<1||level>MaximumLevel)throw new ArgumentOutOfRangeException(nameof(level));MainLevel=Math.Max(MainLevel,level);}
    }
}
