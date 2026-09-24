using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
namespace EternalSteam.OpenWorld
{
    [Serializable] public sealed class BuildingRecord
    {
        public string id,definition,baseId,owner,supply,foundation;
        public Vector2Int cell;public Vector3 direction;
        public List<SavedState> modules=new();
        public float legacyCooldown;public int legacyShot;public Vector3 legacyDirection;
    }
    [Serializable] public sealed class FoundationRecord {public string id;public Vector3 position;}
    [Serializable] public sealed class SingleMapSnapshot
    {
        public int version=1;public string runId,mapId,configuration,savedUtc;
        public SavedState campaign,clock,power,assault,planner;
        public MapEnergyState energy;
        public List<ResourceBank.Stock> stocks=new();
        public List<string> retiredBases=new();
        public List<FoundationRecord> foundations=new();public List<BuildingRecord> buildings=new();
        public Vector3 cameraFocus;public float cameraZoom;public double pendingTime;
    }
    public static class SaveConfiguration
    {
        static void Append(StringBuilder text,object value,int depth)
        {
            if(depth>12)throw new InvalidOperationException("Configuration nesting exceeds save contract");
            if(value==null){text.Append("null;");return;}
            var type=value.GetType();text.Append(type.FullName).Append(':');
            if(value is string||type.IsPrimitive||type.IsEnum){text.Append(Convert.ToString(value,CultureInfo.InvariantCulture)).Append(';');return;}
            if(value is UnityEngine.Object o&&value is not ScriptableObject){text.Append(o.name).Append(';');return;}
            if(value is IEnumerable sequence){foreach(var item in sequence)Append(text,item,depth+1);text.Append(';');return;}
            if(type.IsValueType){text.Append(JsonUtility.ToJson(value)).Append(';');return;}
            foreach(var f in type.GetFields(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance).Where(f=>!f.IsStatic&&!f.IsNotSerialized&&(f.IsPublic||f.GetCustomAttribute<SerializeField>()!=null)).OrderBy(f=>f.Name,StringComparer.Ordinal)){text.Append(f.Name).Append('=');Append(text,f.GetValue(value),depth+1);}
        }
        public static string Compute(OpenWorldSandbox s)
        {
            var text=new StringBuilder("single-map-1;");Append(text,s.ContentCatalog,0);Append(text,s.LegacyPower,0);Append(text,s.AssaultSettings,0);Append(text,s.EnemyCombat,0);
            foreach(var prefab in s.TowerPrefabs){var tower=prefab.GetComponent<SceneTower>();Append(text,tower.Kind,0);Append(text,tower.Health,0);Append(text,tower.CombatBody,0);Append(text,tower.Rotation,0);Append(text,tower.Upgrade,0);}
            Append(text,s.DayDurationSeconds,0);Append(text,s.NightDurationSeconds,0);Append(text,s.StartingBase.Cell,0);Append(text,s.StartingBase.transform.position,0);
            Append(text,s.Ground.transform.position,0);Append(text,s.Ground.terrainData.size,0);var tiles=s.Ground.GetComponent<TileWorldGround>();if(tiles!=null){Append(text,tiles.Width,0);Append(text,tiles.Height,0);Append(text,tiles.CellSize,0);Append(text,tiles.GridRoot.position,0);Append(text,tiles.GridRoot.rotation,0);text.Append(Convert.ToBase64String(tiles.PlayableCells));}
            return JsonSaveStore.Digest(text.ToString());
        }
    }
}
