#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class CombatSceneScaffoldBuilder
{
    private const string CombatScenePath = "Assets/Scenes/CombatScene.unity";
    private const string BoardScenePath = "Assets/Scenes/BoardScene.unity";

    [MenuItem("Tools/Mythical Monsters/Create Combat Scene Scaffold")]
    public static void CreateCombatSceneScaffold()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        GameObject controllerObject = new GameObject("CombatSceneRoot");
        controllerObject.AddComponent<CombatSceneController>();

        EditorSceneManager.SaveScene(scene, CombatScenePath);
        AddScenesToBuildSettings();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("CombatScene scaffold created and build settings updated.");
    }

    private static void AddScenesToBuildSettings()
    {
        var scenes = EditorBuildSettings.scenes.ToList();

        if (!scenes.Any(scene => scene.path == BoardScenePath))
            scenes.Add(new EditorBuildSettingsScene(BoardScenePath, true));

        if (!scenes.Any(scene => scene.path == CombatScenePath))
            scenes.Add(new EditorBuildSettingsScene(CombatScenePath, true));

        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
#endif
