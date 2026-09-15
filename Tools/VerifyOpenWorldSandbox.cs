using System;
using System.Threading.Tasks;
using EternalSteam.OpenWorld;
using EternalSteam.Demo;
using UnityEngine;
using UnityEngine.UIElements;
public static class VerifyOpenWorldSandbox
{
    static void Check(bool ok,string text){if(!ok)throw new Exception(text);}
    public static async Task<string> Main()
    {
        var s=UnityEngine.Object.FindFirstObjectByType<OpenWorldSandbox>();Check(Application.isPlaying&&s!=null,"OpenWorldSandbox Play required");
        var input=s.GetComponent<OpenWorldInput>();var root=UnityEngine.Object.FindFirstObjectByType<OpenWorldHud>().GetComponent<UIDocument>().rootVisualElement;
        void Click(string name){var b=root.Q<Button>(name);Check(b!=null,"UI "+name);using(var e=NavigationSubmitEvent.GetPooled()){e.target=b;b.SendEvent(e);}}
        Check(input.enabled,"In-game input enabled");
        s.ResetEnemies();
        Check(!s.Foundations.Install(Vector3.zero,Vector3.right,HordeTowerKind.MachineGun,out _),"No terrain-only tower placement");
        Click("foundation");Check(input.Tool==WorldTool.Foundation && !s.Running,"Foundation UI starts editing");
        Check(input.ClickWorld(new Vector3(1,0,1)),"First foundation");
        Check(!input.ClickWorld(new Vector3(2,0,2)),"Duplicate rejected");
        Check(input.ClickWorld(new Vector3(-1,0,1)),"Adjacent foundation");
        Check(!input.ClickWorld(new Vector3(200,0,0)),"Terrain boundary rejected");
        input.Select(WorldTool.Tower,HordeTowerKind.MachineGun);
        Check(input.ClickWorld(new Vector3(1,0,1)) && input.ChoosingDirection,"Cell selects direction");
        Check(s.Towers.Count==0,"Preview does not install");
        foreach(var platform in s.Foundations.Platforms)Check(platform.View.GetComponent<SceneFoundation>()!=null,"Foundation prefab used");input.Select(WorldTool.Explore);
        Check(!input.ChoosingDirection&&s.Towers.Count==0,"Cancel direction");
        for(int i=0;i<4;i++) {
            var point=new Vector3(1+i*2,0,1);input.Select(WorldTool.Tower,(HordeTowerKind)i);
            Check(input.ClickWorld(point),"Select cell "+i);Check(input.ClickWorld(point+Vector3.left*10),"Confirm direction "+i);
            var t=s.Towers[i];Check(t.kind==(HordeTowerKind)i,"Existing kind reused");
            Check(t.root.GetComponent<SceneTower>()!=null,"Prepared tower prefab used");
            Check(s.Foundations.FindCell(point,out var p,out _,out _),"Foundation lookup");
            Check(Mathf.Abs(t.root.transform.position.y-p.Top)<.001f,"Tower on foundation top");
        }
        Check(!s.Foundations.Install(new Vector3(1,0,1),Vector3.forward,HordeTowerKind.Arrow,out _),"Occupied cell rejected");
        Click("edit");Check(input.Tool==WorldTool.Recover,"Edit button");Check(input.ClickWorld(new Vector3(1,0,1)),"Recover tower");Check(s.Towers.Count==3,"Removed combat and view registration");
        Check(s.Foundations.Install(new Vector3(1,0,1),Vector3.left,HordeTowerKind.MachineGun,out _),"Recovered cell reusable");
        Check(!s.Spawn("abc")&&!s.Spawn("0")&&!s.Spawn("-1")&&!s.Spawn("4001"),"Input validation");
        root.Q<TextField>("amount").value="125";Click("spawn");Check(s.Enemies.Alive==125 && s.Running && input.Tool==WorldTool.Explore,"Spawn UI exact count and run");
        await Task.Delay(500);Check(s.Towers.Exists(t=>t.shot>0),"Legacy towers fire");
        Click("reset");Check(s.Enemies.Alive==0 && s.Towers.Count==4 && s.Foundations.Platforms.Count==2,"Reset only enemies");
        Check(s.Spawn("4000") && s.Enemies.Alive==4000,"4000 capacity");Check(!s.Spawn("1") && s.Enemies.Alive==4000,"No partial overflow");s.ResetEnemies();
        var rig=s.CameraRig;var saved=rig.Focus;rig.Pan(Vector2.right,1);Check(rig.Focus.x>saved.x,"Camera pan");rig.Pan(new Vector2(1,1),10000);Check(rig.Focus.x<=120 && rig.Focus.z<=120,"Camera bounds");rig.Focus=saved;
        rig.ChangeZoom(1000);Check(rig.Zoom==10,"Zoom-in limit");rig.ChangeZoom(-1000);Check(rig.Zoom==70,"Zoom-out limit");rig.Zoom=26;
        // Keep a small live example for the screenshot. These are Play-only objects, not saved into the scene.
        input.Select(WorldTool.Explore);s.Spawn("100");await Task.Delay(200);
        return "PASS: Terrain boundaries, adjacent/duplicate foundations, terrain-only rejection, four legacy towers, direction preview/cancel, top elevation, occupancy, edit/recover/reinstall, UI spawn validation/125 exact/4000 capacity/overflow, combat, enemy reset preserving buildings, camera pan/bounds/zoom.";
    }
}
