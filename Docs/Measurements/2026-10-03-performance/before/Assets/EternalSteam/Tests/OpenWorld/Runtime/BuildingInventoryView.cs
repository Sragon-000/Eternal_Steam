using System;
using System.Collections.Generic;
using UnityEngine.UIElements;
using EternalSteam.Demo;
namespace EternalSteam.OpenWorld
{
    public sealed class InventoryBuilding
    {
        public string Id,Name;
        public BuildingDefinition Definition;
        public BuildingCategory Category;
        public WorldTool Tool;
        public HordeTowerKind Kind;
    }
    // Presentation only: entries and selection commands are supplied by the composition root.
    public sealed class BuildingInventoryView
    {
        readonly VisualElement panel,items;
        readonly Label empty;
        readonly List<InventoryBuilding> entries=new();
        readonly Dictionary<BuildingCategory,Button> tabs=new();
        readonly Action<InventoryBuilding> choose;
        public BuildingCategory Category {get;private set;}
        public InventoryBuilding Selected {get;private set;}
        public bool Visible {get;private set;}
        public BuildingInventoryView(VisualElement root,IEnumerable<InventoryBuilding> entries,Action<InventoryBuilding> choose)
        {
            panel=root.Q("panel");items=root.Q("inventory-items");empty=root.Q<Label>("inventory-empty");this.choose=choose;
            this.entries.AddRange(entries);
            Bind(root,"category-defense",BuildingCategory.Defense);Bind(root,"category-resource",BuildingCategory.Resource);
            Bind(root,"category-other",BuildingCategory.Other);Bind(root,"category-installation",BuildingCategory.Installation);
            SelectCategory(BuildingCategory.Defense);SetVisible(true);
        }
        void Bind(VisualElement root,string name,BuildingCategory category)
        {var b=root.Q<Button>(name);b.focusable=false;tabs.Add(category,b);b.clicked+=()=>SelectCategory(category);}
        public void SetVisible(bool visible)
        {Visible=visible;panel.EnableInClassList("folded",!visible);panel.Q("inventory-scroll").style.display=visible?DisplayStyle.Flex:DisplayStyle.None;panel.Q(className:"inventory-footer").style.display=visible?DisplayStyle.Flex:DisplayStyle.None;}
        public void ClearSelection(){Selected=null;RefreshSelection();}
        public void SelectCategory(BuildingCategory category)
        {
            SetVisible(true);Category=category;items.Clear();int count=0;
            foreach(var pair in tabs)pair.Value.EnableInClassList("selected",pair.Key==category);
            foreach(var entry in entries) {
                if(entry.Category!=category)continue;count++;
                var cell=new VisualElement();cell.AddToClassList("inventory-cell");
                var button=new Button(()=>{Selected=entry;choose(entry);RefreshSelection();}){text=entry.Name,name="building-"+entry.Id,focusable=false,userData=entry};
                button.AddToClassList("inventory-item");cell.Add(button);items.Add(cell);
            }
            empty.style.display=count==0?DisplayStyle.Flex:DisplayStyle.None;RefreshSelection();
        }
        void RefreshSelection(){items.Query<Button>().ForEach(b=>b.EnableInClassList("selected",ReferenceEquals(b.userData,Selected)));}
    }
}
