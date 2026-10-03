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
        public void SavedResourceColumnsFitLongQuantitiesWithoutOverlap(string name)
        {
            var scene=EditorSceneManager.OpenPreviewScene("Assets/EternalSteam/Scene/Tests/"+name+".unity");
            try{
                var hud=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<CanvasWorldHud>(true)).Single();
                var names=hud.Texts.Single(x=>x.Id=="resources").View;
                var values=hud.Texts.Single(x=>x.Id=="resource-values").View;
                Canvas.ForceUpdateCanvases();
                Assert.That(names.rectTransform.rect.width,Is.GreaterThanOrEqualTo(names.GetPreferredValues("플라즈마석").x),name);
                Assert.That(values.rectTransform.rect.width,Is.GreaterThanOrEqualTo(values.GetPreferredValues("1,234,567,890").x),name);
                var content=(RectTransform)names.transform.parent;
                var nameRight=content.rect.center.x+names.rectTransform.anchoredPosition.x+names.rectTransform.rect.xMax;
                var valueLeft=content.rect.center.x+values.rectTransform.anchoredPosition.x+values.rectTransform.rect.xMin;
                Assert.That(nameRight,Is.LessThan(valueLeft),name);
            }finally{EditorSceneManager.ClosePreviewScene(scene);}
        }

        [TestCase("StartRegionSandbox")]
        [TestCase("OpenWorldSandbox")]
        public void SavedRailwayPanelClearsResourcePanel(string name)
        {
            var scene=EditorSceneManager.OpenPreviewScene("Assets/EternalSteam/Scene/Tests/"+name+".unity");
            try{
                var roots=scene.GetRootGameObjects();
                var world=roots.SelectMany(r=>r.GetComponentsInChildren<CanvasWorldHud>(true)).Single();
                var railway=roots.SelectMany(r=>r.GetComponentsInChildren<RailwayHud>(true)).Single();
                var resource=world.Layout.ResourcePanel;
                var leftStatus=(RectTransform)resource.parent;
                var panel=(RectTransform)railway.Panel.transform;
                // Preview scenes have no display; normalize the Canvas transform before comparing bounds.
                world.transform.localScale=Vector3.one;
                // Compare the same canvas space after reparenting the railway panel into its workspace.
                var corners=new Vector3[4];resource.GetWorldCorners(corners);
                float resourceRight=world.transform.InverseTransformPoint(corners[2]).x;
                panel.GetWorldCorners(corners);float panelLeft=world.transform.InverseTransformPoint(corners[0]).x;
                Assert.That(panelLeft-resourceRight,Is.GreaterThanOrEqualTo(8f),name);
            }finally{EditorSceneManager.ClosePreviewScene(scene);}
        }

        [TestCase("StartRegionSandbox")][TestCase("OpenWorldSandbox")]
        public void SavedStationContextHasClickableNamesAndDetailEntry(string name)
        {
            var scene=EditorSceneManager.OpenPreviewScene("Assets/EternalSteam/Scene/Tests/"+name+".unity");
            try{
                var roots=scene.GetRootGameObjects();var h=roots.SelectMany(r=>r.GetComponentsInChildren<RailwayHud>(true)).Single();
                Assert.That(h.StationContextPanel,Is.Not.Null);Assert.That(h.StationContextHeader,Is.Not.Null);Assert.That(h.StationRows.Length,Is.EqualTo(4));
                foreach(var command in new[]{"station-row:0","station-row:1","station-row:2","station-row:3","station-prev","station-next","station-context"}){
                    var b=h.Commands.Single(x=>x.Command==command).Button;Assert.That(b.onClick.GetPersistentTarget(0),Is.EqualTo(h));Assert.That(b.onClick.GetPersistentMethodName(0),Is.EqualTo("Execute"));
                }
                var world=roots.SelectMany(r=>r.GetComponentsInChildren<CanvasWorldHud>(true)).Single();var entry=world.Buttons.Single(x=>x.Id=="station-railway").View;
                Assert.That(entry.onClick.GetPersistentTarget(0),Is.EqualTo(world));Assert.That(entry.onClick.GetPersistentMethodName(0),Is.EqualTo("Execute"));
            }finally{EditorSceneManager.ClosePreviewScene(scene);}
        }
        [TestCase("StartRegionSandbox")][TestCase("OpenWorldSandbox")]
        public void SavedRailwayDiagnosticsHaveSelectableRows(string name)
        {
            var scene=EditorSceneManager.OpenPreviewScene("Assets/EternalSteam/Scene/Tests/"+name+".unity");
            try{var h=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<RailwayHud>(true)).Single();Assert.That(h.IssueHeader,Is.Not.Null);Assert.That(h.IssueRows.Length,Is.EqualTo(3));
                foreach(var command in new[]{"issue:0","issue:1","issue:2","issue-prev","issue-next"}){var b=h.Commands.Single(x=>x.Command==command).Button;Assert.That(b.transform.IsChildOf(h.DraftStopsPanel.transform),Is.True);Assert.That(b.onClick.GetPersistentTarget(0),Is.EqualTo(h));Assert.That(b.onClick.GetPersistentMethodName(0),Is.EqualTo("Execute"));}
            }finally{EditorSceneManager.ClosePreviewScene(scene);}
        }
        [TestCase("StartRegionSandbox")][TestCase("OpenWorldSandbox")]
        public void SavedRailwayMapEditorHasFourPortMarkersAndSegmentRenderer(string name)
        {
            var scene=EditorSceneManager.OpenPreviewScene("Assets/EternalSteam/Scene/Tests/"+name+".unity");
            try{
                var h=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<RailwayHud>(true)).Single();var v=h.Sandbox.RailwayView;
                Assert.That(h.SelectedSegment,Is.Not.Null);Assert.That(v.SelectedLegHighlight,Is.Not.Null);Assert.That(v.SelectedLegHighlight,Is.Not.EqualTo(v.RouteHighlight));Assert.That(v.SelectedLegHighlight.enabled,Is.False);
                Assert.That(v.PortFrames.Length,Is.EqualTo(4));Assert.That(v.PortLabels.Length,Is.EqualTo(4));
                for(int i=0;i<4;i++){Assert.That(v.PortFrames[i],Is.Not.Null);Assert.That(v.PortFrames[i].gameObject.activeSelf,Is.False);Assert.That(v.PortLabels[i],Is.Not.Null);Assert.That(v.PortLabels[i].GetComponentInParent<Canvas>(true).renderMode,Is.EqualTo(RenderMode.WorldSpace));Assert.That(v.PortLabels[i].transform.IsChildOf(v.PortFrames[i].transform),Is.True);}
                foreach(var cmd in new[]{"map-arrival","map-departure","map-cancel"}){var b=h.Commands.Single(x=>x.Command==cmd).Button;Assert.That(b.onClick.GetPersistentTarget(0),Is.EqualTo(h));Assert.That(b.onClick.GetPersistentMethodName(0),Is.EqualTo("Execute"));}
            }finally{EditorSceneManager.ClosePreviewScene(scene);}
        }
        [TestCase("StartRegionSandbox")][TestCase("OpenWorldSandbox")]
        public void SavedRailwayOrderEditorHasRowsAndPersistentCommands(string name)
        {
            var scene=EditorSceneManager.OpenPreviewScene("Assets/EternalSteam/Scene/Tests/"+name+".unity");
            try{
                var h=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<RailwayHud>(true)).Single();
                Assert.That(h.DraftStopsPanel,Is.Not.Null);Assert.That(h.DraftStopsPanel.activeSelf,Is.False);Assert.That(h.DraftSelection,Is.Not.Null);Assert.That(h.DraftRows.Length,Is.EqualTo(4));
                foreach(var command in new[]{"draft-row:0","draft-row:1","draft-row:2","draft-row:3","draft-prev","draft-next","down","first","open-loop","close-loop"}){
                    var b=h.Commands.Single(x=>x.Command==command).Button;Assert.That(b.transform.IsChildOf(h.DraftStopsPanel.transform),Is.True);Assert.That(b.onClick.GetPersistentTarget(0),Is.EqualTo(h));Assert.That(b.onClick.GetPersistentMethodName(0),Is.EqualTo("Execute"));Assert.That(b.GetComponent<UnityEngine.UI.Image>().raycastTarget,Is.True);
                }
            }finally{EditorSceneManager.ClosePreviewScene(scene);}
        }
        [TestCase("StartRegionSandbox")][TestCase("OpenWorldSandbox")]
        public void RailwayConfirmationAndDiscardAreAuthoredWithPersistentCommands(string name)
        {
            var scene=EditorSceneManager.OpenPreviewScene("Assets/EternalSteam/Scene/Tests/"+name+".unity");
            try{
                var h=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<RailwayHud>(true)).Single();
                Assert.That(h.ConfirmPanel,Is.Not.Null);Assert.That(h.DiscardPanel,Is.Not.Null);Assert.That(h.CommandHints,Is.Not.Null);
                Assert.That(h.ConfirmPanel.activeSelf,Is.False);Assert.That(h.DiscardPanel.activeSelf,Is.False);
                foreach(var cmd in new[]{"commit","back-edit","keep","discard","new","configure","start","close"}){
                    var bindings=h.Commands.Where(b=>b.Command==cmd).ToArray();Assert.That(bindings,Is.Not.Empty,cmd);
                    foreach(var b in bindings){Assert.That(b.Button,Is.Not.Null);Assert.That(b.Button.onClick.GetPersistentTarget(0),Is.EqualTo(h));Assert.That(b.Button.onClick.GetPersistentMethodName(0),Is.EqualTo("Execute"));}
                }
                Assert.That(h.Commands.Single(b=>b.Command=="commit").Button.transform.IsChildOf(h.ConfirmPanel.transform),Is.True);
                Assert.That(h.Commands.Single(b=>b.Command=="keep").Button.transform.IsChildOf(h.DiscardPanel.transform),Is.True);
            }finally{EditorSceneManager.ClosePreviewScene(scene);}
        }
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
                foreach(var b in hud.Buttons){Assert.That(b.View,Is.Not.Null,b.Id);Assert.That(b.View.onClick.GetPersistentEventCount(),Is.EqualTo(1),b.Id);var workspace=hud.GetComponent<GameplayHudWorkspace>();
                    Assert.That(b.View.onClick.GetPersistentTarget(0),Is.EqualTo(b.Id=="inventory-fold"?(Object)workspace:hud),b.Id);
                    if(b.Id=="inventory-fold"){Assert.That(workspace.ConstructionTab,Is.EqualTo(b.View));Assert.That(b.View.onClick.GetPersistentMethodName(0),Is.EqualTo("Construction"));}Assert.That(b.View.onClick.GetPersistentMethodName(0),Is.EqualTo(b.Id=="inventory-fold"?"Construction":"Execute"),b.Id);}
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
