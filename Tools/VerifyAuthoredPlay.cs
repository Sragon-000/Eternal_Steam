using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;
using EternalSteam.OpenWorld;
public static class VerifyAuthoredPlay
{
 public static async Task<string> Main()
 {
  var w=UnityEngine.Object.FindFirstObjectByType<OpenWorldSandbox>();if(!Application.isPlaying||w==null)throw new Exception("Authored sandbox Play required");
  var authored=w.GetComponentsInChildren<SceneTower>();
  if(authored.Length!=w.Towers.Count||w.Foundations.Platforms.Count!=w.GetComponentsInChildren<SceneFoundation>().Length)throw new Exception("Saved layout must register once");
  foreach(var t in authored)if(!w.Towers.Exists(v=>v.root==t.gameObject))throw new Exception("Runtime must reuse authored GameObject");
  var root=UnityEngine.Object.FindFirstObjectByType<OpenWorldHud>().GetComponent<UIDocument>().rootVisualElement;
  if(root.Q<Button>("foundation")!=null||root.Q<Button>("edit")!=null)throw new Exception("Construction belongs to Editor");
  root.Q<TextField>("amount").value="100";var b=root.Q<Button>("spawn");using(var e=NavigationSubmitEvent.GetPooled()){e.target=b;b.SendEvent(e);}
  if(w.Enemies.Alive!=100)throw new Exception("Spawn count");await Task.Delay(300);
  if(authored.Length>0&&!w.Towers.Exists(t=>t.shot>0))throw new Exception("Saved tower combat");w.ResetEnemies();
  if(w.Towers.Count!=authored.Length||w.GetComponentsInChildren<SceneTower>().Length!=authored.Length)throw new Exception("Reset must preserve scene layout");
  return "PASS: existing scene GameObjects registered without cloning, saved layout registration and attacks when towers exist, spawn UI 100 exact, reset preserves layout; runtime construction UI absent.";
 }
}
