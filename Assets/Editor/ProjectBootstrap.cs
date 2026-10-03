using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace UnityAgentLab.Editor
{
    /// <summary>Creates a new scene through Unity APIs; never rewrites scene YAML.</summary>
    public static class ProjectBootstrap
    {
        private const string ScenePath = "Assets/Scenes/Game.unity";

        [MenuItem("UnityAgentLab/Create A1-A2 Scene")]
        public static void CreateScene()
        {
            if (File.Exists(ScenePath))
            {
                Debug.Log("A1-A2 scene already exists; keeping its contents.");
                return;
            }

            Directory.CreateDirectory("Assets/Scenes");
            Directory.CreateDirectory("Assets/Materials");
            AssetDatabase.Refresh();
            var scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene, NewSceneMode.Single);

            Material floorMaterial = CreateMaterial("Ground", new Color(0.58f, 0.62f, 0.66f));
            Material wallMaterial = CreateMaterial("Wall", new Color(0.26f, 0.32f, 0.38f));
            Material playerMaterial = CreateMaterial("Player", new Color(0.07f, 0.68f, 0.74f));

            CreatePrimitive("Ground", PrimitiveType.Plane, Vector3.zero,
                new Vector3(2f, 1f, 2f), floorMaterial);
            CreatePrimitive("Wall_North", PrimitiveType.Cube, new Vector3(0f, 1f, 10f),
                new Vector3(20.5f, 2f, 0.5f), wallMaterial);
            CreatePrimitive("Wall_South", PrimitiveType.Cube, new Vector3(0f, 1f, -10f),
                new Vector3(20.5f, 2f, 0.5f), wallMaterial);
            CreatePrimitive("Wall_East", PrimitiveType.Cube, new Vector3(10f, 1f, 0f),
                new Vector3(0.5f, 2f, 20.5f), wallMaterial);
            CreatePrimitive("Wall_West", PrimitiveType.Cube, new Vector3(-10f, 1f, 0f),
                new Vector3(0.5f, 2f, 20.5f), wallMaterial);

            GameObject player = CreatePrimitive("Player", PrimitiveType.Sphere,
                new Vector3(0f, 0.65f, 0f), Vector3.one, playerMaterial);
            Rigidbody body = player.AddComponent<Rigidbody>();
            body.mass = 1f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            player.AddComponent<PlayerMotor>();
            player.AddComponent<HumanInput>();

            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
            cameraObject.transform.position = new Vector3(0f, 16f, -14f);
            cameraObject.transform.LookAt(Vector3.zero);
            camera.fieldOfView = 60f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.1f, 0.13f, 0.17f);
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;

            Light light = new GameObject("Directional Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.5f;
            light.shadows = LightShadows.Soft;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.65f, 0.7f, 0.75f);
            RenderSettings.ambientEquatorColor = new Color(0.4f, 0.45f, 0.5f);
            RenderSettings.ambientGroundColor = new Color(0.25f, 0.27f, 0.3f);
            RenderSettings.sun = light;

            PlayerSettings.productName = "UnityAgentLab";
            PlayerSettings.companyName = "UnityAgentLab";
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException("Failed to save the A1-A2 scene.");
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("UNITY_AGENT_LAB_SCENE_CREATED: " + ScenePath);
        }

        [MenuItem("UnityAgentLab/Validate A1-A2 Physics")]
        public static void ValidatePhysics()
        {
            ValidatePhysicsAt(ScenePath);
        }

        public static void ValidatePhysicsAt(string validationScene)
        {
            EditorSceneManager.OpenScene(validationScene);
            GameObject player = GameObject.Find("Player");
            Require(player != null, "Player exists");
            Rigidbody body = player.GetComponent<Rigidbody>();
            PlayerMotor motor = player.GetComponent<PlayerMotor>();
            Require(body != null && motor != null, "Player has Rigidbody and PlayerMotor");
            Require(player.GetComponent<HumanInput>() != null, "HumanInput is attached");
            Camera camera = Camera.main;
            Require(camera != null && camera.transform.parent == null,
                "Camera is independent of the rolling player");

            var checks = new List<string>();
            SimulationMode previousMode = Physics.simulationMode;
            Vector3 originalPosition = body.position;
            Quaternion originalRotation = body.rotation;
            Vector3 cameraPosition = camera.transform.position;
            Quaternion cameraRotation = camera.transform.rotation;
            try
            {
                Physics.simulationMode = SimulationMode.Script;
                Reset(body, motor, new Vector3(0f, 2f, 0f));
                Simulate(motor, Vector2.zero, 150);
                Require(Mathf.Abs(body.position.y - 0.5f) < 0.08f, "Sphere lands on the ground");
                checks.Add("gravity_and_ground_collision");

                Vector2[] directions = { Vector2.up, Vector2.down, Vector2.right, Vector2.left };
                string[] names = { "north", "south", "east", "west" };
                for (int i = 0; i < directions.Length; i++)
                {
                    Reset(body, motor, new Vector3(0f, 0.6f, 0f));
                    Simulate(motor, directions[i], 300);
                    Vector3 position = body.position;
                    float progress = position.x * directions[i].x + position.z * directions[i].y;
                    Require(progress > 8.8f && progress < 9.4f,
                        "Player moves to and is stopped by the " + names[i] + " wall");
                    Require(position.y > 0.42f, "Player stays above the floor at the wall");
                    checks.Add("movement_and_collision_" + names[i]);
                }

                Reset(body, motor, new Vector3(0f, 0.6f, 0f));
                body.rotation = Quaternion.Euler(80f, 135f, 25f);
                Simulate(motor, Vector2.up, 60);
                Require(body.position.z > 3f && Mathf.Abs(body.position.x) < 0.1f,
                    "World movement direction is independent of sphere rotation");
                checks.Add("world_direction_independent_of_rotation");
                Simulate(motor, Vector2.zero, 100);
                Vector3 velocity = body.linearVelocity;
                Require(new Vector2(velocity.x, velocity.z).magnitude < 0.05f,
                    "Releasing movement input brings the sphere to a stop");
                checks.Add("release_stops_movement");

                Require(Vector3.Distance(cameraPosition, camera.transform.position) < 0.001f
                    && Quaternion.Angle(cameraRotation, camera.transform.rotation) < 0.001f,
                    "Physics simulation does not move or roll the camera");
                checks.Add("stable_camera");
                Directory.CreateDirectory("docs");
                File.WriteAllText("docs/physics-validation.json", JsonUtility.ToJson(
                    new ValidationReport { unityVersion = Application.unityVersion,
                        checks = checks.ToArray(), result = "passed",
                        scope = "Editor manual physics simulation; keyboard Play and visual review pending" }, true));
                Debug.Log("UNITY_AGENT_LAB_PHYSICS_PASSED: " + checks.Count + " checks");
            }
            finally
            {
                Reset(body, motor, originalPosition);
                body.rotation = originalRotation;
                Physics.simulationMode = previousMode;
                // Reopen the saved scene so validation never persists a moved player.
                EditorSceneManager.OpenScene(ScenePath);
            }
        }

        private static Material CreateMaterial(string name, Color color)
        {
            string path = "Assets/Materials/A1" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            Require(shader != null, "URP Lit shader is available");
            material = new Material(shader) { name = "A1" + name };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", 0.2f);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static GameObject CreatePrimitive(string name, PrimitiveType type,
            Vector3 position, Vector3 scale, Material material)
        {
            GameObject obj = GameObject.CreatePrimitive(type);
            obj.name = name;
            obj.transform.position = position;
            obj.transform.localScale = scale;
            obj.GetComponent<Renderer>().sharedMaterial = material;
            return obj;
        }

        private static void Reset(Rigidbody body, PlayerMotor motor, Vector3 position)
        {
            motor.SetMoveInput(Vector2.zero);
            body.position = position;
            body.rotation = Quaternion.identity;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.WakeUp();
            Physics.SyncTransforms();
        }

        private static void Simulate(PlayerMotor motor, Vector2 input, int steps)
        {
            const float deltaTime = 0.02f;
            motor.SetMoveInput(input);
            for (int i = 0; i < steps; i++)
            {
                motor.Step(deltaTime);
                Physics.Simulate(deltaTime);
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException("A1-A2 validation failed: " + message);
        }

        [Serializable]
        private sealed class ValidationReport
        {
            public string unityVersion;
            public string[] checks;
            public string result;
            public string scope;
        }
    }
}
