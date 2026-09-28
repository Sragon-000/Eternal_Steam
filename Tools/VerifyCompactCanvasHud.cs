using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using EternalSteam.OpenWorld;
public static class VerifyCompactCanvasHud
{
 static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
 public static string Main()
 {
  var h=UnityEngine.Object.FindFirstObjectByType<CanvasWorldHud>();var l=h.Layout;Check(l!=null,"Saved layout binding");int count=h.GetComponentsInChildren<Transform>(true).Length;Canvas.ForceUpdateCanvases();l.Apply(true);
  int revision=l.Revision;for(int i=0;i<100;i++)l.Apply();Check(l.Revision==revision,"Stationary layout must not rebuild");
  bool inventory=l.InventoryBody.gameObject.activeSelf,clock=l.ClockBody.gameObject.activeSelf,map=l.MapBody.gameObject.activeSelf;
  try {
   if(!inventory)h.Execute("fold:inventory");l.Apply();Check(l.ConstructionBar.rect.height>=l.ExpandedHeight,"Expanded catalog height");h.Execute("fold:inventory");Check(l.ConstructionBar.rect.height==44&&!l.StatusRow.gameObject.activeSelf,"Fold collapses background and status");h.Execute("fold:inventory");
   float top=l.ResourcePanel.anchoredPosition.y;h.Execute("fold:clock");Check(l.ResourcePanel.anchoredPosition.y!=top,"Clock fold moves resources");h.Execute("fold:clock");
   h.Execute("fold:minimap");Check(!h.Minimap.Interacting,"Map fold releases drag");h.Execute("fold:minimap");
   Canvas.ForceUpdateCanvases();var corner=new Vector3[4];l.MapBody.GetWorldCorners(corner);var p=RectTransformUtility.WorldToScreenPoint(null,(corner[0]+corner[2])*.5f);var hits=new List<RaycastResult>();h.Raycaster.Raycast(new PointerEventData(EventSystem.current){position=p},hits);Check(hits.Any(x=>x.gameObject==h.Minimap.gameObject),"Minimap raycast");
   var focus=h.Sandbox.CameraRig.Focus;var e=new PointerEventData(EventSystem.current){position=p,pointerId=73,button=PointerEventData.InputButton.Left};h.Minimap.OnPointerDown(e);e.position+=new Vector2(18,12);h.Minimap.OnDrag(e);Check(h.Minimap.Interacting&&h.Sandbox.CameraRig.Focus!=focus,"Map drag moves camera");h.Minimap.OnPointerUp(e);Check(!h.Minimap.Interacting,"Map release");h.Sandbox.CameraRig.Focus=focus;
   Check(h.Buttons.All(b=>b.View.onClick.GetPersistentEventCount()==1),"Persistent button callbacks");Check(count==h.GetComponentsInChildren<Transform>(true).Length,"No runtime controls created");
   foreach(var name in new[]{"LeftStatus","RightStatus","TopBar"}){var image=h.transform.Find(name).GetComponent<UnityEngine.UI.Image>();Check(!image.raycastTarget&&image.color.a==0,"Empty background released: "+name);}
   return "PASS fold heights, resource reflow, stationary layout cache, minimap raycast/drag, saved callbacks and stable hierarchy; columns="+l.Catalog.constraintCount;
  }finally{
   if(inventory!=l.InventoryBody.gameObject.activeSelf)h.Execute("fold:inventory");if(clock!=l.ClockBody.gameObject.activeSelf)h.Execute("fold:clock");if(map!=l.MapBody.gameObject.activeSelf)h.Execute("fold:minimap");
  }
 }
}
