using System.Collections.Generic;
using Jotunn.Managers;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SuperVikingKart
{
    internal enum RaceLineRole
    {
        StartFinish = 0,
        Start       = 1,
        Finish      = 2
    }

    /// <summary>
    /// Trigger relay — lives on the trigger child GameObject
    /// </summary>
    internal class RaceLineTrigger : MonoBehaviour
    {
        public RaceLineComponent Line;

        private void OnTriggerEnter(Collider other)
        {
            Line?.OnKartEnter(other);
        }
    }

    /// <summary>
    /// Root component
    /// </summary>
    internal class RaceLineComponent : MonoBehaviour, Hoverable, Interactable
    {
        // --- ZDO Keys ---
        private const string ZdoKeyRaceId = "SuperVikingKart_RaceLine_RaceId";
        private const string ZdoKeyRole   = "SuperVikingKart_RaceLine_Role";

        // --- Cooldown ---
        private const float CooldownSeconds = 3f;
        private readonly Dictionary<ZDOID, float> _cooldowns = new();

        // --- Internal refs ---
        private ZNetView    _netView;
        private TextMeshPro _label;

        // Exposed for RaceLineAdminGui.OnConfirm
        internal ZNetView NetView => _netView;

        // ── ZDO-backed properties ──────────────────────────────────

        public string RaceId
        {
            get => _netView?.GetZDO()?.GetString(ZdoKeyRaceId) ?? "";
            set
            {
                _netView?.GetZDO()?.Set(ZdoKeyRaceId, value);
                UpdateLabel();
            }
        }

        public RaceLineRole Role
        {
            get => (RaceLineRole)(_netView?.GetZDO()?.GetInt(ZdoKeyRole) ?? 0);
            set
            {
                _netView?.GetZDO()?.Set(ZdoKeyRole, (int)value);
                UpdateLabel();
            }
        }

        // ── Lifecycle ─────────────────────────────────────────────

        private void Awake()
        {
            _netView = GetComponent<ZNetView>();
            if (!_netView || _netView.GetZDO() == null)
            {
                SuperVikingKart.DebugLog("RaceLine Awake - no ZNetView or ZDO, disabling");
                enabled = false;
                return;
            }

            var labelGo = transform.Find("Label");
            if (labelGo)
                _label = labelGo.GetComponent<TextMeshPro>();

            UpdateLabel();
            SuperVikingKart.DebugLog(
                $"RaceLine Awake - ZDO: {_netView.GetZDO().m_uid}, Role: {Role}, RaceId: {RaceId}");
        }

        private void Update()
        {
            // Purge expired cooldown entries
            var now      = Time.time;
            var toRemove = new List<ZDOID>();
            foreach (var kvp in _cooldowns)
                if (kvp.Value < now)
                    toRemove.Add(kvp.Key);
            foreach (var id in toRemove)
                _cooldowns.Remove(id);
        }

        // ── Trigger entry (forwarded from RaceLineTrigger child) ──

        public void OnKartEnter(Collider other)
        {
            // 1. Match the collider's root against the Instances list
            var kart = FindKart(other);
            if (kart == null)
                return;

            // 2. Resolve attached player from ZDO (works on any client)
            var player = kart.GetAttachedPlayer();
            if (player == null)
                return;

            // 3. Only the rider's own client sends RPCs
            if (player != Player.m_localPlayer)
                return;

            // 4. Race must exist and be in Racing state
            var raceId = RaceId;
            if (string.IsNullOrEmpty(raceId))
                return;

            var race = RaceManager.GetRace(raceId);
            if (race == null || race.State != RaceState.Racing)
                return;

            // 5. Contestant must be registered
            var playerId   = player.GetZDOID();
            var contestant = race.GetContestant(playerId);
            if (contestant == null)
                return;

            // 6. Per-player cooldown
            if (_cooldowns.TryGetValue(playerId, out var cooldownUntil) &&
                Time.time < cooldownUntil)
                return;

            // 7. Direction guard — must be moving in the line's +Z direction
            var rb = kart.GetComponentInParent<Rigidbody>();
            if (rb != null && rb.velocity.sqrMagnitude > 0.01f)
            {
                var dot = Vector3.Dot(rb.velocity.normalized, transform.forward);
                if (dot <= 0f)
                    return;
            }

            // All guards passed — stamp cooldown and branch on role
            _cooldowns[playerId] = Time.time + CooldownSeconds;

            switch (Role)
            {
                case RaceLineRole.StartFinish:
                case RaceLineRole.Start:
                    if (!contestant.CrossedStart)
                        RaceManager.SendCrossedStart(raceId, playerId);
                    else if (Role == RaceLineRole.StartFinish)
                        RaceManager.SendLap(raceId, playerId);
                    break;

                case RaceLineRole.Finish:
                    if (contestant.CrossedStart)
                        RaceManager.SendLap(raceId, playerId);
                    break;
            }
        }

        // ── Hoverable / Interactable ───────────────────────────────

        public string GetHoverText()
        {
            var configured = !string.IsNullOrEmpty(RaceId);
            var info = configured ? $"{Role} — {RaceId}" : "Not configured";

            if (!SynchronizationManager.Instance.PlayerIsAdmin)
                return $"Race Line\n<color=grey>{info}</color>";

            return $"Race Line\n{info}" +
                   "\n[<color=yellow><b>$KEY_Use</b></color>] Configure";
        }

        public string GetHoverName() => "Race Line";

        public bool Interact(Humanoid user, bool hold, bool alt)
        {
            if (hold) return false;

            var player = user as Player;
            if (!player || player != Player.m_localPlayer)
                return false;

            if (!SynchronizationManager.Instance.PlayerIsAdmin)
            {
                player.Message(MessageHud.MessageType.Center,
                    "You need to be an admin to configure this line");
                return false;
            }

            RaceLineAdminGui.Open(this);
            return true;
        }

        public bool UseItem(Humanoid user, ItemDrop.ItemData item) => false;

        // ── Label ─────────────────────────────────────────────────

        public void UpdateLabel()
        {
            if (!_label) return;
            var id = RaceId;
            _label.text = string.IsNullOrEmpty(id)
                ? $"<color=grey>{Role}\nNot configured</color>"
                : $"{Role}\n{id}";
        }

        // ── Helpers ───────────────────────────────────────────────

        private static SuperVikingKartComponent FindKart(Collider other)
        {
            var root = other.transform.root;
            foreach (var kart in SuperVikingKartComponent.Instances)
                if (kart.transform.root == root)
                    return kart;
            return null;
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  Admin GUI — mirrors RaceBoardAdminGui pattern exactly
    // ─────────────────────────────────────────────────────────────
    internal static class RaceLineAdminGui
    {
        private static GameObject        _panel;
        private static RaceLineComponent _currentLine;
        private static InputField        _raceIdField;
        private static Dropdown          _roleDropdown;

        // ── Init ──────────────────────────────────────────────────

        /// <summary>
        /// Rebuilds the panel on every scene change.
        /// Subscribed to GUIManager.OnCustomGUIAvailable in SuperVikingKart.Awake.
        /// </summary>
        public static void Build()
        {
            if (!GUIManager.CustomGUIFront)
                return;

            if (_panel)
            {
                UnityEngine.Object.Destroy(_panel);
                _panel = null;
            }

            // Root woodpanel — same style as RaceBoardAdminGui
            _panel = GUIManager.Instance.CreateWoodpanel(
                GUIManager.CustomGUIFront.transform,
                anchorMin: new Vector2(0.5f, 0.5f),
                anchorMax: new Vector2(0.5f, 0.5f),
                position:  Vector2.zero,
                width:     420f,
                height:    210f,
                draggable: true);
            _panel.SetActive(false);

            var layout = _panel.AddComponent<VerticalLayoutGroup>();
            layout.padding               = new RectOffset(20, 20, 20, 20);
            layout.spacing               = 10f;
            layout.childForceExpandWidth  = true;
            layout.childForceExpandHeight = false;
            layout.childAlignment         = TextAnchor.UpperLeft;

            // Title
            var title = GUIManager.Instance.CreateText(
                "Configure Race Line",
                _panel.transform,
                new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero,
                GUIManager.Instance.AveriaSerifBold, 20,
                GUIManager.Instance.ValheimOrange,
                true, Color.black,
                380f, 30f, false);
            title.GetComponent<Text>().alignment = TextAnchor.MiddleCenter;

            // Race ID row
            AddLabeledField("Race ID", out _raceIdField,
                InputField.ContentType.Standard, "meadows_gp");

            // Role row
            var row = new GameObject("RoleRow",
                typeof(RectTransform), typeof(HorizontalLayoutGroup));
            row.transform.SetParent(_panel.transform, false);
            var rl = row.GetComponent<HorizontalLayoutGroup>();
            rl.spacing               = 10f;
            rl.childForceExpandWidth  = false;
            rl.childForceExpandHeight = true;
            rl.childAlignment         = TextAnchor.MiddleLeft;

            var labelGo = GUIManager.Instance.CreateText(
                "Role", row.transform,
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero,
                GUIManager.Instance.AveriaSerifBold, 16,
                Color.white, true, Color.black,
                90f, 30f, false);
            labelGo.AddComponent<LayoutElement>().preferredWidth = 90f;

            var dropdownGo = GUIManager.Instance.CreateDropDown(
                row.transform,
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero,
                16,
                width: 260f, height: 30f);
            dropdownGo.AddComponent<LayoutElement>().preferredWidth = 260f;

            _roleDropdown = dropdownGo.GetComponent<Dropdown>();
            _roleDropdown.ClearOptions();
            _roleDropdown.AddOptions(new List<string>
            {
                "StartFinish",
                "Start",
                "Finish"
            });

            // Button row
            var buttonRow = new GameObject("ButtonRow",
                typeof(RectTransform), typeof(HorizontalLayoutGroup));
            buttonRow.transform.SetParent(_panel.transform, false);
            var bl = buttonRow.GetComponent<HorizontalLayoutGroup>();
            bl.spacing               = 10f;
            bl.childForceExpandWidth  = true;
            bl.childForceExpandHeight = true;
            bl.childAlignment         = TextAnchor.MiddleCenter;

            var confirmGo = GUIManager.Instance.CreateButton(
                "Confirm", buttonRow.transform,
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero,
                width: 160f, height: 40f);
            confirmGo.GetComponent<Button>().onClick.AddListener(OnConfirm);

            var cancelGo = GUIManager.Instance.CreateButton(
                "Cancel", buttonRow.transform,
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero,
                width: 160f, height: 40f);
            cancelGo.GetComponent<Button>().onClick.AddListener(Close);

            SuperVikingKart.DebugLog("RaceLineAdminGui - Panel built");
        }

        // ── Open / Close ──────────────────────────────────────────

        public static void Open(RaceLineComponent line)
        {
            if (_panel == null)
            {
                SuperVikingKart.DebugLog("RaceLineAdminGui - Panel not built yet, skipping open");
                return;
            }

            _currentLine      = line;
            _raceIdField.text = line.RaceId;

            _roleDropdown.value = (int)line.Role;
            _roleDropdown.RefreshShownValue();

            _panel.SetActive(true);
            GUIManager.BlockInput(true);
            SuperVikingKart.DebugLog("RaceLineAdminGui - Opened");
        }

        private static void Close()
        {
            if (_panel)
                _panel.SetActive(false);
            GUIManager.BlockInput(false);
            _currentLine = null;
            SuperVikingKart.DebugLog("RaceLineAdminGui - Closed");
        }

        // ── Confirm ───────────────────────────────────────────────

        private static void OnConfirm()
        {
            if (_currentLine == null) { Close(); return; }

            var raceId = _raceIdField.text.Trim();
            if (string.IsNullOrEmpty(raceId))
            {
                SuperVikingKart.DebugLog("RaceLineAdminGui - Validation failed: empty raceId");
                return;
            }

            var role = (RaceLineRole)_roleDropdown.value;

            _currentLine.NetView.ClaimOwnership();
            _currentLine.RaceId = raceId;
            _currentLine.Role   = role;

            SuperVikingKart.DebugLog($"RaceLineAdminGui - Configured [{raceId}] Role: {role}");
            Close();
        }

        // ── Helpers ───────────────────────────────────────────────

        private static void AddLabeledField(string label, out InputField field,
            InputField.ContentType contentType, string placeholder)
        {
            var row = new GameObject($"{label}Row",
                typeof(RectTransform), typeof(HorizontalLayoutGroup));
            row.transform.SetParent(_panel.transform, false);
            var rl = row.GetComponent<HorizontalLayoutGroup>();
            rl.spacing               = 10f;
            rl.childForceExpandWidth  = false;
            rl.childForceExpandHeight = true;
            rl.childAlignment         = TextAnchor.MiddleLeft;

            var labelGo = GUIManager.Instance.CreateText(
                label, row.transform,
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero,
                GUIManager.Instance.AveriaSerifBold, 16,
                Color.white, true, Color.black,
                90f, 30f, false);
            labelGo.AddComponent<LayoutElement>().preferredWidth = 90f;

            var inputGo = GUIManager.Instance.CreateInputField(
                row.transform,
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero,
                contentType, placeholder, 16,
                width: 260f, height: 30f);
            inputGo.AddComponent<LayoutElement>().preferredWidth = 260f;

            field = inputGo.GetComponent<InputField>();
        }

    }
}