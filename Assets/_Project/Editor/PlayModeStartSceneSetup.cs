using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Garante que apertar Play no Editor sempre inicia pelo menu principal,
/// mesmo quando alguem esta editando diretamente uma fase.
/// </summary>
[InitializeOnLoad]
public static class PlayModeStartSceneSetup
{
    private const string MenuScenePath = "Assets/_Project/Core/Scenes/MenuPrincipal.unity";

    static PlayModeStartSceneSetup()
    {
        EditorApplication.delayCall += Configure;
    }

    private static void Configure()
    {
        var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MenuScenePath);
        if (scene == null) return;

        if (EditorSceneManager.playModeStartScene != scene)
            EditorSceneManager.playModeStartScene = scene;
    }
}
