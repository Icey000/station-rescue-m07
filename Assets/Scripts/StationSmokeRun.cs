using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

namespace UnityAgentLab
{
    /// <summary>Opt-in Windows checks; normal starts never create this object.</summary>
    public sealed class StationSmokeRun : MonoBehaviour
    {
        private Keyboard keyboard;
        private Mouse mouse;
        private string output, runtimeError;
        private readonly List<string> checks = new List<string>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-station-smoke-test");
            if (index < 0) index = Array.IndexOf(args, "-magnet-smoke-test");
            if (index < 0 || index + 1 >= args.Length) return;
            var obj = new GameObject("StationRegressionRunner"); DontDestroyOnLoad(obj);
            obj.AddComponent<StationSmokeRun>().output = args[index + 1];
        }
        private IEnumerator Start()
        {
            Directory.CreateDirectory(output); Application.logMessageReceived += ObserveError;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            keyboard = InputSystem.AddDevice<Keyboard>(); keyboard.MakeCurrent();
            mouse = InputSystem.AddDevice<Mouse>(); mouse.MakeCurrent();
            // Flatten nested enumerators so exceptions in navigation/capture are reported too.
            var stack = new Stack<IEnumerator>(); stack.Push(Run());
            while (stack.Count > 0)
            {
                object instruction = null; bool more = false;
                try
                {
                    if (runtimeError != null) throw new InvalidOperationException(runtimeError);
                    more = stack.Peek().MoveNext();
                    if (more) instruction = stack.Peek().Current;
                }
                catch (Exception exception)
                { WriteReport("failed", exception.ToString()); Application.Quit(1); yield break; }
                if (!more) { stack.Pop(); continue; }
                if (instruction is IEnumerator child) { stack.Push(child); continue; }
                yield return instruction;
            }
            WriteReport("passed", ""); Application.logMessageReceived -= ObserveError;
            InputSystem.RemoveDevice(keyboard); InputSystem.RemoveDevice(mouse); GameFlow.Quit();
        }
        private void ObserveError(string message, string trace, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) runtimeError = message; }
        private void Check(bool value, string name)
        { if (!value) throw new InvalidOperationException("Station standalone check failed: " + name); checks.Add(name); }
        private void Keys(params Key[] keys)
        { keyboard.MakeCurrent(); InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys)); }
        private IEnumerator Press(Key key)
        { Keys(key); yield return new WaitForSecondsRealtime(0.12f); Keys(); yield return new WaitForSecondsRealtime(0.1f); }
        private RepairGame Game() => FindFirstObjectByType<RepairGame>();
        private void Near(RepairGame game, Transform target, bool center = false)
        {
            game.Player.GetComponent<PlayerMotor>().ResetMotion();
            Vector3 point = target.position + (center ? Vector3.zero : Vector3.back * 1.8f); point.y = 0.65f;
            game.Player.GetComponent<Rigidbody>().position = point; game.Player.position = point; Physics.SyncTransforms();
        }
        private IEnumerator Screenshot(string name)
        {
            yield return new WaitForEndOfFrame();
            foreach (var button in FindFirstObjectByType<GameHud>().InterfaceCanvas.GetComponentsInChildren<UnityEngine.UI.Button>())
            {
                var corners = new Vector3[4]; button.GetComponent<RectTransform>().GetWorldCorners(corners);
                Check(corners[0].x >= -1 && corners[0].y >= -1 && corners[2].x <= Screen.width + 1 && corners[2].y <= Screen.height + 1,
                    "button_in_view_" + name + "_" + button.name);
            }
            Texture2D frame = ScreenCapture.CaptureScreenshotAsTexture(); Color32[] pixels = frame.GetPixels32();
            int bright = 0;
            for (int i = 0; i < pixels.Length && bright < 32; i++)
                if (pixels[i].r + pixels[i].g + pixels[i].b > 280) bright++;
            bool oldSample = false;
            for (int i = 0; i < pixels.Length; i += 512)
                if (pixels[i].r + pixels[i].g + pixels[i].b > 280) { oldSample = true; break; }
            File.WriteAllBytes(Path.Combine(output, name + ".png"), frame.EncodeToPNG());
            Destroy(frame);
            if (!oldSample && bright >= 32) Debug.Log("SCREENSHOT_STRIDE_ALIAS: " + name + " " + Screen.width + "x" + Screen.height);
            Check(bright >= 32, "render_nonblack_" + name);
            yield return new WaitForSecondsRealtime(0.25f);
        }
        private IEnumerator Install(RepairGame game)
        {
            Near(game, game.RequiredModule.transform); yield return new WaitForSeconds(0.12f); yield return Press(Key.E);
            Near(game, game.Device); yield return new WaitForSeconds(0.12f); yield return Press(Key.E);
            while (game.Phase == RepairPhase.Powering) yield return new WaitForSeconds(.05f);
            Check(game.Phase == RepairPhase.Installed, "correct_model_keyboard_install_" + game.RequiredKind);
        }
        private IEnumerator Click(UnityEngine.UI.Button button)
        {
            Check(button != null && button.interactable && button.gameObject.activeInHierarchy, "button_available_" + (button == null ? "null" : button.name));
            var corners = new Vector3[4]; button.GetComponent<RectTransform>().GetWorldCorners(corners);
            Vector2 point = (corners[0] + corners[2]) / 2;
            mouse.MakeCurrent();
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point }.WithButton(MouseButton.Left, true));
            yield return new WaitForSecondsRealtime(.12f);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point });
            yield return new WaitForSecondsRealtime(.15f);
        }
        private UnityEngine.UI.Button Named(string name)
        {
            foreach (var button in FindFirstObjectByType<GameHud>().InterfaceCanvas.GetComponentsInChildren<UnityEngine.UI.Button>(true))
                if (button.name == name && button.gameObject.activeInHierarchy) return button;
            throw new InvalidOperationException("Missing visible button " + name);
        }
        private IEnumerator Run()
        {
            LanguageService.Set(GameLanguage.Chinese);
            yield return new WaitForSeconds(.8f);
            Check(SceneManager.GetActiveScene().name == "Menu", "starts_in_briefing");
            yield return Screenshot("menu-zh");
            yield return Click(Named("English"));
            Check(LanguageService.Current == GameLanguage.English, "mouse_switches_language_in_menu");
            yield return Screenshot("menu-en");
            yield return Click(Named("Chinese"));
            yield return Click(Named("Start"));
            yield return new WaitForSeconds(.3f);
            RepairGame game = Game(); PlayerMotor motor = game.Player.GetComponent<PlayerMotor>();
            Check(game != null && game.Plan != null && game.Spawner.Modules.Length == 2, "random_station_loads");
            Check(FindFirstObjectByType<GameHud>().InterfaceCanvas.renderMode == RenderMode.ScreenSpaceOverlay, "ugui_canvas_running");
            yield return Screenshot("game-zh");
            Near(game, game.Player, true);
            Vector3 start = new Vector3(-4, .65f, -6);
            game.Player.position = start; game.Player.GetComponent<Rigidbody>().position = start; Physics.SyncTransforms();
            RobotVisual robot = FindFirstObjectByType<RobotVisual>();
            Keys(Key.D); yield return new WaitForSeconds(.25f);
            Check(Vector3.Dot(robot.Facing, Vector3.right) > .98f, "head_turns_right");
            Keys(Key.S); yield return new WaitForSeconds(.25f);
            Check(Vector3.Dot(robot.Facing, Vector3.back) > .98f, "head_turns_back");
            Keys(); yield return new WaitForSeconds(.16f);
            yield return Screenshot("robot-facing");
            Keys(Key.D); yield return new WaitForSeconds(.38f);
            Keys(Key.D, Key.LeftShift); yield return new WaitForSeconds(.1f);
            Check(motor.BoostsUsed == 1 && motor.PlanarSpeed <= 11.6f, "boost_force_and_speed_limit");
            BoostVisual fx = FindFirstObjectByType<BoostVisual>();
            Check(fx.IsThrustVisible && fx.Pulses == 1, "accepted_boost_plays_exhaust");
            yield return Screenshot("boost");
            Keys(Key.D); yield return new WaitForSeconds(.07f); Keys(Key.LeftShift); yield return new WaitForSeconds(.09f);
            Check(motor.BoostsUsed == 1 && fx.Pulses == 1, "cooldown_rejects_force_and_fx");
            Keys(); yield return new WaitForSeconds(.6f);
            Near(game, game.OtherModule.transform); yield return new WaitForSeconds(.12f);
            Check(game.Held == null, "proximity_does_not_pickup");
            yield return Click(FindFirstObjectByType<GameHud>().ActionButton);
            Check(game.Held == game.OtherModule, "mouse_button_picks_cargo");
            LanguageService.Set(GameLanguage.English);
            Check(!System.Text.RegularExpressions.Regex.IsMatch(game.Feedback, @"[\u4e00-\u9fff]"), "feedback_arguments_switch_language");
            LanguageService.Set(GameLanguage.Chinese);
            Check(FindFirstObjectByType<GameAudio>().GetComponent<AudioSource>().isPlaying, "interaction_audio");
            Near(game, game.Device); yield return new WaitForSeconds(.12f); yield return Press(Key.E);
            Check(game.WrongInstalls == 1 && game.Held == game.OtherModule && game.PenaltySeconds == 3, "wrong_cell_kept");
            yield return Screenshot("wrong-cell");
            Near(game, game.ReturnPad); yield return new WaitForSeconds(.12f);
            Check(game.Held != null && game.Returns == 0, "approach_does_not_return");
            yield return Click(FindFirstObjectByType<GameHud>().ActionButton);
            Check(game.Held == null && game.OtherModule.State == ModuleState.Returning, "mouse_starts_return_animation");
            yield return new WaitForSeconds(.65f); Check(game.OtherModule.State == ModuleState.OnShelf, "return_animation_finishes");
            Near(game, game.RequiredModule.transform); yield return new WaitForSeconds(.12f); yield return Press(Key.E);
            Check(game.Held == game.RequiredModule, "keyboard_picks_correct_cell");
            var plan = game.Plan; RepairModule held = game.Held;
            yield return Press(Key.Escape); float pausedTime = game.Elapsed; Vector3 pausedPosition = game.Player.position;
            yield return new WaitForSecondsRealtime(.25f);
            Check(GameFlow.IsPaused && game.Elapsed == pausedTime && game.Player.position == pausedPosition, "pause_freezes_time_and_physics");
            yield return Click(Named("English"));
            Check(ReferenceEquals(plan, game.Plan) && game.Held == held, "language_keeps_seed_and_inventory");
            yield return Screenshot("pause-en");
            yield return Click(Named("Resume")); yield return new WaitForSeconds(.1f);
            Check(!GameFlow.IsPaused && FindFirstObjectByType<GameHud>().DisplayedGoal.Contains("Deliver"), "english_goal_updates");
            yield return Screenshot("game-en");
            LanguageService.Set(GameLanguage.Chinese);
            yield return Press(Key.R); yield return new WaitForSeconds(.2f); game = Game(); motor = game.Player.GetComponent<PlayerMotor>();
            Near(game, game.RequiredModule.transform); yield return new WaitForSeconds(.12f); yield return Press(Key.E); RepairModule cell = game.Held;
            while (!game.ArcStrips[0].IsLive || Mathf.Repeat(game.Elapsed, ArcStrip.Cycle) > .5f) yield return new WaitForSeconds(.04f);
            Near(game, game.ArcStrips[0].transform, true); yield return new WaitForSeconds(.08f);
            Check(game.ArcHits == 1 && game.PenaltySeconds == 5 && game.Held == null && cell.State == ModuleState.Dropped && motor.StunRemaining > 0, "fixed_update_shock_stuns_and_drops");
            yield return Screenshot("arc-live-drop");
            yield return new WaitForSeconds(.55f);
            Check(game.ArcHits == 1 && !cell.GetComponent<Rigidbody>().isKinematic && cell.transform.position.y < 1.6f, "cargo_falls_without_repeat_penalty");
            yield return Press(Key.E); Check(game.Held == null, "live_cable_prevents_pickup");
            while (game.ArcStrips[0].IsLive) yield return new WaitForSeconds(.04f);
            yield return Screenshot("arc-safe");
            yield return Press(Key.E); Check(game.Held == cell && cell.State == ModuleState.Carried, "safe_window_recovers_cell");
            Near(game, game.Device); yield return new WaitForSeconds(.12f);
            Check(game.Phase == RepairPhase.NotInstalled, "approach_does_not_install");
            yield return Click(FindFirstObjectByType<GameHud>().ActionButton);
            Check(game.Phase == RepairPhase.Powering && game.Installs == 1, "mouse_install_starts_powering");
            yield return new WaitForSeconds(1.1f); yield return Screenshot("power-flow");
            yield return Press(Key.Escape); float powerProgress = game.PowerProgress;
            yield return new WaitForSecondsRealtime(.22f);
            Check(game.PowerProgress == powerProgress, "pause_freezes_power_animation");
            yield return Press(Key.Escape);
            while (game.Phase == RepairPhase.Powering) yield return new WaitForSeconds(.05f);
            int open = 0; foreach (DockGate dock in game.Gates) if (dock.IsOpen) open++;
            Check(open == 1 && game.ActiveDock.IsOpen && game.Routes.Progress == 1 && !game.ArcStrips[0].IsLive, "only_selected_dock_powered_and_fault_isolated");
            yield return Screenshot("powered");
            foreach (DockGate dock in game.Gates) if (dock != game.ActiveDock)
            { Near(game, dock.BoardingAnchor, true); yield return new WaitForSeconds(.1f); yield return Press(Key.E); Check(game.Phase == RepairPhase.Installed, "inactive_dock_rejects_" + dock.DockName[0]); }
            Near(game, game.Exit, true); yield return new WaitForSeconds(.25f);
            Check(game.Phase == RepairPhase.Installed && game.Completions == 0, "arrival_does_not_autoboard");
            yield return Click(FindFirstObjectByType<GameHud>().ActionButton);
            Check(game.Phase == RepairPhase.Complete && game.Completions == 1 && !game.Player.GetComponent<HumanInput>().enabled, "mouse_manual_boarding");
            yield return Screenshot("complete");
            yield return Press(Key.R); yield return new WaitForSeconds(.25f); game = Game();
            yield return Walk(game, game.RequiredModule.transform, 1.95f, true); yield return Press(Key.E);
            Check(game.Held == game.RequiredModule, "walked_rack_pickup");
            yield return Walk(game, game.Device, 1.95f, true); yield return Press(Key.E);
            while (game.Phase == RepairPhase.Powering) yield return new WaitForSeconds(.05f);
            Check(game.Phase == RepairPhase.Installed, "walked_generator_powered");
            yield return Walk(game, game.Exit, 1.1f, false); yield return new WaitForSeconds(.2f);
            Check(game.Phase == RepairPhase.Installed, "walked_arrival_still_requires_action");
            yield return Press(Key.E);
            Check(game.Phase == RepairPhase.Complete && game.ArcHits == 0, "full_wasd_mission_without_teleports");
            var photos = new HashSet<int>(); int last = game.Plan.ExitIndex;
            for (int i = 0; i < 10; i++)
            {
                yield return Press(Key.R); yield return new WaitForSeconds(.2f); game = Game();
                Check(game.Plan.ExitIndex != last && game.Held == null && game.Installs == 0 && game.ArcHits == 0
                    && game.Routes.Progress == 0 && game.Player.GetComponent<PlayerMotor>().CooldownRemaining == 0, "reset_clean_" + i);
                last = game.Plan.ExitIndex;
                if (photos.Add(last))
                {
                    yield return Install(game); yield return Screenshot("dock-" + game.ExitName[0]);
                    yield return CheckDockBoundary(game);
                }
            }
            Check(photos.Count == 3, "all_three_docks_photographed");
            yield return Press(Key.Escape); yield return Click(Named("PauseHelp")); yield return Screenshot("help-zh");
            yield return Click(Named("English")); yield return Screenshot("help-en");
            yield return Click(Named("Chinese")); yield return Click(Named("CloseHelp")); yield return Click(Named("Menu"));
            Check(SceneManager.GetActiveScene().name == "Menu", "return_to_menu");
        }
        private void DirectionKeys(Vector3 direction)
        { Keys(Mathf.Abs(direction.x) > .5f ? (direction.x > 0 ? Key.D : Key.A) : (direction.z > 0 ? Key.W : Key.S)); }
        private IEnumerator CheckDockBoundary(RepairGame game)
        {
            DockGate dock = game.ActiveDock; Rigidbody body = game.Player.GetComponent<Rigidbody>();
            PlayerMotor motor = body.GetComponent<PlayerMotor>(); motor.ResetMotion();
            Vector3 start = dock.transform.TransformPoint(new Vector3(0, .65f, -1.3f));
            body.position = start; game.Player.position = start; Physics.SyncTransforms();
            DirectionKeys(dock.transform.forward); yield return new WaitForSeconds(3);
            Keys(); yield return new WaitForSeconds(.15f);
            Vector3 local = dock.transform.InverseTransformPoint(body.position);
            Check(body.position.y > .45f && local.z > .7f && local.z < 3.1f, "open_dock_walks_to_safe_end_" + game.ExitName[0]);
            DirectionKeys(dock.transform.right); yield return new WaitForSeconds(1); Keys();
            yield return new WaitForSeconds(.15f); local = dock.transform.InverseTransformPoint(body.position);
            Check(body.position.y > .45f && Mathf.Abs(local.x) < 1.4f, "dock_side_rail_blocks_fall_" + game.ExitName[0]);
            Check(game.Phase == RepairPhase.Installed && game.Completions == 0, "walking_through_dock_does_not_autoboard_" + game.ExitName[0]);
            yield return Screenshot("dock-safe-" + game.ExitName[0]);
            // Brake before the centreline so residual sideways speed cannot lodge us against a doorpost.
            yield return WalkPoint(game, dock.transform.TransformPoint(new Vector3(0, .55f, 2.2f)));
            yield return WalkPoint(game, game.Exit.position);
            Keys(); yield return new WaitForSeconds(.2f);
            Check(Vector3.Distance(new Vector3(body.position.x, 0, body.position.z), new Vector3(game.Exit.position.x, 0, game.Exit.position.z)) < 1.45f,
                "can_walk_back_to_boarding_marker_" + game.ExitName[0]);
            yield return Press(Key.E); Check(game.Phase == RepairPhase.Complete, "manual_boarding_after_safe_dock_walk_" + game.ExitName[0]);
        }
        private IEnumerator Walk(RepairGame game, Transform target, float radius, bool avoidArcs)
        {
            List<Vector3> path = FindPath(game, target.position, radius, avoidArcs);
            foreach (Vector3 goal in path)
                yield return WalkPoint(game, goal);
            Keys(); yield return new WaitForSeconds(0.2f);
        }
        private IEnumerator WalkPoint(RepairGame game, Vector3 goal)
        {
            Rigidbody body = game.Player.GetComponent<Rigidbody>(); float deadline = Time.realtimeSinceStartup + 8;
            while (true)
            {
                if (game.Phase == RepairPhase.Complete) { Keys(); yield break; }
                Vector2 offset = new Vector2(goal.x - body.position.x, goal.z - body.position.z);
                Vector2 velocity = new Vector2(body.linearVelocity.x, body.linearVelocity.z);
                if (offset.magnitude < .24f && velocity.magnitude < .75f) break;
                if (Time.realtimeSinceStartup > deadline)
                    throw new InvalidOperationException("WASD navigation stalled: " + body.position + " toward " + goal);
                float allowed = Mathf.Sqrt(2 * 13 * Mathf.Max(0, offset.magnitude - .08f));
                if (Vector2.Dot(velocity, offset.normalized) > allowed) Keys();
                else
                {
                    var keys = new List<Key>();
                    if (offset.x > .12f) keys.Add(Key.D); else if (offset.x < -.12f) keys.Add(Key.A);
                    if (offset.y > .12f) keys.Add(Key.W); else if (offset.y < -.12f) keys.Add(Key.S);
                    Keys(keys.ToArray());
                }
                yield return new WaitForFixedUpdate();
            }
            Keys();
        }
        private List<Vector3> FindPath(RepairGame game, Vector3 target, float radius, bool avoidArcs)
        {
            const int count = 37; const float step = 0.5f;
            Vector2Int Index(Vector3 p) => new Vector2Int(Mathf.Clamp(Mathf.RoundToInt((p.x + 9) / step), 0, count - 1),
                Mathf.Clamp(Mathf.RoundToInt((p.z + 9) / step), 0, count - 1));
            Vector3 Point(Vector2Int p) => new Vector3(-9 + p.x * step, 0.55f, -9 + p.y * step);
            Vector2Int origin = Index(game.Player.position), end = origin;
            var queue = new Queue<Vector2Int>(); var parents = new Dictionary<Vector2Int, Vector2Int>();
            queue.Enqueue(origin); parents[origin] = origin; bool found = false;
            while (queue.Count > 0)
            {
                Vector2Int cell = queue.Dequeue(); Vector3 position = Point(cell);
                if (Vector2.Distance(new Vector2(position.x, position.z), new Vector2(target.x, target.z)) < radius)
                { end = cell; found = true; break; }
                foreach (Vector2Int direction in new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right })
                {
                    Vector2Int next = cell + direction;
                    if (next.x < 0 || next.y < 0 || next.x >= count || next.y >= count || parents.ContainsKey(next)) continue;
                    Vector3 candidate = Point(next); bool blocked = false;
                    foreach (Collider collider in Physics.OverlapSphere(candidate, 0.53f, ~0, QueryTriggerInteraction.Ignore))
                        if (collider.transform != game.Player && !collider.transform.IsChildOf(game.Player)) { blocked = true; break; }
                    if (avoidArcs) foreach (ArcStrip strip in game.ArcStrips)
                        blocked |= strip.Contains(candidate) || strip.Contains(candidate + Vector3.right * 0.33f)
                            || strip.Contains(candidate + Vector3.left * 0.33f) || strip.Contains(candidate + Vector3.forward * 0.33f)
                            || strip.Contains(candidate + Vector3.back * 0.33f);
                    if (blocked) continue; parents[next] = cell; queue.Enqueue(next);
                }
            }
            if (!found) throw new InvalidOperationException("No walking route to " + target);
            var nodes = new List<Vector2Int>();
            while (end != origin) { nodes.Add(end); end = parents[end]; } nodes.Reverse();
            var path = new List<Vector3>();
            for (int i = 0; i < nodes.Count; i++)
                if (i == nodes.Count - 1 || i == 0 || nodes[i] - nodes[i - 1] != nodes[i + 1] - nodes[i])
                    path.Add(Point(nodes[i]));
            return path;
        }
        private void WriteReport(string result, string error)
        {
            File.WriteAllText(Path.Combine(output, "standalone-validation.json"), JsonUtility.ToJson(new Report {
                unityVersion = Application.unityVersion, result = result, error = error, checks = checks.ToArray(),
                scope = "Actual Windows player, synthetic keyboard, real physics/audio/rendering and scene resets. Focused state checks teleport between stations; one full delivery/boarding run navigates only with WASD, without teleports." }, true));
            Debug.Log("STATION_STANDALONE_" + result.ToUpperInvariant() + ": " + checks.Count + " checks");
        }
        [Serializable] private class Report { public string unityVersion, result, error, scope; public string[] checks; }
    }
}
