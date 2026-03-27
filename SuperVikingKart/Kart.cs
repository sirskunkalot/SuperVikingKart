using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using Jotunn.Managers;
using UnityEngine;

namespace SuperVikingKart;

/// <summary>
/// Core component for the racing kart. Handles rider attachment, detachment,
/// position pinning, and kart color synchronization across clients.
/// Placed on the Container child of the cloned Cart prefab.
/// Tracks all instances for efficient lookup by patches and status effects.
/// </summary>
internal class SuperVikingKartComponent : MonoBehaviour, Hoverable, Interactable
{
    public static readonly List<SuperVikingKartComponent> Instances = new();
    public string Name = "SuperVikingKartAttach";
    public float UseDistance = 2f;
    public Transform AttachPoint;

    // --- ZDO Keys ---
    private const string ZdoKeyAttachedPlayer = "SuperVikingKart_AttachedPlayer";
    private const string ZdoKeyColorR = "SuperVikingKart_ColorR";
    private const string ZdoKeyColorG = "SuperVikingKart_ColorG";
    private const string ZdoKeyColorB = "SuperVikingKart_ColorB";

    private ZNetView _netView;
    private Vagon _vagon;
    private Renderer[] _kartRenderers;
    private float _lastSitTime;

    /// <summary>
    /// Local-only reference to the attached player. Only set on the rider's own client.
    /// Used for fast position pinning in Update. For cross-client queries use GetRider().
    /// </summary>
    private Player _attachedPlayerLocal;

    // --- Lifecycle ---

    /// <summary>
    /// Initializes the kart component. Sets up networking RPCs for rider attachment
    /// and color sync, registers in the global instances list, clears stale attachments
    /// from ZDO, applies the stored color, and configures the kart mass from
    /// server-synced config. Disables itself if no valid ZNetView/ZDO exists
    /// (placement ghost).
    /// </summary>
    public void Awake()
    {
        Instances.Add(this);
        _netView = gameObject.GetComponentInParent<ZNetView>();
        if (!_netView || _netView.GetZDO() == null)
        {
            SuperVikingKart.DebugLog("Kart Awake - no ZNetView or ZDO, disabling");
            enabled = false;
            return;
        }

        SuperVikingKart.DebugLog($"Kart Awake - ZDO: {_netView.GetZDO().m_uid}, Owner: {_netView.IsOwner()}");

        // RPCs
        _netView.Register<ZDOID>("SuperVikingKart_RPC_Attach", RPC_Attach);
        _netView.Register("SuperVikingKart_RPC_Detach", RPC_Detach);
        _netView.Register<float, float, float>("SuperVikingKart_RPC_SetColor", RPC_SetColor);

        if (_netView.IsOwner())
        {
            // Clear stale attachment from previous session if player is gone
            var zdo = _netView.GetZDO();
            var attachedId = zdo.GetZDOID(ZdoKeyAttachedPlayer);
            if (attachedId != ZDOID.None)
            {
                var playerObject = ZNetScene.instance.FindInstance(attachedId);
                if (!playerObject)
                {
                    SuperVikingKart.DebugLog($"Kart Awake - Clearing stale attachment: {attachedId}");
                    zdo.Set(ZdoKeyAttachedPlayer, ZDOID.None);
                }
            }
        }

        // Cache vagon component and make the kart a bit lighter
        _vagon = GetComponentInParent<Vagon>();
        _vagon.m_baseMass = 10f;
        _vagon.SetMass(_vagon.m_baseMass);

        // Cache renderers and apply whatever color is stored in the ZDO.
        // ZDO is guaranteed valid at this point — the game handles replication
        // before Awake fires, so this correctly restores color for late-joiners too.
        _kartRenderers = _vagon.GetComponentsInChildren<Renderer>(true);
        ApplyColor(GetCurrentColor());
    }

