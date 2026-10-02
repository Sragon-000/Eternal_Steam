using UnityEngine;
namespace EternalSteam.OpenWorld
{
    [ExecuteAlways]
    public sealed class SpawnAreaView:MonoBehaviour
    {
        public OpenWorldSandbox Sandbox;
        public LineRenderer Boss;
        public LineRenderer[] Normal;
        void LateUpdate()
        {
            if(Sandbox==null||Sandbox.AssaultSettings==null)return;
            bool visible=!Application.isPlaying||Sandbox.GetComponent<OpenWorldInput>().IsEditing;
            Boss.enabled=visible;if(visible)Draw(Boss,Sandbox.Assault?.BossPosition??Sandbox.AssaultSettings.BossPosition,Sandbox.AssaultSettings.BossSpawnSize);
            for(int i=0;i<Normal.Length;i++){bool show=visible&&Sandbox.Assault!=null&&i<Sandbox.Assault.Planner.Points.Count;Normal[i].enabled=show;if(show)Draw(Normal[i],Sandbox.Assault.Coverage.Centers[Sandbox.Assault.Planner.Points[i]],Vector2Int.one);}
        }
        void Draw(LineRenderer line,Vector3 center,Vector2Int size){for(int i=0;i<5;i++){int corner=i%4;var p=center+WorldGridGeometry.ToWorld(new Vector3(corner==0||corner==3?-size.x:size.x,0,corner<2?-size.y:size.y));p.y=Sandbox.Ground.SampleHeight(p)+Sandbox.Ground.transform.position.y+.25f;line.SetPosition(i,p);}}
    }
}
