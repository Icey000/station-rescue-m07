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
    /// <summary>Opt-in standalone regression run. Inactive during ordinary play.</summary>
    public sealed class PrototypeSmokeRun : MonoBehaviour
    {
        private Keyboard keyboard;
        private string output;
        private string runtimeError;
        private readonly List<string> checks = new List<string>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-legacy-magnet-smoke-test");
            if (index < 0 || index + 1 >= args.Length) return;
            var obj = new GameObject("StandaloneRegressionRunner");
            DontDestroyOnLoad(obj);
            obj.AddComponent<PrototypeSmokeRun>().output = args[index + 1];
        }
        private IEnumerator Start()
        {
            Directory.CreateDirectory(output);
            Application.logMessageReceived += ObserveError;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            keyboard = InputSystem.AddDevice<Keyboard>(); keyboard.MakeCurrent();
            IEnumerator run = Run();
            while (true)
            {
                bool more;
                try
                {
                    if (runtimeError != null) throw new InvalidOperationException(runtimeError);
                    more = run.MoveNext();
                }
                catch (Exception exception)
                {
                    WriteReport("failed", exception.ToString()); Application.Quit(1); yield break;
                }
                if (!more) break;
                yield return run.Current;
            }
            WriteReport("passed", "");
            InputSystem.RemoveDevice(keyboard);
            Application.logMessageReceived -= ObserveError;
            GameFlow.Quit();
        }
        private void ObserveError(string text, string trace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                runtimeError = text;
        }
        private void Check(bool value, string name)
        {
            if (!value) throw new InvalidOperationException("Standalone check failed: " + name);
            checks.Add(name);
        }
        private void Keys(params Key[] keys) { InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys)); }
        private IEnumerator Press(Key key)
        {
            Keys(key); yield return new WaitForSeconds(0.12f);
            Keys(); yield return new WaitForSeconds(0.09f);
        }
        private IEnumerator Screenshot(string name)
        {
            yield return new WaitForEndOfFrame();
            Texture2D frame = ScreenCapture.CaptureScreenshotAsTexture();
            Color32[] pixels = frame.GetPixels32();
            bool visibleContent = false;
            for (int i = 0; i < pixels.Length; i += 512)
                if (pixels[i].r + pixels[i].g + pixels[i].b > 280) { visibleContent = true; break; }
            Destroy(frame);
            Check(visibleContent, "render_nonblack_" + name);
            ScreenCapture.CaptureScreenshot(Path.Combine(output, name + ".png"));
            yield return new WaitForSeconds(0.25f);
        }
        private RepairGame Game()
        {
            var game = FindFirstObjectByType<RepairGame>();
            if (game == null) throw new InvalidOperationException("Game controller not loaded.");
            return game;
        }
        private void Near(RepairGame game, Transform target, bool center = false)
        {
            PlayerMotor motor = game.Player.GetComponent<PlayerMotor>(); motor.ResetMotion();
            Rigidbody body = game.Player.GetComponent<Rigidbody>();
            Vector3 point = target.position + (center ? Vector3.zero : Vector3.back * 1.8f);
            point.y = 0.65f; body.position = point; game.Player.position = point; body.linearVelocity = Vector3.zero;
            Physics.SyncTransforms();
        }
        private IEnumerator Run()
        {
            yield return new WaitForSeconds(0.6f);
            Check(SceneManager.GetActiveScene().name == "Menu", "windows_starts_in_menu");
            yield return Screenshot("menu");
            yield return Press(Key.Enter);
            yield return new WaitForSeconds(0.3f);
            RepairGame game = Game();
            Check(SceneManager.GetActiveScene().name == "Game", "menu_keyboard_starts_game_scene");
            Check(game.Spawner.Modules.Length == 2, "runtime_prefab_spawn_count");
            yield return Screenshot("game-start");
            Vector3 before = game.Player.position;
            Keys(Key.W); yield return new WaitForSeconds(0.4f);
            Check(game.Player.position.z > before.z + 1f, "keyboard_moves_player");
            PlayerMotor motor = game.Player.GetComponent<PlayerMotor>();
            Keys(Key.W, Key.LeftShift); yield return new WaitForSeconds(0.55f);
            Check(motor.BoostsUsed == 1, "held_shift_triggers_one_impulse");
            Keys(Key.W); yield return new WaitForSeconds(0.08f);
            Keys(Key.W, Key.LeftShift); yield return new WaitForSeconds(0.12f);
            Check(motor.BoostsUsed == 1, "repress_in_cooldown_does_not_boost");
            Keys(); yield return new WaitForSeconds(0.65f);
            Keys(Key.LeftShift); yield return new WaitForSeconds(0.1f);
            Check(motor.BoostsUsed == 2, "repress_after_cooldown_boosts");
            Keys();
            Near(game, game.Exit, true); yield return new WaitForSeconds(0.1f);
            Check(game.Phase == RepairPhase.NotInstalled, "early_exit_remains_incomplete");

            RepairModule red = game.Spawner.Modules[1]; Near(game, red.transform);
            yield return new WaitForSeconds(0.1f);
            Keys(Key.E); yield return new WaitForSeconds(0.08f);
            Check(game.Held == red, "keyboard_e_picks_red_module");
            Check(FindFirstObjectByType<GameAudio>().GetComponent<AudioSource>().isPlaying, "pickup_audio_plays");
            yield return new WaitForSeconds(0.35f);
            Check(game.Held == red && game.Returns == 0, "held_e_does_not_repeat_interaction");
            Keys();
            Near(game, game.Device); yield return new WaitForSeconds(0.1f);
            yield return Press(Key.E);
            Check(game.Held == red && game.Installs == 0, "wrong_part_not_consumed_or_installed");
            yield return Screenshot("wrong-part");
            Near(game, game.ReturnPad); yield return new WaitForSeconds(0.1f);
            yield return Press(Key.E);
            Check(game.Held == null && red.State == ModuleState.OnShelf, "return_restores_original_shelf");

            RepairModule blue = game.Spawner.Modules[0]; Near(game, blue.transform);
            yield return new WaitForSeconds(0.1f); yield return Press(Key.E);
            Check(game.Held == blue, "keyboard_e_picks_blue_module");
            // Reset while carrying must restore inventory, velocity, cooldown and both items.
            yield return Press(Key.R); yield return new WaitForSeconds(0.2f); game = Game();
            Check(game.Held == null && game.Phase == RepairPhase.NotInstalled
                && game.Player.GetComponent<PlayerMotor>().CooldownRemaining == 0f, "reset_while_carrying");
            blue = game.Spawner.Modules[0]; Near(game, blue.transform);
            yield return new WaitForSeconds(0.1f); yield return Press(Key.E);
            Near(game, game.Device); yield return new WaitForSeconds(0.1f); yield return Press(Key.E);
            Check(game.Phase == RepairPhase.Installed && game.Installs == 1 && game.Held == null,
                "keyboard_e_installs_correct_part");
            yield return Press(Key.E);
            Check(game.Installs == 1, "repeated_e_does_not_install_twice");
            yield return Screenshot("installed");
            yield return Press(Key.R); yield return new WaitForSeconds(0.2f); game = Game();
            Check(game.Phase == RepairPhase.NotInstalled && game.Installs == 0, "reset_after_installation");
            blue = game.Spawner.Modules[0]; Near(game, blue.transform);
            yield return new WaitForSeconds(0.1f); yield return Press(Key.E);
            Near(game, game.Device); yield return new WaitForSeconds(0.1f); yield return Press(Key.E);
            Near(game, game.Exit, true); yield return new WaitForSeconds(0.2f);
            Check(game.Phase == RepairPhase.Complete && game.Completions == 1, "arrival_at_exit_wins_once");
            Check(!game.Player.GetComponent<HumanInput>().enabled, "win_stops_keyboard_movement");
            yield return Screenshot("complete");
            for (int i = 0; i < 10; i++)
            {
                yield return Press(Key.R); yield return new WaitForSeconds(0.1f); game = Game();
                Check(game.Phase == RepairPhase.NotInstalled && game.Held == null && game.Installs == 0
                    && game.Completions == 0 && game.Spawner.Modules.Length == 2
                    && game.Player.GetComponent<HumanInput>().enabled, "restart_clean_" + (i + 1));
            }
            GameFlow.Menu(); yield return new WaitForSeconds(0.2f);
            Check(SceneManager.GetActiveScene().name == "Menu", "game_returns_to_menu");
            foreach (string name in new[] { "menu", "game-start", "wrong-part", "installed", "complete" })
                Check(File.Exists(Path.Combine(output, name + ".png")), "screenshot_written_" + name);
        }
        private void WriteReport(string result, string error)
        {
            File.WriteAllText(Path.Combine(output, "standalone-validation.json"), JsonUtility.ToJson(new Report {
                unityVersion = Application.unityVersion, result = result, checks = checks.ToArray(), error = error,
                scope = "Actual Windows player: synthetic keyboard input, real physics, runtime scene transitions, audio, screenshots and nonblack render checks; test teleports between stations" }, true));
            Debug.Log("MAGNET_STANDALONE_" + result.ToUpperInvariant() + ": " + checks.Count + " checks");
        }
        [Serializable] private class Report
        {
            public string unityVersion, result, error, scope;
            public string[] checks;
        }
    }
}
