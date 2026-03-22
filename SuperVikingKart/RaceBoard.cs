using System;
using Jotunn.Managers;
using UnityEngine;
using UnityEngine.UI;

namespace SuperVikingKart
{
    internal enum RaceBoardButtonType
    {
        Register,
        Start,
        Reset,
        Admin
    }

    /// <summary>
    /// Placed on each button child of the RaceBoard prefab.
    /// Delegates interactions to the parent RaceBoardComponent.
    /// Admin button is restricted to server admins only.
    /// </summary>
    internal class RaceBoardButton : MonoBehaviour, Hoverable, Interactable
    {
        public RaceBoardButtonType ButtonType;
        public RaceBoardComponent Board;

        public bool Interact(Humanoid user, bool hold, bool alt)
        {
            if (hold)
                return false;

            var player = user as Player;
            if (!player || player != Player.m_localPlayer)
                return false;

            if (ButtonType == RaceBoardButtonType.Admin)
            {
                if (!SynchronizationManager.Instance.PlayerIsAdmin)
                {
                    player.Message(MessageHud.MessageType.Center,
                        "You need to be an admin to configure this board");
                    return false;
                }

                RaceBoardAdminGui.Open(Board);
                return true;
            }

            Board.OnButtonInteract(ButtonType, player);
            return true;
        }

        public bool UseItem(Humanoid user, ItemDrop.ItemData item) => false;

        public string GetHoverText()
        {
            if (Board == null)
                return "";

            var race = RaceManager.GetRace(Board.GetRaceId());

            return Localization.instance.Localize(
                ButtonType switch
                {
                    RaceBoardButtonType.Register => GetRegisterHoverText(race),
                    RaceBoardButtonType.Start    => GetStartHoverText(race),
                    RaceBoardButtonType.Reset    => GetResetHoverText(race),
                    RaceBoardButtonType.Admin    => "[<color=yellow><b>$KEY_Use</b></color>] Configure Board",
                    _                            => ""
                });
        }

        public string GetHoverName() => ButtonType switch
        {
            RaceBoardButtonType.Register => "Register",
            RaceBoardButtonType.Start    => "Start Race",
            RaceBoardButtonType.Reset    => "Reset Race",
            RaceBoardButtonType.Admin    => "Configure Board",
            _                            => ""
        };

        private string GetRegisterHoverText(Race race)
        {
            if (race == null)
                return "<color=grey>No race configured</color>";

            if (race.State != RaceState.Idle)
                return "<color=grey>Race already underway</color>";

            var player = Player.m_localPlayer;
            if (player == null)
                return "";

            return race.IsRegistered(player.GetZDOID())
                ? "[<color=yellow><b>$KEY_Use</b></color>] Leave Race"
                : "[<color=yellow><b>$KEY_Use</b></color>] Register";
        }

        private string GetStartHoverText(Race race)
        {
            if (race == null)
                return "<color=grey>No race configured</color>";

            return race.State != RaceState.Idle
                ? "<color=grey>Race already underway</color>"
                : "[<color=yellow><b>$KEY_Use</b></color>] Start Race";
        }

        private string GetResetHoverText(Race race)
        {
            if (race == null)
                return "<color=grey>No race configured</color>";

            return race.State == RaceState.Idle
                ? "<color=grey>Nothing to reset</color>"
                : "[<color=yellow><b>$KEY_Use</b></color>] Reset Race";
        }
    }

    /// <summary>
    /// Root coordinator for the RaceBoard piece.
    /// Persists race config (raceId, name, laps) in the ZDO.
    /// Drives the status TextMeshPro every Update tick from RaceManager state.
    /// ZNetView, Piece and WearNTear come from the Unity prefab.
    /// Admin GUI is handled by the shared static RaceBoardAdminGui.
    /// </summary>
    internal class RaceBoardComponent : MonoBehaviour, Hoverable
    {
        // --- ZDO Keys ---

        private const string ZdoKeyRaceId = "SuperVikingKart_RaceBoard_RaceId";
        private const string ZdoKeyName   = "SuperVikingKart_RaceBoard_Name";
        private const string ZdoKeyLaps   = "SuperVikingKart_RaceBoard_Laps";

        // --- References set by SuperVikingKart.cs during prefab setup ---

