using UnityEditor;
using UnityEditor.UIElements;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;
using EternalSteam.Demo;
namespace EternalSteam.OpenWorld.Editor
{
    public sealed class OpenWorldAuthoringWindow : EditorWindow
    {
        OpenWorldSandbox world;
        WorldTool tool;
        HordeTowerKind kind;
        Vector3? anchor;
        Label message;
        [MenuItem("Eternal Steam/Open World/씬 배치 편집")]
        public static void Open()=>GetWindow<OpenWorldAuthoringWindow>("테스트 공간 편집");
        void OnEnable()=>SceneView.duringSceneGui+=SceneGUI;
        void OnDisable()=>SceneView.duringSceneGui-=SceneGUI;
        public void CreateGUI()
        {
            world=Object.FindFirstObjectByType<OpenWorldSandbox>();
            rootVisualElement.Add(new HelpBox("Play 없이 Scene 뷰에서 배치합니다. 토대·포탑은 실제 프리팹 개체이며 씬 저장과 Undo를 지원합니다.",HelpBoxMessageType.Info));
            var field=new ObjectField("테스트 공간"){objectType=typeof(OpenWorldSandbox),allowSceneObjects=true,value=world};field.RegisterValueChangedCallback(e=>world=e.newValue as OpenWorldSandbox);rootVisualElement.Add(field);
            Add("토대 설치",()=>Select(WorldTool.Foundation));
            foreach(HordeTowerKind k in System.Enum.GetValues(typeof(HordeTowerKind))){var choice=k;Add(HordeTowerStats.Name(k),()=>{kind=choice;Select(WorldTool.Tower);});}
            Add("수정 · 포탑 회수",()=>Select(WorldTool.Recover));Add("편집 종료 / 취소",()=>Select(WorldTool.Explore));
            Add("씬 저장",()=>{if(world!=null&&!Application.isPlaying)EditorSceneManager.SaveScene(world.gameObject.scene);});
            message=new Label("도구 선택 후 Scene 뷰에서 클릭하세요.");rootVisualElement.Add(message);
        }
        void Add(string label,System.Action action)=>rootVisualElement.Add(new Button(action){text=label});
        void Select(WorldTool value){tool=value;anchor=null;message.text=value==WorldTool.Tower?"토대 칸 → 공격 방향 순서로 클릭하세요.":"Scene 뷰에서 클릭 · Esc 취소 · Ctrl/Cmd+Z Undo";SceneView.RepaintAll();}
        void SceneGUI(SceneView view)
        {
            if(world==null||Application.isPlaying||tool==WorldTool.Explore)return;
            var e=Event.current;if(e.alt)return;
            if(e.type==EventType.Layout)HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
            if(e.type==EventType.KeyDown&&e.keyCode==KeyCode.Escape){Select(WorldTool.Explore);e.Use();return;}
            if(!Physics.Raycast(HandleUtility.GUIPointToWorldRay(e.mousePosition),out var hit,2000))return;
            var point=hit.point;Handles.color=tool==WorldTool.Recover?Color.red:Color.cyan;
            var key=FoundationPlacement.Key(point);
            var center=WorldGridGeometry.Center(WorldGridGeometry.Cell(point,tool==WorldTool.Foundation?8:2),tool==WorldTool.Foundation?8:2,point.y+.1f);
            using(new Handles.DrawingScope(Matrix4x4.TRS(center,WorldGridGeometry.Rotation,Vector3.one)))
                Handles.DrawWireCube(Vector3.zero,new Vector3(tool==WorldTool.Foundation?8:2,.1f,tool==WorldTool.Foundation?8:2));
            if(anchor.HasValue)Handles.DrawLine(anchor.Value+Vector3.up*.2f,point+Vector3.up*.2f);
            if(e.type==EventType.MouseDown&&e.button==0){
                string reason;
                if(tool==WorldTool.Foundation)WorldAuthoring.Foundation(world,point,out reason);
                else if(tool==WorldTool.Recover)WorldAuthoring.Recover(world,point,out reason);
                else if(!anchor.HasValue){anchor=center;reason="공격 방향을 클릭하세요.";}
                else {if(WorldAuthoring.Tower(world,anchor.Value,point-anchor.Value,kind,out reason))anchor=null;}
                message.text=reason;e.Use();
            }
            if(e.type==EventType.MouseMove)view.Repaint();
        }
    }
}
