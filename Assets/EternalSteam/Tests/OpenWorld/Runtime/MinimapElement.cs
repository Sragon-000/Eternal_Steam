using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
namespace EternalSteam.OpenWorld
{
    [UxmlElement]
    public sealed partial class MinimapElement:VisualElement
    {
        public struct Marker {public Vector2 Position;public bool Base;}
        public const int DensitySize=32;
        public readonly int[] Density=new int[DensitySize*DensitySize];
        public readonly List<Marker> Buildings=new(256);
        public readonly Vector2[] ViewCorners=new Vector2[4];
        public Vector2 Focus,Boss;
        public bool HasBoss,HasViewport;
        public bool Interacting {get;private set;}
        public event Action<Vector2> Navigate;
        public MinimapElement()
        {
            focusable=false;generateVisualContent+=Draw;
            RegisterCallback<PointerDownEvent>(e=>{if(e.button!=0)return;Interacting=true;this.CapturePointer(e.pointerId);Move(e.localPosition);e.StopPropagation();});
            RegisterCallback<PointerMoveEvent>(e=>{if(!Interacting||!this.HasPointerCapture(e.pointerId))return;Move(e.localPosition);e.StopPropagation();});
            RegisterCallback<PointerUpEvent>(e=>{if(!Interacting||e.button!=0)return;Interacting=false;this.ReleasePointer(e.pointerId);e.StopPropagation();});
            RegisterCallback<PointerCaptureOutEvent>(_=>Interacting=false);
        }
        void Move(Vector3 point){var r=contentRect;if(r.width<=0||r.height<=0)return;Navigate?.Invoke(new Vector2(Mathf.Clamp01((point.x-r.x)/r.width),Mathf.Clamp01((point.y-r.y)/r.height)));}
        Vector2 Pixel(Vector2 p)=>contentRect.position+Vector2.Scale(p,contentRect.size);
        static bool Inside(Vector2 p)=>p.x>=0&&p.x<=1&&p.y>=0&&p.y<=1;
        static void Box(Painter2D p,Vector2 a,Vector2 b){p.BeginPath();p.MoveTo(a);p.LineTo(new Vector2(b.x,a.y));p.LineTo(b);p.LineTo(new Vector2(a.x,b.y));p.ClosePath();p.Fill();}
        void Draw(MeshGenerationContext context)
        {
            if(contentRect.width<1)return;var p=context.painter2D;
            for(int i=0;i<Density.Length;i++)if(Density[i]>0){float a=Mathf.Min(.95f,.45f+Mathf.Log(1+Density[i])*.1f);p.fillColor=new Color(1,.2f,.16f,a);var v=new Vector2(i%DensitySize,i/DensitySize)/(float)DensitySize;Box(p,Pixel(v),Pixel(v+Vector2.one/DensitySize));}
            foreach(var marker in Buildings){if(!Inside(marker.Position))continue;var c=Pixel(marker.Position);float r=marker.Base?4:2;p.fillColor=marker.Base?new Color(1,.83f,.2f):new Color(.3f,.95f,.95f);Box(p,c-Vector2.one*r,c+Vector2.one*r);}
            if(HasBoss&&Inside(Boss)){p.fillColor=new Color(1,.2f,1);p.BeginPath();p.Arc(Pixel(Boss),5,Angle.Degrees(0),Angle.Degrees(360));p.Fill();}
            if(HasViewport){p.strokeColor=Color.white;p.lineWidth=1.5f;p.BeginPath();for(int i=0;i<4;i++){var v=Pixel(new Vector2(Mathf.Clamp01(ViewCorners[i].x),Mathf.Clamp01(ViewCorners[i].y)));if(i==0)p.MoveTo(v);else p.LineTo(v);}p.ClosePath();p.Stroke();}
            if(Inside(Focus)){var c=Pixel(Focus);p.strokeColor=Color.white;p.lineWidth=2;p.BeginPath();p.MoveTo(c-Vector2.right*4);p.LineTo(c+Vector2.right*4);p.MoveTo(c-Vector2.up*4);p.LineTo(c+Vector2.up*4);p.Stroke();}
        }
    }
}
