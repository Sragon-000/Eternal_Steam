using System;
using System.Threading.Tasks;
using EternalSteam;
using EternalSteam.OpenWorld;
using UnityEngine;
using UnityEngine.UIElements;
public static class VerifyWorldClock
{
 static void Check(bool b,string message){if(!b)throw new Exception(message);}
 static void Click(Button button){using(var e=NavigationSubmitEvent.GetPooled()){e.target=button;button.SendEvent(e);}}
 public static async Task<string> Main(){
  var c=new GameClock(10,5);Check(c.Day==1&&c.Phase==DayPhase.Day,"Initial day");c.Tick(10);Check(c.Phase==DayPhase.Night&&c.Day==1&&c.RemainingSeconds==5,"Day boundary");c.Tick(5);Check(c.Phase==DayPhase.Day&&c.Day==2,"Night increments date");c.Tick(46);Check(c.Day==5&&c.RemainingSeconds==9,"Large delta keeps elapsed time");c.Paused=true;c.Tick(100);Check(c.Day==5&&c.RemainingSeconds==9,"Pause");c.SetPhase(DayPhase.Night);Check(c.Day==5&&c.RemainingSeconds==5,"Developer phase preserves date");c.Paused=false;c.Tick(double.NaN);c.Tick(-1);Check(c.RemainingSeconds==5,"Invalid dt ignored");
  var dialClock=new GameClock(300,180);dialClock.Tick(150);Check(Math.Abs(dialClock.PhaseProgress-.5)<.0001,"Day midpoint");dialClock.SetPhase(DayPhase.Night);dialClock.Tick(90);Check(Math.Abs(dialClock.PhaseProgress-.5)<.0001,"Night midpoint despite different duration");
  var s=UnityEngine.Object.FindFirstObjectByType<OpenWorldSandbox>();Check(Application.isPlaying&&s!=null,"Original sandbox Play required");var input=s.GetComponent<OpenWorldInput>();var hud=UnityEngine.Object.FindFirstObjectByType<OpenWorldHud>();var root=hud.GetComponent<UIDocument>().rootVisualElement;
  input.Cancel();var clock=s.Clock;bool running=s.Running;int pending=s.SpawnStream.Pending;long date=clock.Day;
  try{
   clock.Paused=true;Click(root.Q<Button>("clock-night"));Check(clock.Phase==DayPhase.Night&&clock.Day==date,"Night UI action");Click(root.Q<Button>("clock-day"));Check(clock.Phase==DayPhase.Day&&clock.Day==date,"Day UI action");
   hud.Refresh();Check(root.Q<DayNightDial>("day-night-dial")!=null,"Clock dial exists");Check(root.Q<Label>("clock-date").text.Contains("낮")&&root.Q<Label>("clock-remaining").text.Contains("시간 정지"),"Clock labels");
   Click(root.Q<Button>("clock-pause"));Check(!clock.Paused,"Resume UI action");double before=clock.RemainingSeconds;for(int i=0;i<40&&clock.RemainingSeconds==before;i++)await Task.Delay(100);Check(clock.RemainingSeconds<before,"Time runs in preparation");
   input.BeginEditing();before=clock.RemainingSeconds;await Task.Delay(150);Check(clock.RemainingSeconds==before,"Editing stops time");hud.Refresh();Check(root.Q<Label>("clock-remaining").text.Contains("수정 중 정지"),"Editing status");input.Cancel();
   Check(s.Running==running&&s.SpawnStream.Pending==pending,"Clock does not schedule combat or spawning");
   var bounds=root.Q("clock-panel").worldBound;Check(bounds.width>0&&bounds.xMin>=0&&bounds.yMin>=0&&bounds.xMin<50&&bounds.yMin<50,"Upper-left layout");
   return "PASS: day/night/date boundaries, large dt, pause, invalid dt, developer UI phase controls, date preserved, preparation ticking, editing pause, labels and upper-left layout; no spawn/combat coupling.";
  }finally{input.Cancel();clock.SetPhase(DayPhase.Day);clock.Paused=false;}
 }
}
