using System.Collections.Generic;
using UnityEngine;
namespace UnityAgentLab
{
    public enum RepairPhase { NotInstalled = 0, Installed = 1, Complete = 2, Powering = 3 }
    public enum InteractionKind { None, Pickup, Return, Install, Board }
    public readonly struct InteractionContext
    {
        public readonly InteractionKind Kind;
        public readonly Transform Target;
        public readonly bool Enabled;
        public readonly string Action, Reason;
        public InteractionContext(InteractionKind kind, Transform target, bool enabled, string action, string reason = "")
        { Kind = kind; Target = target; Enabled = enabled; Action = action; Reason = reason; }
    }
    public sealed class RepairGame : MonoBehaviour
    {
        [SerializeField] private Transform player;
        [SerializeField] private ModuleSpawner spawner;
        [SerializeField] private Transform device, socket, returnPad, exit, highlight;
        [SerializeField] private Renderer deviceLamp, exitLamp;
        [SerializeField] private GameAudio audioFeedback;
        [SerializeField] private float interactionDistance = 2.2f;
        [SerializeField] private DockGate[] gates = new DockGate[0];
        [SerializeField] private Transform[] chargingSlots = new Transform[0];
        [SerializeField] private ArcStrip[] arcStrips = new ArcStrip[0];
        [SerializeField] private PowerStationVisual powerVisual;
        [SerializeField] private PowerRouteController routes;
        private readonly HashSet<ArcStrip> contacted = new HashSet<ArcStrip>();
        private bool initialized;
        private float feedbackUntil, powerElapsed;
        private string feedbackKey;
        private object[] feedbackArgs = new object[0];
        private RepairModule installingCell;
        private Vector3 installationStart;
        public const float PowerDuration = 2.6f;
        public RepairPhase Phase { get; private set; }
        public RepairModule Held { get; private set; }
        public StationPlan Plan { get; private set; }
        public ModuleKind RequiredKind => Plan == null ? ModuleKind.BlueCircle : Plan.RequiredKind;
        public float Elapsed { get; private set; }
        public float PenaltySeconds { get; private set; }
        public float MissionTime => Elapsed + PenaltySeconds;
        public float PowerProgress => Mathf.Clamp01(powerElapsed / PowerDuration);
        public int Returns { get; private set; }
        public int Installs { get; private set; }
        public int Completions { get; private set; }
        public int WrongInstalls { get; private set; }
        public int ArcHits { get; private set; }
        public int Stars => MissionTime <= 55 ? 3 : MissionTime <= 90 ? 2 : 1;
        public Transform Device => device;
        public Transform ReturnPad => returnPad;
        public Transform Exit => exit;
        public Transform Player => player;
        public ModuleSpawner Spawner => spawner;
        public DockGate[] Gates => gates;
        public ArcStrip[] ArcStrips => arcStrips;
        public PowerRouteController Routes => routes;
        public DockGate ActiveDock => Plan == null || gates.Length == 0 ? null : gates[Plan.ExitIndex];
        public string ExitName => ActiveDock == null ? LanguageService.Text("step3") : ActiveDock.DockName;
        public RepairModule RequiredModule => spawner.Modules[(int)RequiredKind];
        public RepairModule OtherModule => spawner.Modules[1 - (int)RequiredKind];
        public string Goal => Phase == RepairPhase.Powering ? LanguageService.Text("goal.powering")
            : Phase == RepairPhase.Installed ? LanguageService.Text("goal.board", ExitName)
            : Phase == RepairPhase.Complete ? LanguageService.Text("goal.complete")
            : Held == null ? LanguageService.Text("goal.fetch", LanguageService.Cell(RequiredKind))
            : Held.Kind != RequiredKind ? LanguageService.Text("goal.wrong", LanguageService.Cell(RequiredKind))
            : LanguageService.Text("goal.deliver");
        public string Feedback
        {
            get
            {
                if (Time.unscaledTime >= feedbackUntil || feedbackKey == null) return "";
                var args = new object[feedbackArgs.Length];
                for (int i = 0; i < args.Length; i++)
                    args[i] = feedbackArgs[i] is ModuleKind kind ? LanguageService.Cell(kind)
                        : feedbackArgs[i] is DockGate dock ? dock.DockName : feedbackArgs[i];
                return LanguageService.Text(feedbackKey, args);
            }
        }
        public float FeedbackOpacity => Mathf.Clamp01((feedbackUntil - Time.unscaledTime) / .6f);
        public string InteractionHint { get { var context = GetInteractionContext(); return context.Reason.Length > 0 ? context.Reason : context.Action; } }
        public void Configure(Transform actor, ModuleSpawner items, Transform machine, Transform attachment,
            Transform returnPoint, Transform exitPoint, Transform targetHighlight, Renderer machineLight, Renderer exitLight, GameAudio sounds)
        {
            player = actor; spawner = items; device = machine; socket = attachment; returnPad = returnPoint;
            exit = exitPoint; highlight = targetHighlight; deviceLamp = machineLight; exitLamp = exitLight; audioFeedback = sounds;
        }
        public void ConfigureStation(DockGate[] docks, Transform[] shelves, ArcStrip[] hazards, PowerStationVisual generator)
        { gates = docks; chargingSlots = shelves; arcStrips = hazards; powerVisual = generator; }
        public void ConfigureRoutes(PowerRouteController value) { routes = value; }
        private void Start() { Initialize(); }
        public void Initialize(int? seed = null)
        {
            if (initialized) return;
            initialized = true;
            if (gates.Length > 0)
            {
                Plan = seed.HasValue ? StationPlan.Create(seed.Value, gates.Length, chargingSlots.Length)
                    : StationPlan.NewRun(gates.Length, chargingSlots.Length);
                spawner.SetHomes(chargingSlots[Plan.BlueShelf], chargingSlots[Plan.OrangeShelf]);
                exit = ActiveDock.BoardingAnchor;
                foreach (DockGate gate in gates) gate.SetPowered(false, false);
                powerVisual.SetRequest(RequiredKind); powerVisual.SetPowered(false);
            }
            routes?.ResetRoutes(); spawner.SpawnIfNeeded();
        }
        private void Update() { Step(Time.deltaTime); }
        public void Step(float deltaTime)
        {
            if (deltaTime <= 0 || GameFlow.IsPaused || Phase == RepairPhase.Complete) return;
            Elapsed += deltaTime;
            if (Phase != RepairPhase.Powering) return;
            powerElapsed += deltaTime;
            if (installingCell != null) installingCell.transform.position =
                Vector3.Lerp(installationStart, socket.position, Mathf.SmoothStep(0, 1, powerElapsed / 0.5f));
            if (powerElapsed >= 0.5f && powerVisual != null) powerVisual.SetPowered(true);
            routes?.SetProgress(Plan == null ? 0 : Plan.ExitIndex, (powerElapsed - 1.1f));
            if (powerElapsed >= 2.1f)
            {
                foreach (DockGate gate in gates)
                { gate.SetPowered(true, gate == ActiveDock); gate.StepAnimation(deltaTime); }
            }
            if (powerElapsed >= PowerDuration && (ActiveDock == null || ActiveDock.IsOpen))
            {
                Phase = RepairPhase.Installed;
                if (deviceLamp != null) deviceLamp.material.color = new Color(0.1f, 1, 0.6f);
                if (exitLamp != null) exitLamp.material.color = new Color(0.1f, 1, 0.6f);
                Say("power.ready", 4, ActiveDock == null ? (object)ExitName : ActiveDock);
            }
        }
        private void FixedUpdate()
        {
            if (Phase != RepairPhase.NotInstalled) return;
            foreach (ArcStrip strip in arcStrips)
            {
                if (!strip.Contains(player.position)) contacted.Remove(strip);
                else TryArcContact(strip);
            }
        }
        public bool TryArcContact(ArcStrip strip)
        {
            if (!strip.Contains(player.position)) { contacted.Remove(strip); return false; }
            if (Phase != RepairPhase.NotInstalled || !strip.IsLive || !contacted.Add(strip)) return false;
            bool cargo = Held != null;
            if (cargo) { Held.Drop(player.position + Vector3.up * 1.15f); Held = null; }
            player.GetComponent<PlayerMotor>().Stun(0.45f);
            player.GetComponent<PlayerInteraction>()?.CancelRequests();
            ArcHits++; PenaltySeconds += 5;
            Say(cargo ? "shock" : "shock.empty", 3); return true;
        }
        private float Distance(Transform target)
        {
            if (target == null) return float.MaxValue;
            Vector3 offset = player.position - target.position; offset.y = 0; return offset.magnitude;
        }
        private void Consider(Transform candidate, ref Transform best, ref float distance)
        {
            float value = Distance(candidate);
            if (candidate != null && value <= distance) { best = candidate; distance = value; }
        }
        public InteractionContext GetInteractionContext()
        {
            Initialize();
            if (Phase == RepairPhase.Complete) return new InteractionContext(InteractionKind.None, null, false, "");
            Transform best = null; float distance = interactionDistance;
            if (Phase == RepairPhase.NotInstalled)
            {
                Consider(device, ref best, ref distance);
                if (Held != null) Consider(returnPad, ref best, ref distance);
                else foreach (RepairModule module in spawner.Modules)
                    if (module.State == ModuleState.OnShelf || module.State == ModuleState.Dropped) Consider(module.transform, ref best, ref distance);
            }
            else if (Phase == RepairPhase.Powering) Consider(device, ref best, ref distance);
            foreach (DockGate gate in gates) Consider(gate.BoardingAnchor, ref best, ref distance);
            if (best == null) return new InteractionContext(InteractionKind.None, null, false, "");
            InteractionKind kind; bool enabled = true; string reason = "", action;
            if (best == device)
            {
                kind = InteractionKind.Install; action = LanguageService.Text("install");
                if (Phase != RepairPhase.NotInstalled) { enabled = false; reason = LanguageService.Text("connecting"); }
                else if (Held == null) { enabled = false; reason = LanguageService.Text("need", LanguageService.Cell(RequiredKind)); }
                else if (Held.Kind != RequiredKind) reason = LanguageService.Text("mismatch");
            }
            else if (best == returnPad) { kind = InteractionKind.Return; action = LanguageService.Text("return"); }
            else if (best.TryGetComponent(out RepairModule module))
            {
                kind = InteractionKind.Pickup; action = LanguageService.Text("pickup");
                foreach (ArcStrip strip in arcStrips)
                    if (strip.IsLive && strip.Contains(module.transform.position)) { enabled = false; reason = LanguageService.Text("wait.arc"); }
            }
            else
            {
                kind = InteractionKind.Board; action = LanguageService.Text("board");
                enabled = Phase == RepairPhase.Installed && ActiveDock != null && best == ActiveDock.BoardingAnchor && ActiveDock.IsOpen && Distance(best) <= 1.45f;
                reason = enabled ? "" : LanguageService.Text(Phase == RepairPhase.NotInstalled ? "offline"
                    : Phase == RepairPhase.Powering ? "connecting" : best != ActiveDock.BoardingAnchor ? "standby" : "goal.board", ExitName);
            }
            if (player.GetComponent<PlayerMotor>().StunRemaining > 0) { enabled = false; reason = LanguageService.Text("stunned"); }
            return new InteractionContext(kind, best, enabled, action, reason);
        }
        public bool TryInteract()
        {
            if (GameFlow.IsPaused) return false;
            InteractionContext context = GetInteractionContext();
            if (!context.Enabled) return false;
            if (context.Kind == InteractionKind.Board) return TryComplete();
            if (context.Kind == InteractionKind.Install)
            {
                if (Held.Kind != RequiredKind) { WrongInstalls++; PenaltySeconds += 3; Say("denied", 3); return false; }
                installingCell = Held; installationStart = Held.transform.position;
                Held.InstallAt(socket.position); Held.transform.position = installationStart; Held = null;
                Phase = RepairPhase.Powering; powerElapsed = 0; Installs++; Say("power.start", 4); return true;
            }
            if (context.Kind == InteractionKind.Return)
            {
                Held.BeginReturn(returnPad.position + new Vector3(0, 0.8f, -0.5f));
                Held = null; Returns++; Say("returned", 2); return true;
            }
            RepairModule item = context.Target.GetComponent<RepairModule>();
            if (Held != null || item == null || !item.PickUp(player)) return false;
            Held = item; Say("picked", 1, item.Kind); return true;
        }
        public bool TryComplete()
        {
            if (GameFlow.IsPaused || Phase != RepairPhase.Installed || Distance(exit) > 1.45f
                || (ActiveDock != null && !ActiveDock.IsOpen)) return false;
            Phase = RepairPhase.Complete; Completions++;
            player.GetComponent<HumanInput>().enabled = false;
            player.GetComponent<PlayerInteraction>().enabled = false;
            PlayerMotor motor = player.GetComponent<PlayerMotor>(); motor.ResetMotion(); motor.enabled = false;
            player.GetComponent<Rigidbody>().isKinematic = true;
            Say("won", 5); return true;
        }
        private void Say(string key, int clip, params object[] args)
        {
            feedbackKey = key; feedbackArgs = args; feedbackUntil = Time.unscaledTime + 3.5f;
            audioFeedback?.Play(clip);
        }
        private void LateUpdate()
        {
            if (highlight == null) return;
            InteractionContext context = GetInteractionContext(); highlight.gameObject.SetActive(context.Target != null);
            if (context.Target != null) highlight.position = new Vector3(context.Target.position.x, 0.065f, context.Target.position.z);
        }
    }
}
