using UnityEngine;
using UnityEngine.Rendering;
namespace EternalSteam.Demo
{
    public static class HordeVisualPrimitives
    {
        public static Transform MakePart(string name, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            if (Application.isPlaying) Object.Destroy(part.GetComponent<Collider>());
            else Object.DestroyImmediate(part.GetComponent<Collider>());
            part.GetComponent<Renderer>().sharedMaterial = material;
            return part.transform;
        }
        public static LineRenderer MakeLine(string name, Transform parent, Material material, float width, int count)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.widthMultiplier = width;
            line.positionCount = count;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }
    }
}
