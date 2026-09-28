using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using EternalSteam;
using EternalSteam.OpenWorld;
public static class VerifyCatalogBaseLevel
{
 static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
 public static string Main()
 {
  var h=UnityEngine.Object.FindFirstObjectByType<CanvasWorldHud>();
  var content=h.Sandbox.Content;var log=new List<string>();
  if(!content.BaseRules)content.LevelCap=1;
  int count=h.GetComponentsInChildren<Transform>(true).Length;
  h.Sandbox.Clock.Paused=true;h.Input.Cancel();h.Input.BeginEditing();
  var upgrade=content.MainBase?.Module<IUpgradeControl>();
  for(int pass=0;pass<2;pass++){
   h.Execute("category:-1");h.Refresh();int hidden=0,shown=0;
   for(int i=0;i<h.Catalog.Length;i++){
    var entry=h.Catalog[i];
    bool expected=entry.Definition==null || (content.BaseRules&&OpenWorldContent.RoleOf(entry.Definition)==BaseRole.Main) || (entry.Definition.Placement?.RequiredNexusLevel??1)<=content.LevelCap;
    Check(entry.View.gameObject.activeSelf==expected,"Visibility: "+entry.View.name);
    Check(entry.View.interactable==expected,"Interaction: "+entry.View.name);
    if(!expected){hidden++;var selected=h.Input.SelectedDefinition;var tool=h.Input.Tool;h.Execute("build:"+i);Check(h.Input.SelectedDefinition==selected&&h.Input.Tool==tool,"Command bypass: "+i);h.Input.SelectContent(entry.Definition);Check(h.Input.SelectedDefinition==selected&&h.Input.Tool==tool,"Input bypass: "+i);Check(h.Sandbox.Message.Contains("필요"),"Reason missing");}
    else shown++;
   }
   Check(hidden>0,"Expected high-level buildings in catalog");
   foreach(var category in h.Catalog.Select(x=>x.Category).Distinct()){
    h.Execute("category:"+(int)category);
    foreach(var entry in h.Catalog)Check(entry.View.gameObject.activeSelf==((entry.Definition==null||content.MeetsBaseLevel(entry.Definition,out _))&&entry.Category==category),"Category gate");
   }
   log.Add("Lv."+content.LevelCap+": shown="+shown+", hidden="+hidden+", direct selection blocked, category filter passed");
   if(pass==0){int old=content.LevelCap;if(content.BaseRules){Check(upgrade!=null&&upgrade.TryUpgrade(out _),"Upgrade boundary");}else content.LevelCap=old+1;Check(content.LevelCap==old+1,"Level changed");}
  }
  h.Execute("category:-1");h.Input.Cancel();h.Refresh();
  Check(h.GetComponentsInChildren<Transform>(true).Length==count,"Saved hierarchy preserved");
  return string.Join("\n",log)+"\nPASS: no UI objects created or removed.";
 }
}
