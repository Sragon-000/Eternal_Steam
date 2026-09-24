using System;
using UnityEditor;
using UnityEngine;
using EternalSteam;
public static class ApplyNexusGroundDefense
{
 public static string Main(){if(Application.isPlaying)throw new Exception("Exit Play first");var catalog=AssetDatabase.LoadAssetAtPath<BuildingCatalog>("Assets/EternalSteam/Content/Buildings/DocumentContent/DocumentBuildings.asset");int n=0;foreach(var d in catalog.Buildings)if(d.Category==BuildingCategory.Defense){d.Placement.Surface=BuildingSurface.GroundOrFoundation;d.Placement.RequiresBuildArea=true;EditorUtility.SetDirty(d.Placement);n++;}AssetDatabase.SaveAssets();return "Enabled ground or foundation placement within nexus area for "+n+" defense definitions.";}
}
