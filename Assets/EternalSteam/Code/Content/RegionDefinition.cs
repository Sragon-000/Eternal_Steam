using UnityEngine;
namespace EternalSteam
{
    // Region metadata for the start-map validation. Production/rounds are separate consumers.
    [CreateAssetMenu(menuName="Eternal Steam/World Region")]
    public sealed class RegionDefinition:ScriptableObject
    {
        public string Id;
        public string DisplayName;
        public Rect Bounds;
        public string ResourceId;
        public bool VerificationSettings=true;
        public bool Contains(Vector3 position)=>Bounds.Contains(new Vector2(position.x,position.z));
    }
}
