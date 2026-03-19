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
        
        // --- References set by SuperVikingKart.cs during prefab setup ---
        public TextMeshPro Label;

        // --- Private ---
        private ZNetView _netView;

        // --- Lifecycle ---

        private void Awake()
        {
            _netView = GetComponent<ZNetView>();
            if (!_netView || _netView.GetZDO() == null)
            {
                SuperVikingKart.DebugLog("RaceLine Awake - no ZNetView or ZDO, disabling");
                enabled = false;
                return;
            }

            UpdateLabel();
            SuperVikingKart.DebugLog(
                $"RaceLine Awake - ZDO: {_netView.GetZDO().m_uid}, Role: {GetRole()}, RaceId: {GetRaceId()}");
        }

        private void Update()
        {
            // Purge expired cooldown entries
            var now = Time.time;
            var toRemove = new List<ZDOID>();
            foreach (var kvp in _cooldowns)
                if (kvp.Value < now)
                    toRemove.Add(kvp.Key);
            foreach (var id in toRemove)
                _cooldowns.Remove(id);
        }

        // --- Trigger entry (forwarded from RaceLineTrigger child)---

        public void OnRaceLineTriggerEnter(Collider other)
        {
            SuperVikingKart.DebugLog($"RaceLine trigger entered by: {other.name} (parent: {other.transform.root.name})");

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
            var raceId = GetRaceId();
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

            switch (GetRole())
            {
                case RaceLineRole.StartFinish:
                case RaceLineRole.Start:
                    if (!contestant.CrossedStart)
                        RaceManager.SendCrossedStart(raceId, playerId);
                    else if (GetRole() == RaceLineRole.StartFinish)
                        RaceManager.SendLap(raceId, playerId);
                    break;

                case RaceLineRole.Finish:
                    if (contestant.CrossedStart)
                        RaceManager.SendLap(raceId, playerId);
                    break;
            }
        }

        // --- Configure (called by RaceLineAdminGui on confirm) ---

        public void Configure(string raceId, RaceLineRole role)
        {
            // Claim ZDO ownership and persist config locally
            _netView.ClaimOwnership();
            _netView.GetZDO().Set(ZdoKeyRaceId, raceId);
            _netView.GetZDO().Set(ZdoKeyRole, (int)role);
            
            UpdateLabel();

            SuperVikingKart.DebugLog($"RaceLine - Configured [{raceId}] {role}");
        }

        // --- Label ---

        public void UpdateLabel()
        {
            if (!Label) return;
            var id = GetRaceId();
            Label.text = string.IsNullOrEmpty(id)
                ? $"<color=grey>{GetRole()}\nNot configured</color>"
                : $"{GetRole()}\n{id}";
        }

        // --- ZDO Accessors ---

        public string GetRaceId()
        {
            return _netView?.GetZDO()?.GetString(ZdoKeyRaceId) ?? "";
        }

        public RaceLineRole GetRole()
        {
            var roleInt = _netView?.GetZDO()?.GetInt(ZdoKeyRole) ?? 0;
            return (RaceLineRole)roleInt;
        }

        // --- Hoverable / Interactable ---

        public string GetHoverText()
        {
            var configured = !string.IsNullOrEmpty(GetRaceId());
            var info = configured ? $"{GetRole()} — {GetRaceId()}" : "Not configured";

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

        // --- Helpers ---

        private static SuperVikingKartComponent FindKart(Collider other)
        {
            var root = other.transform.root;
            foreach (var kart in SuperVikingKartComponent.Instances)
                if (kart.transform.root == root)
                    return kart;
            return null;
        }
    }

    /// <summary>
    /// Trigger relay — lives on the trigger child GameObject
    /// </summary>
    internal class RaceLineTrigger : MonoBehaviour
    {
        public RaceLineComponent Line;

        private void OnTriggerEnter(Collider other)
        {
            Line?.OnRaceLineTriggerEnter(other);
        }
    }

    /// <summary>
    /// Shared static admin GUI for all RaceLine instances.
    /// Only one panel exists at a time, parented to GUIManager.CustomGUIFront.
    /// Rebuilt on each scene change via GUIManager.OnCustomGUIAvailable.
    /// Populated from the interacted line's ZDO on open.
    /// </summary>
    internal static class RaceLineAdminGui
    {
        private static GameObject        _panel;
        private static RaceLineComponent _currentLine;
        private static InputField        _raceIdField;
        private static Dropdown          _roleDropdown;

        // --- Init ---

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
                UnityEngine.Object.DestroyImmediate(_panel);
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
            var dropdownRow = new GameObject("RoleRow",
                typeof(RectTransform), typeof(HorizontalLayoutGroup));
            dropdownRow.transform.SetParent(_panel.transform, false);
            var rl = dropdownRow.GetComponent<HorizontalLayoutGroup>();
            rl.spacing               = 10f;
            rl.childForceExpandWidth  = false;
            rl.childForceExpandHeight = true;
            rl.childAlignment         = TextAnchor.MiddleLeft;

            var labelGo = GUIManager.Instance.CreateText(
                "Role", dropdownRow.transform,
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero,
                GUIManager.Instance.AveriaSerifBold, 16,
                Color.white, true, Color.black,
                90f, 30f, false);
            labelGo.AddComponent<LayoutElement>().preferredWidth = 90f;

            var dropdownGo = GUIManager.Instance.CreateDropDown(
                dropdownRow.transform,
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

        // --- Open / Close ---

        public static void Open(RaceLineComponent line)
        {
            if (_panel == null)
            {
                SuperVikingKart.DebugLog("RaceLineAdminGui - Panel not built yet, skipping open");
                return;
            }

            _currentLine      = line;
            _raceIdField.text = line.GetRaceId();

            _roleDropdown.value = (int)line.GetRole();
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

        // --- Confirm ---

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
            _currentLine.Configure(raceId, role);
            
            SuperVikingKart.DebugLog($"RaceLineAdminGui - Configured [{raceId}] Role: {role}");
            Close();
        }

        // --- Helpers ---

        /// <summary>
        /// Creates a label + input field row inside the panel's vertical layout.
        /// </summary>
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