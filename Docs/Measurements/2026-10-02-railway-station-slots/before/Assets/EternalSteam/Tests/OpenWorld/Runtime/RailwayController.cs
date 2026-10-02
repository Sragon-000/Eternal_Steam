using System;
using System.Collections.Generic;
using System.Linq;
using EternalSteam.Railway;
using UnityEngine;
namespace EternalSteam.OpenWorld
{
    public sealed class RailwayController:IDisposable
    {
        readonly OpenWorldContent content;readonly RailwaySceneView view;int revision=-1;
        public RailwayNetwork Network {get;}
        public TrainDefenseController Defense {get;private set;}
        public void InitializeDefense(ITargetQuery targets,IReadOnlyList<BuildingDefinition> definitions)=>Defense=new TrainDefenseController(content,Network,view,targets,definitions);
        public RailwayController(OpenWorldContent content,RailwaySceneView view)
        {this.content=content;this.view=view;Network=new RailwayNetwork(new Inventory(content),"coal",1,id=>content.Bases.Bases.TryGetValue(id,out var b)?b.Nexus.DisplayName:"상실 기지");Refresh();}
        public void Refresh(){content.Bases.Refresh();if(revision==content.Bases.Revision)return;revision=content.Bases.Revision;Network.Refresh(content.Bases.Buildings);
            var tracks=content.Bases.Buildings.Where(b=>b.Module<RailFacility>()?.Kind==RailFacilityKind.Track).ToDictionary(b=>b.Cell);
            foreach(var pair in tracks){if(!content.Views.TryGetValue(pair.Value,out var go)||go.GetComponent<RailTrackView>() is not RailTrackView trackView)continue;
                var directions=new List<Vector2Int>();foreach(var d in new[]{Vector2Int.left,Vector2Int.right,Vector2Int.up,Vector2Int.down})if(tracks.ContainsKey(pair.Key+d))directions.Add(d);
                foreach(var station in Network.Stations)for(int port=0;port<4;port++)if(RailwayNetwork.Port(station,port)==pair.Key){var offset=station.Cell+Vector2Int.one-pair.Key;var d=Math.Abs(offset.x)>Math.Abs(offset.y)?new Vector2Int(Math.Sign(offset.x),0):new Vector2Int(0,Math.Sign(offset.y));if(!directions.Contains(d))directions.Add(d);}
                var first=directions.Count>0?directions[0]:Vector2Int.down;var second=directions.Count>1?directions[1]:-first;trackView.Configure(first,second);
            }}
        public void Tick(double dt,bool combatEnabled=false){Refresh();Network.Tick(dt);view?.Present(Network,content);Defense?.Tick((float)dt,combatEnabled);}
        public void Dispose(){Defense?.Dispose();view?.Clear();}
        sealed class Inventory:IRailInventory
        {
            readonly OpenWorldContent c;public Inventory(OpenWorldContent c){this.c=c;}
            public bool Available(string id)=>c.Inventories.Available(id)!=null;
            public double Load(string id,string resource,double maximum)=>c.Inventories.Available(id)?.Withdraw(resource,maximum)??0;
            public double Unload(string id,string resource,double maximum)=>c.Inventories.Available(id)?.Deposit(resource,maximum)??0;
        }
    }
}