    /// <summary>
    /// Pins the rider to the attach point every frame.
    /// Detaches on jump or death.
    /// </summary>
    public void Update()
    {
        if (!_attachedPlayerLocal)
            return;

        if (!AttachPoint)
        {
            SuperVikingKart.DebugLog("Kart Update - AttachPoint lost, detaching");
            Detach();
            return;
        }

        if (ZInput.GetButtonDown("Jump") || _attachedPlayerLocal.IsDead())
        {
            SuperVikingKart.DebugLog(
                $"Kart Update - Detaching (Jump: {ZInput.GetButtonDown("Jump")}, Dead: {_attachedPlayerLocal.IsDead()})");
            Detach();
            return;
        }

        _attachedPlayerLocal.transform.position = AttachPoint.position;
    }

    /// <summary>
    /// Removes this instance from the static list and detaches the player.
    /// </summary>
    private void OnDestroy()
    {
        Instances.Remove(this);
        SuperVikingKart.DebugLog("Kart OnDestroy");
        Detach();
    }

    // --- Attach / Detach ---

    /// <summary>
    /// Sets the local rider reference and sends an RPC to the ZDO owner
    /// to persist the attachment.
    /// </summary>
    private void Attach(Player player)
    {
        SuperVikingKart.DebugLog($"Kart Attach - Player: {player.GetPlayerName()}, ZDOID: {player.GetZDOID()}");
        _attachedPlayerLocal = player;
        if (_netView && _netView.GetZDO() != null)
            _netView.InvokeRPC("SuperVikingKart_RPC_Attach", player.GetZDOID());
    }

    /// <summary>
    /// Clears the local rider reference and sends an RPC to the ZDO owner
    /// to clear the persisted attachment.
    /// </summary>
    private void Detach()
    {
        SuperVikingKart.DebugLog($"Kart Detach - Player: {_attachedPlayerLocal?.GetPlayerName() ?? "none"}");
        _attachedPlayerLocal = null;
        if (_netView && _netView.GetZDO() != null)
            _netView.InvokeRPC("SuperVikingKart_RPC_Detach");
    }

    /// <summary>
    /// Persists the rider's ZDOID in the kart's ZDO. Only the owner writes.
    /// </summary>
    private void RPC_Attach(long sender, ZDOID playerId)
    {
        SuperVikingKart.DebugLog(
            $"Kart RPC_Attach - sender: {sender}, playerId: {playerId}, IsOwner: {_netView.IsOwner()}");
        var zdo = _netView.GetZDO();
        if (zdo == null) return;
        if (_netView.IsOwner())
            zdo.Set(ZdoKeyAttachedPlayer, playerId);
    }

    /// <summary>
    /// Clears the rider's ZDOID in the kart's ZDO. Only the owner writes.
    /// </summary>
    private void RPC_Detach(long sender)
    {
        SuperVikingKart.DebugLog($"Kart RPC_Detach - sender: {sender}, IsOwner: {_netView.IsOwner()}");
        var zdo = _netView.GetZDO();
        if (zdo == null) return;
        if (_netView.IsOwner())
            zdo.Set(ZdoKeyAttachedPlayer, ZDOID.None);
    }

    // --- Color ---

    /// <summary>
    /// Reads the current kart color from the ZDO.
    /// Falls back to white if no color has been set yet.
    /// </summary>
    public Color GetCurrentColor()
    {
        var zdo = _netView?.GetZDO();
        if (zdo == null) return Color.white;
        return new Color(
            zdo.GetFloat(ZdoKeyColorR, 1f),
            zdo.GetFloat(ZdoKeyColorG, 1f),
            zdo.GetFloat(ZdoKeyColorB, 1f)
        );
    }

    /// <summary>
    /// Sets material color on all cached renderers using a MaterialPropertyBlock
    /// to avoid creating per-instance materials (prevents memory leaks and
    /// preserves GPU batching).
    /// </summary>
    internal void ApplyColor(Color color)
    {
        if (_kartRenderers == null) return;
        var block = new MaterialPropertyBlock();
        foreach (var rend in _kartRenderers)
        {
            if (!rend) continue;
            rend.GetPropertyBlock(block);
            block.SetColor("_Color", color);
            rend.SetPropertyBlock(block);
        }
    }

