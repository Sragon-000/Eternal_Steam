using UnityEngine;

namespace EternalSteam.Demo
{
    public enum HordeTowerKind { MachineGun, Cannon, Frost, Arrow }

    public static class HordeTowerStats
    {
        public static int Damage(HordeTowerKind kind) => kind == HordeTowerKind.Arrow ? 20 : kind == HordeTowerKind.Cannon ? 20 : kind == HordeTowerKind.Frost ? 0 : 10;
        public static float Interval(HordeTowerKind kind) => kind == HordeTowerKind.Arrow ? 0.35f : kind == HordeTowerKind.Cannon ? 0.7f : kind == HordeTowerKind.Frost ? 0.18f : 0.055f;
        public static string Name(HordeTowerKind kind) => kind == HordeTowerKind.Arrow ? "관통 화살 포탑" : kind == HordeTowerKind.Cannon ? "대포 포탑" : kind == HordeTowerKind.Frost ? "냉각 포탑" : "기관총 포탑";
        public static string Description(HordeTowerKind kind) => kind == HordeTowerKind.Arrow ? "피해 20 / 사선에 있는 모든 적 관통" : kind == HordeTowerKind.Cannon ? "피해 20 / 폭발 반경 3" : kind == HordeTowerKind.Frost ? "부채꼴 관통 / 2.5초 동안 60% 감속 / 피해 없음" : "피해 10 / 단일 대상 연사";
        public static Color Color(HordeTowerKind kind) => kind == HordeTowerKind.Arrow ? new Color(0.45f, 1, 0.3f) : kind == HordeTowerKind.Cannon ? new Color(1, 0.58f, 0.12f) : kind == HordeTowerKind.Frost ? new Color(0.65f, 0.45f, 1) : new Color(0.2f, 0.82f, 0.95f);
    }
}