        public TMPro.TextMeshPro StatusDisplay;
        public RaceBoardButton RegisterButton;
        public RaceBoardButton StartButton;
        public RaceBoardButton ResetButton;
        public RaceBoardButton AdminButton;

        // --- Private ---

        private ZNetView _netView;

        // --- Lifecycle ---

        private void Awake()
        {
            _netView = GetComponent<ZNetView>();

            if (!_netView || _netView.GetZDO() == null)
            {
                SuperVikingKart.DebugLog("RaceBoard Awake - no ZNetView or ZDO, disabling");
                enabled = false;
                return;
            }

            SuperVikingKart.DebugLog($"RaceBoard Awake - ZDO: {_netView.GetZDO().m_uid}");

            // Bootstrap RaceManager with persisted config if race doesn't exist yet.
            // Safe to call on all clients - RPC_CreateRace is idempotent.
            var raceId = GetRaceId();
            if (!string.IsNullOrEmpty(raceId) && RaceManager.GetRace(raceId) == null)
            {
                var name = GetRaceName();
                var laps = GetLaps();
                SuperVikingKart.DebugLog($"RaceBoard Awake - Bootstrapping race [{raceId}] \"{name}\" ({laps} laps)");
                RaceManager.SendCreateRace(raceId, name, laps);
            }
        }

        private void Update()
        {
            UpdateStatusDisplay();
        }

        // --- Status Display ---

        private void UpdateStatusDisplay()
        {
            if (!StatusDisplay)
                return;

            var raceId = GetRaceId();
            if (string.IsNullOrEmpty(raceId))
            {
                StatusDisplay.text = "Not configured\nUse the Admin button to set up this board.";
                return;
            }

            var race = RaceManager.GetRace(raceId);
            if (race == null)
            {
                StatusDisplay.text = $"[{raceId}]\nWaiting for race data...";
                return;
            }

            StatusDisplay.text = BuildStatusText(race);
        }

        private string BuildStatusText(Race race)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"<b>{race.Name}</b>");
            sb.AppendLine($"{race.TotalLaps} {(race.TotalLaps == 1 ? "lap" : "laps")}");
            sb.AppendLine();

            switch (race.State)
            {
                case RaceState.Idle:
                    sb.AppendLine(race.Contestants.Count == 0
                        ? "<color=#888888>Waiting for players...</color>"
                        : $"{race.Contestants.Count} registered");
                    foreach (var c in race.Contestants)
                        sb.AppendLine($"  {c.PlayerName}");
                    break;

                case RaceState.Countdown:
                    sb.AppendLine("<color=yellow>Get ready!</color>");
                    break;

                case RaceState.Racing:
                    sb.AppendLine("<color=green>RACING</color>");
                    sb.AppendLine();
                    foreach (var c in race.Contestants)
                    {
                        if (c.Finished && c.IsDnf)
                            sb.AppendLine($"  <color=red>DNF</color>  {c.PlayerName}");
                        else if (c.Finished && c.Position > 0)
                            sb.AppendLine($"  <color=yellow>P{c.Position}</color>  {c.PlayerName} - {c.FinishTime:F1}s");
                        else if (c.Finished)
                            sb.AppendLine($"  <color=yellow>Finished</color>  {c.PlayerName} - {c.FinishTime:F1}s");
                        else
                            sb.AppendLine($"  Lap {c.CurrentLap}/{race.TotalLaps}  {c.PlayerName}");
                    }
                    break;

                case RaceState.Finished:
                    sb.AppendLine("<b>FINISHED</b>");
                    sb.AppendLine();
                    sb.Append(race.GetResultsText());
                    break;
            }