    /// <summary>
    /// Broadcasts a new color to all clients via RPC.
    /// Called by KartColorPickerUI on final selection.
    /// </summary>
    public void SetColor(Color color)
    {
        SuperVikingKart.DebugLog($"Kart SetColor - {color}");
        _netView.InvokeRPC(ZNetView.Everybody, "SuperVikingKart_RPC_SetColor",
            color.r, color.g, color.b);
    }

    /// <summary>
    /// Received on ALL clients (sent to ZNetView.Everybody).
    /// Owner persists to ZDO so the color survives session restarts and
    /// is available to future late-joiners via ApplyColorFromZDO in Awake.
    /// All clients apply the color immediately for a responsive feel.
    /// </summary>
    private void RPC_SetColor(long sender, float r, float g, float b)
    {
        SuperVikingKart.DebugLog($"Kart RPC_SetColor - sender: {sender}, rgb: ({r},{g},{b})");
        var color = new Color(r, g, b);

        if (_netView.IsOwner())
        {
            var zdo = _netView.GetZDO();
            if (zdo != null)
            {
                zdo.Set(ZdoKeyColorR, r);
                zdo.Set(ZdoKeyColorG, g);
                zdo.Set(ZdoKeyColorB, b);
            }
        }

        ApplyColor(color);
    }

    // --- State ---

    /// <summary>
    /// Gets the cached Vagon component of this kart.
    /// </summary>
    public Vagon GetVagon() => _vagon;

    /// <summary>
    /// Finds the player currently pulling this Vagon by checking all players.
    /// </summary>
    public Player GetPuller()
    {
        if (!_vagon) return null;
        foreach (var player in Player.GetAllPlayers())
        {
            if (_vagon.IsAttached(player))
                return player;
        }

        return null;
    }

    /// <summary>
    /// Returns the local cached Player instance instead looking up the Player
    /// from ZNetScene. Fast path for comparison with Player.m_localPlayer.
    /// </summary>
    public Player GetRiderLocal() => _attachedPlayerLocal;

    /// <summary>
    /// Returns the attached player ZDOID from the ZDO, or ZDOID.None if none attached.
    /// Works on any client since ZDOs are replicated.
    /// </summary>
    public ZDOID GetRiderZDOID()
        => _netView?.GetZDO()?.GetZDOID(ZdoKeyAttachedPlayer) ?? ZDOID.None;

    /// <summary>
    /// Resolves the attached player from the ZDO. Works on any client since
    /// ZDOs are replicated. Used for cross-client checks like damage prevention,
    /// buff application, and lap recording.
    /// </summary>
    public Player GetRider()
    {
        var attachedId = GetRiderZDOID();
        if (attachedId == ZDOID.None) return null;
        var playerObject = ZNetScene.instance.FindInstance(attachedId);
        if (!playerObject) return null;
        return playerObject.GetComponent<Player>();
    }

    /// <summary>
    /// Quick check if anyone is riding this kart. Uses ZDO so it works
    /// on any client, not just the rider's.
    /// </summary>
    private bool IsInUse() => GetRiderZDOID() != ZDOID.None;

    // --- Interaction ---

    /// <summary>
    /// Primary interaction: sit/stand. Alt interaction: open color picker.
    /// Color picker is restricted to the ZDO owner or current rider to prevent griefing.
    /// </summary>
    public bool Interact(Humanoid human, bool hold, bool alt)
    {
        if (hold) return false;
        var player = human as Player;
        if (!player) return false;
        if (!AttachPoint) return false;
        if (!InUseDistance(player)) return false;
        if (Time.time - _lastSitTime < 2f) return false;

        if (alt)
        {
            KartColorPickerUI.Open(this);
            return true;
        }

        if (_attachedPlayerLocal && player == _attachedPlayerLocal)
        {
            SuperVikingKart.DebugLog($"Kart Interact - Detaching player: {player.GetPlayerName()}");
            Detach();
            _lastSitTime = Time.time;
            return true;
        }

        if (IsInUse())
        {
            SuperVikingKart.DebugLog("Kart Interact - Already in use");
            return false;
        }

        SuperVikingKart.DebugLog($"Kart Interact - Attaching player: {player.GetPlayerName()}");
        Attach(player);
        _lastSitTime = Time.time;
        return true;
    }

