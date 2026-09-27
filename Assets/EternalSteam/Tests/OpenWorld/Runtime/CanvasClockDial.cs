using UnityEngine;
namespace EternalSteam.OpenWorld
{
    // Authored clock face; only the saved hand transform moves with game time.
    public sealed class CanvasClockDial:UnityEngine.UI.MaskableGraphic
    {
        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper vh)
        {
            vh.Clear();var rect=rectTransform.rect;var center=rect.center;float radius=Mathf.Min(rect.width,rect.height)*.5f;
            for(int i=0;i<64;i++){
                float a=i*Mathf.PI*2/64,b=(i+1)*Mathf.PI*2/64;var color=i<32?new Color(.85f,.59f,.2f):new Color(.12f,.22f,.42f);int n=vh.currentVertCount;
                vh.AddVert(center,color,Vector2.zero);vh.AddVert(center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,color,Vector2.zero);vh.AddVert(center+new Vector2(Mathf.Cos(b),Mathf.Sin(b))*radius,color,Vector2.zero);vh.AddTriangle(n,n+1,n+2);
            }
        }
    }
}
