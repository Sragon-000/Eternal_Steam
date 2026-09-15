#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
namespace EternalSteam.Tests
{
    public sealed class ExtensionPlayTests
    {
        [UnityTest] public IEnumerator ExtendedCatalogUpgradeEconomyAndNexusLossWorkThroughScene()
        {
            EditorSceneManager.LoadSceneInPlayMode("Assets/EternalSteam/Scene/Tests/ModuleSandbox.unity",new LoadSceneParameters(LoadSceneMode.Single));
            yield return null; yield return null;
            var sample=Object.FindFirstObjectByType<FoundationSandbox>();
            var hud=Object.FindFirstObjectByType<FoundationHud>(); var input=Object.FindFirstObjectByType<SandboxInput>();
            var root=hud.GetComponent<UIDocument>().rootVisualElement;
            Assert.That(sample.Error,Is.Null); Assert.That(root.Q("catalog").childCount,Is.EqualTo(12));
            var session=sample.Session; session.BeginPlacement();
            session.Add(sample.Available.Single(x=>x.Id=="sample.modules.nexus"),new Vector2Int(-5,-4),out _);
            session.Add(sample.Available.Single(x=>x.Id=="sample.modules.tower3"),new Vector2Int(0,-2),out _);
            session.Add(sample.Available.Single(x=>x.Id=="sample.modules.storage"),new Vector2Int(2,-2),out _);
            session.Add(sample.Available.Single(x=>x.Id=="sample.modules.generator"),new Vector2Int(3,-2),out _);
            Assert.That(session.Confirm().Success,Is.True);
            var nexus=session.World.Buildings.Single(x=>x.Module<NexusModule>()!=null);
            var tower=session.World.Buildings.Single(x=>x.Module<AttackModule>()!=null);
            Assert.That(session.Upgrade(tower.Id,out _),Is.False);
            input.SelectedId=nexus.Id; input.NotifyChanged();
            using(var evt=NavigationSubmitEvent.GetPooled()) { evt.target=root.Q<Button>("upgrade"); root.Q<Button>("upgrade").SendEvent(evt); }
            Assert.That(nexus.Module<UpgradeModule>().Level,Is.EqualTo(2)); Assert.That(session.Upgrade(tower.Id,out _),Is.True);
            Assert.That(sample.StartCombat(),Is.True); Assert.That(session.Upgrade(tower.Id,out _),Is.False);
            session.Tick(1); Assert.That(sample.Resources.Amount("sample.energy"),Is.EqualTo(5));
            nexus.Module<HealthModule>().ApplyDamage(1000); Assert.That(session.Defeated,Is.True); Assert.That(session.Phase,Is.EqualTo(SandboxPhase.Result));
            yield return null;
            Assert.That(root.Q<Label>("phase").text,Does.Contain("넥서스 파괴"));
            sample.ResetSample(); hud.RebuildCatalog(); yield return null;
            Assert.That(sample.Nexus.Defeated,Is.False); Assert.That(sample.Nexus.LevelCap,Is.Zero);
            Assert.That(sample.Resources.Amount("sample.energy"),Is.Zero); Assert.That(sample.Resources.Capacity("sample.energy"),Is.Zero);
            Assert.That(sample.Session.World.Buildings,Is.Empty); Assert.That(sample.Session.World.Grid.ReservationCount,Is.Zero);
            LogAssert.NoUnexpectedReceived();
        }
    }
}
#endif
