using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace UnityAgentLab.Editor
{
    public static class PrototypeValidation
    {
        private static readonly List<string> Checks = new List<string>();
        private static void Check(bool passed, string name)
        {
            if (!passed) throw new InvalidOperationException("Prototype check failed: " + name);
            Checks.Add(name);
        }
        private static void Near(RepairGame game, Transform target, bool center = false)
        {
            Rigidbody body = game.Player.GetComponent<Rigidbody>();
            Vector3 point = target.position + (center ? Vector3.zero : Vector3.back * 1.8f);
            point.y = 0.65f; body.position = point; game.Player.position = point;
            body.linearVelocity = Vector3.zero; Physics.SyncTransforms();
        }
        private static RepairGame Fresh()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Game.unity");
            RepairGame game = UnityEngine.Object.FindFirstObjectByType<RepairGame>();
            game.Initialize(); return game;
        }
        [MenuItem("UnityAgentLab/Validate Magnet Prototype")]
        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Game.unity");
            if (GameObject.Find("StationWorld") != null) { StationValidation.Run(); return; }
            Checks.Clear();
            PrototypeBuilder.Upgrade();
            // Repeat the existing physics contract after adding obstacles and boost support.
            ProjectBootstrap.ValidatePhysics();
            Check(true, "existing_eight_physics_checks");
            SimulationMode previous = Physics.simulationMode;
            try
            {
                Physics.simulationMode = SimulationMode.Script;
                RepairGame game = Fresh();
                Rigidbody body = game.Player.GetComponent<Rigidbody>();
                PlayerMotor motor = body.GetComponent<PlayerMotor>();
                motor.ResetMotion(); body.position = new Vector3(0, 0.6f, 0); Physics.SyncTransforms();
                motor.SetMoveInput(Vector2.up);
                for (int i = 0; i < 60; i++) { motor.Step(0.02f); Physics.Simulate(0.02f); }
                float ordinary = body.linearVelocity.z;
                body.rotation = Quaternion.Euler(73, 137, 26);
                motor.RequestBoost(); motor.Step(0.02f); Physics.Simulate(0.02f);
                Check(body.linearVelocity.z > ordinary + 4f && Mathf.Abs(body.linearVelocity.x) < 0.1f,
                    "boost_impulse_uses_world_direction");
                motor.RequestBoost(); motor.Step(0.02f); Physics.Simulate(0.02f);
                Check(motor.BoostsUsed == 1, "cooldown_rejects_second_request");
                for (int i = 0; i < 70; i++) { motor.Step(0.02f); Physics.Simulate(0.02f); }
                motor.RequestBoost(); motor.Step(0.02f); Physics.Simulate(0.02f);
                Check(motor.BoostsUsed == 2, "boost_ready_after_cooldown");
                for (int i = 0; i < 150; i++) { motor.Step(0.02f); Physics.Simulate(0.02f); }
                Check(body.position.z < 9.4f && body.position.y > 0.42f, "boost_does_not_cross_wall");

                game = Fresh();
                Check(game.Spawner.Modules.Length == 2 && game.Spawner.Modules[0].UniqueId != game.Spawner.Modules[1].UniqueId,
                    "two_unique_prefab_modules");
                Near(game, game.Exit, true);
                Check(!game.TryComplete() && game.Completions == 0, "early_exit_is_not_success");
                Near(game, game.Device);
                Check(!game.TryInteract() && game.Installs == 0, "empty_hands_cannot_install");
                RepairModule red = game.Spawner.Modules[1]; Near(game, red.transform);
                Check(game.Held == null && red.State == ModuleState.OnShelf, "proximity_does_not_auto_pickup");
                Check(game.TryInteract() && game.Held == red, "red_module_can_be_carried");
                Near(game, game.Device);
                Check(!game.TryInteract() && game.Held == red && red.State == ModuleState.Carried
                    && game.Phase == RepairPhase.NotInstalled, "wrong_module_is_rejected_without_consumption");
                Near(game, game.ReturnPad);
                Check(game.TryInteract() && game.Held == null && red.State == ModuleState.OnShelf,
                    "wrong_choice_can_be_returned");
                RepairModule blue = game.Spawner.Modules[0]; Near(game, blue.transform);
                Check(game.TryInteract() && game.Held == blue, "blue_module_pickup");
                Check(!game.TryInteract() && game.Held == blue, "repeated_pickup_does_not_duplicate");
                Near(game, game.Device);
                Check(game.TryInteract() && game.Installs == 1 && game.Held == null
                    && blue.State == ModuleState.Installed, "correct_module_installs_once");
                Check(!game.TryInteract() && game.Installs == 1, "repeated_install_does_not_score_twice");
                Near(game, game.Exit, true);
                Check(game.TryComplete() && game.Completions == 1, "installed_then_exit_completes");
                Check(!game.TryComplete() && game.Completions == 1, "completion_is_idempotent");
                for (int i = 0; i < 10; i++)
                {
                    game = Fresh();
                    Check(game.Phase == RepairPhase.NotInstalled && game.Held == null && game.Returns == 0
                        && game.Installs == 0 && game.Completions == 0
                        && game.Spawner.Modules[0].State == ModuleState.OnShelf,
                        "fresh_scene_reset_" + (i + 1));
                }
                Directory.CreateDirectory("docs");
                File.WriteAllText("docs/prototype-validation.json", JsonUtility.ToJson(new Report {
                    unityVersion = Application.unityVersion, result = "passed", checks = Checks.ToArray(),
                    scope = "Editor real physics and interaction-state validation; standalone keyboard/render checks separate" }, true));
                Debug.Log("MAGNET_PROTOTYPE_VALIDATION_PASSED: " + Checks.Count + " checks");
            }
            finally { Physics.simulationMode = previous; EditorSceneManager.OpenScene("Assets/Scenes/Game.unity"); }
        }
        [Serializable] private class Report
        {
            public string unityVersion, result, scope;
            public string[] checks;
        }
    }
}
