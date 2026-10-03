using System.Linq;using System.Reflection;using NUnit.Framework;using UnityEngine;using UnityEditor.SceneManagement;using EternalSteam.OpenWorld;
namespace EternalSteam.Tests{
public sealed class CircularMinimapTests{
[TestCase(.5f,.5f,true)][TestCase(1,.5f,true)][TestCase(.5f,0,true)][TestCase(0,0,false)][TestCase(1,1,false)][TestCase(1.001f,.5f,false)][TestCase(.85f,.85f,true)][TestCase(.86f,.86f,false)]
public void CircularInputBoundary(float x,float y,bool inside){Assert.That(CanvasWorldMinimap.ContainsNormalized(new Vector2(x,y)),Is.EqualTo(inside));}
[TestCase("StartRegionSandbox")][TestCase("OpenWorldSandbox")]
public void SavedCircleAndInputAgree(string name){var scene=EditorSceneManager.OpenPreviewScene("Assets/EternalSteam/Scene/Tests/"+name+".unity");try{var h=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<CanvasWorldHud>(true)).Single();var l=h.Layout;Assert.That(l.CircularMap,Is.True);var mask=l.MapBody.GetComponent<UnityEngine.UI.Mask>();Assert.That(mask,Is.Not.Null);Assert.That(mask.showMaskGraphic,Is.False);Assert.That(l.MapBody.GetComponent<UnityEngine.UI.Image>().raycastTarget,Is.False);Assert.That(h.Minimap.transform.IsChildOf(mask.transform),Is.True);Assert.That(l.MapPanel.GetComponent<UnityEngine.UI.Image>().enabled,Is.False);Assert.That(h.Buttons.Single(b=>b.Id=="minimap-fold").View.gameObject.activeSelf,Is.False);var ring=l.MapPanel.Find("MinimapBezel").GetComponent<UnityEngine.UI.Image>();Assert.That(ring.sprite,Is.Not.Null);Assert.That(ring.raycastTarget,Is.False);
var r=h.Minimap.rectTransform.rect;var p=(Vector2)typeof(CanvasWorldMinimap).GetMethod("Pixel",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(h.Minimap,new object[]{new Vector2(-1,2)});Assert.That(p.x,Is.LessThan(r.xMin));Assert.That(p.y,Is.LessThan(r.yMin),"Viewport lines must be clipped by mask, not clamped to square edges");
}finally{EditorSceneManager.ClosePreviewScene(scene);}}
}}
