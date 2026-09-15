using UnityEngine;
namespace EternalSteam.OpenWorld
{
    // Serialized scene geometry. Edit-mode construction is owned by the Editor tool.
    public sealed class SceneFoundation : MonoBehaviour
    {
        public float Top => transform.position.y + transform.lossyScale.y * .5f;
        public Vector2Int Key => FoundationPlacement.Key(transform.position);
    }
}
