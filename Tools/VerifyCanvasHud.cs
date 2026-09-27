using System;
using System.Linq;
using UnityEngine;
using EternalSteam.OpenWorld;
public static class VerifyCanvasHud
{
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    // Fresh Play session only. Does not create UI or modify scene assets.
    public static string Main()
    {
        var hud=UnityEngine.Object.FindFirstObjectByType<CanvasWorldHud>();Check(hud!=null,"Saved Canvas HUD required");Check(!hud.Input.IsEditing,"Start without pending edits");
        int before=hud.GetComponentsInChildren<Transform>(true).Length;
        try {
            hud.Execute("edit");Check(hud.Input.IsEditing,"Edit command");Check(hud.Sandbox.BuildAreaHologram.Visible,"Authored hologram visible");
            hud.Execute("cancel");Check(!hud.Input.IsEditing&&!hud.Sandbox.BuildAreaHologram.Visible,"Immediate cancel hide");
            hud.Execute("fold:minimap");Check(!hud.Minimap.gameObject.activeInHierarchy&&!hud.Minimap.Interacting,"Map hide clears interaction");hud.Execute("fold:minimap");
            hud.Execute("category:0");Check(hud.Catalog.All(e=>e.View.gameObject.activeSelf==(e.Category==EternalSteam.BuildingCategory.Defense)),"Saved catalog category filtering");hud.Execute("category:-1");
            hud.Refresh();Check(before==hud.GetComponentsInChildren<Transform>(true).Length,"No HUD controls created by commands");
            Check(hud.Buttons.All(b=>b.View.onClick.GetPersistentEventCount()==1),"Saved callbacks");
            return "PASS Canvas command wiring, saved catalog filtering, authored construction visibility, stable control hierarchy. Visual/font/input and profiler checks remain separate.";
        } finally {hud.Input.Cancel();hud.Execute("category:-1");}
    }
}
