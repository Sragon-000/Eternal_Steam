using UnityEngine;
namespace EternalSteam.OpenWorld
{
    // The scene owns the fixed model; the normal building transaction adopts it on startup.
    public sealed class SceneStartingBase : MonoBehaviour
    {
        public BuildingDefinition Definition;
        public Vector2Int Cell;
    }
}
