using System;
using Jotunn.Managers;
using UnityEngine;
using UnityEngine.UI;

namespace SuperVikingKart;

internal enum RaceBoardButtonType
{
    Register,
    Start,
    Reset,
    Admin
}

/// <summary>
/// Attached to each button on the RaceBoard prefab.
/// Forwards player interactions to the parent RaceBoardComponent.
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
                RaceBoardButtonType.Start => GetStartHoverText(race),
                RaceBoardButtonType.Reset => GetResetHoverText(race),
                RaceBoardButtonType.Admin => "[<color=yellow><b>$KEY_Use</b></color>] Configure Board",
                _ => ""
            });
    }

    public string GetHoverName() => ButtonType switch
    {
        RaceBoardButtonType.Register => "Register",
        RaceBoardButtonType.Start => "Start Race",
        RaceBoardButtonType.Reset => "Reset Race",
        RaceBoardButtonType.Admin => "Configure Board",
        _ => ""
    };

    private string GetRegisterHoverText(Race race)
    {
        if (race == null)
            return "<color=grey>No race configured</color>";

        if (race.State == RaceState.Countdown)
            return "<color=grey>Race is about to begin</color>";

        if (race.State == RaceState.Finished)
            return "<color=grey>Race is already finished</color>";

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

        if (race.State == RaceState.Countdown)
            return "<color=grey>Race is about to begin</color>";

        if (race.State == RaceState.Racing)
            return "<color=grey>Race is already running</color>";

        if (race.State == RaceState.Finished)
            return "<color=grey>Race is already finished</color>";

        return "[<color=yellow><b>$KEY_Use</b></color>] Start Race";
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
/// Root component for the RaceBoard piece.
/// Persists race configuration (ID, name, laps, description) in the ZDO and
/// keeps the status TextMeshPro display in sync with RaceManager state changes.
/// Admin configuration is handled by the shared static RaceBoardAdminGui.
/// </summary>
internal class RaceBoardComponent : MonoBehaviour, Hoverable
{
    private const string ZdoKeyRaceId = "SuperVikingKart_RaceBoard_RaceId";
    private const string ZdoKeyName = "SuperVikingKart_RaceBoard_Name";
    private const string ZdoKeyLaps = "SuperVikingKart_RaceBoard_Laps";
    private const string ZdoKeyDescription = "SuperVikingKart_RaceBoard_Description";

    public TMPro.TextMeshPro StatusDisplay;
    public RaceBoardButton RegisterButton;
    public RaceBoardButton StartButton;
    public RaceBoardButton ResetButton;
    public RaceBoardButton AdminButton;

    private ZNetView _netView;

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

        // Only the ZDO owner is responsible for bootstrapping or syncing race state.
        if (!_netView.IsOwner()) return;

        var raceId = GetRaceId();
        if (string.IsNullOrEmpty(raceId)) return;

        var existingRace = RaceManager.GetRace(raceId);
        if (existingRace == null)
        {
            // No race exists yet — create one from the values stored in the ZDO.
            var name = GetRaceName();
            var laps = GetLaps();
            var description = GetDescription();
            SuperVikingKart.DebugLog(
                $"RaceBoard Awake - Bootstrapping race [{raceId}] \"{name}\" ({laps} laps)");
            RaceManager.SendCreateRace(raceId, name, laps, description);
        }
        else
        {
            // Race already exists — mirror its current values back into the ZDO.
            _netView.GetZDO().Set(ZdoKeyName, existingRace.Name);
            _netView.GetZDO().Set(ZdoKeyLaps, existingRace.TotalLaps);
            _netView.GetZDO().Set(ZdoKeyDescription, existingRace.Description);
            SuperVikingKart.DebugLog(
                $"RaceBoard Awake - Synced ZDO from RaceManager for [{raceId}]");
        }
    }

    private void OnEnable()
    {
        RaceManager.OnRaceChanged += OnRaceChanged;
        UpdateStatusDisplay();
        UpdateRegisterButtonText();
    }

    private void OnDisable()
    {
        RaceManager.OnRaceChanged -= OnRaceChanged;
    }

    /// <summary>
    /// Responds to race state changes broadcast by RaceManager.
    /// Ignores events that belong to a different race.
    /// If this client owns the ZDO, also writes any changed fields back to it.
    /// </summary>
    private void OnRaceChanged(string raceId)
    {
        if (raceId != GetRaceId()) return;

        if (_netView.IsOwner())
        {
            var race = RaceManager.GetRace(raceId);
            if (race != null)
            {
                if (GetRaceName() != race.Name)
                    _netView.GetZDO().Set(ZdoKeyName, race.Name);
                if (GetLaps() != race.TotalLaps)
                    _netView.GetZDO().Set(ZdoKeyLaps, race.TotalLaps);
                if (GetDescription() != race.Description)
                    _netView.GetZDO().Set(ZdoKeyDescription, race.Description);
            }
        }

        UpdateStatusDisplay();
        UpdateRegisterButtonText();
    }

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

    /// <summary>
    /// Builds the full status string shown on the board for a given race,
    /// including name, lap count, description, and per-state contestant details.
    /// </summary>
    private string BuildStatusText(Race race)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"<b>{race.Name}</b>");
        sb.AppendLine($"{race.TotalLaps} {(race.TotalLaps == 1 ? "lap" : "laps")}");

        if (!string.IsNullOrWhiteSpace(race.Description))
        {
            sb.AppendLine();
            sb.AppendLine($"<color=#aaaaaa><i>{race.Description}</i></color>");
        }

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
                        sb.AppendLine(
                            $"  <color=yellow>P{c.Position}</color>  {c.PlayerName} - {c.FinishTime:F1}s");
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

    private void UpdateRegisterButtonText()
    {
        if (!RegisterButton) return;

        var label = RegisterButton.GetComponentInChildren<TMPro.TextMeshPro>();
        if (label == null) return;

        var race = RaceManager.GetRace(GetRaceId());
        var player = Player.m_localPlayer;
        var isRegistered = race != null && player != null && race.IsRegistered(player.GetZDOID());
        label.text = isRegistered ? "Unregister" : "Register";
    }

    public void OnButtonInteract(RaceBoardButtonType buttonType, Player player)
    {
        var raceId = GetRaceId();
        if (string.IsNullOrEmpty(raceId) && buttonType != RaceBoardButtonType.Admin)
        {
            player.Message(MessageHud.MessageType.Center, "This board is not configured yet");
            return;
        }

        var race = RaceManager.GetRace(raceId);
        if (race == null && buttonType != RaceBoardButtonType.Admin)
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
            case RaceBoardButtonType.Admin:
                HandleAdmin(race, player);
                break;
        }
    }

    private void HandleRegister(Race race, Player player)
    {
        if (race.State == RaceState.Countdown)
        {
            player.Message(MessageHud.MessageType.Center, $"{race.Name} is about to start");
            return;
        }

        if (race.State == RaceState.Finished)
        {
            player.Message(MessageHud.MessageType.Center, $"{race.Name} is already finished");
            return;
        }

        if (race.IsRegistered(player.GetZDOID()))
            RaceManager.SendUnregister(race.RaceId, player.GetZDOID());
        else
            RaceManager.SendRegister(race.RaceId, player.GetPlayerName(), player.GetZDOID());
    }

    private void HandleStart(Race race, Player player)
    {
        if (race.State == RaceState.Countdown)
        {
            player.Message(MessageHud.MessageType.Center, $"{race.Name} is about to start");
            return;
        }

        if (race.State == RaceState.Racing)
        {
            player.Message(MessageHud.MessageType.Center, $"{race.Name} is already running");
            return;
        }

        if (race.State == RaceState.Finished)
        {
            player.Message(MessageHud.MessageType.Center, $"{race.Name} is already finished");
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

        if (race.State == RaceState.Racing)
        {
            RaceBoardResetConfirmGui.Open(this);
            return;
        }

        RaceManager.SendReset(race.RaceId);
    }

    private void HandleAdmin(Race race, Player player)
    {
        if (!SynchronizationManager.Instance.PlayerIsAdmin)
        {
            player.Message(MessageHud.MessageType.Center,
                "You need to be an admin to configure this board");
            return;
        }

        RaceBoardAdminGui.Open(this);
    }

    /// <summary>
    /// Persists new board configuration to the ZDO and applies it to
    /// RaceManager via RPCs. Called by RaceBoardAdminGui when the player
    /// confirms the admin panel.
    /// </summary>
    public void Configure(string raceId, string name, int laps, string description)
    {
        _netView.ClaimOwnership();
        _netView.GetZDO().Set(ZdoKeyRaceId, raceId);
        _netView.GetZDO().Set(ZdoKeyName, name);
        _netView.GetZDO().Set(ZdoKeyLaps, laps);
        _netView.GetZDO().Set(ZdoKeyDescription, description);

        var existing = RaceManager.GetRace(raceId);
        if (existing == null)
            RaceManager.SendCreateRace(raceId, name, laps, description);
        else
        {
            RaceManager.SendSetName(raceId, name);
            RaceManager.SendSetLaps(raceId, laps);
            RaceManager.SendSetDescription(raceId, description);
        }

        SuperVikingKart.DebugLog($"RaceBoard - Configured [{raceId}] \"{name}\" ({laps} laps)");
    }

    public string GetRaceId() => _netView?.GetZDO()?.GetString(ZdoKeyRaceId) ?? "";
    public string GetRaceName() => _netView?.GetZDO()?.GetString(ZdoKeyName) ?? "";
    public int GetLaps() => _netView?.GetZDO()?.GetInt(ZdoKeyLaps, 1) ?? 1;
    public string GetDescription() => _netView?.GetZDO()?.GetString(ZdoKeyDescription) ?? "";

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
/// Shared static admin panel used by all RaceBoardComponent instances.
/// A single panel GameObject is parented to GUIManager.CustomGUIFront and
/// rebuilt each scene via GUIManager.OnCustomGUIAvailable.
/// Fields are pre-filled from the interacting board's ZDO when the panel is opened.
/// </summary>
internal static class RaceBoardAdminGui
{
    private static GameObject _panel;
    private static RaceBoardComponent _currentBoard;
    private static InputField _raceIdField;
    private static InputField _nameField;
    private static InputField _lapsField;
    private static InputField _descriptionField;

    /// <summary>
    /// Constructs the panel hierarchy and wires up button listeners.
    /// Safe to call multiple times — destroys any existing panel before rebuilding.
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

        _panel = GUIManager.Instance.CreateWoodpanel(
            GUIManager.CustomGUIFront.transform,
            anchorMin: new Vector2(0.5f, 0.5f),
            anchorMax: new Vector2(0.5f, 0.5f),
            position: Vector2.zero,
            width: 420f,
            height: 320f,
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
        var titleLE = title.AddComponent<LayoutElement>();
        titleLE.preferredHeight = 30f;
        titleLE.minHeight = 30f;
        titleLE.flexibleHeight = 0f;

        // Input rows
        AddLabeledField("Race ID", out _raceIdField,
            InputField.ContentType.Standard, "meadows_gp",
            fieldHeight: 30f, multiLine: false);
        _raceIdField.onValueChanged.AddListener(OnRaceIdChanged);

        AddLabeledField("Name", out _nameField,
            InputField.ContentType.Standard, "Meadows Grand Prix",
            fieldHeight: 30f, multiLine: false);

        AddLabeledField("Laps", out _lapsField,
            InputField.ContentType.IntegerNumber, "1",
            fieldHeight: 30f, multiLine: false);

        AddLabeledField("Description", out _descriptionField,
            InputField.ContentType.Standard, "Optional track description...",
            fieldHeight: 60f, multiLine: true);

        // Cancel / Confirm buttons
        var buttonRow = new GameObject("ButtonRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        buttonRow.transform.SetParent(_panel.transform, false);
        var buttonRowLE = buttonRow.AddComponent<LayoutElement>();
        buttonRowLE.preferredHeight = 40f;
        buttonRowLE.minHeight = 40f;
        buttonRowLE.flexibleHeight = 0f;
        var buttonLayout = buttonRow.GetComponent<HorizontalLayoutGroup>();
        buttonLayout.spacing = 10f;
        buttonLayout.childForceExpandWidth = false;
        buttonLayout.childForceExpandHeight = false;
        buttonLayout.childAlignment = TextAnchor.MiddleCenter;

        var cancelGo = GUIManager.Instance.CreateButton(
            "Cancel",
            buttonRow.transform,
            new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero,
            width: 160f, height: 40f);
        var cancelLE = cancelGo.AddComponent<LayoutElement>();
        cancelLE.preferredWidth = 160f;
        cancelLE.minWidth = 160f;
        cancelLE.flexibleWidth = 0f;
        cancelLE.preferredHeight = 40f;
        cancelLE.minHeight = 40f;
        cancelLE.flexibleHeight = 0f;
        cancelGo.GetComponent<Button>().onClick.AddListener(Close);

        var confirmGo = GUIManager.Instance.CreateButton(
            "Confirm",
            buttonRow.transform,
            new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero,
            width: 160f, height: 40f);
        var confirmLE = confirmGo.AddComponent<LayoutElement>();
        confirmLE.preferredWidth = 160f;
        confirmLE.minWidth = 160f;
        confirmLE.flexibleWidth = 0f;
        confirmLE.preferredHeight = 40f;
        confirmLE.minHeight = 40f;
        confirmLE.flexibleHeight = 0f;
        confirmGo.GetComponent<Button>().onClick.AddListener(OnConfirm);

        SuperVikingKart.DebugLog("RaceBoardAdminGui - Panel built");
    }

    /// <summary>
    /// Populates the fields from the board's ZDO and shows the panel.
    /// </summary>
    public static void Open(RaceBoardComponent board)
    {
        if (_panel == null)
        {
            SuperVikingKart.DebugLog("RaceBoardAdminGui - Panel not built yet, skipping open");
            return;
        }

        _currentBoard = board;
        _raceIdField.text = board.GetRaceId();
        _nameField.text = board.GetRaceName();
        _lapsField.text = board.GetLaps().ToString();
        _descriptionField.text = board.GetDescription();
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

    /// <summary>
    /// When the Race ID field changes, auto-fills the remaining fields if a
    /// matching race already exists in RaceManager.
    /// </summary>
    private static void OnRaceIdChanged(string value)
    {
        var race = RaceManager.GetRace(value.Trim());
        if (race == null) return;

        _nameField.text = race.Name;
        _lapsField.text = race.TotalLaps.ToString();
        _descriptionField.text = race.Description;
    }

    /// <summary>
    /// Validates input, then forwards the configuration to the current board
    /// and closes the panel. Rejects empty race IDs or lap counts below 1.
    /// </summary>
    private static void OnConfirm()
    {
        if (_currentBoard == null)
        {
            Close();
            return;
        }

        var raceId = _raceIdField.text.Trim();
        if (string.IsNullOrEmpty(raceId))
        {
            SuperVikingKart.DebugLog("RaceBoardAdminGui - Validation failed: empty raceId");
            return;
        }

        var name = _nameField.text.Trim();
        if (string.IsNullOrEmpty(name))
            name = raceId;

        if (!int.TryParse(_lapsField.text.Trim(), out var laps) || laps < 1)
        {
            SuperVikingKart.DebugLog("RaceBoardAdminGui - Validation failed: invalid laps");
            return;
        }

        var description = _descriptionField.text;
        _currentBoard.Configure(raceId, name, laps, description);
        Close();
    }

    /// <summary>
    /// Creates a horizontal row containing a fixed-width label and an input field,
    /// then appends it to the panel's vertical layout.
    /// </summary>
    private static void AddLabeledField(
        string label,
        out InputField field,
        InputField.ContentType contentType,
        string placeholder,
        float fieldHeight,
        bool multiLine)
    {
        var row = new GameObject($"{label}Row", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        row.transform.SetParent(_panel.transform, false);
        var rowLE = row.AddComponent<LayoutElement>();
        rowLE.preferredHeight = fieldHeight;
        rowLE.minHeight = fieldHeight;
        rowLE.flexibleHeight = 0f;
        var rowLayout = row.GetComponent<HorizontalLayoutGroup>();
        rowLayout.spacing = 10f;
        rowLayout.childForceExpandWidth = false;
        rowLayout.childForceExpandHeight = true;
        rowLayout.childAlignment = TextAnchor.MiddleLeft;

        // Label — fixed width, overflow allowed so it never wraps or resizes.
        var labelGo = GUIManager.Instance.CreateText(
            label,
            row.transform,
            new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero,
            GUIManager.Instance.AveriaSerifBold, 16,
            Color.white,
            true, Color.black,
            90f, fieldHeight, false);
        var labelText = labelGo.GetComponent<Text>();
        if (labelText != null)
        {
            labelText.horizontalOverflow = HorizontalWrapMode.Overflow;
            labelText.verticalOverflow = VerticalWrapMode.Truncate;
            labelText.resizeTextForBestFit = false;
        }

        var labelLE = labelGo.AddComponent<LayoutElement>();
        labelLE.preferredWidth = 90f;
        labelLE.minWidth = 90f;
        labelLE.flexibleWidth = 0f;
        labelLE.preferredHeight = fieldHeight;
        labelLE.minHeight = fieldHeight;
        labelLE.flexibleHeight = 0f;

        // Input field — fixed size, never stretches to fill available space.
        var inputGo = GUIManager.Instance.CreateInputField(
            row.transform,
            new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero,
            contentType, placeholder, 16,
            width: 260f, height: fieldHeight);
        var inputLE = inputGo.AddComponent<LayoutElement>();
        inputLE.preferredWidth = 260f;
        inputLE.minWidth = 260f;
        inputLE.flexibleWidth = 0f;
        inputLE.preferredHeight = fieldHeight;
        inputLE.minHeight = fieldHeight;
        inputLE.flexibleHeight = 0f;

        field = inputGo.GetComponent<InputField>();
        if (multiLine)
        {
            field.lineType = InputField.LineType.MultiLineNewline;
            var textComponent = field.textComponent;
            if (textComponent != null)
            {
                textComponent.horizontalOverflow = HorizontalWrapMode.Wrap;
                textComponent.verticalOverflow = VerticalWrapMode.Truncate;
            }
        }
        else
        {
            field.lineType = InputField.LineType.SingleLine;
        }
    }
}

/// <summary>
/// Shared static confirmation dialog shown before resetting an active race.
/// Built and managed identically to RaceBoardAdminGui.
/// </summary>
internal static class RaceBoardResetConfirmGui
{
    private static GameObject _panel;
    private static RaceBoardComponent _currentBoard;

    public static void Build()
    {
        if (!GUIManager.CustomGUIFront)
            return;

        if (_panel)
        {
            UnityEngine.Object.DestroyImmediate(_panel);
            _panel = null;
        }

        _panel = GUIManager.Instance.CreateWoodpanel(
            GUIManager.CustomGUIFront.transform,
            anchorMin: new Vector2(0.5f, 0.5f),
            anchorMax: new Vector2(0.5f, 0.5f),
            position: Vector2.zero,
            width: 360f,
            height: 160f,
            draggable: false);
        _panel.SetActive(false);

        var layout = _panel.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(20, 20, 20, 20);
        layout.spacing = 16f;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        layout.childAlignment = TextAnchor.UpperCenter;

        // Message
        var message = GUIManager.Instance.CreateText(
            "A race is currently underway.\nDo you really want to reset?",
            _panel.transform,
            new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero,
            GUIManager.Instance.AveriaSerifBold, 18,
            Color.white,
            true, Color.black,
            320f, 60f, false);
        message.GetComponent<Text>().alignment = TextAnchor.MiddleCenter;
        var messageLE = message.AddComponent<LayoutElement>();
        messageLE.preferredHeight = 60f;
        messageLE.minHeight = 60f;
        messageLE.flexibleHeight = 0f;

        // Button row
        var buttonRow = new GameObject("ButtonRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        buttonRow.transform.SetParent(_panel.transform, false);
        var buttonRowLE = buttonRow.AddComponent<LayoutElement>();
        buttonRowLE.preferredHeight = 40f;
        buttonRowLE.minHeight = 40f;
        buttonRowLE.flexibleHeight = 0f;

        var buttonLayout = buttonRow.GetComponent<HorizontalLayoutGroup>();
        buttonLayout.spacing = 10f;
        buttonLayout.childForceExpandWidth = false;
        buttonLayout.childForceExpandHeight = false;
        buttonLayout.childAlignment = TextAnchor.MiddleCenter;

        var cancelGo = GUIManager.Instance.CreateButton(
            "Cancel",
            buttonRow.transform,
            new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero,
            width: 140f, height: 40f);
        var cancelLE = cancelGo.AddComponent<LayoutElement>();
        cancelLE.preferredWidth = 140f;
        cancelLE.minWidth = 140f;
        cancelLE.flexibleWidth = 0f;
        cancelLE.preferredHeight = 40f;
        cancelLE.minHeight = 40f;
        cancelLE.flexibleHeight = 0f;
        cancelGo.GetComponent<Button>().onClick.AddListener(Close);

        var confirmGo = GUIManager.Instance.CreateButton(
            "Reset",
            buttonRow.transform,
            new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero,
            width: 140f, height: 40f);
        var confirmLE = confirmGo.AddComponent<LayoutElement>();
        confirmLE.preferredWidth = 140f;
        confirmLE.minWidth = 140f;
        confirmLE.flexibleWidth = 0f;
        confirmLE.preferredHeight = 40f;
        confirmLE.minHeight = 40f;
        confirmLE.flexibleHeight = 0f;
        confirmGo.GetComponent<Button>().onClick.AddListener(OnConfirm);

        SuperVikingKart.DebugLog("RaceBoardResetConfirmGui - Panel built");
    }

    public static void Open(RaceBoardComponent board)
    {
        if (_panel == null)
        {
            SuperVikingKart.DebugLog("RaceBoardResetConfirmGui - Panel not built yet, skipping open");
            return;
        }

        _currentBoard = board;
        _panel.SetActive(true);
        GUIManager.BlockInput(true);
        SuperVikingKart.DebugLog("RaceBoardResetConfirmGui - Opened");
    }

    private static void Close()
    {
        if (_panel)
            _panel.SetActive(false);

        GUIManager.BlockInput(false);
        _currentBoard = null;
        SuperVikingKart.DebugLog("RaceBoardResetConfirmGui - Closed");
    }

    private static void OnConfirm()
    {
        if (_currentBoard != null)
            RaceManager.SendReset(_currentBoard.GetRaceId());

        Close();
    }
}