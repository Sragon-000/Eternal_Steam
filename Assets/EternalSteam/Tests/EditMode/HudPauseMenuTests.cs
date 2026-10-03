using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using EternalSteam.OpenWorld;
namespace EternalSteam.Tests
{
 [Category("CurrentScene")]
public sealed class HudPauseMenuTests
 {
[Test]

  public void SavedModalOwnsPauseAndSaveAndPreservesConstructionGroup()
  {
   var scene=CurrentSceneFixture.Open();var name=scene.name;
   try{
    var h=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<CanvasWorldHud>(true)).Single();var m=h.Sandbox.PauseMenu;
    foreach(var input in h.GetComponentsInChildren<TMPro.TMP_InputField>(true))Assert.That(input.restoreOriginalTextOnEscape,Is.False,"Esc must preserve new input: "+input.name);
    Assert.That(m,Is.Not.Null);Assert.That(m.Content,Is.Not.Null);Assert.That(m.SaveSection,Is.Not.Null);Assert.That(m.ProgressSection,Is.Not.Null);Assert.That(m.Scroll,Is.Not.Null);Assert.That(m.DeveloperEntry,Is.Not.Null);Assert.That(m.Content.transform,Is.SameAs(m.Panel));Assert.That(m.Hud,Is.SameAs(h));Assert.That(m.Overlay.gameObject.activeSelf,Is.False);
    Assert.That(h.Buttons.Single(b=>b.Id=="pause").View.transform.IsChildOf(m.Panel),Is.True);
    Assert.That(h.Buttons.Single(b=>b.Id=="save").View.transform.IsChildOf(m.Panel),Is.True);
    Assert.That(h.Buttons.Single(b=>b.Id=="menu-fold").View.gameObject.activeSelf,Is.False);
    Assert.That(h.Layout.FloatingPanels.Contains(m.Panel),Is.False,"Generic layout must not resize the modal");
    Assert.That(m.Panel.GetComponentInChildren<UnityEngine.UI.ScrollRect>(true).transform is RectTransform,Is.True);
    var close=h.Buttons.Single(b=>b.Id=="menu-close").View;Assert.That(close.onClick.GetPersistentTarget(0),Is.SameAs(m));
    Assert.That(m.LoadConfirmation.GetComponentsInChildren<UnityEngine.UI.Button>(true).All(b=>b.onClick.GetPersistentTarget(0)==m),Is.True);
    h.Groups.Select(true);h.Groups.Advance(1);m.SetOpen(true);m.Advance(1);h.Groups.Select(false);
    Assert.That(h.Groups.ConstructionSelected,Is.True,"Modal blocks group changes");Assert.That(h.Sandbox.SimulationPaused,Is.True);
    Assert.That(m.AllowsHudCommand("infinite-resources"),Is.False);Assert.That(m.AllowsHudCommand("save"),Is.True);
    m.LoadConfirmation.SetActive(true);m.Escape();Assert.That(m.IsOpen,Is.True);Assert.That(m.LoadConfirmation.activeSelf,Is.False);
    m.Escape();m.Advance(1);Assert.That(m.IsOpen,Is.False);Assert.That(h.Groups.ConstructionSelected,Is.True);
   }finally{CurrentSceneFixture.Close(scene);}
  }
 }
}
