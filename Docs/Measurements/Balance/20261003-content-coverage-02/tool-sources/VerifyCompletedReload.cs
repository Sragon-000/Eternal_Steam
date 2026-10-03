using System;
using System.IO;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using EternalSteam.OpenWorld;
using Newtonsoft.Json.Linq;

public static class VerifyCompletedReload
{
 static (OpenWorldSandbox world,string root) Context()
 {
  var save=Environment.GetEnvironmentVariable("ETERNAL_STEAM_SAVE_TEST_ROOT");
  if(!Application.isPlaying||save==null||!save.Contains("/Docs/Measurements/Balance/"))throw new Exception("Isolated recorded Play required");
  var root=Path.GetDirectoryName(save);var config=JObject.Parse(File.ReadAllText(root+"/configuration.json"));
  var status=JObject.Parse(File.ReadAllText(root+"/status.json"));
  if(!(bool)status["stopped"]||status["error"].Type!=JTokenType.Null)throw new Exception("Clean recorder stop required before same-scene reload");
  if(File.Exists(root+"/combat-coverage-status.json")){var coverage=JObject.Parse(File.ReadAllText(root+"/combat-coverage-status.json"));if(!(bool)coverage["stopped"]||coverage["error"].Type!=JTokenType.Null)throw new Exception("Clean supplemental observer stop required");}
  var world=UnityEngine.Object.FindFirstObjectByType<OpenWorldSandbox>();
  if(world==null||SceneManager.sceneCount!=1||world.gameObject.scene.path!=(string)config["scene"]||world.Persistence.Blocked||!world.Clock.Paused||!world.Assault.Energy.PerfectOrb)throw new Exception("Completed paused campaign in the original current scene required");
  return (world,root);
 }
 public static string Request()
 {
  var (s,root)=Context();if(!s.Persistence.HasContinue||!File.Exists(root+"/completed-snapshot.json"))throw new Exception("Completed snapshot and normal continue save required");
  string scene=s.gameObject.scene.path;s.Persistence.ContinueSaved();return "Normal ContinueSaved requested for the same current scene: "+scene;
 }
 public static string Capture()
 {
  var (s,root)=Context();string file=root+"/reloaded-snapshot.json";
  if(File.Exists(file)||File.Exists(root+"/reloaded-screen.png"))throw new Exception("Never overwrite reload evidence");
  File.WriteAllText(file,JsonUtility.ToJson(s.Persistence.Capture(),true));
  var host=new GameObject("Completed reload screenshot observer").AddComponent<CompletedReloadScreenshot>();host.Begin(root,s);
  return "Reload snapshot saved; screenshot queued at next rendered frame";
 }
 public static string Describe()=>"Same-current-scene ContinueSaved and exact snapshot/screenshot evidence, only after a clean completed recording stop.";
}
public sealed class CompletedReloadScreenshot:MonoBehaviour
{
 public void Begin(string root,OpenWorldSandbox s){StartCoroutine(Capture(root,s));}
 IEnumerator Capture(string root,OpenWorldSandbox s)
 {
  yield return new WaitForEndOfFrame();Texture2D picture=null;
  try{
   picture=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(root+"/reloaded-screen.png",picture.EncodeToPNG());
   File.WriteAllText(root+"/reloaded-state.json",new JObject{{"scene",s.gameObject.scene.path},{"run",s.Persistence.RunId},{"paused",s.Clock.Paused},{"perfectOrb",s.Assault.Energy.PerfectOrb},{"buildings",s.Content.Bases.Buildings.Count},{"foundations",s.Foundations.Platforms.Count},{"width",picture.width},{"height",picture.height},{"error",null}}.ToString());
  }catch(Exception e){File.WriteAllText(root+"/reloaded-state.json",new JObject{{"error",e.ToString()}}.ToString());}
  finally{if(picture!=null)Destroy(picture);Destroy(gameObject);}
 }
}
