using System.Collections.Generic;
using EternalSteam.Demo;
using UnityEngine;

namespace EternalSteam.OpenWorld
{
    // Spawn spacing is independent of combat indexing and only constrains spawn positions.
    public sealed class EnemySpawnLayout
    {
        readonly Dictionary<Vector2Int, int> heads;
        readonly Vector3[] positions;
        readonly int[] next;
        public EnemySpawnLayout(int capacity) { heads = new(capacity); positions = new Vector3[capacity]; next = new int[capacity]; }
        int used;
        float spacing;
        public void Begin(HordeEnemyWorld enemies, float minimumSpacing)
        {
            spacing = Mathf.Max(.5f, minimumSpacing); heads.Clear(); used = 0;
            for (int i = 0; i < enemies.MaxCount; i++)
                if (enemies.GetEnemy(i).alive) Add(enemies.GetEnemy(i).position);
        }
        Vector2Int Key(Vector3 p) => new(Mathf.FloorToInt(p.x / spacing), Mathf.FloorToInt(p.z / spacing));
        public bool IsFree(Vector3 p)
        {
            var cell = Key(p);
            for (int z = -1; z <= 1; z++) for (int x = -1; x <= 1; x++)
                if (heads.TryGetValue(cell + new Vector2Int(x,z), out int id))
                    for (; id >= 0; id = next[id]) {
                        var delta = positions[id] - p; delta.y = 0;
                        if (delta.sqrMagnitude < spacing * spacing) return false;
                    }
            return true;
        }
        public void Add(Vector3 p)
        {
            var cell = Key(p); positions[used] = p;
            next[used] = heads.TryGetValue(cell, out int head) ? head : -1;
            heads[cell] = used++;
        }
    }
}
