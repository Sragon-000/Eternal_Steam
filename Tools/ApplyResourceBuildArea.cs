using System;
using UnityEditor;
using UnityEngine;
using EternalSteam;
public static class ApplyResourceBuildArea
{
 public static string Main(){if(Application.isPlaying)throw new Exception("Exit Play before updating definitions");var catalog=AssetDatabase.LoadAssetAtPath<BuildingCatalog>("Assets/EternalSteam/Content/Buildings/DocumentContent/DocumentBuildings.asset");int n=0;
 foreach(var d in catalog.Buildings)if(d.Category==BuildingCategory.Resource){if(d.Placement==null)throw new Exception("Missing placement profile");d.Placement.RequiresBuildArea=true;EditorUtility.SetDirty(d.Placement);n++;}AssetDatabase.SaveAssets();return "Enabled nexus coverage requirement for "+n+" resource definitions.";}
}
