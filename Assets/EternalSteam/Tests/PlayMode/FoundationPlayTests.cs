#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace EternalSteam.Tests
{
    public sealed class FoundationPlayTests
    {
        const string Scene = "Assets/EternalSteam/Scene/Tests/FoundationSandbox.unity";
        [UnitySetUp] public IEnumerator Load()
        {
            EditorSceneManager.LoadSceneInPlayMode(Scene,new LoadSceneParameters(LoadSceneMode.Single));
            yield return null; yield return null;
        }
        [UnityTearDown] public IEnumerator Unload()
        {
            var sample = Object.FindFirstObjectByType<FoundationSandbox>();
            if (sample != null) sample.Session.Dispose();
            yield return null;
        }
        [UnityTest] public IEnumerator BatchPlacementRecoveryAndResetLeaveNoState()
        {
            var sample = Object.FindFirstObjectByType<FoundationSandbox>();
            Assert.That(sample.Error,Is.Null);
            var session = sample.Session;
            session.BeginPlacement();
            session.Add(sample.Available[0],new Vector2Int(-3,-2),out _);
            session.Add(sample.Available[1],new Vector2Int(0,-2),out _);
            Assert.That(sample.Factory.Views.Count,Is.Zero,"Reservations must not instantiate active buildings.");
            Assert.That(session.Confirm().Success,Is.True);
            yield return null;
            var buildings = session.World.Buildings.ToArray();
            Assert.That(buildings.Length,Is.EqualTo(2));
            session.BeginRecovery(); foreach (var building in buildings) session.ToggleRecovery(building.Id);
            session.Cancel(); Assert.That(session.World.Grid.OccupiedCount,Is.EqualTo(3));
            session.BeginRecovery(); foreach (var building in buildings) session.ToggleRecovery(building.Id);
            Assert.That(session.Confirm().Success,Is.True);
            yield return null;
            Assert.That(Object.FindObjectsByType<BuildingView>(FindObjectsSortMode.None),Is.Empty);
            Assert.That(session.World.Grid.OccupiedCount,Is.Zero);
            sample.ResetSample(); yield return null;
            Assert.That(session.World.Buildings,Is.Empty);
            Assert.That(sample.Session.World.Grid.ReservationCount,Is.Zero);
        }
        [UnityTest] public IEnumerator DirectionPreviewAndModuleFreeCatalogItemWorkThroughUi()
        {
            var sample = Object.FindFirstObjectByType<FoundationSandbox>();
            var hud = Object.FindFirstObjectByType<FoundationHud>();
            var input = Object.FindFirstObjectByType<SandboxInput>();
            var root = hud.GetComponent<UIDocument>().rootVisualElement;
            Assert.That(root.Q("catalog").childCount,Is.EqualTo(sample.Available.Count));
            void Click(string name)
            { using var evt = NavigationSubmitEvent.GetPooled(); evt.target = root.Q<Button>(name); root.Q<Button>(name).SendEvent(evt); }
            Click("install");
            sample.Session.Add(sample.Available[2],new Vector2Int(0,-2),out _);
            input.NotifyChanged(); Click("confirm");
            var building = sample.Session.World.Buildings.Single();
            input.SelectedId = building.Id; input.NotifyChanged(); Click("direction");
            sample.Session.Aim(building.Position+Vector3.right);
            Assert.That(building.Direction,Is.EqualTo(Vector3.forward));
            Click("cancel"); Assert.That(building.EditingDirection,Is.False);
            input.SelectedId = building.Id; input.NotifyChanged(); Click("direction");
            sample.Session.Aim(building.Position+Vector3.right); Click("confirm");
            Assert.That(building.Direction,Is.EqualTo(Vector3.right));
            Assert.That(sample.Available[0].Modules.Count,Is.Zero);
            yield return null;
            var pivot = sample.Factory.Views[building.Id].transform.Find("AttackPivot");
            Assert.That(pivot, Is.Not.Null);
            Assert.That(Vector3.Dot(pivot.forward, building.Direction), Is.GreaterThan(0.999f));
            LogAssert.NoUnexpectedReceived();
        }
        [UnityTest] public IEnumerator RuntimeCatalogAdditionNeedsNoUiCodeAndCombatCompletes()
        {
            var sample = Object.FindFirstObjectByType<FoundationSandbox>();
            var original = sample.Catalog;
            var catalog = Object.Instantiate(original);
            var extra = Object.Instantiate(original.Buildings[0]); extra.Id = "test.extra"; extra.DisplayName = "Extra";
            catalog.Buildings.Add(extra);
            try
            {
                sample.Catalog = catalog; sample.ResetSample();
                var hud = Object.FindFirstObjectByType<FoundationHud>(); hud.RebuildCatalog();
                Assert.That(hud.GetComponent<UIDocument>().rootVisualElement.Q("catalog").childCount,Is.EqualTo(4));
                sample.Session.BeginPlacement();
                sample.Session.Add(sample.Available[2],new Vector2Int(0,-2),out _); sample.Session.Confirm();
                Assert.That(sample.StartCombat(),Is.True);
                Assert.That(sample.Session.BeginDirection(sample.Session.World.Buildings.Single().Id),Is.False);
                // Deterministic ticks validate combat without a 30-second wall-clock wait.
                for (int i = 0; i < 80; i++) sample.Session.Tick(0.5f);
                yield return null;
                Assert.That(sample.Remaining,Is.Zero);
                Assert.That(sample.Session.Phase,Is.EqualTo(SandboxPhase.Result));
            }
            finally { sample.Catalog = original; Object.Destroy(catalog); Object.Destroy(extra); }
        }
    }
}
#endif
