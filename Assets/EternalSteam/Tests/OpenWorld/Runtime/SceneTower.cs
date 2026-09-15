using UnityEngine;
using EternalSteam.Demo;
namespace EternalSteam.OpenWorld
{
    public sealed class SceneTower : MonoBehaviour
    {
        public HordeTowerKind Kind;
        public float Range=22;
        public Transform Head;
        public LineRenderer Tracer,Impact,Coverage;
        public void Configure(HordeTower view)
        {
            Kind=view.kind;Range=view.range;Head=view.head;Tracer=view.tracer;Impact=view.impact;Coverage=view.coverage;
            // Persist local vertices so scene/prefab movement carries every line along.
            foreach(var line in GetComponentsInChildren<LineRenderer>()) {
                if(!line.useWorldSpace)continue;
                var positions=new Vector3[line.positionCount];line.GetPositions(positions);
                for(int i=0;i<positions.Length;i++)positions[i]=line.transform.InverseTransformPoint(positions[i]);
                line.useWorldSpace=false;line.SetPositions(positions);
            }
        }
        public HordeTower RuntimeView()
        {
            foreach(var line in GetComponentsInChildren<LineRenderer>()) {
                if(line.useWorldSpace)continue;
                var positions=new Vector3[line.positionCount];line.GetPositions(positions);
                for(int i=0;i<positions.Length;i++)positions[i]=line.transform.TransformPoint(positions[i]);
                line.useWorldSpace=true;line.SetPositions(positions);
            }
            return new HordeTower {root=gameObject,head=Head,tracer=Tracer,impact=Impact,coverage=Coverage,kind=Kind,range=Range,
                halfAngle=HordeDeploymentGeometry.SpreadHalfAngle(Range)};
        }
    }
}
