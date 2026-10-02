using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using EternalSteam.OpenWorld;
namespace EternalSteam.Tests
{
 public sealed class ResponsiveHudBoundsTests
 {
  static Rect Bounds(RectTransform root,RectTransform item){var c=new Vector3[4];item.GetWorldCorners(c);var a=root.InverseTransformPoint(c[0]);var b=root.InverseTransformPoint(c[2]);return Rect.MinMaxRect(a.x,a.y,b.x,b.y);}
  [TestCase("StartRegionSandbox",800,600)]
  [TestCase("StartRegionSandbox",1280,720)]
  [TestCase("StartRegionSandbox",1920,1080)]
  [TestCase("StartRegionSandbox",2560,1080)]
  [TestCase("StartRegionSandbox",3840,1080)]
  [TestCase("OpenWorldSandbox",3840,1080)]
  public void SavedHudFitsWideAndSmallWindowsWithExpandedCatalogueAndSelection(string name,int width,int height)
  {
   var scene=EditorSceneManager.OpenPreviewScene("Assets/EternalSteam/Scene/Tests/"+name+".unity");
   try{
    var h=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<CanvasWorldHud>(true)).Single();var l=h.Layout;
    // Preview scenes have no display. Reproduce the CanvasScaler's 0.5 width/height match without a render target.
    h.GetComponent<UnityEngine.UI.CanvasScaler>().enabled=false;l.Canvas.renderMode=RenderMode.WorldSpace;l.Canvas.enabled=false;l.Root.localScale=Vector3.one;
    float scale=Mathf.Sqrt(width/1920f*height/1080f);l.Canvas.scaleFactor=scale;l.Root.sizeDelta=new Vector2(width/scale,height/scale);
    l.InventoryBody.gameObject.SetActive(true);l.SelectionPanel.gameObject.SetActive(true);l.MapBody.gameObject.SetActive(true);l.Apply(true);
    var screen=l.Root.rect;var construction=Bounds(l.Root,l.ConstructionBar);var selected=Bounds(l.Root,l.SelectionPanel);var map=Bounds(l.Root,l.MapPanel);
    foreach(var p in new[]{l.ClockPanel,l.ResourcePanel,l.MapPanel,l.SelectionPanel,l.ConstructionBar,l.Notifications}){
     var r=Bounds(l.Root,p);Assert.That(r.xMin,Is.GreaterThanOrEqualTo(screen.xMin-.1f),p.name);Assert.That(r.xMax,Is.LessThanOrEqualTo(screen.xMax+.1f),p.name);Assert.That(r.yMin,Is.GreaterThanOrEqualTo(screen.yMin-.1f),p.name);Assert.That(r.yMax,Is.LessThanOrEqualTo(screen.yMax+.1f),p.name);
    }
    Assert.That(selected.Overlaps(construction),Is.False,"Selected information cannot enter bottom dock");Assert.That(selected.Overlaps(map),Is.False,"Minimap and selected information remain separate");Assert.That(Bounds(l.Root,l.ResourcePanel).Overlaps(construction),Is.False,"Resources stay above construction");
    var scroll=l.Catalog.GetComponentInParent<UnityEngine.UI.ScrollRect>();Assert.That(scroll.viewport.rect.height+.1f,Is.GreaterThanOrEqualTo(l.Catalog.cellSize.y),"Card height includes category and scrollbar space");
    foreach(var p in l.FloatingPanels){var r=Bounds(l.Root,p);Assert.That(r.yMin,Is.GreaterThan(construction.yMax),p.name);Assert.That(r.xMin,Is.GreaterThanOrEqualTo(screen.xMin),p.name);Assert.That(r.xMax,Is.LessThanOrEqualTo(screen.xMax),p.name);}
   }finally{EditorSceneManager.ClosePreviewScene(scene);}
  }
 }
}
