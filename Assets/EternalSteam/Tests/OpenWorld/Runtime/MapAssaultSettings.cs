using UnityEngine;
namespace EternalSteam.OpenWorld
{
    [CreateAssetMenu(menuName="Eternal Steam/Verification/Map Assault")]
    public sealed class MapAssaultSettings:ScriptableObject
    {
        public string MapId="start-region",EnergyName="시작 지역 에너지";
        [Header("검증 수치 · 에너지 단위는 1/1000")]
        [Min(10)] public int Target=1000000;
        [Min(1)] public int EnergyPerCell=10000,ExtractionPerSecond=10,KillEnergy=1000;
        [Header("검증 수치 · 적 능력치는 DAY와 무관")]
        [Min(1)] public int FirstNightCount=100,AdditionalPerDay=25,BossHealth=1000;
        [Min(.1f)] public float SpawnInterval=.2f;
        public int Seed=20260920;
        public Vector3 BossPosition;
        public Vector2Int NormalSpawnSize=Vector2Int.one;
        public Vector2Int BossSpawnSize=new Vector2Int(3,3);
        public Vector3[] BossWavePoints=new Vector3[8];
        public void ValidateConfiguration(){if(NormalSpawnSize!=Vector2Int.one||BossSpawnSize.x<=0||BossSpawnSize.y<=0||(long)BossSpawnSize.x*BossSpawnSize.y!=9||!float.IsFinite(BossPosition.x)||!float.IsFinite(BossPosition.y)||!float.IsFinite(BossPosition.z)||string.IsNullOrWhiteSpace(MapId)||Target<=0||Target%10!=0||EnergyPerCell<1||ExtractionPerSecond<1||KillEnergy<0||FirstNightCount<1||AdditionalPerDay<0||BossHealth<1||!float.IsFinite(SpawnInterval)||SpawnInterval<=0||BossWavePoints==null||BossWavePoints.Length!=8)throw new System.InvalidOperationException("Invalid map assault verification settings");}
    }
}
