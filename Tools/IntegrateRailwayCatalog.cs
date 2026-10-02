using System;using System.Linq;using System.IO;using UnityEngine;using UnityEditor;using UnityEditor.Events;using UnityEditor.SceneManagement;using TMPro;using EternalSteam.OpenWorld;using Newtonsoft.Json.Linq;
public static class IntegrateRailwayCatalog{
 public static string Main(){if(EditorApplication.isPlaying||EditorSceneManager.GetActiveScene().isDirty)throw new Exception("Clean edit mode required");string original=EditorSceneManager.GetActiveScene().path;var results=new JArray();
 try{foreach(var name in new[]{"StartRegionSandbox","OpenWorldSandbox"}){var scene=EditorSceneManager.OpenScene("Assets/EternalSteam/Scene/Tests/"+name+".unity");var h=UnityEngine.Object.FindFirstObjectByType<CanvasWorldHud>();var railway=h.Sandbox.RailwayHud;var railIcon=AssetDatabase.LoadAllAssetsAtPath("Assets/EternalSteam/Shared/UI/Railway/RailwayIcon.png").OfType<Sprite>().Single();var coalIcon=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/EternalSteam/Shared/UI/ReferenceHUD/coal.png")??railIcon;
 foreach(var d in new[]{railway.Station,railway.Track,railway.CoalProducer}){
  var icon=d==railway.CoalProducer?coalIcon:railIcon;
  if(!h.Portraits.Any(p=>p.DefinitionId==d.Id))h.Portraits=h.Portraits.Append(new CanvasWorldHud.PortraitBinding{DefinitionId=d.Id,Sprite=icon}).ToArray();
  if(h.Catalog.Any(e=>e.Definition==d)){h.Catalog.Single(e=>e.Definition==d).View.GetComponentInChildren<TMP_Text>().text=d.DisplayName.Replace("검증비 ","").Replace(" · 검증용","");continue;}
  var template=h.Catalog[0];var b=UnityEngine.Object.Instantiate(template.View,h.CatalogLayout.transform);b.name="Build_"+d.Id;var image=b.GetComponentsInChildren<UnityEngine.UI.Image>().Single(i=>i.name=="Preview");image.sprite=icon;image.preserveAspect=true;
  var label=b.GetComponentInChildren<TMP_Text>();label.text=d.DisplayName.Replace("검증비 ","").Replace(" · 검증용","");while(b.onClick.GetPersistentEventCount()>0)UnityEventTools.RemovePersistentListener(b.onClick,0);UnityEventTools.AddStringPersistentListener(b.onClick,h.Execute,"build:"+h.Catalog.Length);
  h.Catalog=h.Catalog.Append(new CanvasWorldHud.CatalogEntry{Definition=d,Category=d.Category,View=b,Icon=image}).ToArray();
 }
 EditorUtility.SetDirty(h);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);results.Add(new JObject{{"scene",name},{"catalogEntries",h.Catalog.Length},{"railwayCards",3}});
 }}finally{EditorSceneManager.OpenScene(original);}File.WriteAllText("Docs/Measurements/2026-10-02-train-hud-integration/catalog-integration.json",results.ToString());return results.ToString();}
}
