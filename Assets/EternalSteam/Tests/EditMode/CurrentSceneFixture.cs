using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using EternalSteam.OpenWorld;

namespace EternalSteam.Tests
{
    // No scene loading or preview scene. Temporary layout edits belong to one Undo group.
    internal static class CurrentSceneFixture
    {
        static int undoGroup = -1;
        static int sceneHandle;
        static readonly List<(object owner, FieldInfo field, object value)> transient = new();
        public static Scene Open()
        {
            var scene = SceneManager.GetActiveScene();
            Assert.That(Application.isPlaying, Is.False, "Run authored checks in Edit mode.");
            Assert.That(undoGroup, Is.EqualTo(-1), "Previous fixture was not restored.");
            Assert.That(SceneManager.sceneCount, Is.EqualTo(1), "Keep only the scene under test loaded.");
            Assert.That(scene.isDirty, Is.False, "Save your changes before validation.");
            var hud = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<CanvasWorldHud>(true)).ToArray();
            if (hud.Length == 0) Assert.Ignore("Current scene has no gameplay Canvas HUD. No other scene will be opened.");
            Assert.That(hud.Length, Is.EqualTo(1));
            transient.Clear();
            foreach (var component in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>(true)))
            {
                if (component == null || !(component.GetType().Namespace ?? "").StartsWith("EternalSteam")) continue;
                foreach (var field in component.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                    if (!field.IsInitOnly && (field.IsNotSerialized || (!field.IsPublic && !field.IsDefined(typeof(SerializeField), true))))
                        transient.Add((component, field, field.GetValue(component)));
            }
            Undo.IncrementCurrentGroup();
            undoGroup = Undo.GetCurrentGroup();
            sceneHandle = scene.handle;
            foreach (var root in scene.GetRootGameObjects())
                Undo.RegisterFullObjectHierarchyUndo(root, "Current scene layout validation");
            return scene;
        }

        public static void Close(Scene scene)
        {
            int group = undoGroup;
            undoGroup = -1;
            if (group < 0) throw new InvalidOperationException("No active scene fixture.");
            Undo.RevertAllDownToGroup(group);
            foreach (var state in transient) state.field.SetValue(state.owner, state.value);
            transient.Clear();
            Assert.That(SceneManager.GetActiveScene().handle == sceneHandle, Is.True, "Test changed scene.");
            Assert.That(SceneManager.sceneCount, Is.EqualTo(1));
            Assert.That(scene.isDirty, Is.False, "Undo did not restore the clean scene; do not save test mutations.");
        }
    }
}
