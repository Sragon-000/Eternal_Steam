using System;
using UnityEngine;
using UnityEditor;
using EternalSteam;
public static class ApplyNexusConstruction
{
 const string Root="Assets/EternalSteam/Content/Buildings/DocumentContent/";
 static T Asset<T>(string name) where T:ScriptableObject{var p=Root+name+".asset";var a=AssetDatabase.LoadAssetAtPath<T>(p);if(!a){a=ScriptableObject.CreateInstance<T>();AssetDatabase.CreateAsset(a,p);}return a;}
 public static string Main(){if(Application.isPlaying)throw new Exception("Exit Play first");
 var d=Asset<BuildingDefinition>("installation.nexus");d.Id="installation.nexus";d.DisplayName="넥서스";d.Category=BuildingCategory.Installation;d.Footprint=new Vector2Int(4,4);d.Recoverable=true;
 var placement=Asset<BuildingPlacementDefinition>("installation.nexus.placement");placement.Surface=BuildingSurface.Ground;placement.SnapCells=4;placement.RequiredNexusLevel=1;placement.VerificationSettings=true;d.Placement=placement;
 var area=Asset<BuildAreaModuleDefinition>("installation.nexus.area");d.Modules.Clear();d.Modules.Add(area);
 var prefab=Root+"installation.nexus.prefab";d.ViewPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(prefab);
 if(!d.ViewPrefab){var g=new GameObject("Nexus");var steel=AssetDatabase.LoadAssetAtPath<Material>(Root+"Model_Steel.mat");var glow=AssetDatabase.LoadAssetAtPath<Material>(Root+"resource.nanometal.mat");
 Action<PrimitiveType,Vector3,Vector3,Material> part=(type,pos,scale,mat)=>{var p=GameObject.CreatePrimitive(type);p.transform.SetParent(g.transform,false);p.transform.localPosition=pos;p.transform.localScale=scale;p.GetComponent<Renderer>().sharedMaterial=mat;UnityEngine.Object.DestroyImmediate(p.GetComponent<Collider>());};
 part(PrimitiveType.Cube,new Vector3(0,.2f,0),new Vector3(3.6f,.4f,3.6f),steel);part(PrimitiveType.Cylinder,new Vector3(0,.8f,0),new Vector3(2,.6f,2),steel);part(PrimitiveType.Sphere,new Vector3(0,1.9f,0),Vector3.one*1.25f,glow);
 for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2)part(PrimitiveType.Cube,new Vector3(x*1.2f,1.35f,z*1.2f),new Vector3(.35f,2.3f,.35f),steel);
 var c=g.AddComponent<BoxCollider>();c.center=new Vector3(0,1.3f,0);c.size=new Vector3(3.6f,2.6f,3.6f);d.ViewPrefab=PrefabUtility.SaveAsPrefabAsset(g,prefab);UnityEngine.Object.DestroyImmediate(g);}
 // Preserve the prefab GUID while sizing the authored model to exactly one foundation.
 var model=PrefabUtility.LoadPrefabContents(prefab);
 try{
  var plate=model.transform.GetChild(0);float ratio=8f/plate.localScale.x;
  for(int i=1;i<model.transform.childCount;i++){var t=model.transform.GetChild(i);var pos=t.localPosition;pos.x*=ratio;pos.z*=ratio;t.localPosition=pos;var scale=t.localScale;scale.x*=ratio;scale.z*=ratio;t.localScale=scale;}
  plate.localPosition=new Vector3(0,.1f,0);plate.localScale=new Vector3(8,.4f,8);
  var box=model.GetComponent<BoxCollider>();box.center=new Vector3(0,1.2f,0);box.size=new Vector3(8,2.6f,8);
  PrefabUtility.SaveAsPrefabAsset(model,prefab);
 }finally{PrefabUtility.UnloadPrefabContents(model);}
 var catalog=Asset<BuildingCatalog>("DocumentBuildings");if(!catalog.Buildings.Contains(d))catalog.Buildings.Insert(0,d);
 foreach(var a in new UnityEngine.Object[]{d,placement,area,catalog})EditorUtility.SetDirty(a);var errors=catalog.Validate();if(errors.Count>0)throw new Exception(string.Join("\n",errors));AssetDatabase.SaveAssets();return "Nexus registered in installation tab; ground 4x4 cells, 8m snap and 8x8m model; configurable 40m build radius.";
 }
}
