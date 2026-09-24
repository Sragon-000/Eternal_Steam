using System;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using EternalSteam;
using EternalSteam.OpenWorld;
public static class ApplyDocumentContent
{
 const string Root="Assets/EternalSteam/Content/Buildings/DocumentContent";
 static T Asset<T>(string name) where T:ScriptableObject {var path=Root+"/"+name+".asset";var result=AssetDatabase.LoadAssetAtPath<T>(path);if(result!=null)return result;result=ScriptableObject.CreateInstance<T>();AssetDatabase.CreateAsset(result,path);return result;}
 static void Folder(string path){if(AssetDatabase.IsValidFolder(path))return;int i=path.LastIndexOf('/');Folder(path.Substring(0,i));AssetDatabase.CreateFolder(path.Substring(0,i),path.Substring(i+1));}
 static BuildingDefinition Building(string id,string name,BuildingCategory category,int size,int unlock,Material source)
 {
  var d=Asset<BuildingDefinition>(id);d.Id=id;d.DisplayName=name;d.Category=category;d.Footprint=new Vector2Int(size,size);d.Modules.Clear();
  var placement=Asset<BuildingPlacementDefinition>(id+".placement");placement.Surface=category==BuildingCategory.Resource?BuildingSurface.Ground:BuildingSurface.GroundOrFoundation;placement.RequiredNexusLevel=unlock;placement.RequiresBuildArea=category==BuildingCategory.Resource||category==BuildingCategory.Defense;placement.VerificationSettings=true;d.Placement=placement;EditorUtility.SetDirty(placement);
  var path=Root+"/"+id+".prefab";d.ViewPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
  if(d.ViewPrefab==null){var material=new Material(source);material.SetColor("_BaseColor",category==BuildingCategory.Resource?new Color(.7f,.55f,.2f):Color.HSVToRGB(unlock/9f,.65f,.85f));AssetDatabase.CreateAsset(material,Root+"/"+id+".mat");var root=new GameObject(name);var body=GameObject.CreatePrimitive(category==BuildingCategory.Resource?PrimitiveType.Cylinder:PrimitiveType.Cube);body.transform.SetParent(root.transform,false);body.transform.localPosition=new Vector3(0,.65f,0);body.transform.localScale=new Vector3(size*1.7f,.65f,size*1.7f);body.GetComponent<Renderer>().sharedMaterial=material;d.ViewPrefab=PrefabUtility.SaveAsPrefabAsset(root,path);UnityEngine.Object.DestroyImmediate(root);}
  EditorUtility.SetDirty(d);return d;
 }
 public static string Main()
 {
  if(Application.isPlaying)throw new Exception("Stop Play first");Folder(Root);
  var world=UnityEngine.Object.FindFirstObjectByType<OpenWorldSandbox>();if(world==null)throw new Exception("Open world scene required");
  var catalog=Asset<BuildingCatalog>("DocumentBuildings");catalog.Buildings.Clear();
  string[] resources={"iron","copper","titanium","tungsten","uranium","plasma_ore","nanometal"};string[] names={"철","구리","티타늄","텅스텐","우라늄","플라즈마","나노메탈"};float[] intervals={2,5,10,20,45,60,120};
  for(int i=0;i<7;i++){var d=Building("resource."+resources[i],names[i]+" 생성기",BuildingCategory.Resource,1,i+1,world.TowerMaterial);var production=Asset<ProductionModuleDefinition>(d.Id+".production");production.InputAmount=0;production.OutputId=resources[i];production.OutputAmount=1;production.Interval=intervals[i];EditorUtility.SetDirty(production);d.Modules.Add(production);catalog.Buildings.Add(d);EditorUtility.SetDirty(d);}
  string[] ids={"tesla","plasma_laser","vulcan_aa","rail_aa","arc","railgun","sky_plasma","smart_missile","emp"};
  string[] titles={"테슬라 타워","플라즈마 레이저 포탑","발칸 대공포","레일 대공포","아크 블래스터","레일건 터렛","스카이 플라즈마 포탑","스마트 미사일 발사대","EMP 펄스 타워"};
  int[] sizes={1,1,1,1,2,2,2,3,3},unlocks={1,3,5,7,2,4,4,6,8},levels={10,10,10,10,7,7,7,3,3};
  float[] coefficient={.8f,.25f,.15f,2.5f,1.5f,3,1.8f,2,0},ranges={15,45,25,100,40,60,50,70,25},angles={60,30,360,15,360,180,360,360,360},periods={4,.5f,.2f,5,3,5,4,6,10};
  WeaponDelivery[] deliveries={WeaponDelivery.Chain,WeaponDelivery.Instant,WeaponDelivery.Instant,WeaponDelivery.Pierce,WeaponDelivery.Projectile,WeaponDelivery.Pierce,WeaponDelivery.Projectile,WeaponDelivery.Projectile,WeaponDelivery.Pulse};
  for(int i=0;i<9;i++){
   var d=Building("defense."+ids[i],titles[i],BuildingCategory.Defense,sizes[i],unlocks[i],world.TowerMaterial);
   var w=Asset<WeaponModuleDefinition>(d.Id+".weapon");w.BaseDamage=100;w.DamageCoefficient=coefficient[i];w.Range=ranges[i];w.Angle=360;w.Interval=periods[i];w.Delivery=deliveries[i];w.Schedule=i==1?WeaponSchedule.Sustained:i==2?WeaponSchedule.Burst:WeaponSchedule.Periodic;w.Source=i==1?WeaponDamageSource.PlasmaLaser:WeaponDamageSource.Normal;w.Targets=i==2||i==6?TargetKind.Air:TargetKind.All;w.Status=i==1?WeaponStatus.Overheat:i==6?WeaponStatus.Burn:i==8?WeaponStatus.Stun:WeaponStatus.None;w.StatusDuration=i==8?2:3;w.Radius=i==4?8:i==6?7:i==7?5:0;w.MaximumTargets=i==7?3:1;w.MaximumHits=i==3?100000:3;w.JumpRange=8;w.Retention=.2f;w.JumpInterval=.5f;w.ChainDuration=5;w.TickDamage=10;EditorUtility.SetDirty(w);
   var upgrade=Asset<PerformanceUpgradeDefinition>(d.Id+".upgrade");upgrade.MaximumLevel=levels[i];EditorUtility.SetDirty(upgrade);d.Modules.Add(w);d.Modules.Add(upgrade);catalog.Buildings.Add(d);EditorUtility.SetDirty(d);
  }
  var nexus=AssetDatabase.LoadAssetAtPath<BuildingDefinition>(Root+"/installation.nexus.asset");if(nexus!=null)catalog.Buildings.Insert(0,nexus);
  world.ContentCatalog=catalog;EditorUtility.SetDirty(catalog);EditorUtility.SetDirty(world);var errors=catalog.Validate();if(errors.Count>0)throw new Exception(string.Join("\n",errors));AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(world.gameObject.scene);EditorSceneManager.SaveScene(world.gameObject.scene);return "Created 7 resource + 9 defense definitions, profiles, modules and prefab assets; catalog assigned to scene. Verification tuning only.";
 }
}
