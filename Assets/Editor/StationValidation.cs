using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace UnityAgentLab.Editor
{
    public static class StationValidation
    {
        private static readonly List<string> Checks = new List<string>();
        private static void Check(bool value, string name)
        { if (!value) throw new InvalidOperationException("Station check failed: " + name); Checks.Add(name); }
        private static RepairGame Fresh(int? seed = null)
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Game.unity");
            RepairGame game = UnityEngine.Object.FindFirstObjectByType<RepairGame>();
            game.Initialize(seed); return game;
        }
        private static void Near(RepairGame game, Transform target, bool center = false)
        {
            Vector3 pos = target.position + (center ? Vector3.zero : Vector3.back * 1.8f); pos.y = 0.65f;
            SetPosition(game, pos);
        }
        private static void SetPosition(RepairGame game, Vector3 pos)
        {
            Rigidbody body = game.Player.GetComponent<Rigidbody>(); body.position = pos; game.Player.position = pos;
            body.linearVelocity = Vector3.zero; Physics.SyncTransforms();
        }
        private static void Install(RepairGame game)
        {
            Near(game, game.RequiredModule.transform);
            if (!game.TryInteract()) throw new InvalidOperationException("Cannot pick required cell for seed " + game.Plan.Seed);
            Near(game, game.Device);
            if (!game.TryInteract()) throw new InvalidOperationException("Cannot install required cell for seed " + game.Plan.Seed);
        }
        [MenuItem("UnityAgentLab/Validate Station Rescue")]
        public static void Run()
        {
            Checks.Clear();
            StationBuilder.Upgrade();
            ProjectBootstrap.ValidatePhysicsAt("Assets/Scenes/Baselines/Game_A1A2.unity");
            Check(true, "eight_original_physics_contracts_on_preserved_baseline");
            SimulationMode previous = Physics.simulationMode;
            try
            {
                Physics.simulationMode = SimulationMode.Script;
                var exits = new HashSet<int>(); var requests = new HashSet<ModuleKind>(); var shelves = new HashSet<int>();
                bool stable = true, disjoint = true;
                for (int seed = 0; seed < 120; seed++)
                {
                    StationPlan plan = StationPlan.Create(seed, 3, 4);
                    StationPlan repeat = StationPlan.Create(seed, 3, 4);
                    exits.Add(plan.ExitIndex); requests.Add(plan.RequiredKind); shelves.Add(plan.BlueShelf);
                    stable &= plan.ExitIndex == repeat.ExitIndex && plan.BlueShelf == repeat.BlueShelf
                        && plan.OrangeShelf == repeat.OrangeShelf && plan.RequiredKind == repeat.RequiredKind;
                    disjoint &= plan.BlueShelf != plan.OrangeShelf;
                }
                Check(exits.Count == 3 && requests.Count == 2 && shelves.Count == 4, "120_seed_samples_cover_all_docks_models_and_shelves");
                Check(stable && disjoint, "seed_is_reproducible_and_cells_never_share_a_shelf");
                RepairGame game = Fresh(0);
                Check(game.Gates.Length == 3 && game.ArcStrips.Length == 2, "three_real_boarding_gates_and_two_shortcuts");
                Check(game.Spawner.Modules.Length == 2 && game.RequiredModule.UniqueId != game.OtherModule.UniqueId, "two_unique_prefab_cells");
                StationPlan firstPlan = game.Plan; game.Initialize(200);
                Check(ReferenceEquals(firstPlan, game.Plan), "layout_never_rerolls_during_a_run");
                Check(Array.TrueForAll(game.Gates, dock => !dock.IsOpen), "all_hatches_closed_before_power");
                Near(game, game.Exit, true); Check(!game.TryComplete(), "early_boarding_is_rejected");
                Near(game, game.Device); Check(!game.TryInteract() && game.Installs == 0, "empty_hands_cannot_power_generator");
                RepairModule wrong = game.OtherModule; Vector3 home = wrong.transform.position;
                Near(game, wrong.transform);
                Check(game.Held == null && wrong.State == ModuleState.OnShelf, "proximity_is_not_pickup");
                Check(game.TryInteract() && game.Held == wrong, "either_model_is_carryable");
                wrong.SendMessage("LateUpdate");
                Check(Vector3.Distance(wrong.transform.position, game.Player.position + Vector3.up * 1.35f) < 0.01f,
                    "cargo_follows_above_rolling_robot");
                Near(game, game.Device);
                Check(!game.TryInteract() && game.Held == wrong && wrong.State == ModuleState.Carried
                    && game.WrongInstalls == 1 && game.PenaltySeconds == 3, "wrong_type_kept_with_visible_score_penalty");
                Near(game, game.ReturnPad);
                Check(game.TryInteract() && game.Held == null && wrong.State == ModuleState.OnShelf
                    && Vector3.Distance(wrong.transform.position, home) < 0.01f, "reclaim_restores_random_home");
                RepairModule right = game.RequiredModule; Near(game, right.transform);
                Check(game.TryInteract() && game.Held == right, "requested_type_pickup");
                Check(!game.TryInteract() && game.Held == right, "no_duplicate_pickup");
                Near(game, game.Device);
                Check(game.TryInteract() && game.Installs == 1 && right.State == ModuleState.Installed
                    && game.Held == null, "requested_cell_installs_exactly_once");
                Check(!game.TryInteract() && game.Installs == 1, "repeated_install_does_not_rescore");
                int opened = 0; foreach (DockGate gate in game.Gates) if (gate.IsOpen) opened++;
                Check(opened == 1 && game.ActiveDock.IsOpen, "only_selected_hatch_opens_on_power");
                foreach (DockGate gate in game.Gates) if (gate != game.ActiveDock)
                { Near(game, gate.transform, true); Check(!game.TryComplete(), "closed_" + gate.DockName[0] + "_cannot_win"); }
                Near(game, game.Exit, true);
                Check(game.TryComplete() && game.Completions == 1, "selected_hatch_completes");
                Check(!game.TryComplete() && game.Completions == 1, "completion_is_idempotent");
                Check(!game.Player.GetComponent<HumanInput>().enabled && game.Player.GetComponent<Rigidbody>().isKinematic, "win_freezes_player");
                Check(game.Stars == 3 && game.MissionTime == 3, "score_includes_mistake_seconds");

                game = Fresh(17); Near(game, game.RequiredModule.transform); game.TryInteract();
                RepairModule carried = game.Held; Near(game, game.ArcStrips[0].transform, true);
                Check(game.TryArcContact(game.ArcStrips[0]) && game.ArcHits == 1 && game.PenaltySeconds == 5
                    && game.Held == null && carried.State == ModuleState.OnShelf, "live_bus_returns_cell_without_deleting_it");
                Check(!game.TryArcContact(game.ArcStrips[0]) && game.ArcHits == 1, "empty_robot_is_not_repeatedly_penalized");
                Check(game.ArcStrips[0].IsLiveAt(0) && !game.ArcStrips[0].IsLiveAt(2.1f)
                    && game.ArcStrips[0].IsLiveAt(4.6f), "bus_alternates_between_readable_live_and_safe_windows");
                Near(game, game.RequiredModule.transform); game.TryInteract();
                SetPosition(game, new Vector3(-1.6f, 0.65f, 1.5f));
                Check(!game.TryArcContact(game.ArcStrips[0]) && game.Held != null, "outer_detour_keeps_cargo");

                game = Fresh(2);
                Rigidbody body = game.Player.GetComponent<Rigidbody>(); PlayerMotor motor = body.GetComponent<PlayerMotor>();
                motor.ResetMotion(); SetPosition(game, new Vector3(0, 0.65f, 0)); motor.SetMoveInput(Vector2.up);
                for (int i = 0; i < 50; i++) { motor.Step(0.02f); Physics.Simulate(0.02f); }
                float ordinary = body.linearVelocity.z; body.rotation = Quaternion.Euler(73, 137, 26);
                motor.RequestBoost(); motor.Step(0.02f); Physics.Simulate(0.02f);
                Check(body.linearVelocity.z > ordinary + 4 && Mathf.Abs(body.linearVelocity.x) < 0.1f,
                    "same_movement_speed_and_world_direction_boost_in_station");
                motor.RequestBoost(); motor.Step(0.02f); Physics.Simulate(0.02f);
                Check(motor.BoostsUsed == 1, "boost_cooldown_rejects_repeat");
                for (int i = 0; i < 140; i++) { motor.Step(0.02f); Physics.Simulate(0.02f); }
                Check(body.position.z < 9.4f && body.position.y > 0.42f, "station_wall_stops_boost");

                game = Fresh(6); Install(game);
                CheckRoutes(game);
                int last = -1; bool changed = true; var liveExits = new HashSet<int>();
                for (int i = 0; i < 12; i++)
                {
                    game = Fresh(); changed &= game.Plan.ExitIndex != last; last = game.Plan.ExitIndex; liveExits.Add(last);
                    Check(game.Phase == RepairPhase.NotInstalled && game.Held == null && game.Installs == 0
                        && game.Returns == 0 && game.PenaltySeconds == 0 && game.ArcHits == 0
                        && game.Spawner.Modules[0].State == ModuleState.OnShelf
                        && game.Player.GetComponent<HumanInput>().enabled, "new_run_clean_" + (i + 1));
                }
                Check(changed && liveExits.Count >= 2, "ordinary_restarts_choose_a_different_exit_from_previous_run");
                for (int seed = 0; seed < 6; seed++)
                {
                    game = Fresh(seed); Install(game); Near(game, game.Exit, true);
                    Check(game.TryComplete(), "complete_random_request_and_dock_seed_" + seed);
                }
                Directory.CreateDirectory("docs");
                File.WriteAllText("docs/station-validation.json", JsonUtility.ToJson(new Report {
                    unityVersion = Application.unityVersion, result = "passed", checks = Checks.ToArray(),
                    scope = "Editor real physics, 120 deterministic layout samples, interaction/recovery and collision-grid reachability; standalone keyboard/render checks separate" }, true));
                Debug.Log("STATION_VALIDATION_PASSED: " + Checks.Count + " checks");
            }
            finally { Physics.simulationMode = previous; EditorSceneManager.OpenScene("Assets/Scenes/Game.unity"); }
        }

        private static void CheckRoutes(RepairGame game)
        {
            // Flood the actual collider layout at robot height, with a small clearance margin.
            // Treat both arc strips as blocked: all cells/machines remain reachable by a safe detour.
            foreach (DockGate gate in game.Gates) gate.SetPowered(true, true);
            SetPosition(game, new Vector3(30, 0.65f, 30));
            Physics.SyncTransforms();
            const int count = 37; const float step = 0.5f;
            var seen = new bool[count, count]; var queue = new Queue<Vector2Int>();
            Vector3 start = new Vector3(-0.5f, 0.55f, -7.5f);
            Vector2Int origin = Index(start); queue.Enqueue(origin); seen[origin.x, origin.y] = true;
            Vector2Int[] directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
            while (queue.Count > 0)
            {
                Vector2Int cell = queue.Dequeue();
                foreach (Vector2Int direction in directions)
                {
                    Vector2Int next = cell + direction;
                    if (next.x < 0 || next.y < 0 || next.x >= count || next.y >= count || seen[next.x, next.y]) continue;
                    Vector3 point = new Vector3(-9 + next.x * step, 0.55f, -9 + next.y * step);
                    bool blocked = Physics.CheckSphere(point, 0.53f, ~0, QueryTriggerInteraction.Ignore);
                    foreach (ArcStrip strip in game.ArcStrips) blocked |= strip.Contains(point);
                    if (blocked) continue;
                    seen[next.x, next.y] = true; queue.Enqueue(next);
                }
            }
            bool Approachable(Vector3 target, float radius)
            {
                for (int x = 0; x < count; x++) for (int z = 0; z < count; z++)
                    if (seen[x, z] && Vector2.Distance(new Vector2(-9 + x * step, -9 + z * step),
                        new Vector2(target.x, target.z)) < radius) return true;
                return false;
            }
            Check(Approachable(game.Device.position, 2.1f) && Approachable(game.ReturnPad.position, 2.1f),
                "safe_route_reaches_generator_and_reclaim_using_actual_colliders");
            foreach (RepairModule module in game.Spawner.Modules)
                Check(Approachable(module.transform.position, 2.1f), "safe_route_reaches_" + module.UniqueId);
            foreach (DockGate gate in game.Gates)
                Check(Approachable(gate.transform.position, 0.85f), "route_reaches_open_dock_" + gate.DockName[0]);
            // The four racks are fixed, while the cells change homes between runs.
            foreach (Transform rack in game.transform.GetComponentsInChildren<Transform>())
                if (rack.name == "CellHome") Check(Approachable(rack.position, 2.1f), "safe_route_reaches_charging_rack_" + rack.parent.name);
        }
        private static Vector2Int Index(Vector3 pos)
            => new Vector2Int(Mathf.RoundToInt((pos.x + 9) / 0.5f), Mathf.RoundToInt((pos.z + 9) / 0.5f));
        [Serializable] private class Report { public string unityVersion, result, scope; public string[] checks; }
    }
}
