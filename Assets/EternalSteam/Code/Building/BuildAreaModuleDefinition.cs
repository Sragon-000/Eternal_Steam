using System;
using System.Collections.Generic;
using UnityEngine;
namespace EternalSteam
{
    public enum BuildAreaShape { Circle, Square }
    public interface IBuildArea
    {
        float Radius { get; }
        BuildAreaShape Shape { get; }
        bool Contains(Vector3 center,Vector2 halfSize,Quaternion rotation);
        Vector3 Boundary(float fraction);
    }
    public interface IBuildAreaGeometry
    {
        Vector3 Center { get; }
        Vector3 ToLocal(Vector3 worldPoint);
    }
    [CreateAssetMenu(menuName="Eternal Steam/Modules/Build Area")]
    public sealed class BuildAreaModuleDefinition:BuildingModuleDefinition
    {
        public BuildAreaShape Shape;
        [Tooltip("Circle radius, or half the side of a square, in metres.")]
        public float Radius=40;
        public float Yaw=45;
        public bool FromFrontEdge;
        [Tooltip("Align an odd-width forward zone with the construction cell boundaries.")] public bool AlignForwardCells;
        [Min(.01f)] public float CellSize=2;
        [Min(0)] public float RadiusPerLevel;
        public override IBuildingModule CreateRuntime()=>new Area(Radius,Shape,Yaw,RadiusPerLevel,FromFrontEdge,CellSize,AlignForwardCells);
        public override void Validate(List<string> errors) {
            if(!float.IsFinite(CellSize)||CellSize<=0||!float.IsFinite(RadiusPerLevel)||RadiusPerLevel<0||!float.IsFinite(Radius)||Radius<=0||!float.IsFinite(Yaw)||!Enum.IsDefined(typeof(BuildAreaShape),Shape)) errors.Add("Invalid build area shape, size or orientation.");
        }
        public override void ValidateComposition(BuildingDefinition definition,List<string> errors) {
            if(definition.Placement!=null&&definition.Placement.RequiresOperationalArea) errors.Add("Area providers cannot depend on coverage: chained/circular outposts are not supported.");
        }
        public override bool Provides(Type capability)=>capability==typeof(IBuildArea);
        sealed class Area:IBuildingModule,IBuildArea,IBuildAreaGeometry
        {
            readonly float initialRadius,growth,cellSize;readonly bool fromFront,alignCells;
            public Vector3 Center {get {float side=fromFront&&alignCells?Mathf.Floor((owner.Footprint.x*cellSize*.5f-Radius)/cellSize+.5f)*cellSize+Radius-owner.Footprint.x*cellSize*.5f:0;return owner.Position+orientation*new Vector3(side,0,fromFront?owner.Footprint.y*cellSize*.5f+Radius:0);}}
            public Vector3 ToLocal(Vector3 worldPoint)=>inverse*(worldPoint-Center);
            public float Radius=>initialRadius+growth*((owner?.Module<IUpgradeControl>()?.Level??1)-1); public BuildAreaShape Shape{get;}
            readonly Quaternion orientation,inverse;BuildingInstance owner;
            public Area(float radius,BuildAreaShape shape,float yaw,float growth,bool fromFront,float cellSize,bool alignCells){this.alignCells=alignCells;this.fromFront=fromFront;this.cellSize=cellSize;initialRadius=radius;this.growth=growth;Shape=shape;orientation=Quaternion.Euler(0,yaw,0);inverse=Quaternion.Inverse(orientation);}
            public void Initialize(BuildingInstance owner,BuildingServices services){this.owner=owner;}
            public void Activate(){} public void Tick(float dt){} public void Dispose(){}
            public bool Contains(Vector3 center,Vector2 halfSize,Quaternion rotation){
                if(owner==null||!owner.Active||owner.Disposed)return false;
                for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2){
                    var d=ToLocal(center+rotation*new Vector3(x*halfSize.x,0,z*halfSize.y));d.y=0;
                    if(Shape==BuildAreaShape.Circle?d.sqrMagnitude>Radius*Radius+.001f:Mathf.Abs(d.x)>Radius+.0001f||Mathf.Abs(d.z)>Radius+.0001f)return false;
                }return true;
            }
            public Vector3 Boundary(float fraction) {
                float t=Mathf.Repeat(fraction,1)*4;Vector3 p;
                if(Shape==BuildAreaShape.Circle)p=new Vector3(Mathf.Cos(fraction*Mathf.PI*2),0,Mathf.Sin(fraction*Mathf.PI*2))*Radius;
                else if(t<1)p=new Vector3(-Radius+2*Radius*t,0,-Radius);
                else if(t<2)p=new Vector3(Radius,0,-Radius+2*Radius*(t-1));
                else if(t<3)p=new Vector3(Radius-2*Radius*(t-2),0,Radius);
                else p=new Vector3(-Radius,0,Radius-2*Radius*(t-3));
                return Center+orientation*p;
            }
        }
    }
}
