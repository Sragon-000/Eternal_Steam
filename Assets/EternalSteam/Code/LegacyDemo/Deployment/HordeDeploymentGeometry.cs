using UnityEngine;
namespace EternalSteam.Demo
{
    public static class HordeDeploymentGeometry
    {
        public const float Range=22f, MinRange=6f, MaxRange=26f;
        public static float SpreadHalfAngle(float distance)
        {
            distance=Mathf.Clamp(distance,MinRange,MaxRange);
            float halfWidth=Mathf.Lerp(4.6f,1.8f,Mathf.InverseLerp(MinRange,MaxRange,distance));
            return Mathf.Asin(halfWidth/distance)*Mathf.Rad2Deg;
        }
    }
}