            return sb.ToString();
        }

        // --- Button Interactions ---

        public void OnButtonInteract(RaceBoardButtonType buttonType, Player player)
        {
            var raceId = GetRaceId();
            if (string.IsNullOrEmpty(raceId))
            {
                player.Message(MessageHud.MessageType.Center, "This board is not configured yet");
                return;
            }

            var race = RaceManager.GetRace(raceId);
            if (race == null)
            {
                player.Message(MessageHud.MessageType.Center, "Race not found");
                return;
            }

            switch (buttonType)
            {
                case RaceBoardButtonType.Register:
                    HandleRegister(race, player);
                    break;
                case RaceBoardButtonType.Start:
                    HandleStart(race, player);
                    break;
                case RaceBoardButtonType.Reset:
                    HandleReset(race, player);
                    break;
            }
        }

        private void HandleRegister(Race race, Player player)
        {
            if (race.State != RaceState.Idle)
            {
                player.Message(MessageHud.MessageType.Center, $"{race.Name} is already underway");
                return;
            }

            if (race.IsRegistered(player.GetZDOID()))
                RaceManager.SendUnregister(race.RaceId, player.GetZDOID());
            else
                RaceManager.SendRegister(race.RaceId, player.GetPlayerName(), player.GetZDOID());
        }

        private void HandleStart(Race race, Player player)
        {
            if (race.State != RaceState.Idle)
            {
                player.Message(MessageHud.MessageType.Center, $"{race.Name} is already underway");
                return;
            }

            if (race.Contestants.Count == 0)
            {
                player.Message(MessageHud.MessageType.Center, "No contestants registered");
                return;
            }

            RaceManager.SendStartCountdown(race.RaceId);
        }

        private void HandleReset(Race race, Player player)
        {
            if (race.State == RaceState.Idle)
            {
                player.Message(MessageHud.MessageType.Center, "Nothing to reset");
                return;
            }

            RaceManager.SendReset(race.RaceId);
        }

        // --- Configure (called by RaceBoardAdminGui on confirm) ---

        public void Configure(string raceId, string name, int laps)
        {
            // Claim ZDO ownership and persist config locally
            _netView.ClaimOwnership();
            _netView.GetZDO().Set(ZdoKeyRaceId, raceId);
            _netView.GetZDO().Set(ZdoKeyName,   name);
            _netView.GetZDO().Set(ZdoKeyLaps,   laps);

            // Drive RaceManager state via RPCs
            var existing = RaceManager.GetRace(raceId);
            if (existing == null)
                RaceManager.SendCreateRace(raceId, name, laps);
            else
            {
                RaceManager.SendSetName(raceId, name);
                RaceManager.SendSetLaps(raceId, laps);
            }

            SuperVikingKart.DebugLog($"RaceBoard - Configured [{raceId}] \"{name}\" ({laps} laps)");
        }

        // --- ZDO Accessors ---

        public string GetRaceId()
        {
            return _netView?.GetZDO()?.GetString(ZdoKeyRaceId) ?? "";
        }

        public string GetRaceName()
        {
            return _netView?.GetZDO()?.GetString(ZdoKeyName) ?? "";
        }

        public int GetLaps()
        {
            return _netView?.GetZDO()?.GetInt(ZdoKeyLaps, 1) ?? 1;
        }

        // --- Hoverable ---

        public string GetHoverText()
        {
            var raceId = GetRaceId();
            if (string.IsNullOrEmpty(raceId))
                return "Race Board\n<color=grey>Not configured</color>";

            var race = RaceManager.GetRace(raceId);
            return race == null
                ? $"Race Board\n<color=grey>[{raceId}]</color>"
                : $"Race Board\n{race.Name}";
        }

        public string GetHoverName() => "Race Board";
    }

    /// <summary>
    /// Shared static admin GUI for all RaceBoard instances.
    /// Only one panel exists at a time, parented to GUIManager.CustomGUIFront.
    /// Rebuilt on each scene change via GUIManager.OnCustomGUIAvailable.
    /// Populated from the interacted board's ZDO on open.
    /// </summary>
    internal static class RaceBoardAdminGui
    {
        private static GameObject _panel;
        private static RaceBoardComponent _currentBoard;

        private static InputField _raceIdField;
        private static InputField _nameField;
        private static InputField _lapsField;

        // --- Init ---

        /// <summary>
        /// Rebuilds the panel on every scene change.
        /// Subscribed to GUIManager in SuperVikingKart.Awake
        /// </summary>
        public static void Build()
        {
            if (!GUIManager.CustomGUIFront)
                return;

            // Destroy any leftover panel from previous scene
            if (_panel)
            {
                UnityEngine.Object.DestroyImmediate(_panel);
                _panel = null;
            }

            // Root panel — Valheim woodpanel style, centered, draggable
            _panel = GUIManager.Instance.CreateWoodpanel(
                GUIManager.CustomGUIFront.transform,
                anchorMin: new Vector2(0.5f, 0.5f),
                anchorMax: new Vector2(0.5f, 0.5f),
                position: Vector2.zero,
                width: 420f,
                height: 250f,
                draggable: true);

            _panel.SetActive(false);

            var layout = _panel.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(20, 20, 20, 20);
            layout.spacing = 10f;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.UpperLeft;

            // Title
            var title = GUIManager.Instance.CreateText(
                "Configure Race Board",
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

            // Name row
            AddLabeledField("Name", out _nameField,
                InputField.ContentType.Standard, "Meadows Grand Prix");

            // Laps row
            AddLabeledField("Laps", out _lapsField,
                InputField.ContentType.IntegerNumber, "1");

            // Button row
            var buttonRow = new GameObject("ButtonRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            buttonRow.transform.SetParent(_panel.transform, false);
            var buttonLayout = buttonRow.GetComponent<HorizontalLayoutGroup>();
            buttonLayout.spacing = 10f;
            buttonLayout.childForceExpandWidth = true;
            buttonLayout.childForceExpandHeight = true;
            buttonLayout.childAlignment = TextAnchor.MiddleCenter;

            var confirmGo = GUIManager.Instance.CreateButton(
                "Confirm",
                buttonRow.transform,
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero,
                width: 160f, height: 40f);
            confirmGo.GetComponent<Button>().onClick.AddListener(OnConfirm);

            var cancelGo = GUIManager.Instance.CreateButton(
                "Cancel",
                buttonRow.transform,
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero,
                width: 160f, height: 40f);
            cancelGo.GetComponent<Button>().onClick.AddListener(Close);

            SuperVikingKart.DebugLog("RaceBoardAdminGui - Panel built");
        }

        // --- Open / Close ---

        public static void Open(RaceBoardComponent board)
        {
            if (_panel == null)
            {
                SuperVikingKart.DebugLog("RaceBoardAdminGui - Panel not built yet, skipping open");
                return;
            }

            _currentBoard = board;

            // Populate from current ZDO values
            _raceIdField.text = board.GetRaceId();
            _nameField.text   = board.GetRaceName();
            _lapsField.text   = board.GetLaps().ToString();

            _panel.SetActive(true);
            GUIManager.BlockInput(true);

            SuperVikingKart.DebugLog("RaceBoardAdminGui - Opened");
        }

        private static void Close()
        {
            if (_panel)
                _panel.SetActive(false);

            GUIManager.BlockInput(false);
            _currentBoard = null;

            SuperVikingKart.DebugLog("RaceBoardAdminGui - Closed");
        }

        // --- Confirm ---

        private static void OnConfirm()
        {
            if (_currentBoard == null)
            {
                Close();
                return;
            }

            var raceId = _raceIdField.text.Trim();
            var name   = _nameField.text.Trim();

            if (string.IsNullOrEmpty(raceId))
            {
                SuperVikingKart.DebugLog("RaceBoardAdminGui - Validation failed: empty raceId");
                return;
            }

            if (string.IsNullOrEmpty(name))
                name = raceId;

            if (!int.TryParse(_lapsField.text.Trim(), out var laps) || laps < 1)
            {
                SuperVikingKart.DebugLog("RaceBoardAdminGui - Validation failed: invalid laps");
                return;
            }

            _currentBoard.Configure(raceId, name, laps);
            Close();
        }

        // --- Helpers ---

        /// <summary>
        /// Creates a label + input field row inside the panel's vertical layout.
        /// </summary>
        private static void AddLabeledField(string label, out InputField field,
            InputField.ContentType contentType, string placeholder)
        {
            var row = new GameObject($"{label}Row", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            row.transform.SetParent(_panel.transform, false);
            var rowLayout = row.GetComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 10f;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = true;
            rowLayout.childAlignment = TextAnchor.MiddleLeft;

            // Label
            var labelGo = GUIManager.Instance.CreateText(
                label,
                row.transform,
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero,
                GUIManager.Instance.AveriaSerifBold, 16,
                Color.white,
                true, Color.black,
                90f, 30f, false);
            labelGo.AddComponent<LayoutElement>().preferredWidth = 90f;

            // Input field
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