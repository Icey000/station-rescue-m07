using System;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace UnityAgentLab.Editor
{
    public static partial class StationBuilder
    {
        [MenuItem("UnityAgentLab/Upgrade Robot, Power and Interface")]
        public static void UpgradePresentation()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play before upgrading.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            AssetDatabase.Refresh();
            var scene = EditorSceneManager.OpenScene(GamePath);
            Directory.CreateDirectory("Assets/Scenes/Baselines");
            if (!File.Exists("Assets/Scenes/Baselines/Game_StationRescueV1.unity"))
                EditorSceneManager.SaveScene(scene, "Assets/Scenes/Baselines/Game_StationRescueV1.unity", true);
            Directory.CreateDirectory("Assets/Materials/Station");
            Directory.CreateDirectory("Assets/Prefabs/Station");
            ImportModularKit(); AssetDatabase.Refresh(); Palette();
            foreach (string rootName in new[] { "StationWorld", "RepairPrototype" })
            {
                GameObject old = GameObject.Find(rootName);
                if (old != null) UnityEngine.Object.DestroyImmediate(old);
            }
            GameObject oldBloom = GameObject.Find("StationBloom");
            if (oldBloom != null) UnityEngine.Object.DestroyImmediate(oldBloom);
            Transform player = GameObject.Find("Player").transform;
            for (int i = player.childCount - 1; i >= 0; i--) UnityEngine.Object.DestroyImmediate(player.GetChild(i).gameObject);
            Transform root = Group("StationWorld", null, Vector3.zero);
            RepairGame game = root.gameObject.AddComponent<RepairGame>();
            player.SetPositionAndRotation(new Vector3(-0.5f, 0.65f, -7.5f), Quaternion.identity);
            player.GetComponent<Renderer>().sharedMaterial = shell;
            Rigidbody actorBody = player.GetComponent<Rigidbody>(); actorBody.mass = 1; actorBody.isKinematic = false;
            player.GetComponent<PlayerMotor>().enabled = true; player.GetComponent<HumanInput>().enabled = true;
            RobotV2(player, game, root);
            Camera camera = Camera.main;
            camera.transform.position = new Vector3(0, 24, -19.5f); camera.transform.LookAt(new Vector3(0, 0.2f, 0));
            camera.fieldOfView = 46; camera.backgroundColor = new Color(0.014f, 0.027f, 0.065f);
            Light sun = GameObject.Find("Directional Light").GetComponent<Light>();
            sun.transform.rotation = Quaternion.Euler(53, -35, 0); sun.intensity = 1.35f;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.42f, 0.53f, 0.65f);
            RenderSettings.ambientEquatorColor = new Color(0.25f, 0.33f, 0.42f);
            RenderSettings.ambientGroundColor = new Color(0.12f, 0.17f, 0.24f);
            Floor(root);
            foreach (TextMesh label in root.GetComponentsInChildren<TextMesh>()) UnityEngine.Object.DestroyImmediate(label.gameObject);
            Vector3[] points = { new Vector3(-6, 0, 1.5f), new Vector3(-6, 0, -3), new Vector3(-3, 0, 4), new Vector3(2, 0, 6.5f) };
            var slots = new Transform[points.Length];
            for (int i = 0; i < points.Length; i++)
            {
                slots[i] = ChargingRack(points[i], i + 1, root);
                foreach (TextMesh label in slots[i].parent.GetComponentsInChildren<TextMesh>()) UnityEngine.Object.DestroyImmediate(label.gameObject);
            }
            ModuleSpawner spawner = Group("ModuleSpawner", root, Vector3.zero).gameObject.AddComponent<ModuleSpawner>();
            spawner.Configure(CellPrefab(ModuleKind.BlueCircle), CellPrefab(ModuleKind.RedTriangle), slots[0], slots[1]);
            Transform device = Generator(new Vector3(5, 0, 3.8f), root, out PowerStationVisual power);
            Transform socket = Group("InstalledCellSocket", root, new Vector3(5, 1.25f, 2.45f));
            Transform reclaim = Reclaim(new Vector3(-3, 0, -1.4f), root);
            DockGate[] docks = {
                Dock("A", new Vector3(-6, 0, 8.7f), 0, root),
                Dock("B", new Vector3(8.7f, 0, -3.8f), 90, root),
                Dock("C", new Vector3(-8.7f, 0, -6), -90, root) };
            foreach (DockGate dock in docks)
            {
                dock.SetBoardingAnchor(dock.transform.Find("BoardingMarker"));
                Modular("gate", dock.transform, new Vector3(0, 0, 0.08f), new Vector3(3.5f, 2.6f, 0.5f));
                foreach (float x in new[] { -1.75f, 1.75f })
                    Shape("AirlockBulkhead", PrimitiveType.Cube, dock.transform, new Vector3(x, 1.05f, 0),
                        new Vector3(0.65f, 2.1f, 0.65f), dark, true);
                Shape("DockIdentification", PrimitiveType.Cube, dock.transform, new Vector3(0, 2.6f, 0),
                    new Vector3(0.75f, 0.3f, 0.35f), teal);
                Label(dock.name.Substring(dock.name.Length - 1), dock.transform, new Vector3(0, 2.68f, -0.25f), 0.1f);
            }
            Bulkheads(root);
            Transform highlight = Group("InteractionHighlight", root, Vector3.zero);
            highlight.gameObject.AddComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Meshes/TargetRing.asset");
            highlight.gameObject.AddComponent<MeshRenderer>().sharedMaterial = Mat("TargetGlowV2", new Color(0.08f, 0.8f, 1), 2);
            highlight.gameObject.SetActive(false);
            GameAudio audio = root.gameObject.AddComponent<GameAudio>();
            string[] soundNames = { "Boost", "Pickup", "Return", "Denied", "Install", "Complete" };
            AudioClip[] clips = new AudioClip[soundNames.Length];
            for (int i = 0; i < clips.Length; i++) clips[i] = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/" + soundNames[i] + ".wav");
            audio.Configure(player.GetComponent<PlayerMotor>(), clips);
            game.Configure(player, spawner, device, socket, reclaim, docks[0].BoardingAnchor, highlight, null, null, audio);
            ArcStrip[] strips = { HazardV2(game, new Vector3(0, 0, 1.5f), new Vector2(1.8f, 6), 0, root),
                HazardV2(game, new Vector3(1.7f, 0, -3.5f), new Vector2(4.3f, 1.35f), 2.25f, root) };
            game.ConfigureStation(docks, slots, strips, power);
            game.ConfigureRoutes(PowerCables(root));
            PlayerInteraction interaction = player.GetComponent<PlayerInteraction>() ?? player.gameObject.AddComponent<PlayerInteraction>();
            interaction.enabled = true; interaction.Configure(game);
            GameHud hud = root.gameObject.AddComponent<GameHud>(); hud.Configure(game, false); hud.BuildInterface();
            Crates(new Vector3(2.7f, 0, 0.25f), root); PostProcessing(camera);
            Save(scene, GamePath); MenuV2(); AssetDatabase.SaveAssets(); EditorSceneManager.OpenScene(GamePath);
            Debug.Log("STATION_V2_CREATED: directional droid, manual boarding, dropped cargo, powered circuits, bilingual Canvas");
        }

        private static void ImportModularKit()
        {
            string folder = "Assets/ThirdParty/KenneyModularSpaceKit"; Directory.CreateDirectory(folder);
            string[] models = { "template-wall", "template-wall-half", "gate", "cables" };
            bool imported = File.Exists(folder + "/License.txt") && File.Exists(folder + "/SOURCE.md");
            foreach (string model in models) imported &= File.Exists(folder + "/" + model + ".fbx");
            // A source checkout already contains all selected models; the external download is optional.
            if (imported) return;
            string archive = Path.GetFullPath("../SourceAssets/Kenney/kenney_modular-space-kit_1.0.zip");
            using (ZipArchive zip = ZipFile.OpenRead(archive))
            {
                foreach (string name in models)
                {
                    string target = folder + "/" + name + ".fbx";
                    if (File.Exists(target)) continue;
                    ZipArchiveEntry entry = zip.GetEntry("Models/FBX format/" + name + ".fbx");
                    if (entry == null) throw new InvalidOperationException("Missing modular model " + name);
                    using (Stream source = entry.Open()) using (FileStream dest = File.Create(target)) source.CopyTo(dest);
                }
                string license = folder + "/License.txt";
                if (!File.Exists(license)) using (Stream source = zip.GetEntry("License.txt").Open()) using (FileStream dest = File.Create(license)) source.CopyTo(dest);
            }
            File.WriteAllText(folder + "/SOURCE.md", "# Kenney Modular Space Kit\n\nAuthor: Kenney. Source: https://kenney.nl/assets/modular-space-kit\n\nLicense: CC0. The original license is preserved in License.txt.\nSelected wall, gate and cable FBX models are used; materials are adapted to URP.\n");
        }
        private static GameObject Modular(string name, Transform parent, Vector3 pos, Vector3 dimensions, float yaw = 0)
        {
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ThirdParty/KenneyModularSpaceKit/" + name + ".fbx");
            if (asset == null) throw new InvalidOperationException("Missing modular asset " + name);
            Transform wrapper = Group("Modular_" + name, null, Vector3.zero);
            GameObject model = UnityEngine.Object.Instantiate(asset, wrapper);
            Bounds bounds = default; bool first = true;
            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>())
            {
                if (first) { bounds = renderer.bounds; first = false; } else bounds.Encapsulate(renderer.bounds);
                var mats = renderer.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = i % 2 == 0 ? metal : dark;
                renderer.sharedMaterials = mats;
            }
            Vector3 scale = new Vector3(dimensions.x / Mathf.Max(.01f, bounds.size.x), dimensions.y / Mathf.Max(.01f, bounds.size.y), dimensions.z / Mathf.Max(.01f, bounds.size.z));
            model.transform.localScale = scale;
            model.transform.localPosition = -Vector3.Scale(new Vector3(bounds.center.x, bounds.min.y, bounds.center.z), scale);
            wrapper.SetParent(parent, false); wrapper.localPosition = pos; wrapper.localRotation = Quaternion.Euler(0, yaw, 0); return wrapper.gameObject;
        }
        private static void Bulkheads(Transform root)
        {
            foreach (string side in new[] { "North", "South", "East", "West" })
            {
                GameObject wall = GameObject.Find("Wall_" + side);
                wall.GetComponent<Renderer>().enabled = false;
                wall.GetComponent<Collider>().enabled = false;
            }
            // Hull sits behind the airlock frames; solid segments leave matching door gaps.
            for (int i = 0; i < 9; i++)
            {
                float along = -8 + i * 2;
                foreach (int side in new[] { 0, 1, 2, 3 })
                {
                    bool gap = side == 0 && Mathf.Abs(along + 6) < 1.8f
                        || side == 1 && Mathf.Abs(along + 4) < 1.8f
                        || side == 2 && Mathf.Abs(along + 6) < 1.8f;
                    if (gap) continue;
                    Vector3 pos = side == 0 ? new Vector3(along, 0, 8.7f) : side == 1 ? new Vector3(8.7f, 0, along)
                        : side == 2 ? new Vector3(-8.7f, 0, along) : new Vector3(along, 0, -9.3f);
                    float height = side == 3 ? 0.8f : 2.1f, yaw = side == 1 || side == 2 ? 90 : 0;
                    Modular(side == 3 ? "template-wall-half" : "template-wall", root, pos, new Vector3(1.98f, height, 0.4f), yaw);
                    Shape("HullCollider", PrimitiveType.Cube, root, pos + Vector3.up * height / 2,
                        side == 1 || side == 2 ? new Vector3(.4f, height, 1.98f) : new Vector3(1.98f, height, .4f), dark, true).GetComponent<Renderer>().enabled = false;
                    if (i % 2 == 0)
                    {
                        Shape("HullLight", PrimitiveType.Cube, root, pos + new Vector3(side == 1 ? -.24f : side == 2 ? .24f : 0, height * .7f, side == 0 ? -.24f : 0),
                            new Vector3(.18f, .52f, .18f), Mat("HullLightV2", new Color(1, .71f, .37f), 2));
                    }
                }
            }
            // Close the unused outer corner gaps without covering the airlock openings.
            foreach (Vector3 p in new[] { new Vector3(-8.7f, 0, 8.7f), new Vector3(8.7f, 0, 8.7f), new Vector3(-8.7f,0,-9.3f), new Vector3(8.7f,0,-9.3f) })
                Shape("HullCorner", PrimitiveType.Cube, root, p + Vector3.up * .9f, new Vector3(.65f, 1.8f, .65f), dark, true);
        }
        private static Mesh Hemisphere()
        {
            const string path = "Assets/Meshes/M07Hemisphere.asset";
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path); if (existing != null) return existing;
            var vertices = new List<Vector3>(); var tris = new List<int>(); int rings = 12, sides = 36;
            for (int r = 0; r <= rings; r++)
            {
                float elevation = r * Mathf.PI / (rings * 2);
                for (int i = 0; i <= sides; i++)
                { float a = i * Mathf.PI * 2 / sides; vertices.Add(new Vector3(Mathf.Cos(a) * Mathf.Cos(elevation), Mathf.Sin(elevation), Mathf.Sin(a) * Mathf.Cos(elevation))); }
            }
            for (int r = 0; r < rings; r++) for (int i = 0; i < sides; i++)
            { int n = r * (sides + 1) + i; tris.AddRange(new[] { n, n + sides + 1, n + 1, n + 1, n + sides + 1, n + sides + 2 }); }
            var mesh = new Mesh { name = "M07 hemisphere" }; mesh.SetVertices(vertices); mesh.SetTriangles(tris, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, path); return mesh;
        }
        private static void RobotV2(Transform player, RepairGame game, Transform root)
        {
            Material accent = Mat("RobotOrangeV2", new Color(1, .42f, .055f));
            foreach (Vector3 direction in new[] { Vector3.up, Vector3.right, Vector3.forward, Vector3.left, Vector3.back, Vector3.down })
            {
                Quaternion q = Quaternion.FromToRotation(Vector3.up, direction);
                Transform ring = Shape("RollingOrangePanel", PrimitiveType.Cylinder, player, direction * .475f, new Vector3(.55f, .024f, .55f), accent).transform; ring.localRotation = q;
                Transform center = Shape("PanelCenter", PrimitiveType.Cylinder, player, direction * .504f, new Vector3(.34f, .012f, .34f), shell).transform; center.localRotation = q;
                Transform core = Shape("PanelHub", PrimitiveType.Cylinder, player, direction * .52f, new Vector3(.12f, .008f, .12f), metal).transform; core.localRotation = q;
            }
            Transform head = Group("M07_StableHead", root, player.position, 180);
            Transform dome = Group("HalfDome", head, new Vector3(0, .43f, 0));
            dome.localScale = new Vector3(.42f, .34f, .42f);
            dome.gameObject.AddComponent<MeshFilter>().sharedMesh = Hemisphere(); dome.gameObject.AddComponent<MeshRenderer>().sharedMaterial = shell;
            Shape("HeadBand", PrimitiveType.Cylinder, head, new Vector3(0, .44f, 0), new Vector3(.86f, .04f, .86f), accent);
            Shape("MainLens", PrimitiveType.Sphere, head, new Vector3(-.13f, .62f, .33f), new Vector3(.23f, .23f, .12f), dark);
            Shape("LensGlass", PrimitiveType.Sphere, head, new Vector3(-.13f, .62f, .395f), new Vector3(.105f, .105f, .035f), Mat("LensGlowV2", new Color(.04f, .55f, .95f), 1.5f));
            Shape("AuxLens", PrimitiveType.Sphere, head, new Vector3(.16f, .56f, .36f), new Vector3(.095f, .095f, .08f), dark);
            Shape("Antenna", PrimitiveType.Cylinder, head, new Vector3(.23f, .89f, -.12f), new Vector3(.028f, .19f, .028f), metal);
            Shape("AntennaTip", PrimitiveType.Sphere, head, new Vector3(.23f, 1.07f, -.12f), Vector3.one * .055f, blue);
            Transform magnet = Group("MagnetArms", head, Vector3.zero);
            foreach (float x in new[] { -.49f, .49f })
            {
                Shape("MagnetBracket", PrimitiveType.Cube, magnet, new Vector3(x, .35f, .1f), new Vector3(.11f, .35f, .18f), metal);
                Shape("MagnetTip", PrimitiveType.Cube, magnet, new Vector3(x, .52f, .15f), new Vector3(.19f, .1f, .2f), teal);
            }
            head.gameObject.AddComponent<RobotVisual>().Configure(player, magnet, game);
            if (player.GetComponent<PlayerMotor>() == null) return;
            Transform thrust = Group("IonThrusters", head, new Vector3(0, .1f, -.41f));
            var nozzles = new List<Renderer>();
            foreach (float x in new[] { -.27f, .27f })
                nozzles.Add(Shape("ExhaustNozzle", PrimitiveType.Sphere, thrust, new Vector3(x, 0, 0), new Vector3(.18f, .16f, .14f), Mat("NozzleV2", new Color(.05f, .65f, 1), 1)).GetComponent<Renderer>());
            ParticleSystem particles = Particles(thrust, "BlueIonExhaust", new Color(.1f, .75f, 1), .14f, .25f, 4, 0);
            particles.transform.localRotation = Quaternion.Euler(0, 180, 0);
            TrailRenderer trail = Group("BoostTrail", thrust, Vector3.zero).gameObject.AddComponent<TrailRenderer>();
            trail.sharedMaterial = Mat("TrailGlowV2", new Color(.04f, .7f, 1), 2.5f); trail.time = .25f; trail.startWidth = .32f; trail.endWidth = .01f;
            trail.minVertexDistance = .07f; trail.emitting = false;
            Light glow = Group("BoostLight", thrust, Vector3.zero).gameObject.AddComponent<Light>(); glow.type = LightType.Point; glow.color = new Color(.06f, .55f, 1); glow.range = 2.2f; glow.intensity = 0;
            head.gameObject.AddComponent<BoostVisual>().Configure(player.GetComponent<PlayerMotor>(), particles, trail, glow, nozzles.ToArray());
        }
        private static ParticleSystem Particles(Transform parent, string name, Color color, float size, float lifetime, float speed, float rate)
        {
            ParticleSystem particles = Group(name, parent, Vector3.zero).gameObject.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main; main.startColor = color; main.startSize = new ParticleSystem.MinMaxCurve(size * .5f, size); main.startLifetime = lifetime;
            main.startSpeed = speed; main.maxParticles = 60; main.playOnAwake = false; main.simulationSpace = ParticleSystemSimulationSpace.World;
            var shape = particles.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 12; shape.radius = .12f;
            var emission = particles.emission; emission.rateOverTime = rate;
            particles.GetComponent<ParticleSystemRenderer>().sharedMaterial = Mat("ParticlesGlowV2", new Color(.2f, .8f, 1), 2);
            return particles;
        }
        private static ArcStrip HazardV2(RepairGame game, Vector3 pos, Vector2 size, float offset, Transform root)
        {
            ArcStrip strip = Hazard(game, pos, size, offset, root);
            foreach (TextMesh label in strip.GetComponentsInChildren<TextMesh>()) UnityEngine.Object.DestroyImmediate(label.gameObject);
            Transform arcs = strip.transform.Find("ElectricArcs");
            Material electric = Mat("ElectricArcV2", new Color(.64f, .48f, 1), 5);
            LineRenderer[] lines = arcs.GetComponentsInChildren<LineRenderer>();
            foreach (LineRenderer line in lines)
            {
                line.sharedMaterial = electric; line.startWidth = line.endWidth = .095f;
                for (int j = 0; j < line.positionCount; j++)
                { Vector3 p = line.GetPosition(j); p.y += .6f; line.SetPosition(j, p); }
                foreach (int endpoint in new[] { 0, line.positionCount - 1 })
                {
                    Vector3 p = line.GetPosition(endpoint);
                    Shape("LiveElectrode", PrimitiveType.Cylinder, strip.transform, new Vector3(p.x, .42f, p.z), new Vector3(.23f, .42f, .23f), metal);
                    Shape("ElectrodeHead", PrimitiveType.Sphere, strip.transform, p, Vector3.one * .23f, electric);
                }
            }
            Modular("cables", strip.transform, new Vector3(0, .09f, 0), new Vector3(size.x * .7f, .13f, size.y * .85f));
            Shape("DamagedJunctionBox", PrimitiveType.Cube, strip.transform, new Vector3(-size.x * .5f - .32f, .24f, 0), new Vector3(.48f, .48f, .6f), orange);
            Transform symbol = Group("ElectricalWarning", strip.transform, new Vector3(-size.x * .5f - .32f, .5f, 0));
            Shape("WarningBolt", PrimitiveType.Cube, symbol, Vector3.zero, new Vector3(.07f, .04f, .3f), white).transform.localRotation = Quaternion.Euler(0, 27, 0);
            Light glow = Group("FaultGlow", strip.transform, new Vector3(0, .8f, 0)).gameObject.AddComponent<Light>(); glow.type = LightType.Point; glow.range = 4; glow.color = new Color(.68f, .42f, 1); glow.intensity = 0;
            ParticleSystem sparks = Particles(strip.transform, "ElectricalSparks", new Color(1, .55f, .15f), .09f, .2f, 2, 18);
            sparks.transform.localPosition = new Vector3(0, .65f, 0); sparks.transform.localRotation = Quaternion.Euler(-90, 0, 0);
            AudioSource sound = strip.gameObject.AddComponent<AudioSource>(); sound.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/ElectricArc.wav");
            sound.playOnAwake = false; sound.loop = true; sound.volume = .055f; sound.spatialBlend = 0;
            strip.ConfigureEffects(sparks, glow, sound); return strip;
        }
        private static PowerRouteController PowerCables(Transform root)
        {
            Transform bus = Group("EmergencyPowerCircuits", root, Vector3.zero);
            Vector3[][] paths = {
                new[] { new Vector3(5,.065f,2.1f), new Vector3(3.9f,.065f,2.1f), new Vector3(3.9f,.065f,-1.8f), new Vector3(1.1f,.065f,-1.8f), new Vector3(1.1f,.065f,5.9f), new Vector3(-6,.065f,5.9f), new Vector3(-6,.065f,8.1f) },
                new[] { new Vector3(5,.065f,2.1f), new Vector3(3.9f,.065f,2.1f), new Vector3(3.9f,.065f,-1.8f), new Vector3(7,.065f,-1.8f), new Vector3(7,.065f,-3.8f), new Vector3(8.1f,.065f,-3.8f) },
                new[] { new Vector3(5,.065f,2.1f), new Vector3(3.9f,.065f,2.1f), new Vector3(3.9f,.065f,-1.8f), new Vector3(-1.7f,.065f,-1.8f), new Vector3(-1.7f,.065f,-6), new Vector3(-8.1f,.065f,-6) }
            };
            var lines = new LineRenderer[3];
            var channels = new HashSet<string>();
            for (int i = 0; i < 3; i++)
            {
                for (int p = 0; p < paths[i].Length; p++) paths[i][p].y = .13f;
                for (int j = 1; j < paths[i].Length; j++)
                {
                    Vector3 start = paths[i][j - 1], end = paths[i][j]; Vector3 delta = end - start;
                    if (!channels.Add(start.ToString("F2") + "/" + end.ToString("F2"))) continue;
                    Transform channel = Shape("CableChannel_" + i, PrimitiveType.Cube, bus, (start + end) / 2 - Vector3.up * .055f,
                        new Vector3(.24f, .055f, delta.magnitude), dark).transform; channel.rotation = Quaternion.LookRotation(delta);
                }
                LineRenderer line = Group("PoweredBranch_" + i, bus, Vector3.zero).gameObject.AddComponent<LineRenderer>(); line.useWorldSpace = false;
                line.positionCount = paths[i].Length; line.SetPositions(paths[i]); line.startWidth = line.endWidth = .3f;
                line.sharedMaterial = PowerWireMaterial(); line.numCornerVertices = 4; lines[i] = line;
            }
            PowerRouteController controller = bus.gameObject.AddComponent<PowerRouteController>(); controller.Configure(lines); return controller;
        }
        private static Material PowerWireMaterial()
        {
            const string path = "Assets/Materials/Station/PowerWireUnlitV2.mat";
            Material result = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (result != null) return result;
            result = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "PowerWireUnlitV2" };
            result.SetColor("_BaseColor", new Color(.08f, 3, 1.2f, 1));
            AssetDatabase.CreateAsset(result, path); return result;
        }
        private static void PostProcessing(Camera camera)
        {
            var data = camera.GetComponent<UniversalAdditionalCameraData>() ?? camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            const string path = "Assets/Settings/StationRescueV2Volume.asset";
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>(); profile.name = "StationRescueV2Volume";
                AssetDatabase.CreateAsset(profile, path); Bloom bloom = profile.Add<Bloom>(true);
                bloom.intensity.Override(.4f); bloom.threshold.Override(1); bloom.scatter.Override(.5f);
                AssetDatabase.AddObjectToAsset(bloom, profile); EditorUtility.SetDirty(profile);
            }
            GameObject obj = new GameObject("StationBloom"); Volume volume = obj.AddComponent<Volume>(); volume.isGlobal = true; volume.sharedProfile = profile;
        }
        private static void MenuV2()
        {
            Menu();
            var scene = EditorSceneManager.OpenScene(MenuPath);
            Transform robot = GameObject.Find("MenuRobot").transform;
            Transform oldHead = GameObject.Find("M07_StableHead").transform;
            UnityEngine.Object.DestroyImmediate(oldHead.gameObject);
            for (int i = robot.childCount - 1; i >= 0; i--)
                if (robot.GetChild(i).name != "RollingChassis") UnityEngine.Object.DestroyImmediate(robot.GetChild(i).gameObject);
            RobotV2(robot, null, null);
            Transform head = GameObject.Find("M07_StableHead").transform; head.localScale = Vector3.one * 3;
            Save(scene, MenuPath);
        }
        public static void BuildV2()
        { UpgradePresentation(); StationValidationV2.Run(); BuildWindows(); }
    }
}
