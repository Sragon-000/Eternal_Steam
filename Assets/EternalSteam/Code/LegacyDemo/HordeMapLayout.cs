using UnityEngine;

namespace EternalSteam.Demo
{
    public enum HordeMapKind { Lane, WideFront, Pincer }

    public static class HordeMapLayout
    {
        public const float BuildCellSize = 2f;
        public const float TowerScale = 1.3f;
        public const float TowerHalfWidth = 0.675f * TowerScale;
        public const float BuildMargin = BuildCellSize * 0.5f;
        public static float SurfaceHeight(HordeMapKind kind) => kind == HordeMapKind.Lane ? 0.01f : 0.045f;
        public static Vector3 Snap(Vector3 position) => new Vector3(Mathf.Round(position.x / BuildCellSize) * BuildCellSize, 0, Mathf.Round(position.z / BuildCellSize) * BuildCellSize);

        public static bool ContainsFootprint(Rect zone, Vector3 position, float halfWidth)
            => position.x - halfWidth >= zone.xMin && position.x + halfWidth <= zone.xMax
            && position.z - halfWidth >= zone.yMin && position.z + halfWidth <= zone.yMax;

        public static Rect GridBounds(Rect zone)
        {
            float xMin = Mathf.Ceil((zone.xMin + BuildMargin) / BuildCellSize) * BuildCellSize - BuildMargin;
            float zMin = Mathf.Ceil((zone.yMin + BuildMargin) / BuildCellSize) * BuildCellSize - BuildMargin;
            float xMax = Mathf.Floor((zone.xMax - BuildMargin) / BuildCellSize) * BuildCellSize + BuildMargin;
            float zMax = Mathf.Floor((zone.yMax - BuildMargin) / BuildCellSize) * BuildCellSize + BuildMargin;
            return Rect.MinMaxRect(xMin, zMin, xMax, zMax);
        }
        public static Vector2 Size(HordeMapKind kind) => kind == HordeMapKind.Lane ? new Vector2(54, 26) : new Vector2(86, 64);
        public static string Title(HordeMapKind kind) => kind == HordeMapKind.Lane ? "통로" : kind == HordeMapKind.WideFront ? "넓은 전선" : "협공";
        public static string SceneName(HordeMapKind kind) => kind == HordeMapKind.Lane ? "HordeDemo" : kind == HordeMapKind.WideFront ? "HordeWideFront" : "HordePincer";

        // Rect uses world X/Z. The same rectangles drive both floor art and placement.
        public static Rect[] BuildZones(HordeMapKind kind)
        {
            switch (kind)
            {
                case HordeMapKind.WideFront:
                    return new[] { new Rect(-36, -29, 72, 7), new Rect(-36, 22, 72, 7), new Rect(26, -20, 12, 40) };
                case HordeMapKind.Pincer:
                    return new[] { new Rect(-18, -5, 38, 10), new Rect(30, -20, 8, 40) };
                default:
                    return new[] { new Rect(-24, -12, 48, 6), new Rect(-24, 6, 48, 6) };
            }
        }

        public static void SpawnRoute(HordeMapKind kind, System.Random random, out Vector3 start, out Vector3 end)
        {
            float t = (float)random.NextDouble();
            if (kind == HordeMapKind.Pincer)
            {
                float sign = random.Next(2) == 0 ? -1 : 1;
                start = new Vector3(-40 + t * 28, 0.55f, sign * 29);
                end = new Vector3(41, 0.55f, sign * (7 + t * 5));
            }
            else if (kind == HordeMapKind.WideFront)
            {
                start = new Vector3(-41, 0.55f, Mathf.Lerp(-20, 20, t));
                end = new Vector3(41, 0.55f, Mathf.Lerp(-18, 18, t));
            }
            else
            {
                start = new Vector3(-25, 0.55f, Mathf.Lerp(-5.6f, 5.6f, t));
                end = new Vector3(25, 0.55f, start.z);
            }
        }

        public static Vector3[] StartingTowers(HordeMapKind kind)
        {
            if (kind == HordeMapKind.WideFront)
                return new[] { new Vector3(28, 0, -15), new Vector3(28, 0, -5), new Vector3(28, 0, 5), new Vector3(28, 0, 15), new Vector3(-12, 0, -24), new Vector3(-12, 0, 24) };
            if (kind == HordeMapKind.Pincer)
                return new[] { new Vector3(-12, 0, -3), new Vector3(0, 0, -3), new Vector3(12, 0, -3), new Vector3(-12, 0, 3), new Vector3(0, 0, 3), new Vector3(12, 0, 3) };
            return new[] { new Vector3(-12, 0, -8), new Vector3(-4, 0, 8), new Vector3(4, 0, -8), new Vector3(12, 0, 8) };
        }

        public static Vector3 DefaultDirection(HordeMapKind kind, Vector3 position)
        {
            if (kind == HordeMapKind.WideFront && position.x >= 26) return Vector3.left;
            if (kind == HordeMapKind.Pincer)
                return position.x >= 30 ? Vector3.left : new Vector3(-0.3f, 0, Mathf.Sign(position.z)).normalized;
            return new Vector3(-0.25f, 0, -Mathf.Sign(position.z)).normalized;
        }
    }
}
