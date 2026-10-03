using System;
using System.Linq;
using EternalSteam.Railway;
using TMPro;
using UnityEngine;
namespace EternalSteam.OpenWorld
{
    public sealed partial class RailwayHud
    {
        public GameObject ConnectionPanel;
        public UnityEngine.UI.Button ConnectionFrom,ConnectionTo,ConnectionFromPort,ConnectionToPort;
        bool connecting;string connectionFrom,connectionTo;int fromPort=-1,toPort=-1,plannedWorldRevision,plannedConnectionRevision;
        RailwayConstruction construction;
        bool ConnectionStale=>construction?.Plan!=null&&(plannedWorldRevision!=Sandbox.Content.Bases.Revision||plannedConnectionRevision!=Network.ConnectionRevision);
        string[] ConnectionStations()=>Network.Stations.Where(s=>Network.StationActive(s.PersistentId)).OrderBy(s=>s.PersistentId,StringComparer.Ordinal).Select(s=>s.PersistentId).ToArray();
        bool CanConnectionCommand(string command,out string reason)
        {
            reason=null;
            if(command=="connect"){
                bool available=!drafting&&!connecting&&!Sandbox.GetComponent<OpenWorldInput>().IsEditing&&ConnectionStations().Length>=2;
                reason=available?null:"건설 편집을 종료하고 활성 역 두 곳을 준비하세요.";return available;
            }
            if(!connecting){reason="역 연결 화면에서 사용할 수 있습니다.";return false;}
            if(command is "close" or "connection-cancel" or "connection-from" or "connection-to" or "connection-from-port" or "connection-to-port")return true;
            if(command=="connection-preview")return connectionFrom!=connectionTo&&Network.StationActive(connectionFrom)&&Network.StationActive(connectionTo);
            if(command=="connection-confirm"){
                bool ok=construction?.Plan?.Status==RailPlanStatus.Found&&!ConnectionStale&&construction.Plan.NewCells<=512&&construction.Payer==Sandbox.Content.Bases.SelectedBaseId&&construction.Quote().Affordable;
                reason=ok?null:"최신 경로와 지불 기지의 건설 비용을 확인하세요.";return ok;
            }
            reason="연결을 확정하거나 취소하세요.";return false;
        }
        void ExecuteConnection(string command)
        {
            if(command=="connect"){
                connecting=true;selected=null;armament=false;construction=null;fromPort=toPort=-1;
                var ids=ConnectionStations();connectionFrom=ids.Contains(context)?context:ids[0];connectionTo=ids.First(id=>id!=connectionFrom);Message("출발·도착 역을 선택한 뒤 경로를 미리 보세요. 지도에서 도착 역을 선택할 수 있습니다.");
            }else if(command is "connection-cancel" or "close"){
                connecting=false;construction=null;Sandbox.RailwayView.Highlight(null,Sandbox.Content);if(command=="close")Panel.SetActive(false);
            }else if(command=="connection-preview"){
                construction=new RailwayConstruction(Sandbox.Content,Sandbox.GetComponent<OpenWorldInput>().Edits,Track);
                construction.Preview(connectionFrom,connectionTo,fromPort,toPort);plannedWorldRevision=Sandbox.Content.Bases.Revision;plannedConnectionRevision=Network.ConnectionRevision;Message("미리보기 완료 · 확정 전에는 선로와 비용이 변경되지 않습니다.");
            }else if(command=="connection-confirm"){
                if(construction.Confirm(out var error)){connecting=false;construction=null;context=connectionFrom;Sandbox.Content.Railway.Tick(0);Sandbox.Persistence?.Changed();Sandbox.RailwayView.Highlight(null,Sandbox.Content);Message("역 연결 완료 · 새 노선에서 두 역을 등록하면 같은 선로를 왕복합니다.");}
                else Message(error);
            }else {
                if(command=="connection-from-port")fromPort=fromPort==3?-1:fromPort+1;
                else if(command=="connection-to-port")toPort=toPort==3?-1:toPort+1;
                else {var ids=ConnectionStations();if(ids.Length>0){if(command=="connection-from")connectionFrom=ids[(Array.IndexOf(ids,connectionFrom)+1)%ids.Length];else connectionTo=ids[(Array.IndexOf(ids,connectionTo)+1)%ids.Length];}}
                construction=null;Sandbox.RailwayView.Highlight(null,Sandbox.Content);
            }
            Refresh();
        }
        bool TryConnectionWorldClick(Vector3 point)
        {
            if(!connecting||!Panel.activeSelf)return false;
            if(Sandbox.Content.GroundAt(point,out var building)&&Network.StationActive(building.PersistentId)){
                connectionTo=building.PersistentId;construction=null;Sandbox.RailwayView.Highlight(null,Sandbox.Content);Refresh();
            }
            return true;
        }
        void RefreshConnection()
        {
            Title.text="역 사이 선로 연결";CommandHints.gameObject.SetActive(false);
            ConnectionFrom.GetComponentInChildren<TMP_Text>().text="출발 · "+Network.StationName(connectionFrom)+"  ›";
            ConnectionTo.GetComponentInChildren<TMP_Text>().text="도착 · "+Network.StationName(connectionTo)+"  ›";
            ConnectionFromPort.GetComponentInChildren<TMP_Text>().text="출발 연결구 · "+(fromPort<0?"자동":(fromPort+1).ToString());
            ConnectionToPort.GetComponentInChildren<TMP_Text>().text="도착 연결구 · "+(toPort<0?"자동":(toPort+1).ToString());
            string text=$"{Network.StationName(connectionFrom)} ↔ {Network.StationName(connectionTo)}\n버튼으로 역 전환 · 지도에서 도착 역 선택\n선로는 직선·모서리로 연결됩니다. 분기·교차는 지원하지 않습니다.\n";
            var p=construction?.Plan;
            if(p==null)text+="경로 미리보기를 누르세요. 연결구는 자동 선택하거나 직접 지정할 수 있습니다.";
            else if(p.Status!=RailPlanStatus.Found)text+=p.Status switch {RailPlanStatus.SearchLimit=>"탐색 한도에 도달했습니다. 가까운 역 또는 다른 연결구를 선택하세요.",RailPlanStatus.Cancelled=>"경로 탐색이 취소됐습니다.",_=>"설치 가능한 경로를 찾지 못했습니다. 장애물·영역·기존 연결을 확인하세요."};
            else {
                text+=$"신규 {p.NewCells}칸 · 재사용 {p.ReusedCells}칸 · 전체 {p.Connection.cells.Count}칸\n연결구 {p.Connection.fromPort+1} ↔ {p.Connection.toPort+1} · 회전 {p.Turns}회\n지불 기지 · {Network.BaseDisplayName(construction.Payer)}\n"+(p.NewCells==0?"기존 선로만 연결 · 비용 0":construction.Quote().Summary);
                if(p.NewCells>512)text+="\n새 선로가 512칸을 넘습니다. 경유역을 추가하세요.";
                if(ConnectionStale)text+="\n월드의 연결 상태가 변경됐습니다. 다시 미리 보세요.";
                Sandbox.RailwayView.HighlightPreview(new[]{new RailStop{stationId=connectionFrom},new RailStop{stationId=connectionTo}},new[]{p.Connection.Leg()},Sandbox.Content);
            }
            Summary.text=text;float height=Math.Max(222,Summary.preferredHeight+12);Summary.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,height);((RectTransform)Summary.rectTransform.parent).SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,height);
        }
    }
}
