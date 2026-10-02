using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using EternalSteam.Railway;
namespace EternalSteam.OpenWorld
{
    // Owns one pointer gesture, not the edit transaction. Clicks remain delegated to the existing input.
    public sealed class ConstructionDragEditor:IDisposable
    {
        const float ThresholdPixels=6;
        readonly OpenWorldInput input;
        readonly OpenWorldSandbox sandbox;
        readonly WorldEditSession edits;
        readonly WallPlacementStroke stroke;
        readonly List<BuildingSelectionItem> candidates=new();
        readonly LineRenderer rectangle;
        readonly Material material;
        Vector2 startScreen;
        Vector3? startWorld;
        BuildingDefinition definition;
        BuildingDefinition wallDefinition;
        bool dragged,paint;
        Vector3? wallStart;
        public static Vector3 WallEnd(Vector3 start,Vector3 point)
        {
            var a=WorldGridGeometry.Cell(start,2);var b=WorldGridGeometry.Cell(point,2);
            if(Math.Abs(b.x-a.x)>=Math.Abs(b.y-a.y))b.y=a.y;else b.x=a.x;
            return WorldGridGeometry.Center(b,2);
        }
        public void PreviewWall(Vector3? point,bool overUI)
        {
            if(!wallStart.HasValue)return;
            rectangle.enabled=false;
            if(overUI||!point.HasValue)return;
            var a=wallStart.Value;var b=WallEnd(a,point.Value);
            a.y=sandbox.Ground.SampleHeight(a)+sandbox.Ground.transform.position.y+.2f;
            b.y=sandbox.Ground.SampleHeight(b)+sandbox.Ground.transform.position.y+.2f;
            rectangle.positionCount=2;rectangle.widthMultiplier=.15f;rectangle.SetPosition(0,a);rectangle.SetPosition(1,b);rectangle.enabled=true;
            if(wallDefinition!=null){
                var start=WorldGridGeometry.Cell(a,2);var end=WorldGridGeometry.Cell(b,2);int count=Math.Abs(end.x-start.x)+Math.Abs(end.y-start.y)+1;
                var quote=edits.Quote(wallDefinition,count);int failures=0;Vector2Int first=default;string why=null;
                if(count<=512)for(int i=0;i<count;i++){var cell=new Vector2Int(start.x+Math.Sign(end.x-start.x)*i,start.y+Math.Sign(end.y-start.y)*i);if(!edits.PreviewContent(wallDefinition,WorldGridGeometry.Center(cell,2),out _,out _,out _,out var reason)){if(failures++==0){first=cell;why=reason;}}}
                rectangle.startColor=rectangle.endColor=failures>0||!quote.Affordable?Color.red:Color.cyan;
                string kind=wallDefinition.Modules.OfType<RailFacilityDefinition>().Any(r=>r.Kind==RailFacilityKind.Track)?"선로":"방벽";
                sandbox.Message=$"{kind} {count}칸 직선 · {quote.Summary}"+(failures>0?$" · 설치 불가 {failures}칸 (첫 칸 {first.x},{first.y}: {why})":quote.Reason==null?" · 클릭하면 임시 예약":" · "+quote.Reason);
            }
        }
        public bool Active {get;private set;}
        public ConstructionDragEditor(OpenWorldInput input,OpenWorldSandbox sandbox,WorldEditSession edits)
        {
            this.input=input;this.sandbox=sandbox;this.edits=edits;stroke=new WallPlacementStroke(edits);
            var view=new GameObject("Drag selection rectangle");view.transform.SetParent(input.transform,false);
            rectangle=view.AddComponent<LineRenderer>();rectangle.useWorldSpace=true;rectangle.positionCount=5;rectangle.enabled=false;
            material=new Material(sandbox.LineMaterial);material.SetColor("_BaseColor",Color.cyan);material.SetColor("_Color",Color.cyan);
            material.SetInt("_ZTest",(int)CompareFunction.Always);material.renderQueue=4000;rectangle.sharedMaterial=material;
            rectangle.shadowCastingMode=ShadowCastingMode.Off;rectangle.receiveShadows=false;
        }
        public void Begin(Vector2 screen,Vector3? world,bool forceRectangle)
        {
            Reset();Active=true;startScreen=screen;startWorld=world;definition=input.SelectedDefinition;
            paint=!forceRectangle&&input.Tool==WorldTool.Content&&WallPlacementStroke.Supports(definition)&&world.HasValue;
        }
        public void Move(Vector2 screen,Vector3? world,bool overUI)
        {
            if(!Active)return;
            if(overUI){Abort();sandbox.Message="UI 위로 이동해 드래그를 취소했습니다. 이전 임시 작업은 유지됩니다.";return;}
            if(paint){PreviewWall(world,overUI);return;}
            if(!dragged&&(screen-startScreen).sqrMagnitude<ThresholdPixels*ThresholdPixels)return;
            dragged=true;
            var bounds=BuildingRectangleSelection.Bounds(startScreen,screen);
            BuildingRectangleSelection.Collect(sandbox.CameraRig.View,bounds,sandbox.Content.GroundWorld,sandbox.Foundations.Platforms,candidates);
            DrawRectangle(bounds);sandbox.Message=$"범위 내 건물 {candidates.Count}개 · 놓으면 회수 예정 · 토대는 유지";
        }

