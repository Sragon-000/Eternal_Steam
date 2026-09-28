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
                foreach(var entry in hud.Catalog){Assert.That(entry.View,Is.Not.Null);Assert.That(entry.Icon,Is.Not.Null);Assert.That(entry.Icon.sprite,Is.Not.Null);Assert.That(entry.Icon.raycastTarget,Is.False);Assert.That(AssetDatabase.Contains(entry.Icon.sprite),Is.True);}
                Assert.That(hud.CardFrame,Is.Not.Null);Assert.That(hud.SelectedFrame,Is.Not.Null);Assert.That(hud.CategoryFrame,Is.Not.Null);
                Assert.That(hud.ResourceIcons.Length,Is.EqualTo(7));foreach(var icon in hud.ResourceIcons){Assert.That(icon.View,Is.Not.Null);Assert.That(icon.View.GetComponent<UnityEngine.UI.Image>().sprite,Is.Not.Null);}
                Assert.That(hud.SelectionPortrait,Is.Not.Null);Assert.That(hud.SelectionHealthFill,Is.Not.Null);Assert.That(hud.PowerFill,Is.Not.Null);
                Assert.That(hud.Portraits.Any(p=>p.Sprite!=null),Is.True);
                var catalogScroll=hud.CatalogLayout.GetComponentInParent<UnityEngine.UI.ScrollRect>();Assert.That(catalogScroll.horizontal,Is.True);Assert.That(catalogScroll.vertical,Is.False);Assert.That(catalogScroll.horizontalScrollbar,Is.Not.Null);
                Assert.That(hud.Buttons.Single(b=>b.Id=="upgrade").View.transform.parent.parent,Is.EqualTo(hud.Layout.SelectionPanel));
                foreach(var renderer in hud.Sandbox.WorldGrid.GetComponentsInChildren<MeshRenderer>(true))Assert.That(renderer.sharedMaterial.shader.name,Is.EqualTo("EternalSteam/NeutralConstructionGrid"));
                Assert.That(hud.Layout,Is.Not.Null);Assert.That(hud.Layout.Root,Is.EqualTo(hud.transform));
                Assert.That(hud.Layout.FontSizes.Length,Is.EqualTo(hud.Layout.Texts.Length));
                Assert.That(hud.GetComponent<UnityEngine.UI.CanvasScaler>().referenceResolution,Is.EqualTo(new Vector2(1920,1080)));
                Assert.That(hud.GetComponent<UnityEngine.UI.CanvasScaler>().matchWidthOrHeight,Is.EqualTo(.5f));
                foreach(string container in new[]{"TopBar","LeftStatus","RightStatus"}){
                    var image=hud.transform.Find(container).GetComponent<UnityEngine.UI.Image>();
                    Assert.That(image.raycastTarget,Is.False,container);Assert.That(image.color.a,Is.Zero,container);
                }
                Assert.That(hud.Texts.Any(t=>t.Id=="resource-values"),Is.True);
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
