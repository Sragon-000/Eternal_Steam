using UnityEngine;
namespace EternalSteam.OpenWorld
{
    // Two line renderers are saved in the track prefab. Runtime only changes their geometry.
    public sealed class RailTrackView:MonoBehaviour
    {
        public LineRenderer LeftRail,RightRail;
        public void Configure(Vector2Int first,Vector2Int second)
        {
            if(LeftRail==null||RightRail==null)throw new System.InvalidOperationException("선로 프리팹의 레일 참조가 없습니다.");
            Vector3 a=new Vector3(first.x,0,first.y),b=new Vector3(second.x,0,second.y);
            bool straight=first+second==Vector2Int.zero;const int count=17;LeftRail.positionCount=RightRail.positionCount=count;
            LeftRail.useWorldSpace=RightRail.useWorldSpace=false;LeftRail.enabled=RightRail.enabled=true;
            for(int i=0;i<count;i++){float t=i/(float)(count-1);Vector3 p=straight?Vector3.Lerp(a,b,t):(1-t)*(1-t)*a+t*t*b;
                Vector3 tangent=straight?b-a:-2*(1-t)*a+2*t*b;Vector3 normal=new Vector3(-tangent.z,0,tangent.x).normalized*.45f;p.y=.1f;
                LeftRail.SetPosition(i,p+normal);RightRail.SetPosition(i,p-normal);}
        }
    }
}
