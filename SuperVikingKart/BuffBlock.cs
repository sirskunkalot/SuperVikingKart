using UnityEngine;

namespace SuperVikingKart
{
    internal enum BuffTarget
    {
        Puller,
        Rider,
        Both
    }

    internal class BuffDefinition
    {
        public string Name;
        public string StatusEffect;
        public BuffTarget Target;

        public BuffDefinition(string name, string statusEffect, BuffTarget target)
        {
            Name = name;
            StatusEffect = statusEffect;
            Target = target;
        }
    }

    internal class BuffBlockComponent : MonoBehaviour
    {
        public GameObject Visual;

        private const string ZdoKeyIsActive = "SuperVikingKart_BuffBlockActive";

        private ZNetView _netView;
        private float _respawnTimer;

        private static readonly BuffDefinition[] Buffs =
        {
            new BuffDefinition("Speed Boost", "SE_Wind", BuffTarget.Puller),
            new BuffDefinition("Stamina Regen", "SE_Rested", BuffTarget.Puller),
            new BuffDefinition("Shield", "SE_Shield", BuffTarget.Rider),
            new BuffDefinition("Health Regen", "SE_Potion_healthmedium", BuffTarget.Rider),
        };

        // --- Lifecycle ---

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
            _netView.Register("SuperVikingKart_RPC_BuffBlockCollected", RPC_BuffBlockCollected);
            _netView.Register("SuperVikingKart_RPC_BuffBlockRespawn", RPC_BuffBlockRespawn);
            _netView.Register<int, ZDOID>("SuperVikingKart_RPC_ApplyBuff", RPC_ApplyBuff);

            var isActive = _netView.GetZDO().GetBool(ZdoKeyIsActive, true);
            SuperVikingKart.DebugLog($"BuffBlock Awake - IsActive: {isActive}");
            SetVisual(isActive);
        }

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

        public void OnBuffBlockTriggerEnter(Collider other)
        {
            SuperVikingKart.DebugLog($"BuffBlock trigger entered by: {other.name} (parent: {other.transform.root.name})");

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

            var cart = other.GetComponentInParent<SuperVikingKartComponent>();
            if (!cart)
            {
                SuperVikingKart.DebugLog("BuffBlock - No kart found on collider");
                return;
            }

            var vagon = cart.GetComponentInParent<Vagon>();
            if (!vagon)
            {
                SuperVikingKart.DebugLog("BuffBlock - No vagon found on kart");
                return;
            }

            var puller = GetPuller(vagon);
            SuperVikingKart.DebugLog($"BuffBlock - Puller: {puller?.GetPlayerName() ?? "none"}, LocalPlayer: {localPlayer.GetPlayerName()}");

            if (puller != localPlayer)
            {
                SuperVikingKart.DebugLog("BuffBlock - Local player is not the puller, ignoring");
                return;
            }

            var cartNetView = cart.GetComponentInParent<ZNetView>();
            if (!cartNetView)
            {
                SuperVikingKart.DebugLog("BuffBlock - No ZNetView on kart");
                return;
            }

            var cartId = cartNetView.GetZDO().m_uid;
            var buffIndex = Random.Range(0, Buffs.Length);

            SuperVikingKart.DebugLog($"BuffBlock - Requesting collection! Buff: {Buffs[buffIndex].Name} (index: {buffIndex}), CartId: {cartId}");
            _netView.InvokeRPC("SuperVikingKart_RPC_RequestCollection", buffIndex, cartId);
        }

        private Player GetPuller(Vagon vagon)
        {
            foreach (var player in Player.GetAllPlayers())
            {
                if (vagon.IsAttached(player))
                    return player;
            }
            return null;
        }

        // --- Collection Authority ---

        private void RPC_RequestCollection(long sender, int buffIndex, ZDOID cartId)
        {
            SuperVikingKart.DebugLog($"BuffBlock RPC_RequestCollection - sender: {sender}, buffIndex: {buffIndex}, cartId: {cartId}, IsOwner: {_netView.IsOwner()}");

            if (!_netView.IsOwner())
                return;

            if (!IsActive())
            {
                SuperVikingKart.DebugLog("BuffBlock RPC_RequestCollection - Already collected, rejecting");
                return;
            }

            _netView.GetZDO().Set(ZdoKeyIsActive, false);
            _respawnTimer = SuperVikingKart.BuffBlockRespawnTimeConfig.Value;

            _netView.InvokeRPC(ZNetView.Everybody, "SuperVikingKart_RPC_ApplyBuff", buffIndex, cartId);
            _netView.InvokeRPC(ZNetView.Everybody, "SuperVikingKart_RPC_BuffBlockCollected");
        }