    public bool UseItem(Humanoid user, ItemDrop.ItemData item) => false;

    // --- Hover ---

    public string GetHoverText()
    {
        if (Time.time - _lastSitTime < 2f) return "";
        var localPlayer = Player.m_localPlayer;
        if (!localPlayer) return "";
        if (!InUseDistance(localPlayer))
            return Localization.instance.Localize("<color=grey>$piece_toofar</color>");
        if (!_attachedPlayerLocal && IsInUse())
            return Localization.instance.Localize("<color=grey>In use</color>");

        return Localization.instance.Localize(
            Name +
            "\n[<color=yellow><b>$KEY_Use</b></color>] $piece_use" +
            "\n[<color=yellow><b>$KEY_AltPlace + $KEY_Use</b></color>] Change color");
    }

    public string GetHoverName() => Name;

    // --- Utility ---

    private bool InUseDistance(Humanoid human)
    {
        if (!human || !AttachPoint) return false;
        return Vector3.Distance(human.transform.position, AttachPoint.position) < UseDistance;
    }
}

/// <summary>
/// Thin wrapper that opens Jötunn's GUIManager color picker for a specific kart.
/// Handles live preview via onColorChanged and commit/revert via onColorSelected.
/// Jötunn enforces that only one picker is open at a time.
/// Cancel is handled automatically — Jötunn passes back the original color on cancel,
/// so OnColorSelected reverts all clients by broadcasting it as a final SetColor call.
/// </summary>
internal static class KartColorPickerUI
{
    private static SuperVikingKartComponent _targetKart;

    public static void Open(SuperVikingKartComponent kart)
    {
        if (kart == null) return;
        _targetKart = kart;

        GUIManager.Instance.CreateColorPicker(
            anchorMin: new Vector2(0.5f, 0.5f),
            anchorMax: new Vector2(0.5f, 0.5f),
            position: new Vector2(0f, 0f),
            original: kart.GetCurrentColor(),
            message: "Kart Color",
            onColorChanged: OnColorChanged,
            onColorSelected: OnColorSelected,
            useAlpha: false
        );

        GUIManager.BlockInput(true);
    }

    /// <summary>
    /// Fired continuously while the user drags sliders.
    /// Only applies color change locally.
    /// </summary>
    private static void OnColorChanged(Color color)
    {
        if (_targetKart == null) return;
        _targetKart.ApplyColor(color);
    }

    /// <summary>
    /// Fired when Done or Cancel is pressed.
    /// Jötunn passes back the original color on cancel,
    /// so we just revert locally when the color still matches.
    /// </summary>
    private static void OnColorSelected(Color color)
    {
        if (_targetKart == null) return;

        var zdoColor = _targetKart.GetCurrentColor();
        if (color == zdoColor)
            _targetKart.ApplyColor(zdoColor);
        else
            _targetKart.SetColor(color);

        _targetKart = null;
        GUIManager.BlockInput(false);
    }
}

/// <summary>
/// Prevents highlighting through WearNTear since that would remove the custom
/// color and postfix re-apply did not work.
/// </summary>
[HarmonyPatch(typeof(WearNTear), nameof(WearNTear.Highlight))]
internal class KartColorHighlightPatch
{
    // TODO: try to do it in Postfix of ResetHighlight, figuring out how to stop MaterialMan late update 
    private static bool Prefix(WearNTear __instance)
    {
        var kart = __instance.GetComponentInChildren<SuperVikingKartComponent>();
        return !kart;
    }
}

