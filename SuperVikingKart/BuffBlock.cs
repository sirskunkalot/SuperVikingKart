using System.Linq;
using UnityEngine;

namespace SuperVikingKart;

internal enum BuffTarget
{
    Puller,
    Rider,
    Both
}

internal enum BlockType
{
    Buff,
    Debuff,
    Mystery
}

internal enum BuffType
{
    Buff,
    Debuff
}

internal class BuffDefinition
{
    public string Name;
    public string StatusEffect;
    public BuffTarget Target;
    public BuffType Type;

    public BuffDefinition(string name, string statusEffect, BuffTarget target, BuffType type = BuffType.Buff)
    {
        Name = name;
        StatusEffect = statusEffect;
        Target = target;
        Type = type;
    }
}

/// <summary>
/// Collectible block that applies random buffs/debuffs when a kart drives through it.
/// Collection flow:
/// 1. Puller's client detects trigger and sends RequestCollection RPC to ZDO owner
/// 2. Owner validates (prevents double collection) and broadcasts ApplyBuff + Collected RPCs
/// 3. Each client checks if local player is puller/rider and applies effect accordingly
/// 4. Block visual is hidden and respawns after configurable delay
/// </summary>
internal class BuffBlockComponent : MonoBehaviour
{
    public GameObject Visual;
    public BlockType BlockType;
    public GameObject CollectEffectPrefab;

    private const string ZdoKeyIsActive = "SuperVikingKart_BuffBlockActive";

    private ZNetView _netView;
    private float _respawnTimer;

    private static readonly BuffDefinition[] Buffs =
    {
        // Puller
        new("Speed Boost", "SuperVikingKart_SpeedBoost", BuffTarget.Puller),
        new("Stamina Regen", "SuperVikingKart_StaminaRegen", BuffTarget.Puller),
        new("Stamina Burst", "SuperVikingKart_StaminaBurst", BuffTarget.Puller),
        // Rider
        new("Shield", "SuperVikingKart_Shield", BuffTarget.Rider),
        new("Health Regen", "SuperVikingKart_HealthRegen", BuffTarget.Rider),
        new("Health Burst", "SuperVikingKart_HealthBurst", BuffTarget.Rider),
        new("Ooze Bombs", "SuperVikingKart_OozeBombs", BuffTarget.Rider),
        new("Bile Bombs", "SuperVikingKart_BileBombs", BuffTarget.Rider),
        new("Smoke Bombs", "SuperVikingKart_SmokeBombs", BuffTarget.Rider),
        new("Fire Arrows", "SuperVikingKart_FireArrows", BuffTarget.Rider),
        new("Harpoon", "SuperVikingKart_Harpoon", BuffTarget.Rider),
        new("Berserk", "SuperVikingKart_Berserk", BuffTarget.Rider),
        // Both
        new("Living Dead", "SuperVikingKart_LivingDead", BuffTarget.Both),
    };

    private static readonly BuffDefinition[] Debuffs =
    {
        // Puller
        new("Frost", "SuperVikingKart_Frost", BuffTarget.Puller, BuffType.Debuff),
        new("Tarred", "SuperVikingKart_Tarred", BuffTarget.Puller, BuffType.Debuff),
        new("Bouncy", "SuperVikingKart_Bounce", BuffTarget.Puller, BuffType.Debuff),
        // Rider
        new("Poison", "SuperVikingKart_Poison", BuffTarget.Rider, BuffType.Debuff),
        new("Burning", "SuperVikingKart_Burn", BuffTarget.Rider, BuffType.Debuff),
        new("Stagger", "SuperVikingKart_Stagger", BuffTarget.Rider, BuffType.Debuff),
        new("Disarm", "SuperVikingKart_Disarm", BuffTarget.Rider, BuffType.Debuff),
        // Both
        new("Wet", "Wet", BuffTarget.Both, BuffType.Debuff),
        new("Shock", "SuperVikingKart_Shock", BuffTarget.Both, BuffType.Debuff),
        new("Blind", "SuperVikingKart_Blind", BuffTarget.Both, BuffType.Debuff),
    };

    public static readonly BuffDefinition[] AllEffects = Buffs.Concat(Debuffs).ToArray();

    private BuffDefinition[] ActiveEffects => BlockType switch
    {
        BlockType.Buff => Buffs,
        BlockType.Debuff => Debuffs,
        BlockType.Mystery => AllEffects,
        _ => Buffs
    };

