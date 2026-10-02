using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using EternalSteam;
using EternalSteam.OpenWorld;

// Read-only geometry aid for physical pointer QA. Never places or changes content.
public static class InspectPointerRailLayout
{
    public static string Main()
    {
        var s=UnityEngine.Object.FindFirstObjectByType<OpenWorldSandbox>();
        var c=s.Content;var g=c.GroundWorld.Grid;
        var rel=new List<Vector2Int>();
        for(int x=2;x<=5;x++)rel.Add(new Vector2Int(x,1));rel.Add(new Vector2Int(5,0));
        for(int y=-1;y>=-6;y--)rel.Add(new Vector2Int(7,y));
        for(int x=6;x>=1;x--)rel.Add(new Vector2Int(x,-6));
        for(int y=-5;y<=-1;y++)rel.Add(new Vector2Int(1,y));
        bool Free(Vector2Int p,Vector2Int size)=>g.Cells(p,size).All(v=>!g.IsOccupied(v))&&c.CheckGround(p,size,out _,out _);
        bool Station(Vector2Int p)=>Free(p,new Vector2Int(2,2))&&c.Bases.Covers(g.Center(p,new Vector2Int(2,2)),new Vector2(2,2),g.Rotation,c.Bases.SelectedBaseId);
        var options=new List<Vector2Int>();
        for(int y=12;y<=24;y++)for(int x=-9;x<=6;x++){
            var a=new Vector2Int(x,y);var b=a+new Vector2Int(6,0);
            if(Station(a)&&Station(b)&&rel.All(r=>Free(a+r,Vector2Int.one)))options.Add(a);
        }
        if(options.Count==0)return "No free template";
        var origin=options.OrderBy(v=>(v-new Vector2Int(-3,12)).sqrMagnitude).First();
        var cam=s.CameraRig.GetComponent<Camera>();if(cam==null)cam=Camera.main;
        string Point(string label,Vector2Int cell){var p=g.Center(cell,Vector2Int.one);p.y=s.Ground.SampleHeight(p)+s.Ground.transform.position.y;var q=cam.WorldToScreenPoint(p);return label+" cell="+cell+" cua="+(220+2*q.x).ToString("0")+","+(262+2*(720-q.y)).ToString("0");}
        return Point("A",origin)+"\n"+Point("B",origin+new Vector2Int(6,0))+"\n"+string.Join("\n",rel.Select(v=>Point("track",origin+v)));
    }
}