        // --- Buff Application ---

        private void RPC_ApplyBuff(long sender, int buffIndex, ZDOID cartId)
        {
            SuperVikingKart.DebugLog($"BuffBlock RPC_ApplyBuff - sender: {sender}, buffIndex: {buffIndex}, cartId: {cartId}");

            if (buffIndex < 0 || buffIndex >= Buffs.Length)
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

            var cartObject = ZNetScene.instance.FindInstance(cartId);
            if (!cartObject)
            {
                SuperVikingKart.DebugLog($"BuffBlock RPC_ApplyBuff - Kart not found for ZDOID: {cartId}");
                return;
            }

            var cart = cartObject.GetComponentInChildren<SuperVikingKartComponent>();
            if (!cart)
            {
                SuperVikingKart.DebugLog("BuffBlock RPC_ApplyBuff - No SuperVikingKartComponent on kart");
                return;
            }

            var vagon = cart.GetComponentInParent<Vagon>();
            if (!vagon)
            {
                SuperVikingKart.DebugLog("BuffBlock RPC_ApplyBuff - No Vagon on kart");
                return;
            }

            var buff = Buffs[buffIndex];
            var isPuller = vagon.IsAttached(localPlayer);
            var isRider = cart.GetAttachedPlayer() == localPlayer;

            SuperVikingKart.DebugLog($"BuffBlock RPC_ApplyBuff - Player: {localPlayer.GetPlayerName()}, IsPuller: {isPuller}, IsRider: {isRider}, BuffTarget: {buff.Target}");

            switch (buff.Target)
            {
                case BuffTarget.Puller:
                    if (isPuller)
                        ApplyToPlayer(localPlayer, buff);
                    if (isPuller || isRider)
                        localPlayer.Message(MessageHud.MessageType.Center, $"{buff.Name} for Puller!");
                    break;
                case BuffTarget.Rider:
                    if (isRider)
                        ApplyToPlayer(localPlayer, buff);
                    if (isPuller || isRider)
                        localPlayer.Message(MessageHud.MessageType.Center, $"{buff.Name} for Rider!");
                    break;
                case BuffTarget.Both:
                    if (isPuller || isRider)
                    {
                        ApplyToPlayer(localPlayer, buff);
                        localPlayer.Message(MessageHud.MessageType.Center, $"{buff.Name} for Both!");
                    }
                    break;
            }
        }

        private void ApplyToPlayer(Player player, BuffDefinition buff)
        {
            if (!player)
                return;

            var effect = ObjectDB.instance.GetStatusEffect(buff.StatusEffect.GetStableHashCode());
            if (effect == null)
            {
                Jotunn.Logger.LogWarning($"BuffBlock ApplyToPlayer - Could not find status effect: {buff.StatusEffect}");
                return;
            }

            SuperVikingKart.DebugLog($"BuffBlock ApplyToPlayer - Applying {buff.Name} ({buff.StatusEffect}) to {player.GetPlayerName()}");
            player.GetSEMan().AddStatusEffect(effect, true);
        }

        // --- Visual State ---

        private bool IsActive()
        {
            var zdo = _netView.GetZDO();
            if (zdo == null)
                return false;
            return zdo.GetBool(ZdoKeyIsActive, true);
        }

        private void SetVisual(bool active)
        {
            if (Visual)
                Visual.SetActive(active);
        }

        private void RPC_BuffBlockCollected(long sender)
        {
            SuperVikingKart.DebugLog($"BuffBlock RPC_BuffBlockCollected - sender: {sender}, IsOwner: {_netView.IsOwner()}");

            if (_netView.IsOwner())
            {
                _netView.GetZDO().Set(ZdoKeyIsActive, false);
                _respawnTimer = SuperVikingKart.BuffBlockRespawnTimeConfig.Value;
            }

            SetVisual(false);
        }

        private void RPC_BuffBlockRespawn(long sender)
        {
            SuperVikingKart.DebugLog($"BuffBlock RPC_BuffBlockRespawn - sender: {sender}, IsOwner: {_netView.IsOwner()}");

            if (_netView.IsOwner())
                _netView.GetZDO().Set(ZdoKeyIsActive, true);

            SetVisual(true);
        }
    }

    internal class BuffBlockTrigger : MonoBehaviour
    {
        public BuffBlockComponent BuffBlock;

        private void OnTriggerEnter(Collider other)
        {
            if (BuffBlock)
                BuffBlock.OnBuffBlockTriggerEnter(other);
        }
    }

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
}