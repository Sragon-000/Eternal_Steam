using System;using System.IO;using System.Collections;using UnityEngine;using UnityEditor;using UnityEngine.SceneManagement;using EternalSteam.OpenWorld;using Newtonsoft.Json.Linq;
public static class VerifyHiddenDockInput
{
 public const string Root="Docs/Measurements/2026-10-03-hidden-dock-input-fix/";
 public static string Setup(){var scene=SceneManager.GetActiveScene();if(Application.isPlaying||scene.isDirty||SceneManager.sceneCount!=1)throw new Exception("Clean current Edit scene required");if(File.Exists(Root+"play-original.json"))throw new Exception("Never overwrite QA session");File.WriteAllText(Root+"play-original.json",new JObject{{"scene",scene.path},{"background",Application.runInBackground},{"save",Environment.GetEnvironmentVariable("ETERNAL_STEAM_SAVE_TEST_ROOT")}}.ToString());Environment.SetEnvironmentVariable("ETERNAL_STEAM_SAVE_TEST_ROOT",Path.GetFullPath(Root+"isolated-save"));Application.runInBackground=true;return "Same-scene isolated input QA prepared";}
 public static string Start(){if(!Application.isPlaying)throw new Exception("Play required");new GameObject("Hidden dock input QA").AddComponent<HiddenDockInputProbe>();return "Verifying actual hidden/visible HUD command and camera gates";}
 public static string Restore(){if(Application.isPlaying)throw new Exception("Stop Play first");var old=JObject.Parse(File.ReadAllText(Root+"play-original.json"));if(SceneManager.GetActiveScene().path!=(string)old["scene"])throw new Exception("Scene changed");Environment.SetEnvironmentVariable("ETERNAL_STEAM_SAVE_TEST_ROOT",(string)old["save"]);Application.runInBackground=(bool)old["background"];return new JObject{{"scene",SceneManager.GetActiveScene().path},{"dirty",SceneManager.GetActiveScene().isDirty},{"sceneCount",SceneManager.sceneCount},{"save",Environment.GetEnvironmentVariable("ETERNAL_STEAM_SAVE_TEST_ROOT")},{"background",Application.runInBackground}}.ToString();}
}
public sealed class HiddenDockInputProbe:MonoBehaviour
{
 JArray checks=new();bool finished;
 void Check(bool value,string label){checks.Add(new JObject{{"label",label},{"passed",value}});if(!value)throw new Exception(label);}
 IEnumerator Start(){File.WriteAllText(VerifyHiddenDockInput.Root+"play-status.json","{\"running\":true}");var routine=Run();while(true){bool more=false;object next=null;Exception error=null;try{more=routine.MoveNext();if(more)next=routine.Current;}catch(Exception e){error=e;}if(error!=null){Finish(error);yield break;}if(!more)yield break;yield return next;}}
 IEnumerator Run(){var h=UnityEngine.Object.FindFirstObjectByType<CanvasWorldHud>();var s=h.Sandbox;var d=h.Layout.CompactDock;var g=h.Groups;
  yield return null;Check(!g.Construction.gameObject.activeInHierarchy,"Fresh combat group hides construction");Check(d.Transitioning,"Hidden initial dock retains pending layout transition");Check(!d.BlocksInput,"Hidden transition does not own input lock");Check(!s.CameraRig.BlockKeyboard,"Fresh combat camera keyboard input is free");
  bool paused=s.Clock.Paused;h.Execute("pause");Check(s.Clock.Paused!=paused,"Hidden transition allows real HUD pause command");h.Execute("pause");Check(s.Clock.Paused==paused,"Second HUD pause restores clock");
  h.Input.BeginEditing();d.Apply(0);Check(d.Transitioning,"Hidden editing changes layout state");h.Execute("cancel");Check(!h.Input.IsEditing,"Hidden transition allows actual cancel command");
  g.Select(true);yield return new WaitForSecondsRealtime(.6f);h.Input.BeginEditing();d.Apply(0);Check(d.BlocksInput,"Visible dock transition blocks input");h.Execute("cancel");Check(h.Input.IsEditing,"Visible transition rejects click-through cancel");
  yield return new WaitForSecondsRealtime(.4f);Check(!d.BlocksInput,"Visible transition releases input when complete");h.Execute("cancel");Check(!h.Input.IsEditing,"Cancel accepted after transition");yield return new WaitForSecondsRealtime(.4f);
  g.Select(false);yield return new WaitForSecondsRealtime(.6f);Check(!d.BlocksInput&&!s.CameraRig.BlockKeyboard,"Return to combat releases dock and camera locks");Check(SceneManager.sceneCount==1,"QA stayed in current scene");
  yield return new WaitForEndOfFrame();var image=ScreenCapture.CaptureScreenshotAsTexture();try{File.WriteAllBytes(VerifyHiddenDockInput.Root+"play-screen.png",image.EncodeToPNG());}finally{Destroy(image);}Finish(null);
 }
 void Finish(Exception e){finished=true;File.WriteAllText(VerifyHiddenDockInput.Root+"play-result.json",new JObject{{"passed",e==null},{"scene",SceneManager.GetActiveScene().path},{"checks",checks},{"error",e?.ToString()}}.ToString());File.WriteAllText(VerifyHiddenDockInput.Root+"play-status.json",new JObject{{"running",false},{"passed",e==null},{"error",e?.Message}}.ToString());Destroy(gameObject);}
 void OnDestroy(){if(!finished)Finish(new Exception("QA destroyed before completion"));}
}