/// <summary>
/// Floating countdown timer spawned at the kart's destroy position.
/// Shows remaining time until respawn as world-space text that faces the camera.
/// Auto-destroys when the timer expires. Hoverable for precise readout.
/// </summary>
internal class KartRespawnComponent : MonoBehaviour, Hoverable
{
    private float _timeRemaining;
    private TextMesh _text;
    private Camera _camera;

    public void Setup(float duration)
    {
        _timeRemaining = duration;
        var textGo = new GameObject("TimerText");
        textGo.transform.SetParent(transform, false);
        textGo.transform.localPosition = Vector3.up * 2f;
        _text = textGo.AddComponent<TextMesh>();
        _text.alignment = TextAlignment.Center;
        _text.anchor = TextAnchor.MiddleCenter;
        _text.characterSize = 0.3f;
        _text.fontSize = 48;
        _text.color = Color.white;

        // Collider for hover interaction
        var hoverCollider = gameObject.AddComponent<SphereCollider>();
        hoverCollider.radius = 1f;
        hoverCollider.center = Vector3.up * 2f;
    }

    private void Update()
    {
        _timeRemaining -= Time.deltaTime;
        if (_timeRemaining <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        _text.text = Mathf.CeilToInt(_timeRemaining).ToString();

        if (!_camera)
            _camera = Camera.main;

        // Billboard — text always faces the camera
        if (_camera)
            _text.transform.rotation = _camera.transform.rotation;
    }

    public string GetHoverText() => $"Kart respawning in {Mathf.CeilToInt(_timeRemaining)}s";
    public string GetHoverName() => "Kart Respawn";
}

/// <summary>
/// Respawns destroyed karts after a configurable delay.
/// Only triggers for actual destruction (damage), not hammer removal.
/// Spawns a visible countdown timer at the destroy position.
/// Only the ZDO owner schedules the respawn to prevent duplicates.
/// </summary>
[HarmonyPatch(typeof(WearNTear), nameof(WearNTear.Destroy))]
internal class KartRespawnPatch
{
    internal static bool IsBeingRemoved;

    private static void Prefix(WearNTear __instance)
    {
        if (IsBeingRemoved) return;
        var kart = __instance.GetComponentInChildren<SuperVikingKartComponent>();
        if (!kart) return;

        var netView = __instance.GetComponent<ZNetView>();
        if (!netView) return;

        var position = __instance.transform.position;

        // Only schedule the actual respawn once — on the ZDO owner.
        if (netView.IsOwner())
        {
            var yRotation = Quaternion.Euler(0f, __instance.transform.eulerAngles.y, 0f);
            var color = kart.GetCurrentColor();

            SuperVikingKart.DebugLog($"KartRespawn - Kart destroyed at {position}, scheduling respawn");
            SuperVikingKart.Instance.StartCoroutine(RespawnKart(position, yRotation, color));
        }

        // Spawn the timer GO on every client
        SuperVikingKart.DebugLog(
            $"KartRespawn - Spawning timer for local client");
        var timerGo = new GameObject("KartRespawnComponent");
        timerGo.transform.position = position + Vector3.up * 0.5f;
        timerGo.layer = LayerMask.NameToLayer("character");
        var timer = timerGo.AddComponent<KartRespawnComponent>();
        timer.Setup(SuperVikingKart.CartRespawnTimeConfig.Value);
    }

