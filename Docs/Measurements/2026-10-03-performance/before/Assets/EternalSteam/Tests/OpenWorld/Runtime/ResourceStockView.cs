using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine.UIElements;
namespace EternalSteam.OpenWorld
{
    // Empty or unmatched priorities expose the complete catalog, including zero stock.
    public sealed class ResourceStockView
    {
        sealed class Entry {public string Id,Name;public double Amount=double.NaN;public int Priority=int.MaxValue;public int Order;}
        readonly Label label;
        readonly IResourceBank bank;
        readonly List<Entry> entries=new();
        readonly StringBuilder text=new();
        bool dirty=true;
        public bool HasPriority {get;}
        public bool ShowAll {get;private set;}
        public ResourceStockView(Label label,IResourceBank bank,BuildingCatalog catalog,IReadOnlyList<string> priority=null)
        {
            this.label=label;this.bank=bank;var ids=new HashSet<string>();
            if(catalog!=null)foreach(var building in catalog.Buildings)foreach(var module in building.Modules)
                if(module is ProductionModuleDefinition recipe&&ids.Add(recipe.OutputId)){
                    var entry=new Entry{Id=recipe.OutputId,Name=building.DisplayName.Replace(" 생성기",""),Order=entries.Count};
                    if(priority!=null)for(int i=0;i<priority.Count;i++)if(priority[i]==entry.Id){entry.Priority=i;HasPriority=true;break;}
                    entries.Add(entry);
                }
            entries.Sort((a,b)=>a.Priority!=b.Priority?a.Priority.CompareTo(b.Priority):a.Order.CompareTo(b.Order));
            ShowAll=!HasPriority;Refresh();
        }
        public void SetShowAll(bool value){ShowAll=value||!HasPriority;dirty=true;Refresh();}
        public void Refresh()
        {
            foreach(var entry in entries){double value=bank.Amount(entry.Id);if(entry.Amount!=value){entry.Amount=value;dirty=true;}}
            if(!dirty)return;dirty=false;text.Clear();
            foreach(var entry in entries){if(!ShowAll&&entry.Priority==int.MaxValue)continue;if(text.Length>0)text.Append('\n');text.Append(entry.Name).Append("  ").Append(entry.Amount.ToString("N0"));}
            label.text=text.Length==0?"등록된 생산 자원이 없습니다.":text.ToString();
        }
    }
}
