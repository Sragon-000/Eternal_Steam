using System;
using System.Collections.Generic;
using EternalSteam.Railway;
using UnityEngine;
namespace EternalSteam.OpenWorld
{
    // Train instances are gameplay objects. The prefab and route highlight are authored before Play.
    public sealed class RailwaySceneView:MonoBehaviour
    {
        public LineRenderer SelectedLegHighlight;
        public LineRenderer[] PortFrames=Array.Empty<LineRenderer>();
        public TMPro.TMP_Text[] PortLabels=Array.Empty<TMPro.TMP_Text>();
        public GameObject TrainPrefab;public LineRenderer RouteHighlight;public RailRouteMarker MarkerPrefab;
        readonly List<RailRouteMarker> markers=new();
        MaterialPropertyBlock lineTint;
        void Tint(LineRenderer line,Color color){lineTint??=new MaterialPropertyBlock();line.startColor=line.endColor=color;lineTint.SetColor("_BaseColor",color);lineTint.SetColor("_Color",color);line.SetPropertyBlock(lineTint);}
        readonly Dictionary<string,GameObject> trains=new();
        public void Present(RailwayNetwork network,OpenWorldContent content)
        {
            if(TrainPrefab==null)throw new InvalidOperationException("저장된 기차 프리팹 참조가 없습니다.");
            foreach(var r in network.Routes){if(!trains.TryGetValue(r.trainId,out var go)){go=Instantiate(TrainPrefab,transform);go.name=r.name+" 기차";trains.Add(r.trainId,go);}
                var pose=Pose(r,content);go.SetActive(r.train.segmentPaid||network.Station(r.stops[r.train.stop].stationId)!=null);
                go.transform.SetPositionAndRotation(pose.position,Quaternion.LookRotation(pose.direction));
                go.GetComponent<TrainConsistView>()?.Present(r.train.segmentPaid);
            }
        }
        public (Vector3 position,Vector3 direction) Pose(RailRoute r,OpenWorldContent content)
        {
            var t=r.train;var station=content.Railway.Network.Station(r.stops[t.stop].stationId);
            if(station==null&&!t.segmentPaid)return (Vector3.zero,Vector3.forward);
            Vector3 p=station?.Position??Vector3.zero,next=p+(station?.Direction??Vector3.forward);
            if(r.IsShuttle&&!t.segmentPaid){var socket=content.GroundWorld.Grid.Center(r.TravelCell(0),Vector2Int.one);next=p+(socket-p)*(t.reverse?-1:1);}
            if(t.segmentPaid){var leg=r.CurrentLeg;int count=leg.cells.Count;double traveled=Math.Clamp(t.progress*(count+1)/count,0,count+1);int edge=Math.Min((int)traveled,count);
                Vector3 Point(int vertex)=>vertex==0?station?.Position??content.GroundWorld.Grid.Center(r.TravelCell(0),Vector2Int.one):
                    vertex==count+1?content.Railway.Network.Station(r.stops[r.NextStopIndex].stationId)?.Position??content.GroundWorld.Grid.Center(r.TravelCell(count-1),Vector2Int.one):
                    content.GroundWorld.Grid.Center(r.TravelCell(vertex-1),Vector2Int.one);
                var from=Point(edge);var to=Point(edge+1);p=Vector3.Lerp(from,to,(float)(traveled-edge));next=p+(r.IsShuttle&&t.reverse?from-to:to-from);}
            if(t.waitingForStation&&!t.segmentPaid){var direction=next-p;p=content.GroundWorld.Grid.Center(r.TravelCell(0),Vector2Int.one);next=p+direction;}
            var d=next-p;d.y=0;if(d.sqrMagnitude<.001f)d=station?.Direction??Vector3.forward;
            if(content.CheckTerrain(content.GroundWorld.Grid.WorldToCell(p),Vector2Int.one,out float height,out _))p.y=height;
            return (p+Vector3.up*.6f,d.normalized);
        }
        public Vector3 CombatPosition(RailRoute route,OpenWorldContent content){var pose=Pose(route,content);return pose.position+Quaternion.LookRotation(pose.direction)*TrainPrefab.GetComponent<TrainDefenseView>().Turret.localPosition;}
        public TrainDefenseView Defense(string id)=>trains.TryGetValue(id,out var go)?go.GetComponent<TrainDefenseView>():null;
        public void HighlightError(Vector2Int? cell,OpenWorldContent content)
        {
            if(RouteHighlight==null||!cell.HasValue)return;
            var p=content.GroundWorld.Grid.Center(cell.Value,Vector2Int.one);if(content.CheckTerrain(cell.Value,Vector2Int.one,out float h,out _))p.y=h;p.y+=.5f;
            RouteHighlight.enabled=true;RouteHighlight.positionCount=5;RouteHighlight.SetPositions(new[]{p+new Vector3(-1,0,-1),p+new Vector3(1,0,-1),p+new Vector3(1,0,1),p+new Vector3(-1,0,1),p+new Vector3(-1,0,-1)});
        }
        public void Highlight(RailRoute route,OpenWorldContent content)
            =>HighlightPreview(route?.stops,route?.legs,content);
        public void HighlightPreview(IReadOnlyList<RailStop> stops,IReadOnlyList<RailLeg> legs,OpenWorldContent content)
        {
            if(RouteHighlight==null)return;
            int count=legs!=null&&legs.Count>0?stops?.Count??0:0;RouteHighlight.enabled=count>0;if(MarkerPrefab==null)throw new InvalidOperationException("저장된 노선 순번/방향 프리팹이 없습니다.");
            while(markers.Count<count)markers.Add(Instantiate(MarkerPrefab,transform));for(int i=0;i<markers.Count;i++)markers[i].gameObject.SetActive(i<count);
            if(count==0)return;
            for(int i=0;i<count;i++){var station=content.Railway.Network.Station(stops[i].stationId);if(station==null){markers[i].gameObject.SetActive(false);continue;}var cells=legs[Math.Min(i,legs.Count-1)].cells;bool backwards=i>=legs.Count;int head=backwards?cells.Count-1:0;var p=content.GroundWorld.Grid.Center(cells[head],Vector2Int.one);var q=cells.Count>1?content.GroundWorld.Grid.Center(cells[backwards?head-1:1],Vector2Int.one):content.Railway.Network.Station(stops[backwards?i-1:(i+1)%count].stationId)?.Position??p;if(content.CheckTerrain(cells[head],Vector2Int.one,out float h,out _))p.y=h;markers[i].Present(i+1,station.Position,p,q-p);}
            var points=new List<Vector3>();foreach(var l in legs)foreach(var cell in l.cells){var p=content.GroundWorld.Grid.Center(cell,Vector2Int.one);if(content.CheckTerrain(cell,Vector2Int.one,out float h,out _))p.y=h;points.Add(p+Vector3.up*.4f);}RouteHighlight.positionCount=points.Count;RouteHighlight.SetPositions(points.ToArray());
        }
        public void HighlightIssuePort(int port)
        {
            if(port<0||port>=PortFrames.Length||!PortFrames[port].gameObject.activeSelf)return;
            Tint(PortFrames[port],Color.red);PortLabels[port].color=Color.red;PortLabels[port].text="! "+PortLabels[port].text;
        }
        public void ClearDraftSelection()
        {
            if(SelectedLegHighlight!=null)SelectedLegHighlight.enabled=false;
            foreach(var frame in PortFrames)if(frame!=null)frame.gameObject.SetActive(false);
        }
        public void PresentPlacementPorts(Vector2Int cell,Vector3 direction,OpenWorldContent content)
        {
            if(PortFrames.Length!=4||PortLabels.Length!=4)throw new InvalidOperationException("Save four station port markers in the scene.");
            for(int i=0;i<4;i++){
                var port=RailwayNetwork.PortCell(cell,direction,i);var p=content.GroundWorld.Grid.Center(port,Vector2Int.one);
                if(content.CheckTerrain(port,Vector2Int.one,out float height,out _))p.y=height;
                var frame=PortFrames[i];frame.gameObject.SetActive(true);frame.transform.position=p+Vector3.up*.45f;
                Tint(frame,Color.cyan);PortLabels[i].text=$"{i+1} 양방향 연결구";PortLabels[i].color=frame.startColor;
                if(Camera.main!=null)PortLabels[i].transform.parent.rotation=Camera.main.transform.rotation;
            }
        }
        public void PresentDraftSelection(BuildingInstance station,RailStop stop,RailLeg leg,OpenWorldContent content,bool shuttle=false)
        {
            if(station==null||stop==null){ClearDraftSelection();return;}
            SelectedLegHighlight.enabled=false;
            if(PortFrames.Length!=4||PortLabels.Length!=4||SelectedLegHighlight==null)throw new InvalidOperationException("Save the four port markers and selected segment renderer in the scene.");
            for(int i=0;i<4;i++){
                var cell=RailwayNetwork.Port(station,i);var p=content.GroundWorld.Grid.Center(cell,Vector2Int.one);
                if(content.CheckTerrain(cell,Vector2Int.one,out float height,out _))p.y=height;p.y+=.55f;
                var frame=PortFrames[i];frame.gameObject.SetActive(true);frame.transform.position=p;
                Tint(frame,shuttle?(leg!=null&&leg.cells.Contains(cell)?Color.cyan:Color.white):i==stop.arrival?Color.cyan:i==stop.departure?Color.yellow:Color.white);
                PortLabels[i].text=shuttle?$"{i+1} 연결구":$"{i+1}{(i==stop.arrival?" 진입":i==stop.departure?" 진출":"")}";
                PortLabels[i].color=frame.startColor;
                if(Camera.main!=null)PortLabels[i].transform.parent.rotation=Camera.main.transform.rotation;
            }
            if(leg==null||leg.cells.Count==0)return;
            var points=new List<Vector3>();foreach(var cell in leg.cells){var p=content.GroundWorld.Grid.Center(cell,Vector2Int.one);if(content.CheckTerrain(cell,Vector2Int.one,out float height,out _))p.y=height;points.Add(p+Vector3.up*.7f);}
            // A one-cell leg still needs two vertices to be visible.
            if(points.Count==1)points.Add(points[0]+station.Direction*.3f);
            Tint(SelectedLegHighlight,new Color(1,.5f,.1f));SelectedLegHighlight.positionCount=points.Count;SelectedLegHighlight.SetPositions(points.ToArray());SelectedLegHighlight.enabled=true;
        }
        public void Clear(){ClearDraftSelection();foreach(var go in trains.Values)if(go!=null)Destroy(go);trains.Clear();foreach(var marker in markers)if(marker!=null)Destroy(marker.gameObject);markers.Clear();if(RouteHighlight!=null)RouteHighlight.enabled=false;}
    }
}
