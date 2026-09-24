using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine.UIElements;
namespace EternalSteam.OpenWorld
{
    // Presentation adapter: the bank owns stock; production modules own recipes.
    public sealed class ResourceStockView
    {
        readonly Label label;
        readonly IResourceBank bank;
        readonly List<string> ids=new(),names=new();
        readonly List<double> amounts=new();
        readonly StringBuilder text=new();
        public ResourceStockView(Label label,IResourceBank bank,BuildingCatalog catalog)
        {
            this.label=label;this.bank=bank;
            if(catalog!=null)foreach(var building in catalog.Buildings)foreach(var module in building.Modules)
                if(module is ProductionModuleDefinition recipe&&!ids.Contains(recipe.OutputId)){
                    ids.Add(recipe.OutputId);names.Add(building.DisplayName.Replace(" 생성기", ""));amounts.Add(double.NaN);
                }
            Refresh();
        }
        public void Refresh()
        {
            bool changed=false;
            for(int i=0;i<ids.Count;i++){double value=bank.Amount(ids[i]);if(amounts[i]!=value){amounts[i]=value;changed=true;}}
            if(!changed){if(ids.Count==0)label.text="등록된 생산 자원이 없습니다.";return;}
            text.Clear();
            for(int i=0;i<ids.Count;i++){if(i>0)text.Append("    ·    ");text.Append(names[i]).Append(' ').Append(amounts[i].ToString("N0"));}
            label.text=text.ToString();
        }
    }
}
