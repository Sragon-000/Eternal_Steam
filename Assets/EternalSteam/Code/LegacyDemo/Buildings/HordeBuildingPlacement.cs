using System;
using System.Collections.Generic;
using UnityEngine;
namespace EternalSteam.Demo
{
    // Compatibility adapter: common world owns occupancy/lifetime; legacy callbacks retain its build policy.
    public sealed class HordeBuildingPlacement : IDisposable
    {
        readonly IHordeTowerFactory factory;
        readonly Func<bool> permitted;
        readonly Func<int> count;
        readonly Func<HordeTowerKind,int> countFor,limitFor;
        readonly int maximum;
        readonly Dictionary<HordeTowerKind,BuildingDefinition> definitions=new();
        readonly Dictionary<string,HordeTowerKind> kinds=new();
        readonly PlacementSession placement;
        public BuildingWorld World { get; }
        public PlacementResult LastResult { get; private set; }
        public HordeBuildingPlacement(IHordeTowerFactory factory,HordeMapKind map,Func<bool> permitted,Func<int> count,Func<HordeTowerKind,int> countFor,Func<HordeTowerKind,int> limitFor,int maximum=64)
        {
            this.factory=factory;this.permitted=permitted;this.count=count;this.countFor=countFor;this.limitFor=limitFor;this.maximum=maximum;
            var grid=new BuildGrid(new RectInt(-22,-17,44,34),HordeMapLayout.BuildCellSize,new Vector3(-1,0,-1));
            World=new BuildingWorld(grid,factory);
            AddDefinition(HordeTowerKind.MachineGun,"legacy.machine-gun");
            AddDefinition(HordeTowerKind.Cannon,"legacy.cannon");
            AddDefinition(HordeTowerKind.Frost,"legacy.frost");
            AddDefinition(HordeTowerKind.Arrow,"legacy.arrow");
            placement=new PlacementSession(World,new LimitRule(this));ConfigureMap(map);
        }
        void AddDefinition(HordeTowerKind kind,string id)
        {
            var d=ScriptableObject.CreateInstance<BuildingDefinition>();d.Id=id;d.DisplayName=HordeTowerStats.Name(kind);
            d.Category=BuildingCategory.Defense;d.ViewPrefab=factory.Template;d.Footprint=Vector2Int.one;
            definitions.Add(kind,d);kinds.Add(id,kind);
        }
        public void ConfigureMap(HordeMapKind map)
        {
            placement.Cancel();var grid=World.Grid;var zones=HordeMapLayout.BuildZones(map);
            for(int z=grid.Bounds.yMin;z<grid.Bounds.yMax;z++)
                for(int x=grid.Bounds.xMin;x<grid.Bounds.xMax;x++)
                {
                    var cell=new Vector2Int(x,z);var position=grid.Center(cell,Vector2Int.one);bool supported=false;
                    foreach(var zone in zones)
                        if(HordeMapLayout.ContainsFootprint(zone,position,HordeMapLayout.BuildMargin) && HordeMapLayout.ContainsFootprint(zone,position,HordeMapLayout.TowerHalfWidth)) supported=true;
                    grid.SetBlocked(cell,!supported);
                }
        }
        bool Allowed(HordeTowerKind kind)=>definitions.ContainsKey(kind) && permitted() && count()<maximum && countFor(kind)<limitFor(kind);
        public bool CanPlace(Vector3 position,HordeTowerKind kind)
        {
            if(!float.IsFinite(position.x) || !float.IsFinite(position.z) || !Allowed(kind))return false;
            var cell=World.Grid.WorldToCell(HordeMapLayout.Snap(position));
            return World.Grid.IsBuildable(cell) && !World.Grid.IsOccupied(cell) && !World.Grid.ReservationAt(cell).HasValue;
        }
        public bool TryPlace(Vector3 position,Vector3 direction,float range,HordeTowerKind kind)
        {
            direction.y=0;
            if(!float.IsFinite(direction.sqrMagnitude) || direction.sqrMagnitude<0.001f || !float.IsFinite(range) || !CanPlace(position,kind))return false;
            range=Mathf.Clamp(range,HordeDeploymentGeometry.MinRange,HordeDeploymentGeometry.MaxRange);
            var cell=World.Grid.WorldToCell(HordeMapLayout.Snap(position));
            LastResult=placement.Add(definitions[kind],cell,out var request);
            if(!LastResult.Success)return false;
            try
            {
                request.Direction=direction;factory.ConfigureRequest(request.Id,range,kind);
                LastResult=placement.Confirm();return LastResult.Success;
            }
            finally
            {
                factory.ForgetRequest(request.Id);
                // Old click-to-place API retries the whole request; do not leak a hidden reservation.
                placement.Cancel();
            }
        }
        public void Clear() { placement.Cancel();World.Clear(); }
        public void Dispose()
        {
            Clear();
            foreach(var definition in definitions.Values)
            {
                if(Application.isPlaying) UnityEngine.Object.Destroy(definition);
                else UnityEngine.Object.DestroyImmediate(definition);
            }
            definitions.Clear();kinds.Clear();
        }
        sealed class LimitRule:IPlacementRule
        {
            readonly HordeBuildingPlacement owner;
            public LimitRule(HordeBuildingPlacement owner) { this.owner=owner; }
            public PlacementResult Validate(PlacementRequest request,IReadOnlyList<PlacementRequest> batch,BuildGrid grid)
            {
                if(!owner.kinds.TryGetValue(request.Definition.Id,out var kind) || !owner.permitted())return new PlacementResult("legacy-phase","지금은 설치할 수 없습니다.");
                int same=0;foreach(var item in batch)if(item.Definition.Id==request.Definition.Id)same++;
                return owner.count()+batch.Count>owner.maximum || owner.countFor(kind)+same>owner.limitFor(kind)
                    ?new PlacementResult("legacy-limit","포탑 설치 한도에 도달했습니다."):PlacementResult.Ok;
            }
        }
    }
}
