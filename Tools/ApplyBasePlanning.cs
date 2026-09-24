using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using EternalSteam;
using EternalSteam.OpenWorld;
public static class ApplyBasePlanning
{
 const string Folder="Assets/EternalSteam/Content/Buildings/BasePlanning";
 static T Save<T>(T asset,string name) where T:UnityEngine.Object {AssetDatabase.CreateAsset(asset,Folder+"/"+name+".asset");return asset;}
 public static string Main(){
  var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();if(Application.isPlaying||scene.isDirty||!scene.path.EndsWith("StartRegionSandbox.unity"))throw new Exception("Clean original start scene required");
  if(AssetDatabase.IsValidFolder(Folder))throw new Exception("BasePlanning already exists; do not overwrite");AssetDatabase.CreateFolder("Assets/EternalSteam/Content/Buildings","BasePlanning");
  var s=UnityEngine.Object.FindFirstObjectByType<OpenWorldSandbox>();var old=s.ContentCatalog;var source=old.Buildings.Single(d=>d.Id=="installation.nexus");
  BuildingDefinition Make(string id,string title,BaseRole role){
   var d=UnityEngine.Object.Instantiate(source);d.Id=id;d.DisplayName=title;d.Recoverable=role!=BaseRole.Main;d.Footprint=role==BaseRole.Sub?new Vector2Int(3,3):source.Footprint;
   var profile=UnityEngine.Object.Instantiate(source.Placement);profile.Surface=BuildingSurface.Ground;profile.RequiredNexusLevel=1;profile.SnapCells=role==BaseRole.Main?source.Placement.SnapCells:1;profile.RequiresOwnerBase=false;profile.RequiresOperationalArea=false;profile.RequiresBuildArea=false;profile.VerificationSettings=true;d.Placement=Save(profile,role+".placement");
   d.Modules.RemoveAll(m=>m is BaseModuleDefinition||m is BuildAreaModuleDefinition||m is HealthModuleDefinition||m is UpgradeModuleDefinition||m is PerformanceUpgradeDefinition);
   var identity=ScriptableObject.CreateInstance<BaseModuleDefinition>();identity.Role=role;d.Modules.Add(Save(identity,role+".identity"));
   var area=ScriptableObject.CreateInstance<BuildAreaModuleDefinition>();area.Shape=BuildAreaShape.Square;area.Radius=11;area.Yaw=45;area.RadiusPerLevel=role==BaseRole.Sub?1:0;d.Modules.Add(Save(area,role+".area"));
   var health=ScriptableObject.CreateInstance<HealthModuleDefinition>();health.Maximum=100;d.Modules.Add(Save(health,role+".health"));
   var upgrade=ScriptableObject.CreateInstance<UpgradeModuleDefinition>();upgrade.MaximumLevel=10;upgrade.RequireNexus=role==BaseRole.Sub;upgrade.DamagePerLevel=0;upgrade.HealthPerLevel=role==BaseRole.Sub?.2f:0;d.Modules.Add(Save(upgrade,role+".upgrade"));
   var view=(GameObject)PrefabUtility.InstantiatePrefab(source.ViewPrefab);view.name=title;if(role==BaseRole.Sub)view.transform.localScale*=.75f;d.ViewPrefab=PrefabUtility.SaveAsPrefabAsset(view,Folder+"/"+role+".prefab");UnityEngine.Object.DestroyImmediate(view);
   return Save(d,role.ToString());
  }
  var main=Make("installation.main_base","메인 기지",BaseRole.Main);var sub=Make("installation.nexus","서브 기지",BaseRole.Sub);
  var catalog=UnityEngine.Object.Instantiate(old);catalog.Buildings=catalog.Buildings.Select(d=>ReferenceEquals(d,source)?sub:d).ToList();catalog.Buildings.Insert(0,main);
  for(int i=0;i<catalog.Buildings.Count;i++){
   var original=catalog.Buildings[i];if(!original.Modules.Any(m=>m is ProductionModuleDefinition))continue;
   var resource=UnityEngine.Object.Instantiate(original);var placement=UnityEngine.Object.Instantiate(original.Placement);
   placement.RequiresBuildArea=false;placement.RequiresOperationalArea=true;placement.RequiresOwnerBase=true;
   resource.Placement=Save(placement,original.Id+".placement");catalog.Buildings[i]=Save(resource,original.Id);
  }
  var errors=catalog.Validate();if(errors.Count>0)throw new Exception(string.Join("\n",errors));s.ContentCatalog=Save(catalog,"Catalog");s.BasePlanningRules=true;EditorUtility.SetDirty(s);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
  return "Created isolated base catalog. Main: one, 11 cells fixed, HP100. Sub: 3x3, 11..20 cells, HP100..280, limits 5/10. StartRegion only; 2m cell and main footprint retained as verification settings.";
 }
}
