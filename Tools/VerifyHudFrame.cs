using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using EternalSteam.OpenWorld;
public sealed class HudFrameProbe:MonoBehaviour
{
 void LateUpdate(){
  try{var h=UnityEngine.Object.FindFirstObjectByType<CanvasWorldHud>();Canvas.ForceUpdateCanvases();var corners=new Vector3[4];h.Minimap.rectTransform.GetWorldCorners(corners);var p=RectTransformUtility.WorldToScreenPoint(null,(corners[0]+corners[2])*.5f);var hits=new List<RaycastResult>();h.Raycaster.Raycast(new PointerEventData(EventSystem.current){position=p},hits);if(!hits.Any(x=>x.gameObject==h.Minimap.gameObject))throw new Exception("Minimap raycast: "+Screen.width+"x"+Screen.height+" "+p);
   var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("VerifyCompactCanvasHud")).LastOrDefault(t=>t!=null);
   if(type==null)throw new Exception("Load Tools/VerifyCompactCanvasHud.cs first");
   var result=type.GetMethod("Main").Invoke(null,null);
   File.WriteAllText("/tmp/reference-hud-frame-result.txt","PASS actual Game frame minimap raycast at "+Screen.width+"x"+Screen.height+"\n"+result);
  }catch(Exception e){File.WriteAllText("/tmp/reference-hud-frame-result.txt","FAIL "+e);}finally{Destroy(gameObject);}
 }
}
public static class VerifyHudFrame
{
 public static string Main(){if(!Application.isPlaying)throw new Exception("Play required");if(File.Exists("/tmp/reference-hud-frame-result.txt"))File.Delete("/tmp/reference-hud-frame-result.txt");var go=new GameObject("Temporary HUD frame verification");go.hideFlags=HideFlags.HideAndDontSave;go.AddComponent<HudFrameProbe>();UnityEditor.EditorApplication.Step();return "Queued actual-frame raycast check";}
}
