using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using EternalSteam;

// Temporary editor fixture for VerifyA5StoragePlay. Cleanup restores the original asset.
public static class VerifyA5StorageAsset
{
    const string DefinitionPath="Assets/EternalSteam/Content/Buildings/BasePlanning/resource.iron.asset";
    const string ProbePath="Assets/EternalSteam/Tests/OpenWorld/Editor/A5StorageProbe.asset";
    const string Backup=VerifyA5StoragePlay.Root+"/resource.iron.before.asset";
    const string ModifiedHash=VerifyA5StoragePlay.Root+"/resource.iron.probe.sha256";
    static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
    static string Digest(byte[] bytes){using var sha=SHA256.Create();return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","");}

    public static string Prepare()
    {
        Check(!EditorApplication.isPlaying&&SceneManager.GetActiveScene().name=="StartRegionSandbox"&&
            !SceneManager.GetActiveScene().isDirty,"clean StartRegionSandbox Edit Mode required");
        Check(!File.Exists(Backup)&&!File.Exists(ModifiedHash)&&
            AssetDatabase.LoadAssetAtPath<StorageModuleDefinition>(ProbePath)==null,"storage probe already prepared");
        Check(!File.Exists(VerifyA5StoragePlay.Root+"/current.json")&&
            !File.Exists(VerifyA5StoragePlay.Root+"/previous.json"),"isolated save root is not fresh");
        var definition=AssetDatabase.LoadAssetAtPath<BuildingDefinition>(DefinitionPath);
        Check(definition!=null&&definition.Id=="resource.iron"&&
            !definition.Modules.OfType<StorageModuleDefinition>().Any(),"unexpected iron definition");
        Directory.CreateDirectory(VerifyA5StoragePlay.Root);
        File.WriteAllBytes(Backup,File.ReadAllBytes(DefinitionPath));
        var probe=ScriptableObject.CreateInstance<StorageModuleDefinition>();
        probe.ResourceId="iron";probe.Capacity=250;
        AssetDatabase.CreateAsset(probe,ProbePath);
        definition.Modules.Add(probe);EditorUtility.SetDirty(definition);AssetDatabase.SaveAssets();
        File.WriteAllText(ModifiedHash,Digest(File.ReadAllBytes(DefinitionPath)));
        return "PASS temporary resource.iron +250 storage fixture prepared; original asset backed up";
    }

    public static string Cleanup()
    {
        Check(!EditorApplication.isPlaying,"leave Play Mode before fixture cleanup");
        Check(File.Exists(Backup)&&File.Exists(ModifiedHash),"fixture backup missing");
        Check(Digest(File.ReadAllBytes(DefinitionPath))==File.ReadAllText(ModifiedHash),
            "definition changed during probe; preserve the backup and inspect before restoring");
        Check(AssetDatabase.DeleteAsset(ProbePath),"temporary module asset could not be deleted");
        File.WriteAllBytes(DefinitionPath,File.ReadAllBytes(Backup));
        AssetDatabase.ImportAsset(DefinitionPath,ImportAssetOptions.ForceUpdate);
        Check(Digest(File.ReadAllBytes(DefinitionPath))==Digest(File.ReadAllBytes(Backup)),"definition restoration mismatch");
        File.Delete(ModifiedHash);File.Delete(Backup);
        return "PASS production definition restored byte-for-byte; temporary module removed";
    }
}
