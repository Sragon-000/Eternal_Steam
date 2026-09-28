using System;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using EternalSteam;
using EternalSteam.OpenWorld;
public static class VerifyCompactHudInteractions
{
 static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
 public static string Main()
 {
  var h=UnityEngine.Object.FindFirstObjectByType<CanvasWorldHud>();
  foreach(var id in new[]{"menu","power","developer-body"}){var section=h.Sections.Single(x=>x.Id==id);if(section.Body.activeSelf)h.Execute("fold:"+id);}
  h.Input.Cancel();h.Refresh();
  var e=new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left};
  void Click(string id){var b=h.Buttons.Single(x=>x.Id==id).View;Check(b.interactable,id+" interactable");ExecuteEvents.Execute(b.gameObject,e,ExecuteEvents.pointerClickHandler);}
  Click("edit");Check(h.Input.IsEditing,"Button pointer click enters edit");Click("cancel");Check(!h.Input.IsEditing,"Button pointer click cancels edit");
  h.Execute("category:-1");Canvas.ForceUpdateCanvases();var scroll=h.CatalogLayout.GetComponentInParent<UnityEngine.UI.ScrollRect>();scroll.horizontalNormalizedPosition=0;var before=scroll.content.anchoredPosition.x;e.scrollDelta=new Vector2(-5,0);scroll.OnScroll(e);Check(scroll.content.anchoredPosition.x<before,"Catalog wheel scroll");scroll.horizontalNormalizedPosition=0;
  h.Input.ClickWorld(h.Sandbox.Content.MainBase.Position);h.Refresh();h.Layout.Apply(true);var level=h.Sandbox.Content.MainBase.Module<IUpgradeControl>();int old=level.Level;Click("upgrade");Check(level.Level==old+1,"Persistent upgrade button");
  h.Input.ClearSelection();h.Refresh();return "PASS EventSystem button clicks, catalog scroll, saved upgrade callback (verification-free policy).";
 }
}
