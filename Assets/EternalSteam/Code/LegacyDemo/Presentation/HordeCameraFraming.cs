using UnityEngine;
namespace EternalSteam.Demo
{
    public sealed class HordeCameraFraming
    {
        int viewportWidth,viewportHeight;
        public void SetViewport(Camera viewCamera,HordeMapKind mapKind,float leftFraction)
        {
            if (viewCamera == null || !float.IsFinite(leftFraction)) return;
            leftFraction = Mathf.Clamp(leftFraction, 0, 0.8f);
            var rect = new Rect(leftFraction, 0, 1 - leftFraction, 1);
            if (viewCamera.rect == rect && viewportWidth == Screen.width && viewportHeight == Screen.height) return;
            viewCamera.rect = rect;
            viewportWidth = Screen.width;
            viewportHeight = Screen.height;
            Fit(viewCamera,mapKind);
        }
        public void Fit(Camera viewCamera,HordeMapKind mapKind)
        {
            if (viewCamera == null) return;
            Vector2 size = HordeMapLayout.Size(mapKind);
            Vector3 up = viewCamera.transform.up;
            Vector3 right = viewCamera.transform.right;
            float height = Mathf.Abs(up.x) * size.x + Mathf.Abs(up.z) * size.y + 8;
            float width = Mathf.Abs(right.x) * size.x + Mathf.Abs(right.z) * size.y + 8;
            viewCamera.aspect = Mathf.Max(0.01f, viewCamera.pixelRect.width / Mathf.Max(1, viewCamera.pixelRect.height));
            viewCamera.orthographicSize = Mathf.Max(height * 0.5f, width / (2 * viewCamera.aspect));
            viewCamera.transform.position = -viewCamera.transform.forward * 80;
        }
    }
}
