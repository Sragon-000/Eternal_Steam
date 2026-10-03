using System.Linq;using NUnit.Framework;using UnityEngine;using UnityEditor.SceneManagement;using EternalSteam.OpenWorld;
namespace EternalSteam.Tests{[Category("CurrentScene")]
public sealed class CompactConstructionDockTests{
[Test]

public void SavedControlsKeepActionsAndSeparateDetails(){var scene=CurrentSceneFixture.Open();var name=scene.name;try{var h=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<CanvasWorldHud>(true)).Single();var d=h.Layout.CompactDock;Assert.That(d,Is.Not.Null);Assert.That(d.Hud,Is.SameAs(h));Assert.That(d.CostPanel.gameObject.activeSelf,Is.False);Assert.That(d.GoalPanel.gameObject.activeSelf,Is.False);Assert.That(h.Layout.ConstructionSummary.IsChildOf(d.CostPanel),Is.True);Assert.That(h.Layout.ConstructionSummary.IsChildOf(h.Layout.ConstructionBar),Is.False);Assert.That(h.Layout.FirstLoopObjective.IsChildOf(d.GoalPanel),Is.True);Assert.That(d.CostBrief.richText,Is.True);Assert.That(d.CatalogScroll.horizontal,Is.True);Assert.That(d.CatalogScroll.vertical,Is.False);Assert.That(d.CategoryScroll.GetComponent<UnityEngine.UI.ScrollRect>().viewport.GetComponent<UnityEngine.UI.Mask>(),Is.Not.Null);
foreach(var b in new[]{d.ListButton,d.CostButton,d.ResumeButton}){Assert.That(b.onClick.GetPersistentTarget(0),Is.SameAs(d));Assert.That(b.onClick.GetPersistentMethodName(0),Is.EqualTo("Execute"));}
foreach(var id in new[]{"confirm","cancel"}){var b=h.Buttons.Single(x=>x.Id==id).View;Assert.That(b.transform.IsChildOf(d.Placement.transform),Is.True);Assert.That(b.transform.IsChildOf(d.CostPanel),Is.False);Assert.That(b.onClick.GetPersistentTarget(0),Is.SameAs(h));}
Assert.That(d.HeightPixels+16,Is.LessThanOrEqualTo(180));Assert.That(h.ShowDevelopmentControls,Is.True);
}finally{CurrentSceneFixture.Close(scene);}}
[Test]
public void HiddenDockTransitionDoesNotBlockCombatAndVisibleTransitionStillBlocks(){
 var scene=CurrentSceneFixture.Open();try{
  var h=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<CanvasWorldHud>(true)).Single();var d=h.Layout.CompactDock;var g=h.Groups;
  g.Select(false);g.Advance(1);d.Apply(0);
  Assert.That(d.Transitioning,Is.True,"Initial layout remains pending while the construction group is hidden");
  Assert.That(g.Construction.gameObject.activeInHierarchy,Is.False);
  Assert.That(d.BlocksInput,Is.False,"Hidden layout must not lock combat camera or HUD commands");
  g.Select(true);g.Advance(1);
  Assert.That(d.BlocksInput,Is.True,"Visible unfinished dock transitions still suppress clicks");
  d.Apply(1);Assert.That(d.Transitioning,Is.False);Assert.That(d.BlocksInput,Is.False);
  g.Select(false);g.Advance(1);Assert.That(d.BlocksInput,Is.False);
 }finally{CurrentSceneFixture.Close(scene);}
}
}}
