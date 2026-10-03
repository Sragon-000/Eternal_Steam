using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using EternalSteam.OpenWorld;
namespace EternalSteam.Tests {
[Category("CurrentScene")]
public sealed class HudModeGroupsTests {
[Test]

 public void SavedGroupsKeepControlsAndReverseWithoutRebuilding(){
 var scene=CurrentSceneFixture.Open();var name=scene.name;try{
 var h=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<CanvasWorldHud>(true)).Single();var g=h.Groups;
 Assert.That(g,Is.Not.Null);Assert.That(g.Construction.gameObject.activeSelf,Is.False);Assert.That(g.Combat.gameObject.activeSelf,Is.True);Assert.That(h.ShowDevelopmentControls,Is.True);Assert.That(h.Input.HudGroups,Is.SameAs(g));
 var count=h.GetComponentsInChildren<Transform>(true).Length;g.Select(false);g.Advance(1);g.Select(true);g.Advance(.12f);Assert.That(g.Transitioning,Is.True);Assert.That(g.Construction.interactable,Is.False);g.Select(false);g.Advance(1);Assert.That(g.Transitioning,Is.False);Assert.That(g.Combat.interactable,Is.True);Assert.That(g.Construction.gameObject.activeSelf,Is.False);
 g.Select(true);g.Advance(1);Assert.That(g.CanUseConstruction,Is.True);Assert.That(g.Combat.gameObject.activeSelf,Is.False);Assert.That(h.GetComponentsInChildren<Transform>(true).Length,Is.EqualTo(count));Assert.That(h.Input.IsEditing,Is.False,"Showing construction does not begin placement");
 Assert.That(h.Texts.Single(t=>t.Id=="date").View.transform.IsChildOf(h.Layout.ClockPanel),Is.True);Assert.That(h.Texts.Single(t=>t.Id=="remaining").View.transform.IsChildOf(h.Layout.ClockPanel),Is.True);
 }finally{CurrentSceneFixture.Close(scene);}}
 [TestCase(0,0)][TestCase(250,250)][TestCase(-300,700)]
 public void LocalMapRetainsViewCenterAndRoundTrips(float x,float z){var center=new Vector3(x,0,z);var p=MinimapProjection.Around(center);Assert.That(p.Bounds.size,Is.EqualTo(Vector2.one*150));Assert.That(p.ToMap(center),Is.EqualTo(Vector2.one*.5f));var world=center+new Vector3(40,0,-31);Assert.That(Vector3.Distance(p.ToWorld(p.ToMap(world)),world),Is.LessThan(.001f));}
}}
