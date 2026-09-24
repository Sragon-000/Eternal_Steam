using System;
using UnityEngine;

namespace EternalSteam.Demo
{
    public static class HordeTowerEffects
    {
        public static void Reset(HordeTower tower)
        {
            tower.tracerTime = tower.impactTime = 0;
            tower.tracer.enabled = false;
            if (tower.impact != null) tower.impact.enabled = false;
            tower.coverage.enabled = tower.showIdleCoverage;
        }
        public static void Tick(HordeTower tower, float dt, bool waveStarted, bool defeated)
        {
            tower.coverage.enabled = tower.showIdleCoverage && !waveStarted && !defeated;
            tower.tracerTime -= dt;
            tower.tracer.enabled = !defeated && tower.tracerTime > 0;
            tower.impactTime -= dt;
            if (tower.impact != null) tower.impact.enabled = !defeated && tower.impactTime > 0;
        }
        public static void Shot(HordeTower tower, Vector3 direction)
        {
            tower.tracerTime = tower.kind == HordeTowerKind.MachineGun ? 0.035f : 0.1f;
            tower.tracer.SetPosition(0, tower.head.position + direction * 1.2f);
            tower.tracer.SetPosition(1, tower.head.position + direction * tower.range);
            tower.tracer.enabled = true;
        }
        public static void Frost(HordeTower tower)
        {
            tower.impactTime = 0.2f;
            tower.impact.enabled = true;
            Vector3 origin = tower.root.transform.position + Vector3.up * 0.13f;
            tower.impact.SetPosition(0, origin);
            for (int i = 0; i <= 32; i++)
                tower.impact.SetPosition(i + 1, origin + Quaternion.AngleAxis(Mathf.Lerp(-tower.halfAngle, tower.halfAngle, i / 32f), Vector3.up) * tower.head.forward * tower.range);
            tower.impact.SetPosition(34, origin);
        }
        public static void Area(HordeTower tower, Vector3 center, float radius, float surfaceHeight = 0)
        {
            tower.impactTime = 0.2f;
            tower.impact.enabled = true;
            for (int i = 0; i <= 32; i++)
            {
                float arcAngle = i * Mathf.PI * 2 / 32;
                tower.impact.SetPosition(i, new Vector3(center.x + Mathf.Cos(arcAngle) * radius, surfaceHeight + 0.13f, center.z + Mathf.Sin(arcAngle) * radius));
            }
        }

    }
}
