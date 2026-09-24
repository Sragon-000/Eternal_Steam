using UnityEngine;
namespace EternalSteam.Demo
{
        public sealed class HordeTower
        {
            public BuildingInstance building;
            public GameObject root;
            public Transform head;
            public LineRenderer tracer;
            public float range;
            public bool showIdleCoverage=true;
            public float halfAngle;
            public int shot;
            public int targetId=-1,targetGeneration;
            public float cooldown;
            public float tracerTime;
            public HordeTowerKind kind;
            public LineRenderer impact;
            public LineRenderer coverage;
            public float impactTime;
        }
}
