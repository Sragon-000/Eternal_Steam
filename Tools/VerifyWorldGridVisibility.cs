using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using EternalSteam.OpenWorld;
public static class VerifyWorldGridVisibility
{
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    public static string Main(){
        var s=UnityEngine.Object.FindFirstObjectByType<OpenWorldSandbox>();var input=s.GetComponent<OpenWorldInput>();var grid=s.WorldGrid;
        var update=typeof(WorldGridView).GetMethod("LateUpdate",BindingFlags.Instance|BindingFlags.NonPublic);
        var original=s.CameraRig.Focus;
        bool Rendered()=>grid.GetComponentsInChildren<MeshRenderer>().Any(r=>r.enabled);
        try {
            input.Cancel();Check(!grid.Visible&&!Rendered(),"Default/edit cancel hides grid");
            int count=grid.RebuildCount;s.CameraRig.Focus+=Vector3.right*300;update.Invoke(grid,null);Check(grid.RebuildCount==count,"Hidden grid does not rebuild");
            s.CameraRig.Focus=WorldGridGeometry.Center(WorldGridGeometry.Cell(original,128),128);
            input.BeginEditing();update.Invoke(grid,null);Check(grid.Visible&&Rendered(),"Edit shows grid");
            count=grid.RebuildCount;update.Invoke(grid,null);Check(grid.RebuildCount==count,"Stationary grid not rebuilt");
            s.CameraRig.Focus+=WorldGridGeometry.ToWorld(new Vector3(128,0,0));update.Invoke(grid,null);Check(grid.RebuildCount-count==3,"One chunk movement recycles three meshes");
            input.Cancel();Check(!grid.Visible&&!Rendered(),"Cancel immediately hides grid");
            Check(grid.GetComponentsInChildren<MeshFilter>().Length==9,"Nine reusable meshes");
            return "PASS edit-only grid; immediate cancellation hide; no hidden/stationary rebuild; one-chunk movement recycles 3 of 9 meshes.";
        }finally{input.Cancel();s.CameraRig.Focus=original;}
    }
}
