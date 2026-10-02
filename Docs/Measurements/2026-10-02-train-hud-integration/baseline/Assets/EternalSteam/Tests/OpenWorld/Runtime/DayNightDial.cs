using UnityEngine;
using UnityEngine.UIElements;
namespace EternalSteam.OpenWorld
{
    [UxmlElement]
    public sealed partial class DayNightDial : VisualElement
    {
        float turns;
        [UxmlAttribute]
        public float Turns {get=>turns;set {if(Mathf.Approximately(turns,value))return;turns=value;MarkDirtyRepaint();}}
        public DayNightDial()
        {
            generateVisualContent+=Draw;
        }
        void Draw(MeshGenerationContext context)
        {
            var p=context.painter2D;var c=contentRect.center;float r=Mathf.Min(contentRect.width,contentRect.height)*.5f-5;
            if(r<=0)return;
            p.fillColor=new Color(.91f,.72f,.36f);p.BeginPath();p.MoveTo(c);p.Arc(c,r,Angle.Degrees(180),Angle.Degrees(360));p.ClosePath();p.Fill();
            p.fillColor=new Color(.16f,.23f,.38f);p.BeginPath();p.MoveTo(c);p.Arc(c,r,Angle.Degrees(0),Angle.Degrees(180));p.ClosePath();p.Fill();
            p.strokeColor=new Color(.72f,.8f,.82f);p.lineWidth=2;p.BeginPath();p.Arc(c,r,Angle.Degrees(0),Angle.Degrees(360));p.Stroke();
            for(int i=0;i<12;i++){float a=i*Mathf.PI/6;var v=new Vector2(Mathf.Cos(a),Mathf.Sin(a));p.BeginPath();p.MoveTo(c+v*(r-7));p.LineTo(c+v*(r-2));p.Stroke();}
            float angle=(.5f+turns)*Mathf.PI*2;var end=c+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*(r-13);
            p.strokeColor=Color.white;p.lineWidth=4;p.BeginPath();p.MoveTo(c);p.LineTo(end);p.Stroke();
            p.fillColor=Color.white;p.BeginPath();p.Arc(c,5,Angle.Degrees(0),Angle.Degrees(360));p.Fill();
        }
    }
}
