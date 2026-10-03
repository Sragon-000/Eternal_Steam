using System.Linq;using NUnit.Framework;using UnityEngine;using UnityEditor.SceneManagement;using EternalSteam.OpenWorld;
namespace EternalSteam.Tests{
[Category("CurrentScene")]
public sealed class BaseResourceAccordionTests{
[Test]

public void AuthoredRowsHaveIndependentLookupAndPersistentNavigation(){var scene=CurrentSceneFixture.Open();var name=scene.name;try{var h=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<CanvasWorldHud>(true)).Single();var a=h.BaseResources;Assert.That(a,Is.Not.Null);Assert.That(a.Hud,Is.SameAs(h));Assert.That(a.Rows.Length,Is.EqualTo(4));Assert.That(a.Scroll.viewport,Is.Not.Null);Assert.That(a.Scroll.content.IsChildOf(a.Scroll.viewport),Is.True);Assert.That(a.Scroll.verticalScrollbar,Is.Not.Null);Assert.That(a.Scroll.GetComponentInParent<HudModeGroups>(true),Is.SameAs(h.Groups));Assert.That(h.Sections.Single(s=>s.Id=="resources").Body,Is.SameAs(h.Layout.ResourceBody.gameObject));foreach(var row in a.Rows){Assert.That(row.Resources.Select(r=>r.Id).Distinct().Count(),Is.EqualTo(row.Resources.Length));Assert.That(row.Resources.Length,Is.GreaterThan(0));Assert.That(row.Header.onClick.GetPersistentTarget(0),Is.SameAs(a));Assert.That(row.Header.onClick.GetPersistentMethodName(0),Is.EqualTo("Toggle"));Assert.That(row.Body.transform.parent,Is.SameAs(row.Root.transform));Assert.That(row.Resources.All(r=>r.Value!=null),Is.True);}Assert.That(a.Previous.onClick.GetPersistentTarget(0),Is.SameAs(a));Assert.That(a.Next.onClick.GetPersistentTarget(0),Is.SameAs(a));}finally{CurrentSceneFixture.Close(scene);}}
}}
