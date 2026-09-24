using System;
using UnityEngine;
using UnityEngine.UIElements;
namespace EternalSteam.OpenWorld
{
    public sealed class OpenWorldMinimap:IDisposable
    {
        readonly OpenWorldSandbox sandbox;
        readonly VisualElement body,testBody;
        readonly MinimapElement view;
        readonly Texture2D terrainImage;
        readonly Button fold;
        readonly Action toggle;
        double nextObjects,nextView;
        public MinimapProjection Projection {get;}
        public bool Interacting=>view.Interacting;
        public OpenWorldMinimap(VisualElement root,OpenWorldSandbox sandbox)
        {
            this.sandbox=sandbox;view=root.Q<MinimapElement>("minimap");body=root.Q("minimap-body");testBody=root.Q("test-body");fold=root.Q<Button>("minimap-fold");
            Projection=MinimapProjection.ForTerrain(sandbox.Ground);terrainImage=CreateTerrain();view.style.backgroundImage=new StyleBackground(terrainImage);view.Navigate+=Navigate;
            toggle=()=>{bool collapsed=body.ClassListContains("minimap-hidden");body.EnableInClassList("minimap-hidden",!collapsed);fold.text=collapsed?"▲ 접기":"▼ 펼치기";};fold.clicked+=toggle;fold.focusable=false;
            Refresh(0);
        }
        public void Navigate(Vector2 point)
        {
            if(!float.IsFinite(point.x)||!float.IsFinite(point.y)||sandbox.CameraRig==null)return;
            sandbox.CameraRig.MoveFocus(Projection.ToWorld(point));sandbox.Persistence?.Changed();nextView=0;
        }
        Texture2D CreateTerrain()
        {
            const int side=128;var image=new Texture2D(side,side,TextureFormat.RGBA32,false){name="Minimap terrain overview",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
            var colors=new Color32[side*side];var ground=sandbox.Ground;var tiles=ground.GetComponent<TileWorldGround>();float sizeY=Mathf.Max(1,ground.terrainData.size.y);
            for(int y=0;y<side;y++)for(int x=0;x<side;x++){
                var point=Projection.ToWorld(new Vector2((x+.5f)/side,1-(y+.5f)/side));float height=ground.SampleHeight(point)/sizeY;
                bool inside=point.x>=ground.transform.position.x&&point.z>=ground.transform.position.z&&point.x<=ground.transform.position.x+ground.terrainData.size.x&&point.z<=ground.transform.position.z+ground.terrainData.size.z;
                bool playable=inside&&(tiles==null||tiles.IsPlayable(point));
                colors[y*side+x]=playable?Color.Lerp(new Color(.15f,.29f,.24f),new Color(.56f,.58f,.42f),Mathf.Clamp01(height*3)):new Color(.055f,.09f,.13f);
            }
            image.SetPixels32(colors);image.Apply(false,true);return image;
        }
        public void Refresh(double now)
        {
            if(body.ClassListContains("minimap-hidden")||testBody.resolvedStyle.display==DisplayStyle.None)return;
            if(now>=nextObjects){nextObjects=now+.2;Array.Clear(view.Density,0,view.Density.Length);view.Buildings.Clear();
                foreach(var b in sandbox.Content.Bases.Buildings)if(b.Active&&!b.Disposed)view.Buildings.Add(new MinimapElement.Marker{Position=Projection.ToMap(b.Position),Base=b.Module<IBaseIdentity>()!=null});
                if(sandbox.Enemies.Alive>0)for(int i=0;i<sandbox.Enemies.MaxCount;i++){ref readonly var enemy=ref sandbox.Enemies.GetEnemy(i);if(!enemy.alive)continue;var p=Projection.ToMap(enemy.position);if(p.x<0||p.x>1||p.y<0||p.y>1)continue;int x=Mathf.Min(31,(int)(p.x*32)),y=Mathf.Min(31,(int)(p.y*32));view.Density[y*32+x]++;}
                int boss=sandbox.Assault?.BossId??-1;view.HasBoss=boss>=0&&boss<sandbox.Enemies.MaxCount&&sandbox.Enemies.GetEnemy(boss).alive&&sandbox.Enemies.Generation(boss)==sandbox.Assault.BossGeneration;if(view.HasBoss)view.Boss=Projection.ToMap(sandbox.Enemies.GetEnemy(boss).position);
            }
            if(now<nextView)return;nextView=now+.05;view.Focus=Projection.ToMap(sandbox.CameraRig.Focus);var plane=new Plane(Vector3.up,sandbox.CameraRig.Focus);view.HasViewport=true;
            for(int i=0;i<4;i++){var uv=i switch{0=>new Vector3(0,0),1=>new Vector3(1,0),2=>new Vector3(1,1),_=>new Vector3(0,1)};var ray=sandbox.CameraRig.View.ViewportPointToRay(uv);if(!plane.Raycast(ray,out float distance)){view.HasViewport=false;break;}view.ViewCorners[i]=Projection.ToMap(ray.GetPoint(distance));}
            view.MarkDirtyRepaint();
        }
        public void Dispose(){view.Navigate-=Navigate;fold.clicked-=toggle;view.style.backgroundImage=StyleKeyword.None;UnityEngine.Object.Destroy(terrainImage);}
    }
}
