using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using UnityEngine;
using EternalSteam.Railway;
using Newtonsoft.Json.Linq;
public static class MeasureRailPathPlanner
{
 public static string Main(){var report=new JArray();var bounds=new RectInt(0,0,500,500);var starts=Enumerable.Repeat(Vector2Int.zero,4).ToArray();var ends=Enumerable.Repeat(new Vector2Int(499,499),4).ToArray();
  foreach(bool wall in new[]{false,true}){var samples=new double[20];RailPlan last=null;
   for(int i=-1;i<20;i++){var clock=Stopwatch.StartNew();last=RailPathPlanner.Find("00000000000000000000000000000001","00000000000000000000000000000002",starts,ends,p=>p==0,p=>p==0,p=>bounds.Contains(p)&&!(wall&&p.x==250&&p.y<480)?1:-1,0);clock.Stop();if(i>=0)samples[i]=clock.Elapsed.TotalMilliseconds;}
   Array.Sort(samples);report.Add(new JObject{{"case",wall?"500x500-wall":"500x500-empty"},{"runs",20},{"warmups",1},{"medianMs",samples[10]},{"p95Ms",samples[18]},{"maxMs",samples[19]},{"status",last.Status.ToString()},{"expanded",last.Expanded},{"cells",last.Connection?.cells.Count},{"newCells",last.NewCells}});
  }
  File.WriteAllText("Docs/Measurements/2026-10-02-railway-connections/planner-performance.json",report.ToString());return report.ToString();
 }
}
