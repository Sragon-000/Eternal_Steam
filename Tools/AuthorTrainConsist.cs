using System;using System.Linq;using System.IO;using UnityEngine;using UnityEditor;using EternalSteam.OpenWorld;using Newtonsoft.Json;
public static class AuthorTrainConsist
{
 const string Folder="Assets/EternalSteam/Art/Train/",Path=Folder+"TrainConsist.fbx";
 public static string Main(){
  if(EditorApplication.isPlaying)throw new Exception("Edit mode required");
  var importer=(ModelImporter)AssetImporter.GetAtPath(Path);importer.animationType=ModelImporterAnimationType.Legacy;importer.importAnimation=true;importer.importCameras=false;importer.importLights=false;
  var clips=importer.defaultClipAnimations;foreach(var c in clips){c.name="LinkedMotion";c.loopTime=true;c.wrapMode=WrapMode.Loop;}importer.clipAnimations=clips;importer.SaveAndReimport();
  var clip=AssetDatabase.LoadAllAssetsAtPath(Path).OfType<AnimationClip>().Single(c=>!c.name.StartsWith("__preview__"));
  string prefab="Assets/EternalSteam/Content/Buildings/Railway/Train.prefab";var root=PrefabUtility.LoadPrefabContents(prefab);
  try{
   var defense=root.GetComponent<TrainDefenseView>();
   foreach(Transform child in root.transform.Cast<Transform>().ToArray())if(child!=defense.Turret&&child!=defense.ShotLine.transform)UnityEngine.Object.DestroyImmediate(child.gameObject);
   var model=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Path),root.transform);model.name="TrainConsist";model.transform.localPosition=Vector3.down*.55f;model.transform.localRotation=Quaternion.Euler(0,-90,0);model.transform.localScale=Vector3.one*.38f;
   var animation=model.GetComponent<Animation>();if(animation!=null){animation.playAutomatically=false;animation.enabled=false;}
   var materials=AssetDatabase.LoadAllAssetsAtPath(Path).OfType<Material>().ToArray();
   foreach(var renderer in model.GetComponentsInChildren<Renderer>(true))renderer.sharedMaterials=renderer.sharedMaterials.Select(m=>{
    string safe=m.name.Replace("|","-");string p=Folder+safe+".mat";var a=AssetDatabase.LoadAssetAtPath<Material>(p);if(a==null){a=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(a,p);}var col=m.HasProperty("_Color")?m.color:Color.gray;a.SetColor("_BaseColor",col);a.SetFloat("_Metallic",m.name.Contains("Glass")?.25f:.5f);a.SetFloat("_Smoothness",.28f);EditorUtility.SetDirty(a);return a;}).ToArray();
   Transform Find(string prefix)=>model.GetComponentsInChildren<Transform>(true).Single(t=>t.name.StartsWith(prefix));
   var view=root.GetComponent<TrainConsistView>()??root.AddComponent<TrainConsistView>();view.Model=model;view.LinkedMotion=clip;view.LocomotiveRear=Find("LOCOMOTIVE_REAR_SOCKET");view.CargoFront=Find("CARGO_FRONT_SOCKET");view.CargoRear=Find("CARGO_REAR_SOCKET");view.FoundationFront=Find("FOUNDATION_FRONT_SOCKET");view.TurretMount=Find("TURRET_MOUNT");view.MetresPerCycle=2*Mathf.PI*.7f*.38f;
   defense.Turret.localPosition=root.transform.InverseTransformPoint(view.TurretMount.position)+Vector3.up*.1f;defense.Turret.localScale=new Vector3(.6f,.3f,.6f);
   foreach(var col in root.GetComponentsInChildren<Collider>(true))UnityEngine.Object.DestroyImmediate(col);
   PrefabUtility.SaveAsPrefabAsset(root,prefab);AssetDatabase.SaveAssets();
   var report=JsonConvert.SerializeObject(new{prefab,meshes=model.GetComponentsInChildren<MeshRenderer>().Length,clip=clip.name,seconds=clip.length,scale=.38,forward="+Z",socketGap1=Vector3.Distance(view.LocomotiveRear.position,view.CargoFront.position),socketGap2=Vector3.Distance(view.CargoRear.position,view.FoundationFront.position),mount=defense.Turret.localPosition.ToString()},Formatting.Indented);
   File.WriteAllText("Docs/Measurements/2026-10-02-train-hud-integration/train-authoring.json",report);return report;
  }finally{PrefabUtility.UnloadPrefabContents(root);}
 }
}
