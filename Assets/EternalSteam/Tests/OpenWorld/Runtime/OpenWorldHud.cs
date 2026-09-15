using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using EternalSteam.Demo;
namespace EternalSteam.OpenWorld
{
    [DefaultExecutionOrder(-100)]
    public sealed class OpenWorldHud : MonoBehaviour
    {
        public OpenWorldSandbox Sandbox;
        public OpenWorldInput Input;
        VisualElement root, panel;
        TextField amount;
        SpawnQuantityInput quantity;
        Label message,counts,mode;
        Button run;
        float refresh;
        void Start()
        {
            root=GetComponent<UIDocument>().rootVisualElement;panel=root.Q("panel");amount=root.Q<TextField>("amount");
            quantity=new SpawnQuantityInput(root,amount);
            message=root.Q<Label>("message");counts=root.Q<Label>("counts");mode=root.Q<Label>("mode");run=root.Q<Button>("run");
            Bind("foundation",()=>Input.Select(WorldTool.Foundation));Bind("edit",()=>Input.Select(WorldTool.Recover));
            Bind("explore",()=>Input.Select(WorldTool.Explore));
            var list=root.Q("towers");
            foreach(HordeTowerKind kind in System.Enum.GetValues(typeof(HordeTowerKind))) {
                if(list==null)break;
                var captured=kind;list.Add(new Button(()=>{quantity.EndEdit();Input.Select(WorldTool.Tower,captured);}){text=HordeTowerStats.Name(kind),focusable=false});
            }
            Bind("spawn",()=>{if(Sandbox.Spawn(amount.value)){var m=Sandbox.Message;Input.Select(WorldTool.Explore);Sandbox.Message=m;}});
            Bind("reset",()=>Sandbox.ResetEnemies());
            run.focusable=false;
            run.clicked+=()=>{quantity.EndEdit();bool next=!Sandbox.Running;Input.Select(WorldTool.Explore);Sandbox.Running=next;};
        }
        void OnDestroy()=>quantity?.Dispose();
        void Bind(string name,System.Action action) { var button=root.Q<Button>(name);if(button!=null){button.focusable=false;button.clicked+=()=>{quantity.EndEdit();action();};} }
        void Update()
        {
            if(root==null)return;
            var mouse=Mouse.current;
            bool over=mouse!=null && panel.worldBound.Contains(RuntimePanelUtils.ScreenToPanel(root.panel,new Vector2(mouse.position.x.ReadValue(),Screen.height-mouse.position.y.ReadValue())));
            Input.PointerOverUI=over;Sandbox.CameraRig.BlockPointer=over;
            if(mouse!=null && mouse.leftButton.wasPressedThisFrame) {
                var pointer=RuntimePanelUtils.ScreenToPanel(root.panel,new Vector2(mouse.position.x.ReadValue(),Screen.height-mouse.position.y.ReadValue()));
                if(!amount.worldBound.Contains(pointer))quantity.EndEdit();
            }
            if(Keyboard.current?.escapeKey.wasPressedThisFrame??false)quantity.EndEdit();
            Sandbox.CameraRig.BlockKeyboard=quantity.Editing;
            if(Time.unscaledTime<refresh)return;refresh=Time.unscaledTime+.1f;
            message.text=Sandbox.Message;counts.text=$"토대 {Sandbox.Foundations.Platforms.Count} · 포탑 {Sandbox.Towers.Count}\n적 {Sandbox.Enemies.Alive:N0} / 4,000 · 처치 {Sandbox.Enemies.Killed:N0}";
            mode.text=Sandbox.Running?"실행 중":Input.Tool==WorldTool.Recover?"수정 · 회수":Input.Tool==WorldTool.Tower?"포탑 설치":Input.Tool==WorldTool.Foundation?"토대 설치":"일시정지";
            run.text=Sandbox.Running?"일시정지":"전투 실행";
            root.Q<Button>("foundation")?.EnableInClassList("selected",Input.Tool==WorldTool.Foundation);
            root.Q<Button>("edit")?.EnableInClassList("selected",Input.Tool==WorldTool.Recover);
        }
    }
}
