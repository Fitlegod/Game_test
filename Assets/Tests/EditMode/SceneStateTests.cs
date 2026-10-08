using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Стартовое состояние SampleScene (как она лежит на диске): в Edit Mode выключены все экраны и оверлеи (виден только фон камеры).
public class SceneStateTests
{
    const string ScenePath = "Assets/Scenes/SampleScene.unity";

    private static void WithScene(System.Action<Scene> check)
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        try { check(scene); }
        finally { EditorSceneManager.CloseScene(scene, true); }
    }

    private static T[] All<T>(Scene scene) where T : Component
        => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();

    [Test]
    public void NoObjectIsBothScreenRootAndOverlayRoot()
    {
        WithScene(scene =>
        {
            var both = All<ScreenRoot>(scene).Where(s => s.GetComponent<OverlayRoot>() != null).Select(s => s.name).ToList();
            Assert.IsEmpty(both, "ScreenRoot и OverlayRoot на одном объекте: " + string.Join(", ", both));
        });
    }

    [Test]
    public void AllOverlayRootsAreSavedInactive()
    {
        WithScene(scene =>
        {
            var overlays = All<OverlayRoot>(scene);
            Assert.Greater(overlays.Length, 0);
            var active = overlays.Where(o => o.gameObject.activeSelf).Select(o => o.name).ToList();
            Assert.IsEmpty(active, "оверлеи включены в сцене: " + string.Join(", ", active));
        });
    }

    [Test]
    public void AllScreensAreSavedInactive()
    {
        WithScene(scene =>
        {
            var screens = All<ScreenRoot>(scene);
            Assert.Greater(screens.Length, 0);
            var active = screens.Where(s => s.gameObject.activeSelf).Select(s => s.screenId).ToList();
            Assert.IsEmpty(active, "в Edit Mode не должно быть видно ни одного экрана; включены: " + string.Join(", ", active));
        });
    }

    [Test]
    public void ScreenIdsAreUnique()
    {
        WithScene(scene =>
        {
            var ids = All<ScreenRoot>(scene).Select(s => s.screenId).ToList();
            CollectionAssert.AllItemsAreUnique(ids);
        });
    }
}
