using System;
using UnityEditor;
using UnityEngine;
using EternalSteam;
public static class ApplyRadialTargeting
{
 public static string Main(){if(Application.isPlaying)throw new Exception("Exit Play first");var catalog=AssetDatabase.LoadAssetAtPath<BuildingCatalog>("Assets/EternalSteam/Content/Buildings/DocumentContent/DocumentBuildings.asset");int count=0;foreach(var d in catalog.Buildings)foreach(var m in d.Modules)if(m is WeaponModuleDefinition w){w.Angle=360;EditorUtility.SetDirty(w);count++;}AssetDatabase.SaveAssets();return "Updated "+count+" weapons to radial targeting; legacy demo behavior remains opt-in unchanged.";}
}
