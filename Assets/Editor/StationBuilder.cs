using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace UnityAgentLab.Editor
{
    /// <summary>Builds the themed station through Unity APIs; preserves the previous playable.</summary>
    public static partial class StationBuilder
    {
        private const string GamePath = "Assets/Scenes/Game.unity";
        private const string MenuPath = "Assets/Scenes/Menu.unity";
        private static Material shell, dark, metal, blue, orange, teal, white, lamp;

        [MenuItem("UnityAgentLab/Upgrade to Station Rescue")]
        public static void Upgrade()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play before upgrading.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            AssetDatabase.Refresh();
            var scene = EditorSceneManager.OpenScene(GamePath);
            if (GameObject.Find("StationWorld") != null)
            { Debug.Log("STATION_ALREADY_CREATED: keeping current scene"); return; }
            Directory.CreateDirectory("Assets/Scenes/Baselines");
            if (!File.Exists("Assets/Scenes/Baselines/Game_MagnetPrototype.unity"))
                EditorSceneManager.SaveScene(scene, "Assets/Scenes/Baselines/Game_MagnetPrototype.unity", true);
            Directory.CreateDirectory("Assets/Materials/Station");
            Directory.CreateDirectory("Assets/Prefabs/Station");
            AssetDatabase.Refresh();
            Palette();
            GameObject old = GameObject.Find("RepairPrototype");
            if (old != null) UnityEngine.Object.DestroyImmediate(old);
            Transform root = Group("StationWorld", null, Vector3.zero);
            RepairGame game = root.gameObject.AddComponent<RepairGame>();
            Transform player = GameObject.Find("Player").transform;
            player.position = new Vector3(-0.5f, 0.65f, -7.5f);
            player.GetComponent<Renderer>().sharedMaterial = shell;
            Robot(player, game, root);
            Camera camera = Camera.main;
            camera.transform.position = new Vector3(0, 24, -19.5f);
            camera.transform.LookAt(new Vector3(0, 0.2f, 0));
            camera.fieldOfView = 46;
            camera.backgroundColor = new Color(0.014f, 0.027f, 0.065f);
            Light sun = GameObject.Find("Directional Light").GetComponent<Light>();
            sun.transform.rotation = Quaternion.Euler(53, -35, 0);
            sun.intensity = 1.65f;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.52f, 0.64f, 0.75f);
            RenderSettings.ambientEquatorColor = new Color(0.31f, 0.4f, 0.51f);
            RenderSettings.ambientGroundColor = new Color(0.16f, 0.22f, 0.3f);
            foreach (string side in new[] { "North", "South", "East", "West" })
                GameObject.Find("Wall_" + side).GetComponent<Renderer>().sharedMaterial = dark;
            Floor(root);
            Vector3[] points = { new Vector3(-6, 0, 1.5f), new Vector3(-6, 0, -3),
                new Vector3(-3, 0, 4), new Vector3(2, 0, 6.5f) };
            var slots = new Transform[points.Length];
            for (int i = 0; i < points.Length; i++) slots[i] = ChargingRack(points[i], i + 1, root);
            RepairModule round = CellPrefab(ModuleKind.BlueCircle);
            RepairModule square = CellPrefab(ModuleKind.RedTriangle);
            ModuleSpawner spawner = Group("ModuleSpawner", root, Vector3.zero).gameObject.AddComponent<ModuleSpawner>();
            spawner.Configure(round, square, slots[0], slots[1]);
            Transform device = Generator(new Vector3(5, 0, 3.8f), root, out PowerStationVisual power);
            Transform socket = Group("InstalledCellSocket", root, new Vector3(5, 1.25f, 2.45f));
            Transform reclaim = Reclaim(new Vector3(-3, 0, -1.4f), root);
            DockGate[] gates = {
                Dock("A · 北侧港", new Vector3(-6, 0, 5.9f), 0, root),
                Dock("B · 东侧港", new Vector3(5.8f, 0, -6.3f), 90, root),
                Dock("C · 西侧港", new Vector3(-4.9f, 0, -7), -90, root) };
            Transform highlight = Group("InteractionHighlight", root, Vector3.zero);
            highlight.gameObject.AddComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Meshes/TargetRing.asset");
            highlight.gameObject.AddComponent<MeshRenderer>().sharedMaterial = Mat("TargetGold", new Color(1, 0.78f, 0.2f), 0.3f);
            highlight.gameObject.SetActive(false);
            GameAudio sounds = root.gameObject.AddComponent<GameAudio>();
            string[] names = { "Boost", "Pickup", "Return", "Denied", "Install", "Complete" };
            var clips = new AudioClip[names.Length];
            for (int i = 0; i < clips.Length; i++)
                clips[i] = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/" + names[i] + ".wav");
            sounds.Configure(player.GetComponent<PlayerMotor>(), clips);
            game.Configure(player, spawner, device, socket, reclaim, gates[0].transform, highlight, null, null, sounds);
            ArcStrip[] strips = { Hazard(game, new Vector3(0, 0, 1.5f), new Vector2(1.8f, 6), 0, root),
                Hazard(game, new Vector3(1.7f, 0, -3.5f), new Vector2(4.3f, 1.35f), 2.25f, root) };
            game.ConfigureStation(gates, slots, strips, power);
            PlayerInteraction interaction = player.GetComponent<PlayerInteraction>();
            if (interaction == null) interaction = player.gameObject.AddComponent<PlayerInteraction>();
            interaction.Configure(game);
            root.gameObject.AddComponent<GameHud>().Configure(game, false);
            Crates(new Vector3(2.7f, 0, 0.25f), root);
            Save(scene, GamePath);
            Menu();
            PlayerSettings.productName = "Station Rescue";
            PlayerSettings.defaultScreenWidth = 1280; PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true; PlayerSettings.runInBackground = true;
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(MenuPath, true),
                new EditorBuildSettingsScene(GamePath, true) };
            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(GamePath);
            Debug.Log("STATION_SCENES_CREATED: robot, two cell models, generator, reclaim cabinet, three docks and two timed shortcuts");
        }

        private static void Palette()
        {
            shell = Mat("RobotShell", new Color(0.85f, 0.91f, 0.96f));
            dark = Mat("DeepMetal", new Color(0.08f, 0.14f, 0.21f));
            metal = Mat("Steel", new Color(0.43f, 0.55f, 0.63f));
            blue = Mat("IonBlue", new Color(0.08f, 0.55f, 1), 0.18f);
            orange = Mat("AmberCell", new Color(1, 0.48f, 0.09f), 0.14f);
            teal = Mat("ReclaimTeal", new Color(0.06f, 0.62f, 0.5f), 0.13f);
            white = Mat("MarkingWhite", new Color(0.82f, 0.91f, 0.93f), 0.1f);
            lamp = Mat("Indicator", new Color(0.95f, 0.28f, 0.08f), 0.45f);
        }
        private static Material Mat(string name, Color color, float emission = 0)
        {
            string path = "Assets/Materials/Station/" + name + ".mat";
            Material result = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (result != null) return result;
            result = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            result.SetColor("_BaseColor", color); result.SetFloat("_Smoothness", 0.3f);
            if (emission > 0) { result.EnableKeyword("_EMISSION"); result.SetColor("_EmissionColor", color * emission); }
            AssetDatabase.CreateAsset(result, path); return result;
        }
        private static Transform Group(string name, Transform parent, Vector3 position, float yaw = 0)
        {
            var obj = new GameObject(name).transform; obj.SetParent(parent, false);
            obj.localPosition = position; obj.localRotation = Quaternion.Euler(0, yaw, 0); return obj;
        }
        private static GameObject Shape(string name, PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale,
            Material material, bool solid = false)
        {
            GameObject obj = GameObject.CreatePrimitive(type); obj.name = name; obj.transform.SetParent(parent, false);
            obj.transform.localPosition = pos; obj.transform.localScale = scale;
            obj.GetComponent<Renderer>().sharedMaterial = material;
            if (!solid) UnityEngine.Object.DestroyImmediate(obj.GetComponent<Collider>());
            return obj;
        }
        private static void Label(string text, Transform parent, Vector3 position, float size = 0.11f)
        {
            Transform obj = Group(text, parent, position);
            obj.rotation = Camera.main.transform.rotation;
            TextMesh label = obj.gameObject.AddComponent<TextMesh>(); label.text = text;
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 48; label.characterSize = size; label.anchor = TextAnchor.MiddleCenter;
            label.color = Color.white; obj.gameObject.GetComponent<MeshRenderer>().sharedMaterial = label.font.material;
        }
        private static void Floor(Transform root)
        {
            GameObject.Find("Ground").GetComponent<Renderer>().sharedMaterial = dark;
            Material deck = Mat("Deck", new Color(0.22f, 0.32f, 0.39f));
            Material deckOther = Mat("DeckAlternate", new Color(0.25f, 0.35f, 0.42f));
            for (int x = -3; x <= 3; x++) for (int z = -3; z <= 3; z++)
                Shape("DeckTile", PrimitiveType.Cube, root, new Vector3(x * 2.72f, 0.012f, z * 2.72f),
                    new Vector3(2.65f, 0.023f, 2.65f), (x + z) % 2 == 0 ? deck : deckOther);
            for (int z = -8; z <= 8; z += 2)
                Shape("ServiceLane", PrimitiveType.Cube, root, new Vector3(-1.35f, 0.035f, z),
                    new Vector3(0.07f, 0.015f, 0.8f), white);
            for (int i = 0; i < 65; i++)
            {
                var random = new System.Random(103 + i * 47);
                float x = (float)random.NextDouble() * 60 - 30;
                float z = (float)random.NextDouble() * 65 - 15;
                if (Mathf.Abs(x) < 11 && z < 12) x += x >= 0 ? 18 : -18;
                Shape("DistantStar", PrimitiveType.Sphere, root, new Vector3(x, -3, z),
                    Vector3.one * (0.05f + (i % 3) * 0.02f), white);
            }
            Label("N-17 / EMERGENCY DOCK", root, new Vector3(3.6f, 0.12f, 8.2f), 0.09f);
        }

        private static Transform ChargingRack(Vector3 position, int number, Transform root)
        {
            Transform rack = Group("ChargingRack_" + number, root, position);
            Shape("CrateBody", PrimitiveType.Cube, rack, new Vector3(0, 0.38f, 0),
                new Vector3(1.5f, 0.75f, 1.4f), metal, true);
            Shape("ChargingTop", PrimitiveType.Cube, rack, new Vector3(0, 0.79f, 0),
                new Vector3(1.6f, 0.13f, 1.5f), dark);
            foreach (float x in new[] { -0.69f, 0.69f })
                Shape("CrateCorner", PrimitiveType.Cube, rack, new Vector3(x, 0.43f, -0.67f),
                    new Vector3(0.18f, 0.85f, 0.18f), shell);
            Shape("ChargeLight", PrimitiveType.Cube, rack, new Vector3(0, 0.45f, -0.715f),
                new Vector3(0.88f, 0.13f, 0.04f), teal);
            Label("CHG " + number, rack, new Vector3(0, 0.12f, -1.05f), 0.065f);
            return Group("CellHome", rack, new Vector3(0, 1.5f, 0));
        }
        private static RepairModule CellPrefab(ModuleKind kind)
        {
            string file = kind == ModuleKind.BlueCircle ? "IonCylinder" : "AmberBox";
            Transform root = Group(file, null, Vector3.zero);
            RepairModule module = root.gameObject.AddComponent<RepairModule>(); module.Configure(kind);
            Collider trigger;
            if (kind == ModuleKind.BlueCircle)
            {
                CapsuleCollider capsule = root.gameObject.AddComponent<CapsuleCollider>(); capsule.radius = 0.52f; capsule.height = 1.4f;
                trigger = capsule;
            }
            else { BoxCollider box = root.gameObject.AddComponent<BoxCollider>(); box.size = new Vector3(1.1f, 1.4f, 1); trigger = box; }
            trigger.isTrigger = true;
            Rigidbody cellBody = root.gameObject.AddComponent<Rigidbody>(); cellBody.isKinematic = true;
            cellBody.mass = 0.5f; cellBody.linearDamping = 3; cellBody.angularDamping = 4;
            cellBody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            cellBody.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            CellVisual(kind, root);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root.gameObject, "Assets/Prefabs/Station/" + file + ".prefab");
            UnityEngine.Object.DestroyImmediate(root.gameObject);
            return prefab.GetComponent<RepairModule>();
        }
        private static void CellVisual(ModuleKind kind, Transform root)
        {
            bool round = kind == ModuleKind.BlueCircle;
            if (round)
            {
                Shape("CylinderCasing", PrimitiveType.Cylinder, root, Vector3.zero, new Vector3(0.95f, 0.58f, 0.95f), dark);
                Shape("IonCharge", PrimitiveType.Cylinder, root, Vector3.zero, new Vector3(0.98f, 0.36f, 0.98f), blue);
                foreach (float y in new[] { -0.57f, 0.57f })
                    Shape("MetalEndCap", PrimitiveType.Cylinder, root, new Vector3(0, y, 0),
                        new Vector3(1.05f, 0.09f, 1.05f), shell);
                Shape("PositiveTerminal", PrimitiveType.Cylinder, root, new Vector3(0, 0.72f, 0),
                    new Vector3(0.4f, 0.07f, 0.4f), metal);
            }
            else
            {
                Shape("BoxCasing", PrimitiveType.Cube, root, Vector3.zero, new Vector3(1.03f, 1.2f, 0.86f), dark);
                Shape("AmberFace", PrimitiveType.Cube, root, new Vector3(0, 0, -0.45f),
                    new Vector3(0.94f, 0.9f, 0.1f), orange);
                Shape("AmberBack", PrimitiveType.Cube, root, new Vector3(0, 0, 0.45f),
                    new Vector3(0.94f, 0.9f, 0.1f), orange);
                foreach (float y in new[] { -0.56f, 0.56f })
                    Shape("BoxCap", PrimitiveType.Cube, root, new Vector3(0, y, 0),
                        new Vector3(1.13f, 0.18f, 0.96f), shell);
                foreach (float x in new[] { -0.26f, 0.26f })
                    Shape("Terminal", PrimitiveType.Cylinder, root, new Vector3(x, 0.72f, 0),
                        new Vector3(0.2f, 0.09f, 0.2f), metal);
            }
            Shape("PlusHorizontal", PrimitiveType.Cube, root, new Vector3(0, 0, -0.52f),
                new Vector3(0.45f, 0.09f, 0.03f), white);
            Shape("PlusVertical", PrimitiveType.Cube, root, new Vector3(0, 0, -0.535f),
                new Vector3(0.09f, 0.4f, 0.03f), white);
            for (int i = 0; i < 3; i++) Shape("ChargeSegment", PrimitiveType.Cube, root,
                new Vector3((i - 1) * 0.25f, -0.32f, -0.535f), new Vector3(0.17f, 0.1f, 0.04f), white);
        }
        private static void Robot(Transform player, RepairGame game, Transform parent)
        {
            Shape("RollingBand", PrimitiveType.Cylinder, player, Vector3.zero, new Vector3(1.014f, 0.17f, 1.014f), dark)
                .transform.localRotation = Quaternion.Euler(90, 0, 0);
            Transform head = Group("M07_StableHead", parent, player.position);
            Shape("RobotHead", PrimitiveType.Sphere, head, new Vector3(0, 0.48f, 0),
                new Vector3(0.86f, 0.62f, 0.83f), shell);
            Shape("Visor", PrimitiveType.Cube, head, new Vector3(0, 0.48f, -0.38f),
                new Vector3(0.63f, 0.26f, 0.15f), dark);
            for (int i = -1; i <= 1; i += 2)
                Shape("Eye", PrimitiveType.Sphere, head, new Vector3(i * 0.16f, 0.51f, -0.47f),
                    new Vector3(0.12f, 0.15f, 0.06f), teal);
            Shape("Antenna", PrimitiveType.Cylinder, head, new Vector3(0.3f, 0.87f, 0),
                new Vector3(0.05f, 0.16f, 0.05f), metal);
            Shape("AntennaLamp", PrimitiveType.Sphere, head, new Vector3(0.3f, 1.04f, 0),
                Vector3.one * 0.13f, orange);
            Transform coil = Group("MagnetArms", head, Vector3.zero);
            for (int i = -1; i <= 1; i += 2)
            {
                Shape("MagnetBracket", PrimitiveType.Cube, coil, new Vector3(i * 0.51f, 0.5f, 0.14f),
                    new Vector3(0.17f, 0.7f, 0.23f), metal);
                Shape("MagnetTip", PrimitiveType.Cube, coil, new Vector3(i * 0.43f, 0.9f, 0.14f),
                    new Vector3(0.33f, 0.18f, 0.28f), i < 0 ? blue : orange);
            }
            head.gameObject.AddComponent<RobotVisual>().Configure(player, coil, game);
        }
        private static Transform Generator(Vector3 position, Transform root, out PowerStationVisual power)
        {
            Transform device = Group("EmergencyGenerator", root, position);
            Shape("GeneratorFooting", PrimitiveType.Cube, device, new Vector3(0, 0.1f, 0),
                new Vector3(3.1f, 0.18f, 2.7f), dark);
            Kenney("machine_generator", device, new Vector3(0, 0.15f, 0), 3.1f);
            BoxCollider blocker = device.gameObject.AddComponent<BoxCollider>();
            blocker.center = new Vector3(0, 0.9f, 0); blocker.size = new Vector3(2.7f, 1.8f, 2.2f);
            Transform panel = Group("RequestDisplay", device, new Vector3(0, 2.1f, 0));
            Shape("DisplayBack", PrimitiveType.Cube, panel, Vector3.zero, new Vector3(1.9f, 0.22f, 1.6f), dark);
            GameObject round = Shape("RoundSocket", PrimitiveType.Cylinder, panel, new Vector3(0, 0.15f, 0),
                new Vector3(1.1f, 0.06f, 1.1f), blue);
            GameObject square = Shape("SquareSocket", PrimitiveType.Cube, panel, new Vector3(0, 0.15f, 0),
                new Vector3(1.1f, 0.12f, 1.1f), orange);
            Renderer indicator = Shape("PowerStatus", PrimitiveType.Cube, device, new Vector3(0, 0.75f, -1.16f),
                new Vector3(1.4f, 0.2f, 0.09f), lamp).GetComponent<Renderer>();
            Transform turbine = Group("RunningTurbine", device, new Vector3(0.85f, 1.3f, 0.35f));
            Shape("RotorOne", PrimitiveType.Cube, turbine, Vector3.zero, new Vector3(0.9f, 0.12f, 0.14f), shell);
            Shape("RotorTwo", PrimitiveType.Cube, turbine, Vector3.zero, new Vector3(0.14f, 0.12f, 0.9f), shell);
            power = device.gameObject.AddComponent<PowerStationVisual>();
            Renderer halo = Shape("GeneratorPowerHalo", PrimitiveType.Cylinder, panel, new Vector3(0, 0.08f, 0),
                new Vector3(1.58f, 0.035f, 1.58f), Mat("GeneratorHaloV2", new Color(.06f, 1, .48f), 4)).GetComponent<Renderer>();
            Light glow = Group("GeneratorGlow", device, new Vector3(0, 2.2f, 0)).gameObject.AddComponent<Light>();
            glow.type = LightType.Point; glow.color = new Color(.08f, 1, .55f); glow.range = 4; glow.intensity = 0;
            power.Configure(round, square, new[] { indicator, halo }, turbine); power.ConfigureGlow(glow);
            return device;
        }
        private static Transform Reclaim(Vector3 position, Transform root)
        {
            Transform obj = Group("CellReclaimCabinet", root, position);
            Shape("Cabinet", PrimitiveType.Cube, obj, new Vector3(0, 0.83f, 0), new Vector3(1.7f, 1.66f, 1.4f), shell, true);
            Shape("ReturnChute", PrimitiveType.Cube, obj, new Vector3(0, 0.76f, -0.715f),
                new Vector3(1.23f, 0.64f, 0.06f), dark);
            Shape("ConveyorLip", PrimitiveType.Cube, obj, new Vector3(0, 0.43f, -0.87f),
                new Vector3(1.32f, 0.18f, 0.45f), teal);
            Shape("CabinetTop", PrimitiveType.Cube, obj, new Vector3(0, 1.74f, 0),
                new Vector3(1.86f, 0.18f, 1.52f), teal);
            Transform loop = Group("RecycleSymbol", obj, new Vector3(0, 1.87f, 0));
            for (int i = 0; i < 12; i++)
            {
                float a = i * Mathf.PI / 6;
                GameObject segment = Shape("RecycleArrow", PrimitiveType.Cube, loop,
                    new Vector3(Mathf.Cos(a) * 0.43f, 0, Mathf.Sin(a) * 0.43f),
                    new Vector3(0.24f, 0.045f, 0.1f), white);
                segment.transform.localRotation = Quaternion.Euler(0, -i * 30 + 90, 0);
            }
            Shape("ArrowHead", PrimitiveType.Cube, loop, new Vector3(0.42f, 0.01f, 0.18f),
                new Vector3(0.23f, 0.08f, 0.22f), white).transform.localRotation = Quaternion.Euler(0, 45, 0);
            return obj;
        }
        private static DockGate Dock(string name, Vector3 position, float yaw, Transform root)
        {
            Transform obj = Group("Dock_" + name[0], root, position, yaw);
            Shape("LandingPad", PrimitiveType.Cube, obj, new Vector3(0, 0.03f, 1.45f),
                new Vector3(3.4f, 0.05f, 3.5f), metal, true);
            // Open airlocks lead to a walkable platform; its outer edges remain solid until boarding.
            foreach (float x in new[] { -1.62f, 1.62f })
                Shape("PlatformSideRail", PrimitiveType.Cube, obj, new Vector3(x, .5f, 1.45f),
                    new Vector3(.16f, .8f, 3.5f), orange, true);
            Shape("PlatformEndRail", PrimitiveType.Cube, obj, new Vector3(0, .5f, 3.12f),
                new Vector3(3.4f, .8f, .16f), orange, true);
            Kenney("craft_cargoA", obj, new Vector3(0, 0.1f, 2), 3.15f, 180);
            foreach (float x in new[] { -1.35f, 1.35f })
                Shape("DoorPost", PrimitiveType.Cube, obj, new Vector3(x, 1.15f, 0),
                    new Vector3(0.28f, 2.3f, 0.4f), shell, true);
            Shape("DockHeader", PrimitiveType.Cube, obj, new Vector3(0, 2.3f, 0),
                new Vector3(3f, 0.25f, 0.45f), dark);
            Transform left = Shape("LeftHatch", PrimitiveType.Cube, obj, new Vector3(-0.48f, 1.05f, 0),
                new Vector3(0.94f, 2.1f, 0.18f), metal, true).transform;
            Transform right = Shape("RightHatch", PrimitiveType.Cube, obj, new Vector3(0.48f, 1.05f, 0),
                new Vector3(0.94f, 2.1f, 0.18f), metal, true).transform;
            Shape("LeftMark", PrimitiveType.Cube, left, new Vector3(0, 0, -0.58f),
                new Vector3(0.52f, 0.12f, 0.08f), orange);
            Shape("RightMark", PrimitiveType.Cube, right, new Vector3(0, 0, -0.58f),
                new Vector3(0.52f, 0.12f, 0.08f), orange);
            Renderer status = Shape("DockStatusLamp", PrimitiveType.Cube, obj, new Vector3(0, 2.36f, -0.24f),
                new Vector3(2.25f, 0.12f, 0.08f), lamp).GetComponent<Renderer>();
            Renderer ring = Shape("BoardingMarker", PrimitiveType.Cylinder, obj, new Vector3(0, 0.06f, -0.6f),
                new Vector3(1.9f, 0.03f, 1.9f), lamp).GetComponent<Renderer>();
            Transform beacon = Group("BoardingBeacon", obj, Vector3.zero);
            for (int i = 0; i < 4; i++)
                Shape("GreenBeacon", PrimitiveType.Sphere, beacon, new Vector3(0, 0.55f + i * 0.5f, -0.15f),
                    Vector3.one * (0.18f - i * 0.02f), teal);
            beacon.gameObject.SetActive(false);
            DockGate dock = obj.gameObject.AddComponent<DockGate>();
            dock.Configure(name, left, right, new[] { status, ring }, beacon.gameObject); return dock;
        }
        private static ArcStrip Hazard(RepairGame game, Vector3 position, Vector2 size, float offset, Transform root)
        {
            Transform obj = Group("LeakingPowerBus", root, position);
            Renderer floor = Shape("ArcFloor", PrimitiveType.Cube, obj, new Vector3(0, 0.048f, 0),
                new Vector3(size.x, 0.025f, size.y), lamp).GetComponent<Renderer>();
            for (int i = 0; i < 10; i++)
                Shape("WarningDash", PrimitiveType.Cube, obj, new Vector3(-size.x * 0.5f - 0.13f, 0.07f,
                    -size.y * 0.45f + i * size.y * 0.1f), new Vector3(0.22f, 0.02f, size.y * 0.05f), orange);
            Transform arcs = Group("ElectricArcs", obj, Vector3.zero);
            for (int i = 0; i < 3; i++)
            {
                LineRenderer line = Group("VisibleArc", arcs, Vector3.zero).gameObject.AddComponent<LineRenderer>();
                line.useWorldSpace = false; line.sharedMaterial = orange; line.startWidth = line.endWidth = 0.055f;
                line.positionCount = 9;
                for (int j = 0; j < 9; j++) line.SetPosition(j, new Vector3(-size.x * 0.4f + size.x * j / 10f,
                    0.18f + (j % 2) * 0.22f, (i - 1) * size.y * 0.28f));
            }
            Label("ARC / WAIT OR DETOUR", obj, new Vector3(0, 0.17f, -size.y * 0.5f - 0.3f), 0.075f);
            ArcStrip strip = obj.gameObject.AddComponent<ArcStrip>();
            strip.Configure(game, size, floor, arcs.gameObject, offset); return strip;
        }
        private static void Crates(Vector3 pos, Transform root)
        {
            Transform obj = Group("FreightCrates", root, pos);
            Shape("ShippingCrate", PrimitiveType.Cube, obj, new Vector3(0, 0.6f, 0),
                new Vector3(1.45f, 1.2f, 1.5f), metal, true);
            foreach (float z in new[] { -0.61f, 0.61f })
                Shape("Reinforcement", PrimitiveType.Cube, obj, new Vector3(0, 0.63f, z),
                    new Vector3(1.53f, 1.3f, 0.16f), dark);
            Shape("FreightBand", PrimitiveType.Cube, obj, new Vector3(0, 1.22f, 0),
                new Vector3(0.3f, 0.07f, 1.6f), orange);
            Kenney("barrels", root, new Vector3(7.7f, 0, 0.8f), 1.7f);
        }
        private static GameObject Kenney(string name, Transform parent, Vector3 position, float longestSide, float yaw = 0)
        {
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ThirdParty/KenneySpaceKit/" + name + ".fbx");
            if (asset == null) throw new InvalidOperationException("Missing Kenney model: " + name);
            Transform wrapper = Group(name, null, Vector3.zero);
            GameObject obj = UnityEngine.Object.Instantiate(asset, wrapper);
            obj.transform.localPosition = Vector3.zero; obj.transform.localRotation = Quaternion.identity;
            Bounds bounds = new Bounds(); bool first = true;
            foreach (Renderer renderer in obj.GetComponentsInChildren<Renderer>())
            {
                if (first) { bounds = renderer.bounds; first = false; } else bounds.Encapsulate(renderer.bounds);
                Material[] sources = renderer.sharedMaterials;
                for (int i = 0; i < sources.Length; i++)
                {
                    string materialName = sources[i] == null ? "default" : sources[i].name;
                    Color color = materialName.Contains("Red") ? new Color(1, 0.48f, 0.16f)
                        : materialName.Contains("Dark") ? new Color(0.45f, 0.56f, 0.66f)
                        : materialName.Contains("dark") ? new Color(0.09f, 0.14f, 0.2f)
                        : new Color(0.82f, 0.89f, 0.93f);
                    sources[i] = Mat("Kenney_" + materialName.Replace(" ", "_"), color);
                }
                renderer.sharedMaterials = sources;
            }
            float largest = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            if (largest < 0.001f) throw new InvalidOperationException("Empty mesh: " + name);
            float scale = longestSide / largest; obj.transform.localScale *= scale;
            obj.transform.localPosition = -new Vector3(bounds.center.x, bounds.min.y, bounds.center.z) * scale;
            wrapper.SetParent(parent, false); wrapper.localPosition = position; wrapper.localRotation = Quaternion.Euler(0, yaw, 0);
            Debug.Log("STATION_MODEL: " + name + " fitted size " + bounds.size * scale); return wrapper.gameObject;
        }
        private static void Menu()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Camera camera = Group("Main Camera", null, Vector3.zero).gameObject.AddComponent<Camera>();
            camera.tag = "MainCamera"; camera.gameObject.AddComponent<AudioListener>();
            camera.transform.position = new Vector3(0, 10, -16); camera.transform.LookAt(new Vector3(0, 1, 0));
            camera.clearFlags = CameraClearFlags.SolidColor; camera.fieldOfView = 50;
            camera.backgroundColor = new Color(0.014f, 0.027f, 0.065f);
            Light light = Group("Directional Light", null, Vector3.zero).gameObject.AddComponent<Light>();
            light.type = LightType.Directional; light.intensity = 1.8f; light.transform.rotation = Quaternion.Euler(45, -30, 0);
            RenderSettings.ambientLight = new Color(0.45f, 0.58f, 0.7f);
            Transform robot = Group("MenuRobot", null, new Vector3(9.2f, 1.4f, 0));
            Shape("RollingChassis", PrimitiveType.Sphere, robot, Vector3.zero, Vector3.one, shell);
            Robot(robot, null, null);
            robot.localScale = Vector3.one * 3;
            Transform head = GameObject.Find("M07_StableHead").transform; head.localScale = Vector3.one * 3;
            Kenney("craft_cargoA", null, new Vector3(9.2f, -3.3f, 3), 5.5f, 210);
            GameHud menuHud = Group("MenuUI", null, Vector3.zero).gameObject.AddComponent<GameHud>();
            menuHud.Configure(null, true); menuHud.BuildInterface();
            PostProcessing(camera);
            Save(scene, MenuPath);
        }
        [MenuItem("UnityAgentLab/Refresh Station Presentation")]
        public static void RefreshPresentation()
        {
            Palette();
            var scene = EditorSceneManager.OpenScene(GamePath);
            foreach (TextMesh label in UnityEngine.Object.FindObjectsByType<TextMesh>(FindObjectsSortMode.None))
                if (label.text == "POWER" || label.text == "RECLAIM" || label.text.StartsWith("DOCK ") || label.text.StartsWith("CELL "))
                    UnityEngine.Object.DestroyImmediate(label.gameObject);
            Save(scene, GamePath); Menu(); AssetDatabase.SaveAssets(); EditorSceneManager.OpenScene(GamePath);
        }
        private static void Save(UnityEngine.SceneManagement.Scene scene, string path)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, path)) throw new InvalidOperationException("Scene save failed: " + path);
        }
        [MenuItem("UnityAgentLab/Build Station Rescue Windows")]
        public static void BuildWindows()
        {
            string folder = Path.GetFullPath("../Builds/Windows/StationRescueV2_1");
            Directory.CreateDirectory(folder);
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { MenuPath, GamePath }, target = BuildTarget.StandaloneWindows64,
                locationPathName = Path.Combine(folder, "StationRescue.exe"), options = BuildOptions.None });
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("Station Windows build failed: " + report.summary.result);
            Debug.Log("STATION_WINDOWS_BUILD_PASSED: " + folder);
        }
        public static void RefreshAndBuild()
        {
            UpgradePresentation(); BuildWindows();
        }
    }
}
