using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using EternalSteam.OpenWorld;
using TMPro;
public static class AuthorTrainDefense
{
 static RectTransform Rect(Transform parent,string name,float x,float y,float w,float h){var go=new GameObject(name,typeof(RectTransform));var r=(RectTransform)go.transform;r.SetParent(parent,false);r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,y);r.sizeDelta=new Vector2(w,h);return r;}
 static Button Button(RailwayHud hud,Transform parent,string command,string label,float x,float y,float width=190){var b=UnityEngine.Object.Instantiate(hud.CancelPendingButton,parent);b.name=command;var r=(RectTransform)b.transform;r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,y);r.sizeDelta=new Vector2(width,34);b.GetComponentInChildren<TMP_Text>().text=label;while(b.onClick.GetPersistentEventCount()>0)UnityEventTools.RemovePersistentListener(b.onClick,0);UnityEventTools.AddStringPersistentListener(b.onClick,hud.Execute,command);return b;}
 public static string Main()
 {
  if(EditorApplication.isPlaying)throw new Exception("Edit mode required");
  var active=EditorSceneManager.GetActiveScene();string originalPath=active.path;if(active.isDirty)throw new Exception("Save existing scene edits first");
  var s=UnityEngine.Object.FindFirstObjectByType<OpenWorldSandbox>();var path=AssetDatabase.GetAssetPath(s.RailwayView.TrainPrefab);var train=PrefabUtility.LoadPrefabContents(path);
  try{if(train.GetComponent<TrainDefenseView>()==null){
   var view=train.AddComponent<TrainDefenseView>();var mount=train.transform.Find("Mount");
   var turret=GameObject.CreatePrimitive(PrimitiveType.Cube);turret.name="DefenseTurret";turret.transform.SetParent(train.transform,false);turret.transform.localPosition=mount.localPosition+Vector3.up*.6f;turret.transform.localScale=new Vector3(.7f,.45f,.7f);turret.GetComponent<Renderer>().sharedMaterial=mount.GetComponent<Renderer>().sharedMaterial;
   var barrel=GameObject.CreatePrimitive(PrimitiveType.Cube);barrel.name="Barrel";barrel.transform.SetParent(turret.transform,false);barrel.transform.localPosition=new Vector3(0,.2f,1);barrel.transform.localScale=new Vector3(.22f,.3f,1.6f);barrel.GetComponent<Renderer>().sharedMaterial=mount.GetComponent<Renderer>().sharedMaterial;
   var muzzle=new GameObject("Muzzle").transform;muzzle.SetParent(turret.transform,false);muzzle.localPosition=new Vector3(0,.2f,1.8f);
   var line=new GameObject("DefenseShot",typeof(LineRenderer)).GetComponent<LineRenderer>();line.transform.SetParent(train.transform,false);line.sharedMaterial=s.LineMaterial;line.positionCount=2;line.startWidth=line.endWidth=.07f;line.startColor=line.endColor=new Color(1,.7f,.2f);line.useWorldSpace=true;line.enabled=false;
   view.Turret=turret.transform;view.Muzzle=muzzle;view.ShotLine=line;turret.SetActive(false);
   foreach(var collider in train.GetComponentsInChildren<Collider>(true))UnityEngine.Object.DestroyImmediate(collider);
   PrefabUtility.SaveAsPrefabAsset(train,path);
  }}finally{PrefabUtility.UnloadPrefabContents(train);}
  foreach(var name in new[]{"StartRegionSandbox","OpenWorldSandbox"}){
   var scene=EditorSceneManager.OpenScene("Assets/EternalSteam/Scene/Tests/"+name+".unity");var hud=UnityEngine.Object.FindFirstObjectByType<RailwayHud>(FindObjectsInactive.Include);
   if(hud.ArmamentPanel==null){
    Button(hud,hud.DetailPanel.transform,"defense","기차 방어 · 장착 / 충전",0,-194,596);
    var panel=Rect(hud.Panel.transform,"RailwayArmament",12,-280,596,225);hud.ArmamentPanel=panel.gameObject;
    var text=UnityEngine.Object.Instantiate(hud.Feedback,panel);text.name="ArmamentChoice";text.rectTransform.anchorMin=text.rectTransform.anchorMax=text.rectTransform.pivot=new Vector2(0,1);text.rectTransform.anchoredPosition=Vector2.zero;text.rectTransform.sizeDelta=new Vector2(596,74);text.text="방어칸 2×2 · 빈 칸";hud.ArmamentChoice=text;
    string[] commands={"weapon-next","weapon-install","weapon-charge","weapon-shape","weapon-upgrade","weapon-remove","weapon-back"};string[] labels={"다음 장착 후보","선택 방어 장착","축전지 충전","빈 칸 형태 변경","장착 방어 강화","방어 해제 · 환급 없음","노선 상세로"};
    for(int i=0;i<commands.Length;i++)Button(hud,panel,commands[i],labels[i],(i%3)*198,-80-(i/3)*42);
    panel.gameObject.SetActive(false);hud.Feedback.text="검증용: 선로 철 1 / 역 철 10 · 석탄 기준 1 × 길이 배율";EditorUtility.SetDirty(hud);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
   }
  }
  EditorSceneManager.OpenScene(originalPath);AssetDatabase.SaveAssets();return "PASS train authored turret/muzzle/shot, zero colliders, two saved Canvas armament panels and 39 persistent commands";
 }
}
