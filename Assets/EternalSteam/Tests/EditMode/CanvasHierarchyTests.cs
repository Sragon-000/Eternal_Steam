using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using EternalSteam.OpenWorld;
namespace EternalSteam.Tests
{
    public sealed class CanvasHierarchyTests
    {
        [TestCase("StartRegionSandbox")]
        [TestCase("OpenWorldSandbox")]
        public void SavedSceneOwnsCanvasControlsCallbacksAndConstructionObjects(string name)
        {
            var scene=EditorSceneManager.OpenPreviewScene("Assets/EternalSteam/Scene/Tests/"+name+".unity");
            try {
                var roots=scene.GetRootGameObjects();var hud=roots.SelectMany(r=>r.GetComponentsInChildren<CanvasWorldHud>(true)).Single();
                Assert.That(hud.gameObject.activeSelf,Is.True);Assert.That(hud.GetComponent<Canvas>(),Is.Not.Null);Assert.That(hud.GetComponent<UnityEngine.UI.CanvasScaler>(),Is.Not.Null);Assert.That(hud.Raycaster,Is.Not.Null);
                Assert.That(roots.SelectMany(r=>r.GetComponentsInChildren<EventSystem>(true)).Count(),Is.EqualTo(1));
                Assert.That(hud.Sandbox,Is.Not.Null);Assert.That(hud.Input,Is.Not.Null);Assert.That(hud.Amount,Is.Not.Null);Assert.That(hud.Minimap,Is.Not.Null);Assert.That(hud.HealthFill,Is.Not.Null);Assert.That(hud.ClockHand,Is.Not.Null);
                Assert.That(hud.Texts.Select(t=>t.Id).Distinct().Count(),Is.EqualTo(hud.Texts.Length));Assert.That(hud.Buttons.Select(t=>t.Id).Distinct().Count(),Is.EqualTo(hud.Buttons.Length));
                foreach(var t in hud.Texts){Assert.That(t.View,Is.Not.Null,t.Id);Assert.That(t.View.font,Is.Not.Null,t.Id);Assert.That(t.View.font.fallbackFontAssetTable.Any(f=>f!=null&&f.sourceFontFile!=null),Is.True,"Persisted Korean fallback: "+t.Id);}
                foreach(var b in hud.Buttons){Assert.That(b.View,Is.Not.Null,b.Id);Assert.That(b.View.onClick.GetPersistentEventCount(),Is.EqualTo(1),b.Id);Assert.That(b.View.onClick.GetPersistentTarget(0),Is.EqualTo(hud),b.Id);Assert.That(b.View.onClick.GetPersistentMethodName(0),Is.EqualTo("Execute"),b.Id);}
                foreach(var entry in hud.Catalog)Assert.That(entry.View,Is.Not.Null);
                Assert.That(hud.CatalogLayout,Is.Not.Null);Assert.That(hud.CatalogLayout.GetComponent<UnityEngine.UI.ContentSizeFitter>(),Is.Not.Null);
                foreach(var scroll in hud.GetComponentsInChildren<UnityEngine.UI.ScrollRect>(true)){Assert.That(scroll.viewport,Is.Not.Null);Assert.That(scroll.content,Is.Not.Null);Assert.That(scroll.viewport.GetComponent<UnityEngine.UI.Mask>(),Is.Not.Null);}
                foreach(var old in roots.SelectMany(r=>r.GetComponentsInChildren<OpenWorldHud>(true)))Assert.That(old.gameObject.activeSelf,Is.False);
                Assert.That(hud.Input.DragRectangle,Is.Not.Null);
                var input=new SerializedObject(hud.Input);foreach(string key in new[]{"preview","directionLine","invalidPattern","inactivePreviewMaterial"})Assert.That(input.FindProperty(key).objectReferenceValue,Is.Not.Null,key);
                foreach(Component view in new Component[]{hud.Sandbox.WorldGrid,hud.Sandbox.BuildAreaHologram}){
                    Assert.That(view,Is.Not.Null);var serialized=new SerializedObject(view);
                    foreach(string key in new[]{"renderers","filters"}){var array=serialized.FindProperty(key);Assert.That(array.arraySize,Is.EqualTo(9));for(int i=0;i<9;i++)Assert.That(array.GetArrayElementAtIndex(i).objectReferenceValue,Is.Not.Null);}
                    Assert.That(view.GetComponentsInChildren<Collider>(true),Is.Empty);
                }
            } finally {EditorSceneManager.ClosePreviewScene(scene);}
        }
    }
}
