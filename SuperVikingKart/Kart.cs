using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using Jotunn.Managers;
using UnityEngine;

namespace SuperVikingKart
{
    /// <summary>
    /// Core component for the racing kart. Handles rider attachment, detachment,
    /// and position pinning. Placed on the Container child of the cloned Cart prefab.
    /// Tracks all instances for efficient lookup by patches and status effects.
    /// </summary>
    internal class SuperVikingKartComponent : MonoBehaviour, Hoverable, Interactable
    {
        public static readonly List<SuperVikingKartComponent> Instances = new ();

        public string Name = "SuperVikingKartAttach";
        public float UseDistance = 2f;
        public Transform AttachPoint;
        
        private const string ZdoKeyAttachedPlayer = "SuperVikingKart_AttachedPlayer";
        private ZNetView _netView;
        private float _lastSitTime;
        private Player _attachedPlayerLocal;

        // --- Lifecycle ---

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

            _netView.Register<ZDOID>("SuperVikingKart_RPC_Attach", RPC_Attach);
            _netView.Register("SuperVikingKart_RPC_Detach", RPC_Detach);

            if (_netView.IsOwner())
            {
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

                var vagon = GetComponentInParent<Vagon>();
                vagon.m_baseMass = (float)SuperVikingKart.KartMassConfig.Value;
                vagon.SetMass(vagon.m_baseMass);
            }
        }

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
                SuperVikingKart.DebugLog($"Kart Update - Detaching (Jump: {ZInput.GetButtonDown("Jump")}, Dead: {_attachedPlayerLocal.IsDead()})");
                Detach();
                return;
            }

            _attachedPlayerLocal.transform.position = AttachPoint.position;
        }

        private void OnDestroy()
        {    
            Instances.Remove(this);
            SuperVikingKart.DebugLog("Kart OnDestroy");
            Detach();
        }

        // --- Attach / Detach ---

        private void Attach(Player player)
        {
            SuperVikingKart.DebugLog($"Kart Attach - Player: {player.GetPlayerName()}, ZDOID: {player.GetZDOID()}");
            
            _attachedPlayerLocal = player;
            if (_netView && _netView.GetZDO() != null)
                _netView.InvokeRPC("SuperVikingKart_RPC_Attach", player.GetZDOID());
        }
 
        private void Detach()
        {
            SuperVikingKart.DebugLog($"Kart Detach - Player: {_attachedPlayerLocal?.GetPlayerName() ?? "none"}");
            _attachedPlayerLocal = null;
            if (_netView && _netView.GetZDO() != null)
                _netView.InvokeRPC("SuperVikingKart_RPC_Detach");
        }

        private void RPC_Attach(long sender, ZDOID playerId)
        {
            SuperVikingKart.DebugLog($"Kart RPC_Attach - sender: {sender}, playerId: {playerId}, IsOwner: {_netView.IsOwner()}");
            var zdo = _netView.GetZDO();
            if (zdo == null)
                return;
            if (_netView.IsOwner())
                zdo.Set(ZdoKeyAttachedPlayer, playerId);
        }

        private void RPC_Detach(long sender)
        {
            SuperVikingKart.DebugLog($"Kart RPC_Detach - sender: {sender}, IsOwner: {_netView.IsOwner()}");
            var zdo = _netView.GetZDO();
            if (zdo == null)
                return;
            if (_netView.IsOwner())
                zdo.Set(ZdoKeyAttachedPlayer, ZDOID.None);
        }
        
        // --- State ---

        /// <summary>
        /// Resolves the attached player from ZDO, works on any client.
        /// </summary>
        public Player GetAttachedPlayer()
        {
            var zdo = _netView.GetZDO();
            if (zdo == null)
                return null;
            var attachedId = zdo.GetZDOID(ZdoKeyAttachedPlayer);
            if (attachedId == ZDOID.None)
                return null;
            var playerObject = ZNetScene.instance.FindInstance(attachedId);
            if (!playerObject)
                return null;
            return playerObject.GetComponent<Player>();
        }

        private bool IsInUse()
        {
            var zdo = _netView.GetZDO();
            if (zdo == null)
                return false;
            return zdo.GetZDOID(ZdoKeyAttachedPlayer) != ZDOID.None;
        }

        // --- Interaction ---

        public bool Interact(Humanoid human, bool hold, bool alt)
        {
            if (hold)
                return false;
            var player = human as Player;
            if (!player)
                return false;
            if (!AttachPoint)
                return false;
            if (!InUseDistance(player))
                return false;
            if (Time.time - _lastSitTime < 2f)
                return false;
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

        public bool UseItem(Humanoid user, ItemDrop.ItemData item)
        {
            return false;
        }

        // --- Hover ---

        public string GetHoverText()
        {
            if (Time.time - _lastSitTime < 2f)
                return "";
            var localPlayer = Player.m_localPlayer;
            if (!localPlayer)
                return "";
            if (!InUseDistance(localPlayer))
                return Localization.instance.Localize("<color=grey>$piece_toofar</color>");
            if (!_attachedPlayerLocal && IsInUse())
                return Localization.instance.Localize("<color=grey>In use</color>");
            return Localization.instance.Localize(Name + "\n[<color=yellow><b>$KEY_Use</b></color>] $piece_use");
        }

        public string GetHoverName()
        {
            return Name;
        }

        // --- Utility ---

        private bool InUseDistance(Humanoid human)
        {
            if (!human || !AttachPoint)
                return false;
            return Vector3.Distance(human.transform.position, AttachPoint.position) < UseDistance;
        }
    }

    /// <summary>
    /// Floating countdown timer spawned at the kart's destroy position.
    /// Shows remaining time until respawn and auto-destroys when done.
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

            if (_camera)
                _text.transform.rotation = _camera.transform.rotation;
        }

        public string GetHoverText()
        {
            return $"Kart respawning in {Mathf.CeilToInt(_timeRemaining)}s";
        }

        public string GetHoverName()
        {
            return "Kart Respawn";
        }
    }

    /// <summary>
    /// Respawns destroyed karts after a configurable delay.
    /// Only triggers for actual destruction, not hammer removal.
    /// Spawns a visible countdown timer at the destroy position.
    /// </summary>
    [HarmonyPatch(typeof(WearNTear), nameof(WearNTear.Destroy))]
    internal class KartRespawnPatch
    {
        // Static flag could race if a kart gets destroyed while another gets
        // removed in the same frame. Negligible in practice.
        internal static bool IsBeingRemoved;

        private static void Prefix(WearNTear __instance)
        {
            if (IsBeingRemoved)
                return;

            var kart = __instance.GetComponentInChildren<SuperVikingKartComponent>();
            if (!kart)
                return;

            var netView = __instance.GetComponent<ZNetView>();
            if (!netView || !netView.IsOwner())
                return;

            var position = __instance.transform.position;
            var yRotation = Quaternion.Euler(0f, __instance.transform.eulerAngles.y, 0f);

            SuperVikingKart.DebugLog($"KartRespawn - Kart destroyed at {position}, spawning timer, scheduling respawn");
            
            var timerGo = new GameObject("KartRespawnComponent");
            timerGo.transform.position = position + Vector3.up * 0.5f;
            timerGo.layer = LayerMask.NameToLayer("character");
            var timer = timerGo.AddComponent<KartRespawnComponent>();
            timer.Setup(SuperVikingKart.CartRespawnTimeConfig.Value);
            
            SuperVikingKart.Instance.StartCoroutine(RespawnKart(position, yRotation));
        }

        private static IEnumerator RespawnKart(Vector3 position, Quaternion rotation)
        {
            yield return new WaitForSeconds(SuperVikingKart.CartRespawnTimeConfig.Value);

            var prefab = PrefabManager.Instance.GetPrefab(SuperVikingKart.KartPrefabName);
            if (!prefab)
            {
                Jotunn.Logger.LogWarning("KartRespawn - SuperVikingKart prefab not found");
                yield break;
            }

            SuperVikingKart.DebugLog($"KartRespawn - Spawning kart at {position}");
            Object.Instantiate(prefab, position, rotation);
        }
    }

    /// <summary>
    /// Prevents kart respawn when removed with the hammer.
    /// Sets a flag that KartRespawnPatch checks before scheduling a respawn.
    /// </summary>
    [HarmonyPatch(typeof(WearNTear), nameof(WearNTear.Remove))]
    internal class KartRemovePatch
    {
        private static void Prefix()
        {
            KartRespawnPatch.IsBeingRemoved = true;
        }

        private static void Postfix()
        {
            KartRespawnPatch.IsBeingRemoved = false;
        }
    }

    /// <summary>
    /// Prevents the rider from damaging their own kart.
    /// Uses prefab hash for fast early-out on non-kart damage events.
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
            if (!attacker)
                return true;

            var cart = __instance.GetComponentInChildren<SuperVikingKartComponent>();
            if (!cart)
                return true;

            return attacker != cart.GetAttachedPlayer();
        }
    }
    
    /// <summary>
    /// Allows the rider to hit targets through their own kart by temporarily
    /// disabling the kart's colliders during melee attack raycasts.
    /// Only activates for local player when riding a kart.
    /// </summary>
    [HarmonyPatch(typeof(Attack), nameof(Attack.DoMeleeAttack))]
    internal class AttackThroughOwnCartPatch
    {
        private static void Prefix(Attack __instance, out List<Collider> __state)
        {
            __state = null;

            var player = __instance.m_character as Player;
            if (player == null || player != Player.m_localPlayer)
                return;

            foreach (var kart in SuperVikingKartComponent.Instances)
            {
                if (kart.GetAttachedPlayer() != player)
                    continue;

                var vagon = kart.GetComponentInParent<Vagon>();
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
    /// joint reference. LateUpdate runs before FixedUpdate can clean up,
    /// causing NullReferenceException spam.
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

}