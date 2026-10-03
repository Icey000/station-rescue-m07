using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
namespace UnityAgentLab.Editor
{
    public static class StationValidationV2
    {
        private static readonly List<string> checks = new List<string>();
        private static void Check(bool value, string name) { if (!value) throw new InvalidOperationException("V2 check failed: " + name); checks.Add(name); }
        private static RepairGame Fresh(int seed = 0)
        {
            GameFlow.SetPaused(false); EditorSceneManager.OpenScene("Assets/Scenes/Game.unity");
            RepairGame game = UnityEngine.Object.FindFirstObjectByType<RepairGame>(); game.Initialize(seed); return game;
        }
        private static void Near(RepairGame game, Transform target, float offset = 1.8f)
        {
            Vector3 p = target.position + Vector3.back * offset; p.y = .65f;
            Rigidbody body = game.Player.GetComponent<Rigidbody>(); body.position = p; game.Player.position = p; body.linearVelocity = Vector3.zero; Physics.SyncTransforms();
        }
        private static void Power(RepairGame game)
        {
            Near(game, game.RequiredModule.transform); Check(game.TryInteract(), "pick_for_power_" + game.Plan.Seed);
            Near(game, game.Device); Check(game.TryInteract(), "insert_for_power_" + game.Plan.Seed);
            for (int i = 0; i < 140; i++) game.Step(.02f);
            Check(game.Phase == RepairPhase.Installed && game.ActiveDock.IsOpen, "power_sequence_finishes_" + game.Plan.Seed);
        }
        private static void Drive(PlayerMotor motor, Vector3 direction, float duration)
        {
            motor.SetMoveInput(new Vector2(direction.x, direction.z));
            for (int i = 0; i < Mathf.CeilToInt(duration / .02f); i++) { motor.Step(.02f); Physics.Simulate(.02f); }
        }
        [MenuItem("UnityAgentLab/Validate Open Dock Boundaries")]
        public static void ValidateOpenDockBoundaries()
        {
            SimulationMode previous = Physics.simulationMode;
            try
            {
                Physics.simulationMode = SimulationMode.Script;
                for (int index = 0; index < 3; index++)
                {
                    int seed = 0; while (StationPlan.Create(seed, 3, 4).ExitIndex != index) seed++;
                    RepairGame game = Fresh(seed); Power(game); DockGate dock = game.ActiveDock;
                    Rigidbody body = game.Player.GetComponent<Rigidbody>(); PlayerMotor motor = body.GetComponent<PlayerMotor>();
                    Vector3 start = dock.transform.TransformPoint(new Vector3(0, .65f, -1.3f));
                    body.position = start; game.Player.position = start; motor.ResetMotion(); Physics.SyncTransforms();
                    Drive(motor, dock.transform.forward, 3);
                    Vector3 local = dock.transform.InverseTransformPoint(body.position);
                    Debug.Log("DOCK_BOUNDARY_POSITION: " + index + " " + body.position + " local=" + local);
                    Check(body.position.y > .45f, "open_dock_has_walkable_support_" + index);
                    Check(local.z > .7f && local.z < 3.1f, "walked_through_gate_and_stopped_at_end_" + index);
                    Drive(motor, dock.transform.right, 1);
                    local = dock.transform.InverseTransformPoint(body.position);
                    Check(body.position.y > .45f && Mathf.Abs(local.x) < 1.4f, "dock_side_rail_prevents_fall_" + index);
                    Check(game.Phase == RepairPhase.Installed && game.Completions == 0, "walking_on_dock_does_not_board_" + index);
                    motor.ResetMotion(); Near(game, game.Exit, 0);
                    Check(game.TryInteract() && game.Phase == RepairPhase.Complete, "manual_boarding_still_works_after_boundary_walk_" + index);
                }
                Debug.Log("OPEN_DOCK_BOUNDARIES_PASSED: all three powered docks");
            }
            finally { Physics.simulationMode = previous; EditorSceneManager.OpenScene("Assets/Scenes/Game.unity"); }
        }
        [MenuItem("UnityAgentLab/Validate Presentation V2")]
        public static void Run()
        {
            checks.Clear(); GameLanguage previousLanguage = LanguageService.Current;
            SimulationMode previous = Physics.simulationMode;
            try
            {
                ProjectBootstrap.ValidatePhysicsAt("Assets/Scenes/Baselines/Game_A1A2.unity");
                Check(true, "preserved_original_physics_contracts");
                Physics.simulationMode = SimulationMode.Script;
                var docks = new HashSet<int>(); var models = new HashSet<ModuleKind>(); var shelves = new HashSet<int>();
                for (int seed = 0; seed < 120; seed++)
                {
                    StationPlan a = StationPlan.Create(seed, 3, 4), b = StationPlan.Create(seed, 3, 4);
                    Check(a.ExitIndex == b.ExitIndex && a.RequiredKind == b.RequiredKind && a.BlueShelf == b.BlueShelf && a.OrangeShelf == b.OrangeShelf && a.BlueShelf != a.OrangeShelf, "reproducible_disjoint_seed_" + seed);
                    docks.Add(a.ExitIndex); models.Add(a.RequiredKind); shelves.Add(a.BlueShelf);
                }
                Check(docks.Count == 3 && models.Count == 2 && shelves.Count == 4, "random_coverage");
                RepairGame game = Fresh(4); var plan = game.Plan;
                Check(UnityEngine.Object.FindFirstObjectByType<GameHud>().InterfaceCanvas != null && game.GetComponentsInChildren<Canvas>().Length == 1, "serialized_canvas_interface");
                Check(game.GetComponentsInChildren<PowerRouteController>().Length == 1, "single_power_route_controller");
                Near(game, game.OtherModule.transform); Check(game.Held == null, "approach_does_not_pickup");
                Check(game.TryInteract() && game.Held == game.OtherModule, "pickup_wrong_cell");
                Near(game, game.Device); Check(!game.TryInteract() && game.WrongInstalls == 1 && game.PenaltySeconds == 3 && game.Held == game.OtherModule, "wrong_cell_retained");
                Near(game, game.ReturnPad); Check(game.Held != null && game.Returns == 0, "approach_does_not_return");
                RepairModule returned = game.Held;
                Check(game.TryInteract() && game.Held == null && returned.State == ModuleState.Returning, "manual_animated_return");
                returned.StepReturn(.8f); Check(returned.State == ModuleState.OnShelf, "return_finishes_at_shelf");
                Near(game, game.RequiredModule.transform); Check(game.TryInteract(), "pickup_correct_cell");
                Near(game, game.Device); Check(game.Phase == RepairPhase.NotInstalled, "approach_does_not_install");
                Check(game.TryInteract() && game.Phase == RepairPhase.Powering && game.Installs == 1, "insertion_enters_powering");
                Check(!game.TryInteract() && game.Installs == 1, "insertion_idempotent");
                Near(game, game.Exit, 0); Check(!game.TryInteract(), "cannot_board_during_startup");
                game.Step(1.5f); Check(game.Phase == RepairPhase.Powering && game.Routes.Progress > 0 && game.Routes.Progress < 1 && !game.ActiveDock.IsOpen, "visible_energy_propagates_before_gate_ready");
                GameFlow.SetPaused(true); float elapsed = game.Elapsed, progress = game.PowerProgress;
                game.Step(1); Check(game.Elapsed == elapsed && game.PowerProgress == progress && !game.TryInteract(), "pause_blocks_clock_power_and_actions");
                GameFlow.SetPaused(false);
                LanguageService.Set(GameLanguage.English); Check(game.Goal.Contains("power") && ReferenceEquals(plan, game.Plan), "language_preserves_run");
                LanguageService.Set(GameLanguage.Chinese); Check(game.Goal.Contains("供电"), "chinese_goal");
                for (int i = 0; i < 80; i++) game.Step(.02f);
                Check(game.Phase == RepairPhase.Installed && game.Routes.Progress == 1, "power_complete");
                int count = 0; foreach (DockGate dock in game.Gates) if (dock.IsOpen) count++;
                Check(count == 1 && game.ActiveDock.IsOpen, "one_random_gate_ready");
                Near(game, game.Exit, 0); game.Step(.3f);
                Check(game.Phase == RepairPhase.Installed && game.Completions == 0, "approach_does_not_board");
                Check(game.TryInteract() && game.Phase == RepairPhase.Complete && game.Completions == 1, "manual_boarding");
                Check(!game.TryInteract() && game.Completions == 1, "boarding_idempotent");

                game = Fresh(0); Near(game, game.RequiredModule.transform); game.TryInteract(); RepairModule cell = game.Held;
                Near(game, game.ArcStrips[0].transform, 0);
                Check(game.TryArcContact(game.ArcStrips[0]) && game.Held == null && cell.State == ModuleState.Dropped && game.PenaltySeconds == 5, "fault_drops_real_cargo");
                Check(!cell.GetComponent<Rigidbody>().isKinematic && !cell.GetComponent<Collider>().isTrigger, "dropped_cell_has_solid_physics");
                Check(game.Player.GetComponent<PlayerMotor>().StunRemaining > 0 && !game.TryArcContact(game.ArcStrips[0]) && game.ArcHits == 1, "stun_and_single_contact_penalty");
                for (int i = 0; i < 30; i++) { game.Player.GetComponent<PlayerMotor>().Step(.02f); Physics.Simulate(.02f); }
                Check(cell.transform.position.y < 1.75f && cell.transform.position.y > .2f, "cargo_falls_and_stays_on_deck");
                Check(!game.TryInteract() && game.GetInteractionContext().Reason.Contains("放电"), "live_region_blocks_pickup");
                game.Step(2.1f); Check(game.TryInteract() && game.Held == cell, "recover_dropped_cell_in_safe_window");
                Check(cell.State == ModuleState.Carried && cell.GetComponent<Rigidbody>().isKinematic, "recovered_cell_stops_physics");

                game = Fresh(8); Near(game, game.Player, 0);
                Rigidbody body = game.Player.GetComponent<Rigidbody>(); PlayerMotor motor = body.GetComponent<PlayerMotor>();
                body.position = new Vector3(-3, .65f, -6); Physics.SyncTransforms();
                motor.ResetMotion(); motor.SetMoveInput(Vector2.right);
                RobotVisual visual = UnityEngine.Object.FindFirstObjectByType<RobotVisual>(); visual.StepVisual(.5f);
                Check(Vector3.Dot(visual.Facing, Vector3.right) > .99f, "head_faces_right");
                motor.SetMoveInput(Vector2.up); visual.StepVisual(.5f);
                Check(Vector3.Dot(visual.Facing, Vector3.forward) > .99f, "head_faces_forward");
                motor.SetMoveInput(Vector2.zero); Vector3 direction = visual.Facing; visual.StepVisual(.3f);
                Check(Vector3.Distance(direction, visual.Facing) < .01f, "idle_retains_heading");
                for (int i = 0; i < 40; i++) { motor.SetMoveInput(Vector2.right); motor.Step(.02f); Physics.Simulate(.02f); }
                float normal = motor.PlanarSpeed; motor.RequestBoost(); motor.Step(.02f); Physics.Simulate(.02f);
                Check(motor.PlanarSpeed > normal + 4 && motor.PlanarSpeed <= 11.6f && motor.BoostVisualRemaining > 0, "physical_boost_with_speed_cap_and_visual_signal");
                motor.RequestBoost(); motor.Step(.02f); Physics.Simulate(.02f); Check(motor.BoostsUsed == 1, "cooldown_rejects_repeat");
                motor.Stun(.45f); motor.RequestBoost(); motor.Step(.02f);
                Check(motor.BoostsUsed == 1 && motor.BoostVisualRemaining == 0, "stun_cancels_boost");
                for (int seed = 0; seed < 9; seed++)
                {
                    game = Fresh(seed); Power(game);
                    foreach (DockGate dock in game.Gates) if (dock != game.ActiveDock)
                    { Near(game, dock.BoardingAnchor, 0); Check(!game.TryInteract(), "inactive_dock_rejected_" + seed + "_" + dock.DockName[0]); }
                    CheckRoutes(game); Near(game, game.Exit, 0); Check(game.TryInteract(), "seed_mission_complete_" + seed);
                }
                ValidateOpenDockBoundaries();
                game = Fresh(2);
                Check(game.Held == null && game.ArcHits == 0 && game.Installs == 0 && game.Routes.Progress == 0 && game.Player.GetComponent<PlayerMotor>().CooldownRemaining == 0, "fresh_run_cleans_all_state");
                Directory.CreateDirectory("docs");
                File.WriteAllText("docs/station-v2_1-validation.json", JsonUtility.ToJson(new Report { result = "passed", unityVersion = Application.unityVersion, checks = checks.ToArray() }, true));
                Debug.Log("STATION_V2_VALIDATION_PASSED: " + checks.Count);
            }
            finally { GameFlow.SetPaused(false); LanguageService.Set(previousLanguage); Physics.simulationMode = previous; EditorSceneManager.OpenScene("Assets/Scenes/Game.unity"); }
        }
        private static void CheckRoutes(RepairGame game)
        {
            foreach (DockGate dock in game.Gates) { dock.SetPowered(true, true); dock.StepAnimation(1); }
            Near(game, game.Player, 0); game.Player.position = new Vector3(30, .65f, 30); game.Player.GetComponent<Rigidbody>().position = game.Player.position; Physics.SyncTransforms();
            const int count = 37; const float step = .5f;
            var queue = new Queue<Vector2Int>(); var seen = new HashSet<Vector2Int>(); var origin = new Vector2Int(17, 3); queue.Enqueue(origin); seen.Add(origin);
            Vector3 Point(Vector2Int p) => new Vector3(-9 + p.x * step, .55f, -9 + p.y * step);
            while (queue.Count > 0)
            {
                Vector2Int p = queue.Dequeue();
                foreach (Vector2Int d in new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right })
                {
                    Vector2Int n = p + d; if (n.x < 0 || n.y < 0 || n.x >= count || n.y >= count || seen.Contains(n)) continue;
                    Vector3 world = Point(n); bool blocked = Physics.CheckSphere(world, .53f, ~0, QueryTriggerInteraction.Ignore);
                    foreach (ArcStrip strip in game.ArcStrips) blocked |= strip.Contains(world);
                    if (blocked) continue; seen.Add(n); queue.Enqueue(n);
                }
            }
            bool Reach(Vector3 p, float radius) { foreach (var n in seen) if (Vector2.Distance(new Vector2(Point(n).x, Point(n).z), new Vector2(p.x,p.z)) < radius) return true; return false; }
            Check(Reach(game.Device.position, 2.1f) && Reach(game.ReturnPad.position, 2.1f), "safe_machine_routes_" + game.Plan.Seed);
            foreach (RepairModule module in game.Spawner.Modules) if (module.State == ModuleState.OnShelf) Check(Reach(module.transform.position, 2.1f), "safe_cell_route_" + game.Plan.Seed);
            foreach (Transform t in game.GetComponentsInChildren<Transform>()) if (t.name == "CellHome") Check(Reach(t.position, 2.1f), "safe_rack_" + game.Plan.Seed + "_" + t.parent.name);
            foreach (DockGate dock in game.Gates) Check(Reach(dock.BoardingAnchor.position, 1.4f), "dock_reachable_" + game.Plan.Seed + "_" + dock.DockName[0]);
        }
        [Serializable] private class Report { public string result, unityVersion; public string[] checks; }
    }
}
