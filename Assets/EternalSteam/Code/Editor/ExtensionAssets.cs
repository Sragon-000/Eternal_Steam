using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace EternalSteam.Editor
{
    public static class ExtensionAssets
    {
        public const string ScenePath="Assets/EternalSteam/Scene/Tests/ModuleSandbox.unity";
        const string Data="Assets/EternalSteam/Content/Buildings/ModuleSamples/Data/";
        [MenuItem("Eternal Steam/Foundation/Create Extension Samples")]
        public static void Create()
        {
            if(File.Exists(ScenePath) || Directory.Exists(Data)) throw new InvalidOperationException("Extension samples already exist; no assets overwritten.");
            Directory.CreateDirectory(Data); AssetDatabase.Refresh();
            var baseline=AssetDatabase.LoadAssetAtPath<BuildingCatalog>("Assets/EternalSteam/Settings/Catalogs/SampleCatalog.asset");
            var catalog=UnityEngine.Object.Instantiate(baseline);
            var upgrade=Make<UpgradeModuleDefinition>("Upgrade");
            var nexus=Make<NexusModuleDefinition>("Nexus");
            var health=Make<HealthModuleDefinition>("NexusHealth"); health.Maximum=100;
            var core=Building("nexus","넥서스",baseline.Buildings[0],health,nexus,upgrade); core.Recoverable=false; core.Category=BuildingCategory.Other;
            catalog.Buildings.Add(core);
            var slow=Make<StatusEffectDefinition>("Slow"); slow.Kind=StatusKind.Slow;
            var stun=Make<StatusEffectDefinition>("Stun"); stun.Kind=StatusKind.Stun; stun.Strength=1; stun.Duration=0.3f;
            var armor=Make<StatusEffectDefinition>("ArmorReduction"); armor.Kind=StatusKind.ArmorReduction; armor.Strength=0.2f;
            var executions=new AttackExecutionDefinition[] { Make<AreaAttackDefinition>("Area"),Make<PiercingAttackDefinition>("Piercing"),Make<ChainAttackDefinition>("Chain"),Make<ProjectileAttackDefinition>("Projectile") };
            string[] names={"광역 · 둔화 포탑","관통 · 방어 감소 포탑","연쇄 · 기절 포탑","유도 투사체 포탑"};
            for(int i=0;i<executions.Length;i++)
            {
                var attack=Make<AttackModuleDefinition>("Attack"+i); attack.Execution=executions[i]; attack.Damage=8;
                if(i<3) attack.Effects.Add(i==0?slow:i==1?armor:stun);
                var tower=Building("tower"+i,names[i],baseline.Buildings[2],health,attack,upgrade); tower.Category=BuildingCategory.Defense;
                catalog.Buildings.Add(tower);
            }
            var storage=Make<StorageModuleDefinition>("EnergyStorage");
            var partsStorage=Make<StorageModuleDefinition>("PartsStorage"); partsStorage.ResourceId="sample.parts";
            var produce=Make<ProductionModuleDefinition>("EnergyProduction");
            var convert=Make<ProductionModuleDefinition>("Conversion"); convert.InputId="sample.energy"; convert.InputAmount=5; convert.OutputId="sample.parts"; convert.OutputAmount=1;
            catalog.Buildings.Add(Building("storage","에너지 저장소",baseline.Buildings[0],health,storage));
            catalog.Buildings.Add(Building("generator","에너지 생산기",baseline.Buildings[0],health,produce));
            catalog.Buildings.Add(Building("converter","부품 변환기",baseline.Buildings[0],health,partsStorage,convert));
            var obstacle=Make<ObstacleModuleDefinition>("Obstacle");
            var wall=Building("wall","지상 이동 방벽",baseline.Buildings[1],health,obstacle); wall.Category=BuildingCategory.Installation;
            catalog.Buildings.Add(wall);
            AssetDatabase.CreateAsset(catalog,"Assets/EternalSteam/Settings/Catalogs/ModuleCatalog.asset");
            foreach(var asset in AssetDatabase.FindAssets("",new[]{Data.TrimEnd('/')})) { var obj=AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(asset)); if(obj!=null) EditorUtility.SetDirty(obj); }
            AssetDatabase.SaveAssets();
            var errors=catalog.Validate(); if(errors.Count>0) throw new InvalidOperationException(string.Join("\n",errors));
            if(!AssetDatabase.CopyAsset(FoundationAssets.ScenePath,ScenePath)) throw new InvalidOperationException("Scene copy failed.");
            var scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Additive);
            try
            {
                var sample=scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<FoundationSandbox>()).Single();
                sample.Catalog=catalog; sample.ExtendedScenario=true;
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            }
            finally { EditorSceneManager.CloseScene(scene,true); }
            AssetDatabase.SaveAssets();
        }
        static T Make<T>(string name) where T:ScriptableObject
        { var asset=ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(asset,Data+name+".asset"); return asset; }
        static BuildingDefinition Building(string id,string title,BuildingDefinition source,params BuildingModuleDefinition[] modules)
        {
            var definition=Make<BuildingDefinition>("Building_"+id); definition.Id="sample.modules."+id; definition.DisplayName=title;
            definition.ViewPrefab=source.ViewPrefab; definition.Footprint=source.Footprint; definition.Modules.AddRange(modules);
            definition.Category=BuildingCategory.Resource; return definition;
        }
    }
}
