using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace KachiGun.EditorTools
{
    /// <summary>
    /// Main 씬 만들기 (메뉴: 카치건 > 씬 다시 만들기 · 배치: KachiGun.EditorTools.SceneBuilder.BuildAll).
    /// 씬엔 카메라와 Boot 만 있고 나머지는 실행할 때 코드가 만든다.
    /// </summary>
    public static class SceneBuilder
    {
        const string ScenePath = "Assets/Scenes/Main.unity";

        [MenuItem("카치건/씬 다시 만들기")]
        public static void BuildAll()
        {
            AssetDatabase.Refresh();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 12.24f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.16f, 0.12f, 0.09f);
            camGo.transform.position = new Vector3(0, 0, -10);
            camGo.AddComponent<AudioListener>();
            var urp = System.Type.GetType("UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime");
            if (urp != null) camGo.AddComponent(urp);

            new GameObject("Boot").AddComponent<Boot>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            AssetDatabase.SaveAssets();
            Debug.Log("[SceneBuilder] 만들었음: " + ScenePath);
        }
    }
}
