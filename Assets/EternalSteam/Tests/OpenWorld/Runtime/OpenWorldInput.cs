using UnityEngine;
using UnityEngine.InputSystem;
using EternalSteam.Demo;
namespace EternalSteam.OpenWorld
{
    public enum WorldTool { Explore, Foundation, Tower, Recover }
    public sealed class OpenWorldInput : MonoBehaviour
    {
        public OpenWorldSandbox Sandbox;
        public WorldTool Tool { get; private set; }
        public HordeTowerKind Kind { get; private set; }
        public bool PointerOverUI;
        public bool ChoosingDirection { get; private set; }
        Vector3 selected;
        GameObject preview;
        LineRenderer directionLine;
        public void Select(WorldTool tool,HordeTowerKind kind=HordeTowerKind.MachineGun)
        {
            Tool=tool;Kind=kind;ChoosingDirection=false;
            if(tool!=WorldTool.Explore)Sandbox.Running=false;
            Sandbox.Message=tool==WorldTool.Foundation?"클릭: 토대 설치 · 평탄한 지형을 선택하세요.":tool==WorldTool.Tower?"토대 위 칸 선택 → 공격 방향 클릭":tool==WorldTool.Recover?"수정 모드 · 포탑이 있는 칸을 클릭해 회수하세요.":"WASD 이동 · 휠 확대/축소";
        }
        void Start()
        {
            preview=GameObject.CreatePrimitive(PrimitiveType.Cube);preview.name="Placement preview";
            Destroy(preview.GetComponent<Collider>());preview.SetActive(false);
            directionLine=HordeVisualPrimitives.MakeLine("Direction preview",transform,Sandbox.ValidMaterial,.12f,2);directionLine.enabled=false;
        }
        public bool ClickWorld(Vector3 point)
        {
            string reason;
            if(Tool==WorldTool.Foundation){bool ok=Sandbox.Foundations.AddFoundation(point,out reason);Sandbox.Message=reason;return ok;}
            if(Tool==WorldTool.Recover){bool ok=Sandbox.Foundations.Recover(point,out reason);Sandbox.Message=reason;return ok;}
            if(Tool!=WorldTool.Tower)return false;
            if(!ChoosingDirection) {
                if(!Sandbox.Foundations.FindCell(point,out var p,out var cell,out selected)){Sandbox.Message="먼저 토대를 설치한 뒤 토대 위의 칸을 선택하세요.";return false;}
                if(p.World.Grid.IsOccupied(cell)){Sandbox.Message="이미 포탑이 있는 칸입니다.";return false;}
                ChoosingDirection=true;Sandbox.Message="공격 방향을 클릭하세요. 우클릭 / Esc로 취소";return true;
            }
            bool installed=Sandbox.Foundations.Install(selected,point-selected,Kind,out reason);Sandbox.Message=reason;
            if(installed)ChoosingDirection=false;return installed;
        }
        void Update()
        {
            var mouse=Mouse.current;if(mouse==null)return;
            if((Keyboard.current?.escapeKey.wasPressedThisFrame??false)||mouse.rightButton.wasPressedThisFrame){Select(WorldTool.Explore);}
            preview.SetActive(false);directionLine.enabled=false;
            if(PointerOverUI || Tool==WorldTool.Explore)return;
            var ray=Sandbox.CameraRig.View.ScreenPointToRay(mouse.position.ReadValue());
            if(!Physics.Raycast(ray,out var hit,1000))return;
            Vector3 center=hit.point;bool valid=false;
            if(Tool==WorldTool.Foundation) {
                valid=Sandbox.Foundations.CheckFoundation(hit.point,out center,out _);preview.transform.localScale=new Vector3(7.9f,.12f,7.9f);
            } else if(ChoosingDirection) {
                center=selected;valid=true;preview.transform.localScale=new Vector3(1.8f,.12f,1.8f);
                Vector3 d=hit.point-selected;d.y=0;directionLine.enabled=true;directionLine.SetPosition(0,selected+Vector3.up*.25f);
                directionLine.SetPosition(1,selected+Vector3.up*.25f+d.normalized*22);
            } else {
                if(Sandbox.Foundations.FindCell(hit.point,out var p,out var cell,out center))valid=Tool==WorldTool.Recover?p.World.Grid.IsOccupied(cell):!p.World.Grid.IsOccupied(cell);
                preview.transform.localScale=new Vector3(1.8f,.12f,1.8f);
            }
            preview.transform.position=center+Vector3.up*.09f;preview.GetComponent<Renderer>().sharedMaterial=valid?Sandbox.ValidMaterial:Sandbox.InvalidMaterial;preview.SetActive(true);
            if(mouse.leftButton.wasPressedThisFrame)ClickWorld(hit.point);
        }
        void OnDestroy(){if(preview!=null)Destroy(preview);}
    }
}
