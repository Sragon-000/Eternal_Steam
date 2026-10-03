using System;using System.IO;using System.Linq;using System.Text.RegularExpressions;using System.Security.Cryptography;using UnityEditor;using UnityEngine;using UnityEngine.SceneManagement;using EternalSteam;using Newtonsoft.Json.Linq;
public static class AuditCampaignAssets
{
 public static string Main()
 {
  var active=SceneManager.GetActiveScene();const string scene="Assets/EternalSteam/Scene/Tests/StartRegionSandbox.unity";
  string text=File.ReadAllText(scene);string guid=Regex.Match(text,@"ContentCatalog: \{fileID: \d+, guid: ([a-f0-9]+)").Groups[1].Value;
  if(guid=="")throw new Exception("Campaign catalog reference absent");
  var catalog=AssetDatabase.LoadAssetAtPath<BuildingCatalog>(AssetDatabase.GUIDToAssetPath(guid));var entries=new JArray();
  foreach(var d in catalog.Buildings){var modules=new JArray();foreach(var m in d.Modules)modules.Add(new JObject{{"type",m.GetType().FullName},{"asset",AssetDatabase.GetAssetPath(m)},{"values",JObject.Parse(JsonUtility.ToJson(m))}});entries.Add(new JObject{{"id",d.Id},{"name",d.DisplayName},{"asset",AssetDatabase.GetAssetPath(d)},{"validationErrors",JArray.FromObject(d.Validate())},{"placement",d.Placement==null?null:JObject.Parse(JsonUtility.ToJson(d.Placement))},{"placementAsset",AssetDatabase.GetAssetPath(d.Placement)},{"values",JObject.Parse(JsonUtility.ToJson(d))},{"modules",modules}});}
  using var hash=SHA256.Create();var report=new JObject{{"utc",DateTime.UtcNow.ToString("O")},{"currentScene",active.path},{"auditedSceneFile",scene},{"auditedSceneSha256",BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(scene))).Replace("-","").ToLowerInvariant()},{"sceneOpened",false},{"catalog",AssetDatabase.GetAssetPath(catalog)},{"buildings",entries}};
  if(active.handle!=SceneManager.GetActiveScene().handle)throw new Exception("Active scene changed");
  File.WriteAllText("Docs/Measurements/2026-10-03-playthrough-preflight/campaign-assets.json",report.ToString());return entries.Count+" building definitions audited without opening another scene";
 }
}
