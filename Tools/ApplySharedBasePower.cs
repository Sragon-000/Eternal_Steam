using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using EternalSteam;
using EternalSteam.OpenWorld;
public static class ApplySharedBasePower
{
    const string Folder="Assets/EternalSteam/Content/Buildings/BasePower";
    static T Save<T>(T asset,string name)where T:UnityEngine.Object{AssetDatabase.CreateAsset(asset,Folder+"/"+name+".asset");return asset;}
    public static string Main()
    {
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();if(Application.isPlaying||scene.name!="StartRegionSandbox")throw new Exception("Clean original start scene required");
        if(scene.isDirty&&!EditorSceneManager.SaveScene(scene,"Assets/EternalSteam/Scene/Tests/StartRegionBeforeBasePower.unity",true))throw new Exception("Scene backup failed");
        if(AssetDatabase.IsValidFolder(Folder))throw new Exception("BasePower already authored; do not overwrite");AssetDatabase.CreateFolder("Assets/EternalSteam/Content/Buildings","BasePower");
        var s=UnityEngine.Object.FindFirstObjectByType<OpenWorldSandbox>();var catalog=UnityEngine.Object.Instantiate(s.ContentCatalog);catalog.Buildings=catalog.Buildings.ToList();
        var consumer=ScriptableObject.CreateInstance<PowerModuleDefinition>();consumer.Role=PowerRole.Consumer;consumer.Rate=5;Save(consumer,"DefenseConsumer");
        var legacy=UnityEngine.Object.Instantiate(consumer);legacy.ExternalAttackAdapter=true;Save(legacy,"LegacyDefenseConsumer");
        var storage=ScriptableObject.CreateInstance<PowerModuleDefinition>();storage.Role=PowerRole.Storage;storage.Capacity=200;storage.CapacityPerLevel=20;Save(storage,"BaseStorage");
        var shared=Save(ScriptableObject.CreateInstance<MainBaseUpgradeDefinition>(),"SharedMainUpgrade");
        for(int i=0;i<catalog.Buildings.Count;i++){
            var source=catalog.Buildings[i];var role=OpenWorldContent.RoleOf(source);bool weapon=source.Modules.Any(m=>m is WeaponModuleDefinition||m is AttackModuleDefinition);
            if(role!=BaseRole.Main&&role!=BaseRole.Sub&&!weapon)continue;
            var d=UnityEngine.Object.Instantiate(source);d.Modules=d.Modules.ToList();
            if(role==BaseRole.Main){d.Modules.RemoveAll(m=>m is UpgradeModuleDefinition);d.Modules.Add(shared);}
            d.Modules.Add(role==BaseRole.Main||role==BaseRole.Sub?storage:consumer);catalog.Buildings[i]=Save(d,source.Id);
        }
        var generator=UnityEngine.Object.Instantiate(catalog.Buildings.Single(d=>d.Id=="resource.iron"));generator.Id="resource.power_generator";generator.DisplayName="전력 발전기";generator.Footprint=Vector2Int.one;
        generator.Placement=UnityEngine.Object.Instantiate(generator.Placement);generator.Placement.Surface=BuildingSurface.Ground;generator.Placement.RequiredNexusLevel=1;generator.Placement.SnapCells=1;generator.Placement.RequiresBuildArea=false;generator.Placement.RequiresOperationalArea=true;Save(generator.Placement,"GeneratorPlacement");
        generator.Modules=generator.Modules.Where(m=>m is not ProductionModuleDefinition).ToList();var production=ScriptableObject.CreateInstance<PowerModuleDefinition>();production.Role=PowerRole.Producer;production.Rate=20;generator.Modules.Add(Save(production,"GeneratorProduction"));
        var root=new GameObject("전력 발전기");
        void Part(string name,PrimitiveType shape,Vector3 position,Vector3 scale){var part=GameObject.CreatePrimitive(shape);part.name=name;part.transform.SetParent(root.transform,false);part.transform.localPosition=position;part.transform.localScale=scale;part.GetComponent<Renderer>().sharedMaterial=s.TowerMaterial;UnityEngine.Object.DestroyImmediate(part.GetComponent<Collider>());}
        Part("Generator housing",PrimitiveType.Cube,new Vector3(0,.5f,0),new Vector3(1.5f,.9f,1.5f));Part("Rotor",PrimitiveType.Cylinder,new Vector3(0,1.2f,0),new Vector3(.8f,.3f,.8f));Part("Power mast",PrimitiveType.Cylinder,new Vector3(.5f,1.8f,.5f),new Vector3(.18f,.7f,.18f));
        generator.ViewPrefab=PrefabUtility.SaveAsPrefabAsset(root,Folder+"/Generator.prefab");UnityEngine.Object.DestroyImmediate(root);catalog.Buildings.Add(Save(generator,"Generator"));
        var errors=catalog.Validate();if(errors.Count>0)throw new Exception(string.Join("\n",errors));s.ContentCatalog=Save(catalog,"Catalog");s.LegacyPower=legacy;
        // Find an authored 3x3 ground square close to the old boss point; no runtime relocation.
        var tile=s.Ground.GetComponent<TileWorldGround>();var original=s.AssaultSettings.BossPosition;float best=float.PositiveInfinity;Vector3 chosen=default;
        for(int z=0;z<tile.Height;z++)for(int x=0;x<tile.Width;x++){
            var p=WorldGridGeometry.Center(WorldGridGeometry.Cell(tile.GridRoot.TransformPoint(new Vector3(x*tile.CellSize,0,z*tile.CellSize)),2),2);bool valid=true;
            for(int dz=-3;dz<=3&&valid;dz++)for(int dx=-3;dx<=3;dx++)if(!tile.IsPlayable(p+WorldGridGeometry.ToWorld(new Vector3(dx,0,dz)))){valid=false;break;}
            if(valid&&(p-original).sqrMagnitude<best){best=(p-original).sqrMagnitude;chosen=p;}
        }
        if(float.IsPositiveInfinity(best))throw new Exception("No valid boss square");s.AssaultSettings.BossPosition=chosen;EditorUtility.SetDirty(s.AssaultSettings);
        var markers=new GameObject("Spawn areas · normal 1 cell · boss 9 cells");var view=markers.AddComponent<SpawnAreaView>();view.Sandbox=s;
        LineRenderer Line(string name,Color color){var go=new GameObject(name);go.transform.SetParent(markers.transform);var line=go.AddComponent<LineRenderer>();line.sharedMaterial=s.LineMaterial;line.positionCount=5;line.startWidth=line.endWidth=.1f;line.startColor=line.endColor=color;line.useWorldSpace=true;line.enabled=false;return line;}
        view.Boss=Line("Boss reserved 3x3",Color.magenta);view.Normal=new LineRenderer[12];for(int i=0;i<12;i++)view.Normal[i]=Line("Normal 1x1 "+i,Color.yellow);
        EditorUtility.SetDirty(s);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();return "Authored shared main, per-base power, generator, defense adapters, reserved 3x3 boss and scene markers";
    }
}