    // --- Lifecycle ---

    /// <summary>
    /// Initializes the buff block. Sets up networking RPCs for the collection flow
    /// and restores the visual state from ZDO persistence.
    /// Disables itself if no valid ZNetView/ZDO exists (placement ghost).
    /// </summary>
    private void Awake()
    {
        _netView = GetComponent<ZNetView>();
        if (!_netView || _netView.GetZDO() == null)
        {
            SuperVikingKart.DebugLog("BuffBlock Awake - no ZNetView or ZDO, disabling");
            enabled = false;
            return;
        }

        SuperVikingKart.DebugLog($"BuffBlock Awake - ZDO: {_netView.GetZDO().m_uid}, Owner: {_netView.IsOwner()}");

        _netView.Register<int, ZDOID>("SuperVikingKart_RPC_RequestCollection", RPC_RequestCollection);
        _netView.Register<int>("SuperVikingKart_RPC_BuffBlockCollected", RPC_BuffBlockCollected);
        _netView.Register("SuperVikingKart_RPC_BuffBlockRespawn", RPC_BuffBlockRespawn);
        _netView.Register<int, ZDOID>("SuperVikingKart_RPC_ApplyBuff", RPC_ApplyBuff);

        // Restore visual state from ZDO on load
        var isActive = _netView.GetZDO().GetBool(ZdoKeyIsActive, true);
        SuperVikingKart.DebugLog($"BuffBlock Awake - IsActive: {isActive}");
        SetVisual(isActive);
    }

    /// <summary>
    /// Owner-only update. Counts down the respawn timer and broadcasts
    /// respawn RPC when the timer expires.
    /// </summary>
    private void Update()
    {
        if (!_netView.IsOwner())
            return;

        if (IsActive())
            return;

        _respawnTimer -= Time.deltaTime;
        if (_respawnTimer <= 0f)
        {
            SuperVikingKart.DebugLog("BuffBlock - Respawn timer expired, sending respawn RPC");
            _netView.InvokeRPC(ZNetView.Everybody, "SuperVikingKart_RPC_BuffBlockRespawn");
        }
    }

    // --- Collection ---

    /// <summary>
    /// Called by BuffBlockTrigger when a collider enters.
    /// Only the puller's client initiates collection to prevent duplicates.
    /// Picks a random effect from the active pool and sends a request to the ZDO owner.
    /// </summary>
    public void OnBuffBlockTriggerEnter(Collider other)
    {
        SuperVikingKart.DebugLog(
            $"BuffBlock trigger entered by: {other.name} (parent: {other.transform.root.name})");

        if (!IsActive())
        {
            SuperVikingKart.DebugLog("BuffBlock - Not active, ignoring trigger");
            return;
        }

        var localPlayer = Player.m_localPlayer;
        if (!localPlayer)
        {
            SuperVikingKart.DebugLog("BuffBlock - No local player");
            return;
        }

        var kart = other.GetComponentInParent<SuperVikingKartComponent>();
        if (!kart)
        {
            SuperVikingKart.DebugLog("BuffBlock - No kart found on collider");
            return;
        }

        var vagon = kart.GetVagon();
        if (!vagon)
        {
            SuperVikingKart.DebugLog("BuffBlock - No vagon found on kart");
            return;
        }

        var puller = kart.GetPuller();
        SuperVikingKart.DebugLog(
            $"BuffBlock - Puller: {puller?.GetPlayerName() ?? "none"}, LocalPlayer: {localPlayer.GetPlayerName()}");

        if (puller != localPlayer)
        {
            SuperVikingKart.DebugLog("BuffBlock - Local player is not the puller, ignoring");
            return;
        }

        var kartNetView = kart.GetComponentInParent<ZNetView>();
        if (!kartNetView)
        {
            SuperVikingKart.DebugLog("BuffBlock - No ZNetView on kart");
            return;
        }

        var kartId = kartNetView.GetZDO().m_uid;
        var buffIndex = Random.Range(0, ActiveEffects.Length);

        SuperVikingKart.DebugLog(
            $"BuffBlock - Requesting collection! Effect: {ActiveEffects[buffIndex].Name} (index: {buffIndex}), kartId: {kartId}");
        _netView.InvokeRPC("SuperVikingKart_RPC_RequestCollection", buffIndex, kartId);
    }

