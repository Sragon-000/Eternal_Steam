using System;
using UnityEngine;

namespace EternalSteam.Demo
{
    // One local profile is enough for the prototype.
    public sealed class HordeProgress
    {
        [Serializable]
        sealed class Data
        {
            public int parts;
            public int[] cleared = new int[3];
            public string lastReward;
            public int[] capacityUpgrades = new int[4];
        }

        const string DefaultKey = "EternalSteam.Prototype.Progress.v1";
        static HordeProgress current;
        public static HordeProgress Current => current ??= new HordeProgress(DefaultKey);
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetSession() => current = null;
        readonly string key;
        Data data;
        public int Parts => data.parts;

        public HordeProgress(string storageKey)
        {
            key = storageKey;
            try { data = JsonUtility.FromJson<Data>(PlayerPrefs.GetString(key, "")); }
            catch (ArgumentException) { data = null; }
            if (data == null || data.cleared == null || data.cleared.Length != 3) data = new Data();
            data.parts = Mathf.Max(0, data.parts);
            for (int i = 0; i < 3; i++) data.cleared[i] = Mathf.Clamp(data.cleared[i], 0, 3);
            if (data.capacityUpgrades == null || data.capacityUpgrades.Length != 4) data.capacityUpgrades = new int[4];
            for (int i = 0; i < 4; i++) data.capacityUpgrades[i] = Mathf.Clamp(data.capacityUpgrades[i], 0, 12);
        }

        public int TowerLimit(HordeTowerKind kind) => 4 + data.capacityUpgrades[(int)kind];
        public int UpgradeCost(HordeTowerKind kind) => 100 + data.capacityUpgrades[(int)kind] * 50;
        public bool CanUpgrade(HordeTowerKind kind) => TowerLimit(kind) < 16 && Parts >= UpgradeCost(kind);
        public bool UpgradeCapacity(HordeTowerKind kind)
        {
            if (!Enum.IsDefined(typeof(HordeTowerKind), kind) || !CanUpgrade(kind)) return false;
            data.parts -= UpgradeCost(kind);
            data.capacityUpgrades[(int)kind]++;
            Save();
            return true;
        }

        void Save()
        {
            PlayerPrefs.SetString(key, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
        }

        public int Cleared(HordeMapKind map) => data.cleared[(int)map];
        public bool CanDeploy(HordeMapKind map, int stage) => stage >= 1 && stage <= 3 && stage <= Cleared(map) + 1;
        public int RewardPreview(HordeMapKind map, int stage) => stage * 100 + (Cleared(map) < stage ? stage * 100 : 0);

        public int AwardClear(HordeMapKind map, int stage, string battleId)
        {
            if (!CanDeploy(map, stage) || string.IsNullOrEmpty(battleId) || data.lastReward == battleId) return 0;
            int reward = RewardPreview(map, stage);
            data.parts += reward;
            data.cleared[(int)map] = Mathf.Max(Cleared(map), stage);
            data.lastReward = battleId;
            Save();
            return reward;
        }
    }
}
