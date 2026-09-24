using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using EternalSteam.OpenWorld;
public static class VerifyMapAssaultFrames
{
    static OpenWorldSandbox sandbox;
    static double started,remaining;
    static int frame,phase;
    public static string Main()
    {
        sandbox=UnityEngine.Object.FindFirstObjectByType<OpenWorldSandbox>();
        if(!Application.isPlaying)throw new Exception("Play required");
        EditorApplication.isPaused=false;Time.timeScale=1;sandbox.Clock.Paused=false;
        started=EditorApplication.timeSinceStartup;remaining=sandbox.Clock.RemainingSeconds;frame=Time.frameCount;phase=0;
        EditorApplication.update-=Observe;EditorApplication.update+=Observe;
        return "Observing real PlayerLoop frames for 2 seconds, then pause for 1 second";
    }
    static void Observe()
    {
        if(!Application.isPlaying){EditorApplication.update-=Observe;return;}
        EditorApplication.QueuePlayerLoopUpdate();
        if(EditorApplication.timeSinceStartup-started<(phase==0?2:1))return;
        if(phase==0){
            double advanced=remaining-sandbox.Clock.RemainingSeconds;
            File.WriteAllText("Temp/map-assault-frames.txt",$"frames={Time.frameCount-frame}, clock advanced={advanced}\n");
            sandbox.Clock.Paused=true;remaining=sandbox.Clock.RemainingSeconds;phase=1;started=EditorApplication.timeSinceStartup;
        }else{
            File.AppendAllText("Temp/map-assault-frames.txt",$"paused delta={remaining-sandbox.Clock.RemainingSeconds}\n");
            EditorApplication.update-=Observe;
        }
    }
}
