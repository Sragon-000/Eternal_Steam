using System;
using System.Collections.Generic;
using UnityEngine;

namespace EternalSteam
{
    [CreateAssetMenu(menuName = "Eternal Steam/Building Catalog")]
    public sealed class BuildingCatalog : ScriptableObject
    {
        public List<BuildingDefinition> Buildings = new();

        public List<string> Validate()
        {
            var errors = new List<string>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            if (Buildings == null) { errors.Add("Catalog list is missing."); return errors; }
            foreach (var building in Buildings)
            {
                if (building == null) { errors.Add("Missing building reference."); continue; }
                if (!ids.Add(building.Id ?? "")) errors.Add("Duplicate building ID: " + building.Id);
                foreach (var error in building.Validate()) errors.Add(building.name + ": " + error);
            }
            return errors;
        }
    }
}
