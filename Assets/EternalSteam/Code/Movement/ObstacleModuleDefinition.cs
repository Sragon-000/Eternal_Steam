using System;
using System.Collections.Generic;
using UnityEngine;
namespace EternalSteam
{
    public sealed class MovementObstacles : IMovementObstacles
    {
        readonly HashSet<BuildingInstance> buildings=new();
        readonly float cellSize;
        public MovementObstacles(float cellSize) { this.cellSize=cellSize; }
        public void Register(BuildingInstance building)=>buildings.Add(building);
        public void Unregister(BuildingInstance building)=>buildings.Remove(building);
        public BuildingInstance At(Vector3 point)
        {
            BuildingInstance result=null;
            foreach(var building in buildings)
            {
                var delta=point-building.Position;
                if(Mathf.Abs(delta.x)<=building.Footprint.x*cellSize*0.5f && Mathf.Abs(delta.z)<=building.Footprint.y*cellSize*0.5f
                    && (result==null || building.Id<result.Id)) result=building;
            }
            return result;
        }
    }
    [CreateAssetMenu(menuName="Eternal Steam/Modules/Movement Obstacle")]
    public sealed class ObstacleModuleDefinition:BuildingModuleDefinition
    {
        public override IBuildingModule CreateRuntime()=>new Runtime();
        sealed class Runtime:IBuildingModule
        {
            BuildingInstance owner; IMovementObstacles obstacles;
            public void Initialize(BuildingInstance owner,BuildingServices services)
            { this.owner=owner; obstacles=services.Obstacles??throw new InvalidOperationException("Obstacle requires MovementObstacles."); }
            public void Activate()=>obstacles.Register(owner);
            public void Tick(float dt) { }
            public void Dispose() { if(owner!=null) obstacles?.Unregister(owner); }
        }
    }
}
