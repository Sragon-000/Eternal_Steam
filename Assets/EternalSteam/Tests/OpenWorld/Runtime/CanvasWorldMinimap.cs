using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
namespace EternalSteam.OpenWorld
{
    // One authored UI Graphic; markers are mesh data, never runtime GameObjects.
    public sealed class CanvasWorldMinimap:UnityEngine.UI.MaskableGraphic,IPointerDownHandler,IDragHandler,IPointerUpHandler,ICanvasRaycastFilter
    {
        public Color LowTerrainColor=new Color(.38f,.25f,.14f),HighTerrainColor=new Color(.67f,.46f,.26f),BackgroundColor=new Color(.035f,.065f,.085f);
        struct Marker {public Vector2 Point;public bool Base;}
        readonly List<Marker> buildings=new();readonly int[] density=new int[1024];readonly Vector2[] corners=new Vector2[4];
        OpenWorldSandbox sandbox;MinimapProjection projection,terrainProjection,navigationProjection;
        public Rect VisibleWorldBounds=>projection.Bounds;
        public const float VisibleWorldSide=150;Texture2D terrainImage;double nextObjects,nextView;Vector2 focus,boss;bool hasBoss,hasViewport;int pointerId=int.MinValue;
        public bool Interacting {get;private set;}
        public int ObjectRefreshCount {get;private set;}
        public override Texture mainTexture=>terrainImage!=null?terrainImage:Texture2D.whiteTexture;
        public void Initialize(OpenWorldSandbox world)
        {
            sandbox=world;terrainProjection=MinimapProjection.ForTerrain(world.Ground);projection=MinimapProjection.Around(ViewCenter(),VisibleWorldSide);const int side=512;
            terrainImage=new Texture2D(side,side,TextureFormat.RGBA32,false){name="Minimap terrain data",filterMode=FilterMode.Bilinear};var colors=new Color32[side*side];var ground=world.Ground;var tiles=ground.GetComponent<TileWorldGround>();
            for(int y=0;y<side;y++)for(int x=0;x<side;x++){var p=terrainProjection.ToWorld(new Vector2((x+.5f)/side,1-(y+.5f)/side));var o=ground.transform.position;var size=ground.terrainData.size;bool inside=p.x>=o.x&&p.z>=o.z&&p.x<=o.x+size.x&&p.z<=o.z+size.z;bool playable=inside&&(tiles==null||tiles.IsPlayable(p));colors[y*side+x]=playable?Color.Lerp(LowTerrainColor,HighTerrainColor,Mathf.Clamp01(ground.SampleHeight(p)/Mathf.Max(1,size.y)*3)):BackgroundColor;}
            // White texel for tinted marker vertices sharing the terrain material.
            colors[0]=Color.white;terrainImage.SetPixels32(colors);terrainImage.Apply(false,true);SetMaterialDirty();SetVerticesDirty();
        }
        void Update()
        {
            if(sandbox==null)return;double now=Time.unscaledTimeAsDouble;
            if(now<nextView)return;nextView=now+.05;var next=MinimapProjection.Around(ViewCenter(),VisibleWorldSide);if(next.Bounds!=projection.Bounds){projection=next;nextObjects=0;}
            if(now>=nextObjects){nextObjects=now+.2;ObjectRefreshCount++;buildings.Clear();Array.Clear(density,0,density.Length);
                foreach(var b in sandbox.Content.Bases.Buildings)if(b.Active&&!b.Disposed)buildings.Add(new Marker{Point=projection.ToMap(b.Position),Base=b.Module<IBaseIdentity>()!=null});
                if(sandbox.Enemies.Alive>0)for(int i=0;i<sandbox.Enemies.MaxCount;i++){ref readonly var e=ref sandbox.Enemies.GetEnemy(i);if(!e.alive)continue;var p=projection.ToMap(e.position);if(!Inside(p))continue;density[Mathf.Min(31,(int)(p.y*32))*32+Mathf.Min(31,(int)(p.x*32))]++;}
                int id=sandbox.Assault?.BossId??-1;hasBoss=id>=0&&id<sandbox.Enemies.MaxCount&&sandbox.Enemies.GetEnemy(id).alive&&sandbox.Enemies.Generation(id)==sandbox.Assault.BossGeneration;if(hasBoss)boss=projection.ToMap(sandbox.Enemies.GetEnemy(id).position);
            }
            focus=projection.ToMap(sandbox.CameraRig.Focus);var plane=new Plane(Vector3.up,sandbox.CameraRig.Focus);hasViewport=true;
            for(int i=0;i<4;i++){var uv=i switch{0=>new Vector3(0,0),1=>new Vector3(1,0),2=>new Vector3(1,1),_=>new Vector3(0,1)};var ray=sandbox.CameraRig.View.ViewportPointToRay(uv);if(!plane.Raycast(ray,out float distance)){hasViewport=false;break;}corners[i]=projection.ToMap(ray.GetPoint(distance));}SetVerticesDirty();
        }
        Vector3 ViewCenter()
        {
            var rig=sandbox.CameraRig;var ray=rig.View.ViewportPointToRay(new Vector3(.5f,.5f));
            var collider=sandbox.Ground.GetComponent<TerrainCollider>();if(collider!=null&&collider.Raycast(ray,out var hit,2000))return hit.point;
            // Outside the terrain, keep the view center rather than clamping the minimap to the map edge.
            var plane=new Plane(Vector3.up,rig.Focus);return plane.Raycast(ray,out var distance)?ray.GetPoint(distance):rig.Focus;
        }
        void TerrainQuad(UnityEngine.UI.VertexHelper vh)
        {
            var a=projection.Bounds;var b=terrainProjection.Bounds;float x0=Mathf.Max(a.xMin,b.xMin),x1=Mathf.Min(a.xMax,b.xMax),z0=Mathf.Max(a.yMin,b.yMin),z1=Mathf.Min(a.yMax,b.yMax);if(x1<=x0||z1<=z0)return;
            int start=vh.currentVertCount;
            Add(new Vector3(x0,0,z0));Add(new Vector3(x0,0,z1));Add(new Vector3(x1,0,z1));Add(new Vector3(x1,0,z0));
            vh.AddTriangle(start,start+1,start+2);vh.AddTriangle(start,start+2,start+3);
            void Add(Vector3 world){var uv=terrainProjection.ToMap(world);vh.AddVert(Pixel(projection.ToMap(world)),Color.white,new Vector2(uv.x,1-uv.y));}
        }
        public static bool ContainsNormalized(Vector2 p)=>(p-Vector2.one*.5f).sqrMagnitude<=.25f;
        static bool Inside(Vector2 p)=>ContainsNormalized(p);
        public bool ContainsScreenPoint(Vector2 screen,Camera camera=null)
        {
            if(!isActiveAndEnabled||!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform,screen,camera,out var p))return false;
            var r=rectTransform.rect;return r.width>0&&r.height>0&&ContainsNormalized(new Vector2((p.x-r.xMin)/r.width,(p.y-r.yMin)/r.height));
        }
        public bool IsRaycastLocationValid(Vector2 screen,Camera camera)=>ContainsScreenPoint(screen,camera);
        Vector2 Pixel(Vector2 p){var r=rectTransform.rect;return new Vector2(r.xMin+p.x*r.width,r.yMax-p.y*r.height);}
        static void Quad(UnityEngine.UI.VertexHelper vh,Vector2 a,Vector2 b,Vector2 c,Vector2 d,Color color,bool background=false)
        {
            int start=vh.currentVertCount;var white=Vector2.one*(.5f/512);
            vh.AddVert(a,color,background?new Vector2(0,0):white);vh.AddVert(b,color,background?new Vector2(0,1):white);vh.AddVert(c,color,background?new Vector2(1,1):white);vh.AddVert(d,color,background?new Vector2(1,0):white);vh.AddTriangle(start,start+1,start+2);vh.AddTriangle(start,start+2,start+3);
        }
        void Box(UnityEngine.UI.VertexHelper vh,Vector2 p,float radius,Color color){var r=rectTransform.rect;var a=Vector2.Max(p-Vector2.one*radius,r.min);var b=Vector2.Min(p+Vector2.one*radius,r.max);Quad(vh,a,new Vector2(a.x,b.y),b,new Vector2(b.x,a.y),color);}
        static void Line(UnityEngine.UI.VertexHelper vh,Vector2 a,Vector2 b){var n=new Vector2(-(b-a).y,(b-a).x).normalized*.7f;Quad(vh,a+n,b+n,b-n,a-n,Color.white);}
        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper vh)
        {
            vh.Clear();var r=rectTransform.rect;Quad(vh,r.min,new Vector2(r.xMin,r.yMax),r.max,new Vector2(r.xMax,r.yMin),BackgroundColor);if(sandbox==null)return;TerrainQuad(vh);
            for(int i=0;i<density.Length;i++)if(density[i]>0){var a=Pixel(new Vector2(i%32,i/32)/32f);var b=Pixel(new Vector2(i%32+1,i/32+1)/32f);Quad(vh,a,new Vector2(a.x,b.y),b,new Vector2(b.x,a.y),new Color(1,.2f,.16f,Mathf.Min(.95f,.45f+Mathf.Log(1+density[i])*.1f)));}
            foreach(var b in buildings)if(Inside(b.Point))Box(vh,Pixel(b.Point),b.Base?4:2,b.Base?new Color(1,.83f,.2f):Color.cyan);
            if(hasBoss&&Inside(boss))Box(vh,Pixel(boss),5,Color.magenta);
            if(hasViewport)for(int i=0;i<4;i++)Line(vh,Pixel(corners[i]),Pixel(corners[(i+1)%4]));if(Inside(focus)){var p=Pixel(focus);Line(vh,p-Vector2.right*4,p+Vector2.right*4);Line(vh,p-Vector2.up*4,p+Vector2.up*4);}
        }
        public void OnPointerDown(PointerEventData e){if(e.button!=PointerEventData.InputButton.Left||!ContainsScreenPoint(e.position,e.pressEventCamera))return;Interacting=true;navigationProjection=projection;pointerId=e.pointerId;Navigate(e);}
        public void OnDrag(PointerEventData e){if(Interacting&&pointerId==e.pointerId)Navigate(e);}
        public void OnPointerUp(PointerEventData e){if(pointerId==e.pointerId)CancelInteraction();}
        void Navigate(PointerEventData e){if(sandbox==null||!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform,e.position,e.pressEventCamera,out var p))return;var r=rectTransform.rect;var uv=new Vector2((p.x-r.xMin)/r.width,1-(p.y-r.yMin)/r.height);if(!Inside(uv))return;var world=navigationProjection.ToWorld(uv);var ground=sandbox.Ground;var origin=ground.transform.position;var size=ground.terrainData.size;if(world.x<origin.x||world.z<origin.z||world.x>origin.x+size.x||world.z>origin.z+size.z)return;if(ground.TryGetComponent<TileWorldGround>(out var tiles)&&!tiles.IsPlayable(world))return;sandbox.CameraRig.MoveFocus(world);sandbox.Persistence?.Changed();}
        public void CancelInteraction(){Interacting=false;pointerId=int.MinValue;}
        protected override void OnRectTransformDimensionsChange(){CancelInteraction();base.OnRectTransformDimensionsChange();}
        protected override void OnDisable(){CancelInteraction();base.OnDisable();}
        protected override void OnDestroy(){if(terrainImage!=null)Destroy(terrainImage);base.OnDestroy();}
    }
}
