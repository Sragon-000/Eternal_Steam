using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace EternalSteam.OpenWorld
{
    // Saved service presentation; RailwayHud remains the command and input authority.
    public sealed class RailwayServicePanel : MonoBehaviour
    {
        public RailwayOverview Overview;
        public RectTransform Root, Cargo, Defense, Scroll;
        public TMP_Text CargoSummary, DefenseSummary, Feedback, ScrollHint;
        public Button Return;
        public RailwayOverview.Action[] Actions;
        bool? previousCargo;
        public void Execute(string command)
        {
            if(command=="overview") Overview.Execute(command);
            else {Overview.Railway.Execute(command); Refresh();}
        }
        public void Refresh()
        {
            var rail=Overview.Railway;var route=rail.SelectedRoute;if(route==null)return;
            bool cargo=Overview.ShowingCargo;Cargo.gameObject.SetActive(cargo);Defense.gameObject.SetActive(!cargo);
            Scroll.anchorMin=Vector2.zero;Scroll.anchorMax=Vector2.one;Scroll.offsetMin=new Vector2(0,92);Scroll.offsetMax=Vector2.zero;
            var back=(RectTransform)Return.transform;back.anchorMin=back.anchorMax=back.pivot=Vector2.zero;back.anchoredPosition=Vector2.zero;back.sizeDelta=new Vector2(160,40);
            Feedback.rectTransform.anchorMin=new Vector2(0,0);Feedback.rectTransform.anchorMax=new Vector2(1,0);Feedback.rectTransform.pivot=Vector2.zero;Feedback.rectTransform.offsetMin=new Vector2(0,44);Feedback.rectTransform.offsetMax=new Vector2(0,88);Feedback.text=string.IsNullOrEmpty(rail.Feedback.text)?"":"최근 실행 · "+rail.Feedback.text;
            ScrollHint.rectTransform.anchorMin=new Vector2(0,0);ScrollHint.rectTransform.anchorMax=new Vector2(1,0);ScrollHint.rectTransform.offsetMin=new Vector2(180,0);ScrollHint.rectTransform.offsetMax=new Vector2(0,40);
            ScrollHint.text="휠로 위·아래 보기";
            ScrollHint.gameObject.SetActive(Scroll.GetComponent<ScrollRect>().content.rect.height>Scroll.rect.height+1);
            var network=rail.Sandbox.Content.Railway.Network;var stop=route.stops[rail.SelectedStopIndex];
            CargoSummary.text=$"선택 역 {rail.SelectedStopIndex+1}/{route.stops.Count} · {network.StationName(stop.stationId)}\n화물 · {(route.train.HasCargoResource?RailwayOverview.CargoName(route.train.resource)+" (고정)":"최초 지정 전")} · {route.train.cargo:0.##}/1,000\n적재·하역 각각 0~1,000. 적용은 선택 역에만 저장됩니다.\n다른 역 선택 시 미적용 입력이 초기화됩니다.";
            var reasons=new StringBuilder();
            foreach(var a in Actions){a.Button.interactable=rail.CanExecute(a.Command,out var why);if(!a.Button.interactable&&a.Button.gameObject.activeSelf&&a.Button.transform.IsChildOf(cargo?Cargo:Defense)&&!string.IsNullOrEmpty(why))reasons.AppendLine(a.Button.GetComponentInChildren<TMP_Text>().text+" · "+why);}
            if(cargo)CargoSummary.text+="\n"+reasons;
            else {var installed=route.train.armament.definition;var definition=rail.Sandbox.ContentCatalog.Buildings.FirstOrDefault(d=>d.Id==installed);DefenseSummary.text=(definition==null?rail.ArmamentChoice.text:rail.ArmamentChoice.text.Replace(installed,definition.DisplayName))+"\n지불 기지 · 정차 역 소속 기지\n"+reasons;}
            if(previousCargo!=cargo){previousCargo=cargo;var scroll=Scroll.GetComponent<ScrollRect>();LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);scroll.verticalNormalizedPosition=1;}
        }
    }
}
