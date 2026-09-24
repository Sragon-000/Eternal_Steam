using System;
using System.Collections.Generic;
using UnityEngine;

namespace EternalSteam
{
    public enum BuildingCategory { Defense, Resource, Installation, Other }

    [CreateAssetMenu(menuName = "Eternal Steam/Building")]
    public sealed class BuildingDefinition : ScriptableObject
    {
        public string Id;
        public string DisplayName;
        public BuildingCategory Category;
        public Vector2Int Footprint = Vector2Int.one;
        public bool Recoverable = true;
        public BuildingPlacementDefinition Placement;
        public GameObject ViewPrefab;
        public List<BuildingModuleDefinition> Modules = new();

        public bool Provides<T>()
        {
            if (Modules == null) return false;
            foreach (var module in Modules) if (module != null && module.Provides(typeof(T))) return true;
            return false;
        }

        public List<string> Validate()
        {
            var errors = new List<string>();
            if (string.IsNullOrWhiteSpace(Id)) errors.Add("Building ID is required.");
            if (string.IsNullOrWhiteSpace(DisplayName)) errors.Add("Display name is required.");
            if (Footprint.x <= 0 || Footprint.y <= 0) errors.Add("Footprint must be positive.");
            if (ViewPrefab == null) errors.Add("View prefab is required.");
            if(Placement!=null && (Placement.RequiredNexusLevel<1 || Placement.SnapCells<1 || !Enum.IsDefined(typeof(BuildingSurface),Placement.Surface)))errors.Add("Invalid placement or unlock profile.");
            var types = new HashSet<Type>();
            if (Modules == null) errors.Add("Module list is missing.");
            else foreach (var module in Modules)
            {
                if (module == null) { errors.Add("Missing module reference."); continue; }
                if (!types.Add(module.GetType())) errors.Add("Duplicate module type: " + module.GetType().Name);
                module.Validate(errors);
                module.ValidateComposition(this, errors);
            }
            return errors;
        }
    }
}
