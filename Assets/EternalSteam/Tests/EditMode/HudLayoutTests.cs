using System.Collections.Generic;
using EternalSteam.OpenWorld;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object=UnityEngine.Object;
namespace EternalSteam.Tests
{
    public sealed class HudLayoutTests
    {
        readonly List<Object> assets=new();
        T Asset<T>() where T:ScriptableObject{var a=ScriptableObject.CreateInstance<T>();assets.Add(a);return a;}
        static VisualElement Hud()
        {
            var asset=AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/EternalSteam/Shared/UI/OpenWorld/OpenWorldHud.uxml");
            Assert.That(asset,Is.Not.Null);return asset.CloneTree();
        }
        BuildingCatalog Catalog()
        {
            var catalog=Asset<BuildingCatalog>();
            foreach(var id in new[]{"copper","iron","copper"}){
                var building=Asset<BuildingDefinition>();building.Id=id+catalog.Buildings.Count;building.DisplayName=id+" 생성기";
                var recipe=Asset<ProductionModuleDefinition>();recipe.OutputId=id;building.Modules.Add(recipe);catalog.Buildings.Add(building);
            }
            return catalog;
        }
        [TearDown] public void Cleanup(){foreach(var a in assets)Object.DestroyImmediate(a);assets.Clear();}
        [Test] public void PlayerPanelsAreIndependentOfDeveloperPanel()
        {
            var root=Hud();var developer=root.Q("test-panel");
            foreach(var name in new[]{"minimap","selection-panel","save","orb-craft","clock-pause"}){
                Assert.That(root.Q(name),Is.Not.Null,name);Assert.That(developer.Contains(root.Q(name)),Is.False,name);
            }
            Assert.That(root.Q("menu-panel").Contains(root.Q("save-controls")),Is.True);
            Assert.That(root.Q("selection-panel").Contains(root.Q("upgrade")),Is.True);
            Assert.That(root.Q("panel").Contains(root.Q("cancel")),Is.True);
            Assert.That(root.Q("test-body").ClassListContains("is-hidden"),Is.True);
            Assert.That(root.Q("menu-body").ClassListContains("is-hidden"),Is.True);
        }
        [Test] public void ZeroStockAndDuplicateProductionIdsRemainAccessibleWithoutPriority()
        {
            var label=new Label();var view=new ResourceStockView(label,new ResourceBank(),Catalog());
            Assert.That(view.HasPriority,Is.False);Assert.That(view.ShowAll,Is.True);
            Assert.That(label.text,Is.EqualTo("copper  0\niron  0"));
            view.SetShowAll(false);Assert.That(view.ShowAll,Is.True);Assert.That(label.text,Does.Contain("iron"));
        }
        [Test] public void PriorityCanExpandAndHiddenStockUpdatesAreNotLost()
        {
            var bank=new ResourceBank();bank.AddCapacity("copper",10);var label=new Label();
            var view=new ResourceStockView(label,bank,Catalog(),new[]{"iron"});
            Assert.That(view.HasPriority,Is.True);Assert.That(label.text,Is.EqualTo("iron  0"));
            bank.Exchange(null,0,"copper",5);view.Refresh();Assert.That(label.text,Is.EqualTo("iron  0"));
            view.SetShowAll(true);Assert.That(label.text,Is.EqualTo("iron  0\ncopper  5"));
            view.SetShowAll(false);Assert.That(label.text,Is.EqualTo("iron  0"));
        }
        [Test] public void UnknownPriorityFallsBackToEntireCatalog()
        {
            var label=new Label();var view=new ResourceStockView(label,new ResourceBank(),Catalog(),new[]{"removed.resource"});
            Assert.That(view.HasPriority,Is.False);Assert.That(label.text,Does.Contain("copper").And.Contain("iron"));
        }
        [Test] public void FoldingCatalogPreservesChosenEntryAndCategory()
        {
            var root=Hud();var entry=new InventoryBuilding{Id="resource",Name="Resource",Category=BuildingCategory.Resource};
            var view=new BuildingInventoryView(root,new[]{entry},_=>{});view.SelectCategory(BuildingCategory.Resource);
            var window=ScriptableObject.CreateInstance<EditorWindow>();
            try {
                // Navigation events need an attached panel to invoke Button.clicked.
                window.Show();window.rootVisualElement.Add(root);
                var button=root.Q<Button>("building-resource");
                using(var evt=NavigationSubmitEvent.GetPooled()){evt.target=button;button.SendEvent(evt);}
                var selected=view.Selected;Assert.That(selected,Is.SameAs(entry));
                view.SetVisible(false);view.SetVisible(true);
                Assert.That(view.Category,Is.EqualTo(BuildingCategory.Resource));Assert.That(view.Selected,Is.SameAs(selected));
            } finally {window.Close();}
        }
    }
}
