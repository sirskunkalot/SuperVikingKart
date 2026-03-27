using System.Collections.Generic;
using Jotunn.Managers;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SuperVikingKart;

internal enum RaceLineRole
{
    StartFinish = 0,
    Start = 1,
    Finish = 2
}

/// <summary>
/// Root component
/// </summary>
internal class RaceLineComponent : MonoBehaviour, Hoverable, Interactable
{
    // --- ZDO Keys ---
    private const string ZdoKeyRaceId = "SuperVikingKart_RaceLine_RaceId";
    private const string ZdoKeyRole = "SuperVikingKart_RaceLine_Role";

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

        // Destroy arrow when not in ghost mode
        DestroyImmediate(transform.Find("DirectionArrow").gameObject);

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

        // Update label
        UpdateLabel();
    }

    private void UpdateLabel()
    {
        if (!Label)
            return;

        var raceId = GetRaceId();
        if (string.IsNullOrEmpty(raceId))
        {
            Label.text = "Not configured";
            return;
        }

        var race = RaceManager.GetRace(raceId);
        if (race == null)
        {
            Label.text = $"[{raceId}] Waiting for race data...";
            return;
        }

        Label.text = $"{race.Name} ({GetRole()})";
    }

    /// <summary>
    /// Called by RaceLineTrigger when a collider enters.
    /// Only the puller's client initiates collection to prevent duplicates.
    /// </summary>
    public void OnRaceLineTriggerEnter(Collider other)
    {
        SuperVikingKart.DebugLog(
            $"RaceLine trigger entered by: {other.name} (parent: {other.transform.root.name})");

        // 1. Find the Kart component
        var kart = other.GetComponentInParent<SuperVikingKartComponent>();
        if (kart == null)
            return;

        // 2. Resolve puller from kart via Player.AllPlayers
        var puller = kart.GetPuller();
        if (puller == null)
            return;

        // 3. Only the puller's own client sends RPCs
        if (puller != Player.m_localPlayer)
            return;

        // 4. Race must exist and be in Racing state
        var raceId = GetRaceId();
        if (string.IsNullOrEmpty(raceId))
            return;
        var race = RaceManager.GetRace(raceId);
        if (race == null || race.State != RaceState.Racing)
            return;

        // 5. Direction guard - must be moving in the line's +Z direction
        var rb = kart.GetComponentInParent<Rigidbody>();
        if (rb != null && rb.linearVelocity.sqrMagnitude > 0.01f)
        {
            var dot = Vector3.Dot(rb.linearVelocity.normalized, transform.forward);
            if (dot <= 0f)
                return;
        }

        // 6. Process the puller and rider as a pair so they always cross together.
        //    The rider ID may be None if the kart is unoccupied.
        var pullerId = puller.GetZDOID();
        var riderId = kart.GetRiderZDOID();

        ProcessCrossing(race, raceId, pullerId);

        if (riderId != ZDOID.None)
            ProcessCrossing(race, raceId, riderId);
    }

    /// <summary>
    /// Applies cooldown and sends the appropriate crossing RPC for a single contestant.
    /// Separated from OnRaceLineTriggerEnter so the puller and rider go through
    /// identical logic without duplicating guards.
    /// </summary>
    private void ProcessCrossing(Race race, string raceId, ZDOID playerId)
    {
        var contestant = race.GetContestant(playerId);
        if (contestant == null)
            return;

        if (_cooldowns.TryGetValue(playerId, out var cooldownUntil) && Time.time < cooldownUntil)
            return;

        _cooldowns[playerId] = Time.time + CooldownSeconds;

        switch (GetRole())
        {
            case RaceLineRole.StartFinish:
                if (!contestant.CrossedStart)
                    RaceManager.SendCrossedStart(raceId, playerId);
                else
                    RaceManager.SendLap(raceId, playerId);
                break;
            case RaceLineRole.Start:
                if (!contestant.CrossedStart)
                    RaceManager.SendCrossedStart(raceId, playerId);
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

        SuperVikingKart.DebugLog($"RaceLine - Configured [{raceId}] {role}");
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
        string info;
        var raceId = GetRaceId();
        var race = RaceManager.GetRace(raceId);
        if (string.IsNullOrEmpty(raceId))
            info = "Not configured";
        else if (race == null)
            info = $"[{raceId}] Waiting for race data...";
        else
            info = $"{race.Name} ({GetRole()})";

        if (!SynchronizationManager.Instance.PlayerIsAdmin)
            return $"Race Line\n<color=grey>{info}</color>";

        return Localization.instance.Localize(
            $"Race Line\n{info}" +
            "\n[<color=yellow><b>$KEY_Use</b></color>] Configure");
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
    private static GameObject _panel;
    private static RaceLineComponent _currentLine;
    private static InputField _raceIdField;
    private static Dropdown _roleDropdown;

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
            width: 420f, height: 210f,
            draggable: true);
        _panel.SetActive(false);

        var layout = _panel.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(20, 20, 20, 20);
        layout.spacing = 10f;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        layout.childAlignment = TextAnchor.UpperLeft;

        // ----- Title -----
        var title = GUIManager.Instance.CreateText(
            "Configure Race Line", _panel.transform,
            new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero,
            GUIManager.Instance.AveriaSerifBold, 20, GUIManager.Instance.ValheimOrange,
            true, Color.black, 380f, 30f, false);
        title.GetComponent<Text>().alignment = TextAnchor.MiddleCenter;
        var titleLE = title.AddComponent<LayoutElement>();
        titleLE.preferredHeight = 30f;
        titleLE.minHeight = 30f;
        titleLE.flexibleHeight = 0f;

        // ----- Race ID -----
        AddLabeledField("Race ID", out _raceIdField,
            InputField.ContentType.Standard, "meadows_gp", fieldHeight: 30f);

        // ----- Role -----
        var roleRow = new GameObject("RoleRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        roleRow.transform.SetParent(_panel.transform, false);
        var roleRowLE = roleRow.AddComponent<LayoutElement>();
        roleRowLE.preferredHeight = 30f;
        roleRowLE.minHeight = 30f;
        roleRowLE.flexibleHeight = 0f;
        var roleLayout = roleRow.GetComponent<HorizontalLayoutGroup>();
        roleLayout.spacing = 10f;
        roleLayout.childForceExpandWidth = false;
        roleLayout.childForceExpandHeight = true;
        roleLayout.childAlignment = TextAnchor.MiddleLeft;

        var roleLabelGo = GUIManager.Instance.CreateText(
            "Role", roleRow.transform,
            new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero,
            GUIManager.Instance.AveriaSerifBold, 16, Color.white,
            true, Color.black, 90f, 30f, false);
        var roleLabelLE = roleLabelGo.AddComponent<LayoutElement>();
        roleLabelLE.preferredWidth = 90f;
        roleLabelLE.minWidth = 90f;
        roleLabelLE.flexibleWidth = 0f;
        roleLabelLE.preferredHeight = 30f;
        roleLabelLE.minHeight = 30f;
        roleLabelLE.flexibleHeight = 0f;

        var dropdownGo = GUIManager.Instance.CreateDropDown(
            roleRow.transform,
            new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero,
            16, width: 260f, height: 30f);
        var dropdownLE = dropdownGo.AddComponent<LayoutElement>();
        dropdownLE.preferredWidth = 260f;
        dropdownLE.minWidth = 260f;
        dropdownLE.flexibleWidth = 0f;
        dropdownLE.preferredHeight = 30f;
        dropdownLE.minHeight = 30f;
        dropdownLE.flexibleHeight = 0f;
        _roleDropdown = dropdownGo.GetComponent<Dropdown>();
        _roleDropdown.ClearOptions();
        _roleDropdown.AddOptions(new List<string> { "StartFinish", "Start", "Finish" });

        // ----- Buttons -----
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

        var cancelGo = GUIManager.Instance.CreateButton("Cancel", buttonRow.transform,
            new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, 160f, 40f);
        var cancelLE = cancelGo.AddComponent<LayoutElement>();
        cancelLE.preferredWidth = 160f;
        cancelLE.minWidth = 160f;
        cancelLE.flexibleWidth = 0f;
        cancelLE.preferredHeight = 40f;
        cancelLE.minHeight = 40f;
        cancelLE.flexibleHeight = 0f;
        cancelGo.GetComponent<Button>().onClick.AddListener(Close);

        var confirmGo = GUIManager.Instance.CreateButton("Confirm", buttonRow.transform,
            new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, 160f, 40f);
        var confirmLE = confirmGo.AddComponent<LayoutElement>();
        confirmLE.preferredWidth = 160f;
        confirmLE.minWidth = 160f;
        confirmLE.flexibleWidth = 0f;
        confirmLE.preferredHeight = 40f;
        confirmLE.minHeight = 40f;
        confirmLE.flexibleHeight = 0f;
        confirmGo.GetComponent<Button>().onClick.AddListener(OnConfirm);

        SuperVikingKart.DebugLog("RaceLineAdminGui - Panel built");
    }

    public static void Open(RaceLineComponent line)
    {
        if (_panel == null)
        {
            SuperVikingKart.DebugLog("RaceLineAdminGui - Panel not built yet, skipping open");
            return;
        }

        _currentLine = line;
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

    private static void OnConfirm()
    {
        if (_currentLine == null)
        {
            Close();
            return;
        }

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

    private static void AddLabeledField(string label, out InputField field,
        InputField.ContentType contentType, string placeholder, float fieldHeight)
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

        var labelGo = GUIManager.Instance.CreateText(
            label, row.transform,
            new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero,
            GUIManager.Instance.AveriaSerifBold, 16, Color.white,
            true, Color.black, 90f, fieldHeight, false);
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

        var inputGo = GUIManager.Instance.CreateInputField(
            row.transform,
            new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero,
            contentType, placeholder, 16, width: 260f, height: fieldHeight);
        var inputLE = inputGo.AddComponent<LayoutElement>();
        inputLE.preferredWidth = 260f;
        inputLE.minWidth = 260f;
        inputLE.flexibleWidth = 0f;
        inputLE.preferredHeight = fieldHeight;
        inputLE.minHeight = fieldHeight;
        inputLE.flexibleHeight = 0f;

        field = inputGo.GetComponent<InputField>();
        field.lineType = InputField.LineType.SingleLine;
    }
}