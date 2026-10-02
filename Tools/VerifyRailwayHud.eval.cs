var s=UnityEngine.Object.FindFirstObjectByType<EternalSteam.OpenWorld.OpenWorldSandbox>(); s.Clock.Paused=true;s.Persistence.Automatic=false;
var h=s.RailwayHud;h.Execute("open");h.RowButtons[0].onClick.Invoke();
if(h.Load.text!="100"||h.Unload.text!="0")throw new System.Exception("First stop input mismatch");
h.Execute("next-stop");if(h.Load.text!="0"||h.Unload.text!="100")throw new System.Exception("Second stop input mismatch");
h.Load.text="77";h.SendMessage("Update");if(h.Load.text!="77")throw new System.Exception("Typing overwritten by refresh");h.Execute("next-stop");h.Execute("next-stop");
if(h.Load.text!="0"||h.Unload.text!="100")throw new System.Exception("Switch back should restore configured values");
UnityEngine.Canvas.ForceUpdateCanvases();
return "PASS persistent route row callback, selected station input sync, typed edit preservation, station switching";
