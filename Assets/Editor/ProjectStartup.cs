using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnityAgentLab.Editor
{
    [InitializeOnLoad]
    public static class ProjectStartup
    {
        private const string ScenePath = "Assets/Scenes/Game.unity";
        private const string StartupChecked = "UnityAgentLab.StartupSceneChecked";

        static ProjectStartup()
        {
            if (!Application.isBatchMode)
                EditorApplication.delayCall += OpenInitialScene;
        }

        private static void OpenInitialScene()
        {
            if (SessionState.GetBool(StartupChecked, false)
                || EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += OpenInitialScene;
                return;
            }

            SessionState.SetBool(StartupChecked, true);
            Scene activeScene = SceneManager.GetActiveScene();
            // Only replace a fresh unnamed scene. Saved scenes and unsaved work stay open.
            if (SceneManager.sceneCount == 1 && !activeScene.isDirty
                && string.IsNullOrEmpty(activeScene.path) && File.Exists(ScenePath))
                OpenGameScene();
        }

        [MenuItem("UnityAgentLab/Open Game Scene")]
        public static void OpenGameScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(ScenePath))
                return;

            if (!Application.isBatchMode
                && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            Scene scene = EditorSceneManager.OpenScene(ScenePath);
            GameObject player = GameObject.Find("Player");
            if (player == null)
                throw new System.InvalidOperationException("Game scene is missing Player.");

            Selection.activeGameObject = player;
            if (!Application.isBatchMode)
                EditorApplication.delayCall += FramePlayer;

            Debug.Log("UNITY_AGENT_LAB_GAME_OPENED: " + scene.path
                + "; root objects=" + scene.rootCount + "; selected=Player");
        }

        [MenuItem("UnityAgentLab/Open Game Scene", true)]
        private static bool CanOpenGameScene()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode && File.Exists(ScenePath);
        }

        private static void FramePlayer()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            SceneView view = SceneView.lastActiveSceneView ?? EditorWindow.GetWindow<SceneView>();
            view.FrameSelected();
            view.Focus();
        }
    }
}
