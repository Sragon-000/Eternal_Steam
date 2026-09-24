using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using EternalSteam;
using EternalSteam.OpenWorld;
using EternalSteam.Demo;
public static class VerifyBuildingCombatConfiguration
{
 public static string Main(){
  int count=0;foreach(var path in new[]{"Assets/EternalSteam/Content/Buildings/StartRegion/StartRegionBuildings.asset","Assets/EternalSteam/Content/Buildings/DocumentContent/DocumentBuildings.asset"}){
   var catalog=AssetDatabase.LoadAssetAtPath<BuildingCatalog>(path);if(catalog.Validate().Count!=0)throw new Exception("Invalid catalog");
   foreach(var d in catalog.Buildings){var body=d.Modules.OfType<BuildingCombatDefinition>().Single();var health=d.Modules.OfType<HealthModuleDefinition>().Single();float expected=body.Role==BuildingCombatRole.Nexus?1000:body.Role==BuildingCombatRole.General?50:100;if(health.Maximum!=expected||!body.BlocksGround)throw new Exception(d.Id+" incorrect health/body");count++;}
   var wall=catalog.Buildings.Single(d=>d.Id=="installation.wall");if(wall.Footprint!=Vector2Int.one||wall.Placement.Surface!=BuildingSurface.GroundOrFoundation||wall.Placement.RequiresOperationalArea||wall.Placement.RequiresBuildArea||wall.Placement.RequiresOwnerBase)throw new Exception("Wall placement config");
  }
  var scene=UnityEngine.Object.FindFirstObjectByType<OpenWorldSandbox>();if(scene==null||scene.EnemyAttacks==null||scene.EnemyCombat==null||!scene.EnemyCombat.Valid||scene.Enemies.ArrivalPolicy!=EnemyArrivalPolicy.Remain)throw new Exception("Open-world host not wired");
  foreach(HordeTowerKind kind in Enum.GetValues(typeof(HordeTowerKind))){var d=scene.Foundations.Definition(kind);if(d.Modules.OfType<HealthModuleDefinition>().Single().Maximum!=100||d.Modules.OfType<BuildingCombatDefinition>().Single().Role!=BuildingCombatRole.Defense)throw new Exception("Legacy health/body "+kind);}
  return "PASS "+scene.gameObject.scene.name+": enemy attack host/settings/arrival policy, 4 legacy defense bodies, "+count+" catalog entries with expected health and explicit roles, wall one-cell ground/foundation unrestricted placement.";
 }
}
