using System.Reflection;
using TickToTalk;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class TickToTalkEnvironmentBaker
{
    const string MenuPath = "Tools/Tick To Talk/Bake Editable Environment Into Scene";

    [MenuItem(MenuPath)]
    public static void Bake()
    {
        if (Application.isPlaying)
        {
            Debug.LogError("Exit Play Mode before baking the Tick To Talk environment.");
            return;
        }

        var presentation = Object.FindFirstObjectByType<PrisonPresentation>();
        if (!presentation)
        {
            Debug.LogError("PrisonPresentation was not found in the open scene.");
            return;
        }

        Build<CellSequence>(presentation.gameObject);
        Build<PrisonMirrors>(presentation.gameObject);
        Build<PrisonSurveillance>(presentation.gameObject);

        EditorUtility.SetDirty(presentation.gameObject);
        EditorSceneManager.MarkSceneDirty(presentation.gameObject.scene);
        EditorApplication.delayCall += () =>
        {
            EditorSceneManager.SaveOpenScenes();
            Selection.activeGameObject = presentation.gameObject;
            Debug.Log("Tick To Talk environment baked into the scene. Generated rooms, mirrors, CCTV and prison props are now editable in the Hierarchy.");
        };
    }

    static void Build<T>(GameObject host) where T : MonoBehaviour
    {
        var component = host.GetComponent<T>();
        if (!component) component = Undo.AddComponent<T>(host);

        var start = typeof(T).GetMethod("Start", BindingFlags.Instance | BindingFlags.NonPublic);
        if (start == null)
        {
            Debug.LogError(typeof(T).Name + " has no private Start method to bake.");
            return;
        }

        start.Invoke(component, null);
        EditorUtility.SetDirty(component);
    }
}
