using System;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using EternalSteam.OpenWorld;
public static class VerifyReferenceCanvasHud
{
 static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
 public static string Main()
 {
  var h=UnityEngine.Object.FindFirstObjectByType<CanvasWorldHud>();h.Sandbox.Clock.Paused=true;h.Input.Cancel();h.Execute("category:-1");h.Refresh();h.Layout.Apply(true);Canvas.ForceUpdateCanvases();
  int before=h.GetComponentsInChildren<UnityEngine.UI.Image>(true).Length;
  Check(h.Catalog.All(x=>x.Icon!=null&&x.Icon.sprite!=null&&!x.Icon.raycastTarget),"Saved thumbnail references");Check(h.ResourceIcons.Length==7,"Resource icon bindings");
  var scroll=h.CatalogLayout.GetComponentInParent<UnityEngine.UI.ScrollRect>();Check(scroll.horizontal&&!scroll.vertical,"Horizontal catalog");scroll.horizontalNormalizedPosition=0;var point=new PointerEventData(EventSystem.current){scrollDelta=new Vector2(-5,0)};float x=scroll.content.anchoredPosition.x;scroll.OnScroll(point);Check(scroll.content.anchoredPosition.x<x,"Mouse wheel moves catalog");scroll.horizontalNormalizedPosition=0;
  h.Execute("category:0");Check(((UnityEngine.UI.Image)h.Buttons.Single(b=>b.Id=="category-0").View.targetGraphic).sprite==h.CategoryFrame,"Selected category frame");
  h.Execute("edit");int index=Array.FindIndex(h.Catalog,e=>e.View.gameObject.activeSelf&&e.View.interactable);Check(index>=0,"Available defense");h.Execute("build:"+index);Check(((UnityEngine.UI.Image)h.Catalog[index].View.targetGraphic).sprite==h.SelectedFrame,"Selected building frame");
  h.Execute("cancel");Check(((UnityEngine.UI.Image)h.Catalog[index].View.targetGraphic).sprite==h.CardFrame,"Selection reset");h.Execute("category:-1");
  h.Execute("fold:inventory");Check(!h.Layout.CategoryStrip.gameObject.activeSelf,"Folded category strip");Check(Mathf.Approximately(h.Layout.ConstructionBar.rect.height,h.Layout.CollapsedHeight),"Folded height");h.Execute("fold:inventory");
  if(h.Sandbox.Content.MainBase!=null){h.Input.ClickWorld(h.Sandbox.Content.MainBase.Position);h.Refresh();h.Layout.Apply(true);Canvas.ForceUpdateCanvases();Check(h.SelectionPortrait.enabled&&h.SelectionPortrait.sprite!=null,"Main base portrait");var upgrade=h.Buttons.Single(b=>b.Id=="upgrade").View;Check(upgrade.gameObject.activeInHierarchy,"Upgrade visible without scrolling");Check(upgrade.transform.parent.parent==h.Layout.SelectionPanel,"Fixed upgrade area");h.Input.ClearSelection();}
  h.Refresh();Check(h.GetComponentsInChildren<UnityEngine.UI.Image>(true).Length==before,"Stable authored image hierarchy");Check(h.Buttons.All(b=>b.View.onClick.GetPersistentEventCount()==1),"Persistent callbacks");return "PASS thumbnail/resource references, category/build selection frames, horizontal wheel, fold, fixed upgrade controls, stable hierarchy and callbacks";
 }
}