    // --- Collection Authority ---

    /// <summary>
    /// Runs on the ZDO owner only. Validates the collection request and
    /// broadcasts the effect application and visual update to all clients.
    /// Rejects duplicate collections from simultaneous triggers.
    /// </summary>
    private void RPC_RequestCollection(long sender, int buffIndex, ZDOID kartId)
    {
        SuperVikingKart.DebugLog(
            $"BuffBlock RPC_RequestCollection - sender: {sender}, buffIndex: {buffIndex}, kartId: {kartId}, IsOwner: {_netView.IsOwner()}");

        if (!_netView.IsOwner())
            return;

        if (!IsActive())
        {
            SuperVikingKart.DebugLog("BuffBlock RPC_RequestCollection - Already collected, rejecting");
            return;
        }

        // Mark as collected immediately to reject any further requests
        _netView.GetZDO().Set(ZdoKeyIsActive, false);
        _respawnTimer = SuperVikingKart.BuffBlockRespawnTimeConfig.Value;

        // Broadcast to all clients
        _netView.InvokeRPC(ZNetView.Everybody, "SuperVikingKart_RPC_ApplyBuff", buffIndex, kartId);
        _netView.InvokeRPC(ZNetView.Everybody, "SuperVikingKart_RPC_BuffBlockCollected", buffIndex);
    }

    // --- Buff Application ---

    /// <summary>
    /// Runs on all clients. Each client checks if the local player is the
    /// puller or rider of the collecting kart and applies the effect accordingly.
    /// Both puller and rider see the message regardless of who receives the effect.
    /// </summary>
    private void RPC_ApplyBuff(long sender, int buffIndex, ZDOID kartId)
    {
        SuperVikingKart.DebugLog(
            $"BuffBlock RPC_ApplyBuff - sender: {sender}, buffIndex: {buffIndex}, kartId: {kartId}");

        var effects = ActiveEffects;

        if (buffIndex < 0 || buffIndex >= effects.Length)
        {
            Jotunn.Logger.LogWarning($"BuffBlock RPC_ApplyBuff - Invalid buff index: {buffIndex}");
            return;
        }

        var localPlayer = Player.m_localPlayer;
        if (!localPlayer)
        {
            SuperVikingKart.DebugLog("BuffBlock RPC_ApplyBuff - No local player");
            return;
        }

        // Find the kart that collected this block
        var kartObject = ZNetScene.instance.FindInstance(kartId);
        if (!kartObject)
        {
            SuperVikingKart.DebugLog($"BuffBlock RPC_ApplyBuff - Kart not found for ZDOID: {kartId}");
            return;
        }

        var kart = kartObject.GetComponentInChildren<SuperVikingKartComponent>();
        if (!kart)
        {
            SuperVikingKart.DebugLog("BuffBlock RPC_ApplyBuff - No SuperVikingKartComponent on kart");
            return;
        }

        var vagon = kart.GetVagon();
        if (!vagon)
        {
            SuperVikingKart.DebugLog("BuffBlock RPC_ApplyBuff - No vagon on kart");
            return;
        }

        var buff = effects[buffIndex];
        var isPuller = vagon.IsAttached(localPlayer);
        var isRider = kart.GetRider() == localPlayer;

        SuperVikingKart.DebugLog(
            $"BuffBlock RPC_ApplyBuff - Player: {localPlayer.GetPlayerName()}, IsPuller: {isPuller}, IsRider: {isRider}, BuffTarget: {buff.Target}");

        var targetLabel = buff.Target switch
        {
            BuffTarget.Puller => "Puller",
            BuffTarget.Rider => "Rider",
            BuffTarget.Both => "Both",
            _ => "Unknown"
        };

        var prefix = buff.Type == BuffType.Debuff ? "Oh no" : "Yeah";

        // Apply effect to the correct player, show message to both
        switch (buff.Target)
        {
            case BuffTarget.Puller:
                if (isPuller)
                    ApplyToPlayer(localPlayer, buff);
                if (isPuller || isRider)
                    localPlayer.Message(MessageHud.MessageType.Center, $"{prefix}! {buff.Name} for {targetLabel}!");
                break;
            case BuffTarget.Rider:
                if (isRider)
                    ApplyToPlayer(localPlayer, buff);
                if (isPuller || isRider)
                    localPlayer.Message(MessageHud.MessageType.Center, $"{prefix}! {buff.Name} for {targetLabel}!");
                break;
            case BuffTarget.Both:
                if (isPuller || isRider)
                {
                    ApplyToPlayer(localPlayer, buff);
                    localPlayer.Message(MessageHud.MessageType.Center, $"{prefix}! {buff.Name} for {targetLabel}!");
                }

                break;
        }
    }

