using UnityEngine;
namespace EternalSteam
{
    [CreateAssetMenu(menuName="Eternal Steam/Enemy Combat Settings")]
    public sealed class EnemyCombatSettings:ScriptableObject
    {
        [Min(.01f)] public float AttackInterval=1;
        [Min(.01f)] public float AttackReach=1;
        [Min(.01f)] public float SearchRadius=10;
        [Min(.01f)] public float SearchInterval=.25f;
        public bool Valid=>float.IsFinite(AttackInterval)&&AttackInterval>0&&float.IsFinite(AttackReach)&&AttackReach>0&&float.IsFinite(SearchRadius)&&SearchRadius>0&&float.IsFinite(SearchInterval)&&SearchInterval>0;
    }
}
