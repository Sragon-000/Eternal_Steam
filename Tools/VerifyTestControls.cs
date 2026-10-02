using System;
using System.Linq;
using UnityEngine;
using EternalSteam;
using EternalSteam.OpenWorld;
public static class VerifyTestControls
{
 static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
 public static string Main(){
  Check(Application.isPlaying&&Environment.GetEnvironmentVariable("ETERNAL_STEAM_SAVE_TEST_ROOT")=="/tmp/eternal-test-controls","Isolated Play required");
  var s=UnityEngine.Object.FindFirstObjectByType<OpenWorldSandbox>();var h=UnityEngine.Object.FindFirstObjectByType<CanvasWorldHud>();s.Persistence.Automatic=false;s.Clock.Paused=true;
  void Click(string id){h.Refresh();var b=h.Buttons.Single(x=>x.Id==id).View;Check(b.gameObject.activeInHierarchy&&b.interactable,"Visible enabled button: "+id);Check(b.onClick.GetPersistentEventCount()==1,"Saved callback: "+id);b.onClick.Invoke();}
  Check(!s.Content.InfiniteResources,"Default OFF");var bank=s.Content.Inventories.Available(s.Content.Bases.Bases.Keys.First());Check(bank!=null,"Base ledger");var before=JsonUtility.ToJson(new Stocks{values=bank.Capture()});
  Click("infinite-resources");Check(s.Content.InfiniteResources&&bank.InfiniteResources,"Toggle applies to global and base ledgers");Check(bank.Withdraw("coal",100)==100,"Unlimited fuel withdrawal");Check(JsonUtility.ToJson(new Stocks{values=bank.Capture()})==before,"Stocks unchanged");
  Check(ConstructionPurchase.TryCommit(new[]{new ConstructionPurchase.Payment{Bank=bank,Resource="iron",Amount=100000000}},()=>true,out _),"Construction cost bypass");
  Click("infinite-resources");Check(!s.Content.InfiniteResources&&!bank.InfiniteResources,"Toggle OFF");Check(!ConstructionPurchase.TryCommit(new[]{new ConstructionPurchase.Payment{Bank=bank,Resource="iron",Amount=100000000}},()=>true,out _),"Normal cost restored");
  var day=s.Clock.Day;Click("quick-night");Check(s.Clock.Phase==DayPhase.Night&&s.Clock.Day==day&&s.Clock.Paused,"Night while paused, date preserved");Click("quick-day");Check(s.Clock.Phase==DayPhase.Day&&s.Clock.Day==day&&s.Clock.Paused,"Day while paused, date preserved");
  Click("infinite-resources");h.Refresh();Canvas.ForceUpdateCanvases();UnityEditor.EditorApplication.ExecuteMenuItem("Window/General/Game");ScreenCapture.CaptureScreenshot("/tmp/eternal-test-controls.png");return "PASS saved buttons, ON/OFF all ledgers, unchanged stock, construction charging restored, paused day/night switching; grid="+UnityEngine.Object.FindFirstObjectByType<TileWorldGround>().Width;
 }
 [Serializable] public class Stocks{public System.Collections.Generic.List<ResourceBank.Stock> values;}
}