    /// <summary>
    /// Looks up the status effect by name hash and applies it to the player.
    /// </summary>
    private void ApplyToPlayer(Player player, BuffDefinition buff)
    {
        if (!player)
            return;

        var effect = ObjectDB.instance.GetStatusEffect(buff.StatusEffect.GetStableHashCode());
        if (effect == null)
        {
            Jotunn.Logger.LogWarning(
                $"BuffBlock ApplyToPlayer - Could not find status effect: {buff.StatusEffect}");
            return;
        }

        SuperVikingKart.DebugLog(
            $"BuffBlock ApplyToPlayer - Applying {buff.Name} ({buff.StatusEffect}) to {player.GetPlayerName()}");
        player.GetSEMan().AddStatusEffect(effect, true);
    }

    // --- Visual State ---

    /// <summary>
    /// Reads active state from ZDO. Defaults to true if not set.
    /// </summary>
    private bool IsActive()
    {
        var zdo = _netView.GetZDO();
        if (zdo == null)
            return false;
        return zdo.GetBool(ZdoKeyIsActive, true);
    }

    /// <summary>
    /// Toggles the visual child game object.
    /// </summary>
    private void SetVisual(bool active)
    {
        if (Visual)
            Visual.SetActive(active);
    }

    /// <summary>
    /// Runs on all clients. Hides the block visual, starts the respawn timer
    /// on the owner, and spawns an optional collection effect.
    /// </summary>
    private void RPC_BuffBlockCollected(long sender, int buffIndex)
    {
        SuperVikingKart.DebugLog(
            $"BuffBlock RPC_BuffBlockCollected - sender: {sender}, buffIndex: {buffIndex}, IsOwner: {_netView.IsOwner()}");

        if (_netView.IsOwner())
        {
            _netView.GetZDO().Set(ZdoKeyIsActive, false);
            _respawnTimer = SuperVikingKart.BuffBlockRespawnTimeConfig.Value;
        }

        if (CollectEffectPrefab)
        {
            Instantiate(CollectEffectPrefab, Visual.transform.position, Quaternion.identity);
        }

        SetVisual(false);
    }

    /// <summary>
    /// Runs on all clients. Restores the block visual and marks it as active
    /// in the ZDO on the owner.
    /// </summary>
    private void RPC_BuffBlockRespawn(long sender)
    {
        SuperVikingKart.DebugLog(
            $"BuffBlock RPC_BuffBlockRespawn - sender: {sender}, IsOwner: {_netView.IsOwner()}");

        if (_netView.IsOwner())
            _netView.GetZDO().Set(ZdoKeyIsActive, true);

        SetVisual(true);
    }
}

/// <summary>
/// Relays trigger events from the visual child to the parent BuffBlockComponent.
/// Needed because the trigger collider is on the Visual child object,
/// not on the root where BuffBlockComponent lives.
/// </summary>
internal class BuffBlockTrigger : MonoBehaviour
{
    public BuffBlockComponent BuffBlock;

    private void OnTriggerEnter(Collider other)
    {
        if (BuffBlock)
            BuffBlock.OnBuffBlockTriggerEnter(other);
    }
}

/// <summary>
/// Rotates and bobs the block visual for a floating item box look.
/// Attached to the Visual child of the buff block prefab.
/// </summary>
internal class BuffBlockSpin : MonoBehaviour
{
    public float RotationSpeed = 50f;
    public float BobSpeed = 2f;
    public float BobHeight = 0.15f;

    private Vector3 _startPosition;

    private void Start()
    {
        _startPosition = transform.localPosition;
    }

    private void Update()
    {
        transform.Rotate(Vector3.up, RotationSpeed * Time.deltaTime);
        var bob = Mathf.Sin(Time.time * BobSpeed) * BobHeight;
        transform.localPosition = _startPosition + Vector3.up * bob;
    }
}