        public void Release(Vector2 screen,Vector3? world,bool overUI)
        {
            if(!Active)return;
            Move(screen,world,overUI);if(!Active)return;
            if(paint){
                var point=startWorld;var chosen=definition;Reset();
                if(!point.HasValue)return;
                if(!wallStart.HasValue){wallStart=point;wallDefinition=chosen;sandbox.Message=wallDefinition?.Modules.OfType<RailFacilityDefinition>().Any(r=>r.Kind==RailFacilityKind.Track)==true?"선로 시작점 선택 · 끝점을 좌클릭하세요 · 격자 축에 일직선 배치":"방벽 시작점 선택 · 끝점을 좌클릭하세요 · 격자 축에 일직선 배치";return;}
                stroke.Begin(wallDefinition);stroke.AddPoint(wallStart.Value);stroke.AddPoint(WallEnd(wallStart.Value,point.Value));
                sandbox.Message=$"{(wallDefinition?.Modules.OfType<RailFacilityDefinition>().Any(r=>r.Kind==RailFacilityKind.Track)==true?"선로":"방벽")} {stroke.Added}개 임시 배치 · 확정하면 설치";
                if(stroke.Rejected>0)sandbox.Message+=$" · 건너뜀 {stroke.Rejected}칸: {stroke.LastFailure}";
                stroke.Complete();wallStart=null;wallDefinition=null;return;
            }
            if(!dragged){var point=startWorld;Reset();if(point.HasValue)input.ClickWorld(point.Value);return;}
            int selected=0;string failure=null;
            foreach(var item in candidates){if(edits.SelectRecovery(item.World,item.Building,out var reason))selected++;else failure=reason;}
            sandbox.Message=failure??(selected>0?$"범위 내 {selected}개 회수 예정 · 확정하면 회수 / 수정 취소하면 유지":"범위 안에 회수 가능한 건물이 없습니다.");
            Reset();
        }
        void DrawRectangle(Rect bounds)
        {
            var camera=sandbox.CameraRig.View;float depth=camera.nearClipPlane+1;
            rectangle.enabled=true;rectangle.positionCount=5;
            float height=camera.orthographic?camera.orthographicSize*2:2*depth*Mathf.Tan(camera.fieldOfView*Mathf.Deg2Rad*.5f);
            rectangle.widthMultiplier=height/Mathf.Max(1,camera.pixelHeight)*2;
            rectangle.SetPosition(0,camera.ScreenToWorldPoint(new Vector3(bounds.xMin,bounds.yMin,depth)));
            rectangle.SetPosition(1,camera.ScreenToWorldPoint(new Vector3(bounds.xMin,bounds.yMax,depth)));
            rectangle.SetPosition(2,camera.ScreenToWorldPoint(new Vector3(bounds.xMax,bounds.yMax,depth)));
            rectangle.SetPosition(3,camera.ScreenToWorldPoint(new Vector3(bounds.xMax,bounds.yMin,depth)));
            rectangle.SetPosition(4,rectangle.GetPosition(0));
        }
        void Reset(){Active=false;dragged=false;paint=false;definition=null;candidates.Clear();rectangle.enabled=false;}
        public void Abort(){wallStart=null;wallDefinition=null;stroke.Cancel();Reset();}
        public void Dispose(){Abort();UnityEngine.Object.Destroy(rectangle.gameObject);UnityEngine.Object.Destroy(material);}
    }
}
