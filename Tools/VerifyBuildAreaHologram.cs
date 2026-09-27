using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using EternalSteam.OpenWorld;
public static class VerifyBuildAreaHologram
{
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    // Run in a fresh StartRegionSandbox Play session, before adding/upgrading bases.
    public static string Main()
    {
        var s=UnityEngine.Object.FindFirstObjectByType<OpenWorldSandbox>();
        Check(s!=null&&s.BuildAreaHologram!=null,"Start scene and imported hologram shader required");
        var input=s.GetComponent<OpenWorldInput>();Check(!input.IsEditing,"Use a fresh session without pending edits");
        var view=s.BuildAreaHologram;var focus=s.CameraRig.Focus;
        var update=typeof(BuildAreaHologramView).GetMethod("LateUpdate",BindingFlags.Instance|BindingFlags.NonPublic);
        bool Rendered()=>view.GetComponentsInChildren<MeshRenderer>().Any(r=>r.enabled);
        try {
            s.CameraRig.Focus=s.Content.MainBase.Position;input.BeginEditing();update.Invoke(view,null);
            Check(view.Visible&&Rendered(),"Edit shows area meshes");
            Check(view.VisibleCoveredCellCount==121,"Fresh starting main must cover exactly 121 full cells");
            Check(view.GetComponentsInChildren<MeshFilter>().Length==9,"Nine reusable chunks");
            Check(view.GetComponentsInChildren<Collider>().Length==0,"No hologram colliders");
            int before=view.RebuildCount;for(int i=0;i<100;i++)update.Invoke(view,null);
            Check(before==view.RebuildCount,"Stationary view must not rebuild");
            s.CameraRig.Focus=WorldGridGeometry.Center(WorldGridGeometry.Cell(s.CameraRig.Focus,128),128);
            update.Invoke(view,null);before=view.RebuildCount;
            s.CameraRig.Focus+=WorldGridGeometry.ToWorld(new Vector3(128,0,0));update.Invoke(view,null);
            Check(view.RebuildCount-before==3,"One chunk movement recycles three meshes");
            input.Cancel();Check(!view.Visible&&!Rendered(),"Cancel hides immediately");
            before=view.RebuildCount;s.CameraRig.Focus+=Vector3.right*300;update.Invoke(view,null);
            Check(view.RebuildCount==before,"Hidden view must not rebuild");
            return "PASS 121 cells; nine chunks; edit-only visibility; no colliders; stationary/hidden cache; three recycled chunks.";
        } finally {input.Cancel();s.CameraRig.Focus=focus;}
    }
}
