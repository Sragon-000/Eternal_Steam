using System;
using System.IO;
using System.Linq;
using System.Collections;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using EternalSteam.OpenWorld;
using Newtonsoft.Json.Linq;
public static class CurrentScenePlaySmoke
{
 public const string Root="Docs/Measurements/2026-10-03-test-restructure/play/";
 public static string Setup(){var s=SceneManager.GetActiveScene();if(Application.isPlaying||s.isDirty||SceneManager.sceneCount!=1)throw new Exception("One clean current Edit scene required");Directory.CreateDirectory(Root);SessionState.SetString("CurrentSceneSmoke.Path",s.path);SessionState.SetBool("CurrentSceneSmoke.Background",Application.runInBackground);SessionState.SetString("CurrentSceneSmoke.SaveRoot",Environment.GetEnvironmentVariable("ETERNAL_STEAM_SAVE_TEST_ROOT")??"");Environment.SetEnvironmentVariable("ETERNAL_STEAM_SAVE_TEST_ROOT",Path.GetFullPath(Root+"isolated-save"));Application.runInBackground=true;return s.path;}
 public static string Run(){if(!Application.isPlaying||SceneManager.GetActiveScene().path!=SessionState.GetString("CurrentSceneSmoke.Path","")||Environment.GetEnvironmentVariable("ETERNAL_STEAM_SAVE_TEST_ROOT")!=Path.GetFullPath(Root+"isolated-save"))throw new Exception("Prepared isolated current Play scene required");new GameObject("Current scene verification").AddComponent<CurrentSceneSmokeProbe>();return "Running current scene smoke";}
 public static string Finish(){if(Application.isPlaying)throw new Exception("Stop Play first");if(SceneManager.GetActiveScene().path!=SessionState.GetString("CurrentSceneSmoke.Path",""))throw new Exception("Scene changed");var previous=SessionState.GetString("CurrentSceneSmoke.SaveRoot","");Environment.SetEnvironmentVariable("ETERNAL_STEAM_SAVE_TEST_ROOT",string.IsNullOrEmpty(previous)?null:previous);Application.runInBackground=SessionState.GetBool("CurrentSceneSmoke.Background",false);AssetDatabase.SaveAssets();return "Restored settings; scene unchanged";}
}
public sealed class CurrentSceneSmokeProbe:MonoBehaviour
{
 IEnumerator Start(){var scene=SceneManager.GetActiveScene();var h=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<CanvasWorldHud>(true)).Single();var s=h.Sandbox;bool group=h.Groups.ConstructionSelected,paused=s.Clock.Paused,menu=s.PauseMenu.IsOpen;var results=new JObject{{"scene",scene.path},{"researchExperiment",false},{"campaignAvailable",s.Persistence!=null},{"campaignPlaythrough",s.Persistence==null?"NOT_APPLICABLE: current scene has no campaign persistence":"NOT_RUN: smoke only"}};
 try {
  s.Clock.Paused=true;s.PauseMenu.SetOpen(false);s.PauseMenu.Advance(1);h.Groups.Select(false);h.Groups.Advance(1);h.Refresh();
  yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.GetFullPath(CurrentScenePlaySmoke.Root+"combat.png"));
  h.Groups.Select(true);for(int i=0;i<40;i++)yield return null;
  Check(h.Groups.CanUseConstruction,"construction easing completes");h.Refresh();yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.GetFullPath(CurrentScenePlaySmoke.Root+"construction.png"));
  s.PauseMenu.SetOpen(true);for(int i=0;i<40;i++)yield return null;
  Check(s.SimulationPaused&&s.PauseMenu.BlocksInput,"Esc modal blocks simulation/input");h.Groups.Toggle();Check(h.Groups.ConstructionSelected,"Tab blocked while modal open");
  yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.GetFullPath(CurrentScenePlaySmoke.Root+"escape.png"));
  s.PauseMenu.SetOpen(false);for(int i=0;i<40;i++)yield return null;h.Groups.Select(false);h.Groups.Advance(1);
  Check(!h.Groups.ConstructionSelected,"return to combat");Check(Mathf.Approximately(h.Minimap.VisibleWorldBounds.width,150)&&Mathf.Approximately(h.Minimap.VisibleWorldBounds.height,150),"local minimap 150x150");
  Check(!s.Content.InfiniteResources,"infinite resources remain off");Check(h.ShowDevelopmentControls,"test controls retained");Check(SceneManager.GetActiveScene().handle==scene.handle&&SceneManager.sceneCount==1,"current scene held");results["result"]="PASS";
 } finally {s.PauseMenu.SetOpen(false);s.PauseMenu.Advance(1);h.Groups.Select(group);h.Groups.Advance(1);s.Clock.Paused=paused;s.PauseMenu.SetOpen(menu);s.PauseMenu.Advance(1);if(results["result"]==null)results["result"]="FAIL";File.WriteAllText(CurrentScenePlaySmoke.Root+"result.json",results.ToString());Destroy(gameObject);}
 }
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
}
