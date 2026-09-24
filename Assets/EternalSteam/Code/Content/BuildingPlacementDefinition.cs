using UnityEngine;
namespace EternalSteam
{
    public enum BuildingSurface { Foundation, Ground, GroundOrFoundation }
    // Placement and unlock metadata do not grant a runtime capability.
    [CreateAssetMenu(menuName="Eternal Steam/Placement Profile")]
    public sealed class BuildingPlacementDefinition : ScriptableObject
    {
        public BuildingSurface Surface;
        [Min(1)] public int RequiredNexusLevel=1;
        public bool VerificationSettings;
        public bool RequiresBuildArea;
        public bool RequiresOperationalArea;
        public bool RequiresOwnerBase;
        [Min(1)] public int SnapCells=1;
        public Vector2Int Snap(Vector2Int cell){int step=Mathf.Max(1,SnapCells);return new Vector2Int(Mathf.FloorToInt((float)cell.x/step)*step,Mathf.FloorToInt((float)cell.y/step)*step);}
        public bool IsAligned(Vector2Int cell)=>Snap(cell)==cell;
    }
}
