using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;
using EternalSteam;
using EternalSteam.Demo;
using EternalSteam.OpenWorld;
public static class VerifyDocumentConstruction
{
    static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
    public static async Task<string> Main()
    {
        var s=UnityEngine.Object.FindFirstObjectByType<OpenWorldSandbox>();Check(Application.isPlaying&&s!=null,"OpenWorldSandbox Play required");
        var input=s.GetComponent<OpenWorldInput>();var root=UnityEngine.Object.FindFirstObjectByType<OpenWorldHud>().GetComponent<UIDocument>().rootVisualElement;
        async Task Click(string name){await Task.Delay(150);var b=root.Q<Button>(name);Check(b!=null,"UI "+name);using(var e=NavigationSubmitEvent.GetPooled()){e.target=b;b.SendEvent(e);}}
        input.Cancel();s.ResetEnemies();int bases=s.Foundations.Platforms.Count,towers=s.Towers.Count;
        Vector3 first=default,second=default;bool found=false;
        for(int z=-6;z<=6&&!found;z++)for(int x=-6;x<6&&!found;x++) {
            var a=WorldGridGeometry.Center(new Vector2Int(x,z),8);var b=WorldGridGeometry.Center(new Vector2Int(x+1,z),8);
            if(s.Foundations.CheckFoundation(a,out _,out _)&&s.Foundations.CheckFoundation(b,out _,out _)){first=a;second=b;found=true;}
        }
        Check(found,"Two flat unoccupied positions available");
        await Click("foundation");Check(input.ClickWorld(first)&&input.ClickWorld(second),"Two provisional foundations");
        Check(s.Foundations.Platforms.Count==bases,"Foundation previews have no actual occupancy");Check(!s.Spawn("1"),"Cannot start with edit in progress");
        input.ClickWorld(first);await Click("move");Check(!input.ClickWorld(second),"Foundation move collision keeps reservation");
        await Click("remove");Check(input.Edits.Count==1,"Individual foundation removal");
        input.ClickWorld(second);await Click("move");Check(input.ClickWorld(first),"Foundation move to released location");
        await Click("cancel");Check(input.Edits.Count==0&&s.Foundations.Platforms.Count==bases,"Cancel foundations");
        await Click("foundation");input.ClickWorld(first);input.ClickWorld(second);await Click("confirm");
        Check(!input.IsEditing&&s.Foundations.Platforms.Count==bases+2,"Confirm foundations");
        Check(s.Foundations.FindCell(first,out var p1,out _,out _)&&s.Foundations.FindCell(second,out var p2,out _,out _),"Rotated foundation lookup");
        s.Foundations.FindCell(second,out p2,out _,out _);
        Check(Mathf.Abs(Mathf.DeltaAngle(p1.View.transform.eulerAngles.y,45))<.01f,"Visible foundation rotated 45 degrees");
        var a0=p1.World.Grid.Center(Vector2Int.zero,Vector2Int.one)+Vector3.up*.01f;
        var a1=p1.World.Grid.Center(Vector2Int.right,Vector2Int.one)+Vector3.up*.01f;
        var b0=p2.World.Grid.Center(Vector2Int.zero,Vector2Int.one)+Vector3.up*.01f;
        await Click("foundation");input.Select(WorldTool.Tower,HordeTowerKind.MachineGun);
        Check(input.ClickWorld(a0)&&input.ClickWorld(a0+Vector3.forward*10),"Tower provisional placement");
        input.Select(WorldTool.Tower,HordeTowerKind.Cannon);input.ClickWorld(b0);input.ClickWorld(b0+Vector3.forward*10);
        Check(s.Towers.Count==towers&&p1.World.Grid.ReservationCount==1&&p2.World.Grid.ReservationCount==1,"Reservations independent of selection; no active towers");
        input.ClickWorld(a0);await Click("move");Check(input.ClickWorld(a1),"Move pending to rotated cell");
        Check(p1.World.Grid.ReservationAt(Vector2Int.zero)==null&&p1.World.Grid.ReservationAt(Vector2Int.right).HasValue,"Move transfers reservation");
        await Click("move");Check(!input.ClickWorld(b0),"Failed move keeps original");Check(p1.World.Grid.ReservationAt(Vector2Int.right).HasValue,"Failed move reservation retained");
        p2.World.Grid.SetBlocked(Vector2Int.zero,true);Check(!input.Confirm(),"Whole batch rejected");Check(s.Towers.Count==towers&&input.Edits.Count==2,"No partial installation; ghosts kept");
        p2.World.Grid.SetBlocked(Vector2Int.zero,false);await Click("confirm");Check(s.Towers.Count==towers+2&&!input.IsEditing,"Cross-foundation commit");
        await Click("edit");input.ClickWorld(a1);input.ClickWorld(b0);Check(s.Towers.Count==towers+2&&input.Edits.Count==2,"Recovery pending retains registration");
        await Click("cancel");Check(s.Towers.Count==towers+2,"Recovery cancellation");
        input.ClickWorld(a1);var tower=input.SelectedTower;Check(tower!=null,"Select installed tower");var old=tower.head.forward;
        await Click("direction");input.ClickWorld(a1+Vector3.left*10);Check(Vector3.Distance(old,tower.head.forward)<.001f&&tower.building.EditingDirection,"Direction preview does not mutate tower");
        await Click("cancel");Check(Vector3.Distance(old,tower.head.forward)<.001f&&!tower.building.EditingDirection,"Direction cancel restores action");
        input.ClickWorld(a1);await Click("direction");input.ClickWorld(a1+Vector3.left*10);await Click("confirm");Check(Vector3.Distance(tower.head.forward,Vector3.left)<.001f,"Direction confirm");
        await Click("edit");input.ClickWorld(a1);input.ClickWorld(b0);await Click("confirm");Check(s.Towers.Count==towers,"Recovery removes both towers");
        await Click("foundation");input.Select(WorldTool.Tower);input.ClickWorld(a0);input.ClickWorld(a0+Vector3.forward*10);input.ClickWorld(a0);await Click("remove");Check(input.Edits.Count==0&&p1.World.Grid.ReservationCount==0,"Individual removal");await Click("cancel");
        root.Q<TextField>("amount").value="25";await Click("spawn");Check(s.Enemies.Spawned==25,"Spawn allowed after editing");s.ResetEnemies();
        s.Foundations.RemoveFoundation(p1);s.Foundations.RemoveFoundation(p2);await Task.Delay(50);
        Check(s.Foundations.Platforms.Count==bases&&s.Towers.Count==towers,"Clean state after verification");
        return "PASS: 45-degree world placement, foundation previews/cancel/commit, cross-foundation tower reservations/move/failure/commit, delayed recovery/cancel/confirm, direction preview/cancel/confirm, per-item removal, spawn edit lock and cleanup.";
    }
}
