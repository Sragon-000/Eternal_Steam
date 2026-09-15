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
            public float halfAngle;
            public int shot;
            public float cooldown;
            public float tracerTime;
            public HordeTowerKind kind;
            public LineRenderer impact;
            public LineRenderer coverage;
            public float impactTime;
        }
}
