using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace UnityAgentLab.Editor
{
    public static class PrototypeBuilder
    {
        private const string GamePath = "Assets/Scenes/Game.unity";
        private const string MenuPath = "Assets/Scenes/Menu.unity";

        [MenuItem("UnityAgentLab/Build Magnet Prototype Scenes")]
        public static void Upgrade()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play before upgrading scenes.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            AssetDatabase.Refresh();
            var scene = EditorSceneManager.OpenScene(GamePath);
            if (GameObject.Find("RepairPrototype") != null || GameObject.Find("StationWorld") != null)
            {
                Debug.Log("Magnet prototype already exists; keeping current scene contents.");
                return;
            }
            Directory.CreateDirectory("Assets/Scenes/Baselines");
            if (!File.Exists("Assets/Scenes/Baselines/Game_A1A2.unity"))
                EditorSceneManager.SaveScene(scene, "Assets/Scenes/Baselines/Game_A1A2.unity", true);
            Directory.CreateDirectory("Assets/Prefabs");
            Directory.CreateDirectory("Assets/Materials/Prototype");
            Directory.CreateDirectory("Assets/Meshes");
            AssetDatabase.Refresh();

            Transform player = GameObject.Find("Player").transform;
            player.position = new Vector3(-4f, 0.65f, -6f);
            Camera camera = Camera.main;
            camera.transform.position = new Vector3(0f, 24f, -21f);
            camera.transform.LookAt(Vector3.zero);
            camera.fieldOfView = 48f;
            camera.backgroundColor = new Color(0.025f, 0.05f, 0.075f);
            Transform root = new GameObject("RepairPrototype").transform;
            Material blue = Material("BlueCore", new Color(0.05f, 0.6f, 1f));
            Material red = Material("RedCore", new Color(1f, 0.18f, 0.22f));
            Material metal = Material("Metal", new Color(0.12f, 0.19f, 0.24f));
            Material gold = Material("Target", new Color(1f, 0.7f, 0.12f));
            Material off = Material("Offline", new Color(0.85f, 0.3f, 0.12f));
            Material green = Material("Return", new Color(0.1f, 0.58f, 0.45f));
            Primitive("PartsZone", PrimitiveType.Plane, new Vector3(-5, 0.01f, 0), new Vector3(0.95f, 1, 1.9f),
                Material("PartsFloor", new Color(0.27f, 0.37f, 0.44f)), root, false);
            Primitive("RepairZone", PrimitiveType.Plane, new Vector3(5, 0.012f, 0), new Vector3(0.95f, 1, 1.9f),
                Material("RepairFloor", new Color(0.29f, 0.4f, 0.37f)), root, false);
            Material grid = Material("Grid", new Color(0.39f, 0.5f, 0.53f));
            for (int i = -8; i <= 8; i += 4)
            {
                Primitive("GridX", PrimitiveType.Cube, new Vector3(i, 0.024f, 0), new Vector3(0.025f, 0.006f, 19), grid, root, false);
                Primitive("GridZ", PrimitiveType.Cube, new Vector3(0, 0.026f, i), new Vector3(19, 0.006f, 0.025f), grid, root, false);
            }
            Transform blueShelf = Shelf("Blue", new Vector3(-6, 0, 4), blue, root, camera);
            Transform redShelf = Shelf("Red", new Vector3(-6, 0, -2), red, root, camera);
            RepairModule bluePrefab = ModulePrefab("BlueCore", ModuleKind.BlueCircle, blue);
            RepairModule redPrefab = ModulePrefab("RedCore", ModuleKind.RedTriangle, red);
            ModuleSpawner spawner = new GameObject("ModuleSpawner").AddComponent<ModuleSpawner>();
            spawner.transform.SetParent(root); spawner.Configure(bluePrefab, redPrefab, blueShelf, redShelf);

            Transform device = Primitive("RepairDevice", PrimitiveType.Cube, new Vector3(5, 0.7f, 4),
                new Vector3(2, 1.4f, 2), metal, root, true).transform;
            Transform socket = Point("InstallSocket", new Vector3(5, 1.9f, 4), root);
            Renderer deviceLamp = Primitive("DeviceLamp", PrimitiveType.Cylinder, new Vector3(5, 1.44f, 4),
                new Vector3(1.6f, 0.04f, 1.6f), off, root, false).GetComponent<Renderer>();
            Label("REPAIR / BLUE CORE", new Vector3(5, 3.1f, 4), root, camera);
            Transform returnPad = Primitive("ReturnPad", PrimitiveType.Cylinder, new Vector3(-2, 0.07f, -4),
                new Vector3(2.3f, 0.07f, 2.3f), green, root, false).transform;
            Label("RETURN", new Vector3(-2, 0.65f, -4), root, camera);
            Transform exit = Point("Exit", new Vector3(5, 0, -6), root);
            Renderer exitLamp = Primitive("ExitPad", PrimitiveType.Cylinder, new Vector3(5, 0.035f, -6),
                new Vector3(3f, 0.035f, 3f), off, root, false).GetComponent<Renderer>();
            Label("EXIT", new Vector3(5, 0.7f, -6), root, camera);
            Transform highlight = new GameObject("InteractionHighlight").transform;
            highlight.SetParent(root); highlight.gameObject.AddComponent<MeshFilter>().sharedMesh = RingMesh();
            highlight.gameObject.AddComponent<MeshRenderer>().sharedMaterial = gold;
            highlight.gameObject.SetActive(false);

            GameAudio sounds = root.gameObject.AddComponent<GameAudio>();
            string[] clipNames = { "Boost", "Pickup", "Return", "Denied", "Install", "Complete" };
            var clips = new AudioClip[clipNames.Length];
            for (int i = 0; i < clips.Length; i++)
            {
                clips[i] = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/" + clipNames[i] + ".wav");
                if (clips[i] == null) throw new InvalidOperationException("Missing audio: " + clipNames[i]);
            }
            sounds.Configure(player.GetComponent<PlayerMotor>(), clips);
            RepairGame game = root.gameObject.AddComponent<RepairGame>();
            game.Configure(player, spawner, device, socket, returnPad, exit, highlight, deviceLamp, exitLamp, sounds);
            player.gameObject.AddComponent<PlayerInteraction>().Configure(game);
            root.gameObject.AddComponent<GameHud>().Configure(game, false);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, GamePath)) throw new InvalidOperationException("Game save failed.");
            CreateMenu();
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(MenuPath, true),
                new EditorBuildSettingsScene(GamePath, true) };
            PlayerSettings.productName = "Magnet Run";
            PlayerSettings.defaultScreenWidth = 1280; PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed; PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(GamePath);
            Debug.Log("MAGNET_PROTOTYPE_CREATED: Game + Menu + 2 Prefabs + 6 audio clips");
        }

        private static void CreateMenu()
        {
            if (File.Exists(MenuPath)) return;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Camera camera = new GameObject("Main Camera").AddComponent<Camera>();
            camera.tag = "MainCamera"; camera.gameObject.AddComponent<AudioListener>();
            camera.transform.position = new Vector3(0, 8, -14); camera.transform.LookAt(Vector3.zero);
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(0.025f, 0.06f, 0.09f);
            Light light = new GameObject("Directional Light").AddComponent<Light>();
            light.type = LightType.Directional; light.transform.rotation = Quaternion.Euler(50, -30, 0);
            Primitive("MagnetBall", PrimitiveType.Sphere, new Vector3(5, 0, 0), Vector3.one * 3,
                Material("MenuBall", new Color(0.07f, 0.68f, 0.74f)), null, false);
            new GameObject("MenuUI").AddComponent<GameHud>().Configure(null, true);
            if (!EditorSceneManager.SaveScene(scene, MenuPath)) throw new InvalidOperationException("Menu save failed.");
        }

        [MenuItem("UnityAgentLab/Open Menu Scene")]
        public static void OpenMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(MenuPath);
        }

        [MenuItem("UnityAgentLab/Build Windows Prototype")]
        public static void BuildWindows()
        {
            if (File.Exists("Assets/Prefabs/Station/IonCylinder.prefab")) { StationBuilder.BuildWindows(); return; }
            string folder = Path.GetFullPath("../Builds/Windows/MagnetRun");
            Directory.CreateDirectory(folder);
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { MenuPath, GamePath }, target = BuildTarget.StandaloneWindows64,
                locationPathName = Path.Combine(folder, "MagnetRun.exe"), options = BuildOptions.None });
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("Windows build failed: " + report.summary.result);
            Debug.Log("MAGNET_WINDOWS_BUILD_PASSED: " + folder);
        }

        private static Material Material(string name, Color color)
        {
            string path = "Assets/Materials/Prototype/" + name + ".mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name, color = color };
            material.SetFloat("_Smoothness", 0.25f);
            AssetDatabase.CreateAsset(material, path); return material;
        }
        private static GameObject Primitive(string name, PrimitiveType type, Vector3 position, Vector3 scale,
            Material material, Transform parent, bool solid)
        {
            GameObject obj = GameObject.CreatePrimitive(type); obj.name = name;
            obj.transform.SetParent(parent); obj.transform.position = position; obj.transform.localScale = scale;
            obj.GetComponent<Renderer>().sharedMaterial = material;
            if (!solid) UnityEngine.Object.DestroyImmediate(obj.GetComponent<Collider>());
            return obj;
        }
        private static Transform Point(string name, Vector3 position, Transform parent)
        {
            var point = new GameObject(name).transform; point.SetParent(parent); point.position = position; return point;
        }
        private static Transform Shelf(string name, Vector3 position, Material material, Transform root, Camera camera)
        {
            Primitive(name + "Shelf", PrimitiveType.Cube, position + Vector3.up * 0.25f,
                new Vector3(1.5f, 0.5f, 1.5f), material, root, true);
            Label(name == "Blue" ? "BLUE / CIRCLE" : "RED / TRIANGLE", position + Vector3.up * 2.7f, root, camera);
            return Point(name + "Spawn", position + Vector3.up * 1.2f, root);
        }
        private static void Label(string text, Vector3 position, Transform root, Camera camera)
        {
            GameObject obj = new GameObject(text); obj.transform.SetParent(root);
            obj.transform.SetPositionAndRotation(position, camera.transform.rotation);
            TextMesh label = obj.AddComponent<TextMesh>(); label.text = text;
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); label.fontSize = 48;
            label.characterSize = 0.08f; label.anchor = TextAnchor.MiddleCenter; label.color = Color.white;
            obj.GetComponent<MeshRenderer>().sharedMaterial = label.font.material;
        }
        private static RepairModule ModulePrefab(string name, ModuleKind kind, Material material)
        {
            string path = "Assets/Prefabs/" + name + ".prefab";
            var obj = new GameObject(name);
            RepairModule module = obj.AddComponent<RepairModule>(); module.Configure(kind);
            SphereCollider trigger = obj.AddComponent<SphereCollider>(); trigger.isTrigger = true; trigger.radius = 0.65f;
            if (kind == ModuleKind.BlueCircle)
            {
                GameObject disk = Primitive("Circle", PrimitiveType.Cylinder, Vector3.zero,
                    new Vector3(1f, 0.14f, 1f), material, obj.transform, false);
                disk.transform.localRotation = Quaternion.Euler(90, 0, 0);
            }
            else
            {
                var triangle = new GameObject("Triangle"); triangle.transform.SetParent(obj.transform);
                triangle.AddComponent<MeshFilter>().sharedMesh = TriangleMesh();
                triangle.AddComponent<MeshRenderer>().sharedMaterial = material;
            }
            var prefab = PrefabUtility.SaveAsPrefabAsset(obj, path);
            UnityEngine.Object.DestroyImmediate(obj); return prefab.GetComponent<RepairModule>();
        }
        private static Mesh TriangleMesh()
        {
            string path = "Assets/Meshes/Triangle.asset";
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path); if (mesh != null) return mesh;
            mesh = new Mesh { name = "TriangleCore", vertices = new[] {
                new Vector3(-0.6f,-0.5f,-0.14f), new Vector3(0.6f,-0.5f,-0.14f), new Vector3(0,0.6f,-0.14f),
                new Vector3(-0.6f,-0.5f,0.14f), new Vector3(0.6f,-0.5f,0.14f), new Vector3(0,0.6f,0.14f) },
                triangles = new[] { 0,2,1,3,4,5,0,1,4,0,4,3,1,2,5,1,5,4,2,0,3,2,3,5 } };
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); AssetDatabase.CreateAsset(mesh, path); return mesh;
        }
        private static Mesh RingMesh()
        {
            string path = "Assets/Meshes/TargetRing.asset";
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path); if (mesh != null) return mesh;
            const int count = 48; var vertices = new Vector3[count * 2]; var triangles = new int[count * 6];
            for (int i = 0; i < count; i++)
            {
                float angle = i * Mathf.PI * 2 / count; Vector3 direction = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                vertices[i * 2] = direction * 1.25f; vertices[i * 2 + 1] = direction * 1.12f;
                int next = (i + 1) % count * 2, offset = i * 6;
                triangles[offset] = i * 2; triangles[offset + 1] = i * 2 + 1; triangles[offset + 2] = next;
                triangles[offset + 3] = next; triangles[offset + 4] = i * 2 + 1; triangles[offset + 5] = next + 1;
            }
            mesh = new Mesh { name = "InteractionRing", vertices = vertices, triangles = triangles };
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); AssetDatabase.CreateAsset(mesh, path); return mesh;
        }
    }
}