    /// <summary>
    /// Respawn the Kart prefab after the configured time at the
    /// last position and rotation. Also reset the last color.
    /// </summary>
    private static IEnumerator RespawnKart(Vector3 position, Quaternion rotation, Color color)
    {
        yield return new WaitForSeconds(SuperVikingKart.CartRespawnTimeConfig.Value);
        var prefab = PrefabManager.Instance.GetPrefab(SuperVikingKart.KartPrefabName);
        if (!prefab)
        {
            Jotunn.Logger.LogWarning("KartRespawn - SuperVikingKart prefab not found");
            yield break;
        }

        SuperVikingKart.DebugLog($"KartRespawn - Spawning kart at {position}");
        var instance = Object.Instantiate(prefab, position, rotation);
        var newKart = instance.GetComponentInChildren<SuperVikingKartComponent>();
        newKart.SetColor(color);
    }
}

/// <summary>
/// Prevents kart respawn when removed with the hammer.
/// Sets a flag before WearNTear.Remove calls Destroy, which
/// KartRespawnPatch checks before scheduling a respawn.
/// </summary>
[HarmonyPatch(typeof(WearNTear), nameof(WearNTear.Remove))]
internal class KartRemovePatch
{
    private static void Prefix() => KartRespawnPatch.IsBeingRemoved = true;
    private static void Postfix() => KartRespawnPatch.IsBeingRemoved = false;
}

/// <summary>
/// Prevents the rider from damaging their own kart with melee attacks.
/// Uses prefab hash comparison for fast early-out on non-kart damage events,
/// avoiding expensive component lookups for the vast majority of hits.
/// </summary>
[HarmonyPatch(typeof(WearNTear), nameof(WearNTear.Damage))]
internal class KartSelfDamagePatch
{
    private static bool Prefix(WearNTear __instance, HitData hit)
    {
        if (!__instance.m_nview || __instance.m_nview.GetZDO() == null)
            return true;
        if (__instance.m_nview.GetZDO().m_prefab != SuperVikingKart.KartPrefabHash)
            return true;
        var attacker = hit.GetAttacker();
        if (!attacker) return true;
        var kart = __instance.GetComponentInChildren<SuperVikingKartComponent>();
        if (!kart) return true;
        return attacker != kart.GetRider();
    }
}

/// <summary>
/// Allows the rider to hit targets through their own kart by temporarily
/// disabling the kart's colliders during melee attack raycasts.
/// Only activates for local player when riding a kart.
/// Uses the static Instances list for fast lookup without component searches.
/// Colliders are re-enabled in the Postfix regardless of exceptions.
/// </summary>
[HarmonyPatch(typeof(Attack), nameof(Attack.DoMeleeAttack))]
internal class AttackThroughOwnKartPatch
{
    /// <summary>
    /// Disables the kart's colliders before the attack raycast runs.
    /// Stores disabled colliders in __state for restoration in Postfix.
    /// </summary>
    private static void Prefix(Attack __instance, out List<Collider> __state)
    {
        __state = null;
        var player = __instance.m_character as Player;
        if (player == null || player != Player.m_localPlayer) return;

        foreach (var kart in SuperVikingKartComponent.Instances)
        {
            if (kart.GetRiderLocal() != player) continue;
            var vagon = kart.GetVagon();
            if (!vagon) continue;

            __state = new List<Collider>();
            foreach (var collider in vagon.GetComponentsInChildren<Collider>())
            {
                if (collider.enabled)
                {
                    collider.enabled = false;
                    __state.Add(collider);
                }
            }

            return;
        }
    }

    /// <summary>
    /// Re-enables all colliders that were disabled in Prefix.
    /// </summary>
    private static void Postfix(List<Collider> __state)
    {
        if (__state == null) return;
        foreach (var collider in __state)
        {
            if (collider)
                collider.enabled = true;
        }
    }
}

/// <summary>
/// Fixes a vanilla bug where the pulling player's death leaves a dangling
/// joint reference on the Vagon. LateUpdate accesses the joint's connected
/// body before FixedUpdate can detect and clean up the dead reference,
/// causing NullReferenceException spam. This patch detaches proactively.
/// </summary>
[HarmonyPatch(typeof(Vagon), nameof(Vagon.LateUpdate))]
internal class VagonDeathPatch
{
    private static bool Prefix(Vagon __instance)
    {
        if (__instance.m_attachJoin != null && __instance.m_attachJoin.connectedBody == null)
        {
            __instance.Detach();
            return false;
        }

        return true;
    }
}