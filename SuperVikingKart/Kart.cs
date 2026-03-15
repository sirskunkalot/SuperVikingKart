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

        /// <summary>
        /// Local-only reference to the attached player. Only set on the rider's own client.
        /// Used for fast position pinning in Update. For cross-client queries use GetAttachedPlayer().
        /// </summary>
        private Player _attachedPlayerLocal;

        // --- Lifecycle ---

        /// <summary>
        /// Initializes the kart component. Sets up networking RPCs for rider attachment,
        /// registers in the global instances list, clears stale attachments from ZDO,
        /// and configures the cart mass from server-synced config.
        /// Disables itself if no valid ZNetView/ZDO exists (placement ghost).
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

            _netView.Register<ZDOID>("SuperVikingKart_RPC_Attach", RPC_Attach);
            _netView.Register("SuperVikingKart_RPC_Detach", RPC_Detach);

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

                // Apply configured mass to the cart's physics bodies
                var vagon = GetComponentInParent<Vagon>();
                vagon.m_baseMass = (float)SuperVikingKart.KartMassConfig.Value;
                vagon.SetMass(vagon.m_baseMass);
            }
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
                SuperVikingKart.DebugLog($"Kart Update - Detaching (Jump: {ZInput.GetButtonDown("Jump")}, Dead: {_attachedPlayerLocal.IsDead()})");
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
        /// Persists the rider's ZDOID in the cart's ZDO. Only the owner writes.
        /// </summary>
        private void RPC_Attach(long sender, ZDOID playerId)
        {
            SuperVikingKart.DebugLog($"Kart RPC_Attach - sender: {sender}, playerId: {playerId}, IsOwner: {_netView.IsOwner()}");
            var zdo = _netView.GetZDO();
            if (zdo == null)
                return;
            if (_netView.IsOwner())
                zdo.Set(ZdoKeyAttachedPlayer, playerId);
        }

        /// <summary>
        /// Clears the rider's ZDOID in the cart's ZDO. Only the owner writes.
        /// </summary>
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
        /// Resolves the attached player from ZDO. Works on any client since
        /// ZDOs are replicated. Used for cross-client checks like damage prevention
        /// and buff application.
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

        /// <summary>
        /// Quick check if anyone is riding this kart. Uses ZDO so it works
        /// on any client, not just the rider's.
        /// </summary>
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

            // Show "in use" when another player is riding (ZDO says occupied
            // but local reference is null since we're not the rider)
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

            // Billboard - text always faces the camera
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
    /// Only triggers for actual destruction (damage), not hammer removal.
    /// Spawns a visible countdown timer at the destroy position.
    /// Only the ZDO owner schedules the respawn to prevent duplicates.
    /// </summary>
    [HarmonyPatch(typeof(WearNTear), nameof(WearNTear.Destroy))]
    internal class KartRespawnPatch
    {
        /// <summary>
        /// Set by KartRemovePatch to distinguish hammer removal from destruction.
        /// Static flag could race if a kart gets destroyed while another gets
        /// removed in the same frame. Negligible in practice.
        /// </summary>
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

            // Preserve facing direction but reset tilt/roll
            var position = __instance.transform.position;
            var yRotation = Quaternion.Euler(0f, __instance.transform.eulerAngles.y, 0f);

            SuperVikingKart.DebugLog($"KartRespawn - Kart destroyed at {position}, spawning timer, scheduling respawn");
            
            // Spawn visible countdown timer
            var timerGo = new GameObject("KartRespawnComponent");
            timerGo.transform.position = position + Vector3.up * 0.5f;
            timerGo.layer = LayerMask.NameToLayer("character");
            var timer = timerGo.AddComponent<KartRespawnComponent>();
            timer.Setup(SuperVikingKart.CartRespawnTimeConfig.Value);
            
            // Schedule the actual respawn
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
    /// Sets a flag before WearNTear.Remove calls Destroy, which
    /// KartRespawnPatch checks before scheduling a respawn.
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
            if (!attacker)
                return true;

            var cart = __instance.GetComponentInChildren<SuperVikingKartComponent>();
            if (!cart)
                return true;

            // Block damage if the attacker is the rider
            return attacker != cart.GetAttachedPlayer();
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
    internal class AttackThroughOwnCartPatch
    {
        /// <summary>
        /// Disables the kart's colliders before the attack raycast runs.
        /// Stores disabled colliders in __state for restoration in Postfix.
        /// </summary>
        private static void Prefix(Attack __instance, out List<Collider> __state)
        {
            __state = null;

            // Only care about local player attacks
            var player = __instance.m_character as Player;
            if (player == null || player != Player.m_localPlayer)
                return;

            // Find if this player is riding a kart
            foreach (var kart in SuperVikingKartComponent.Instances)
            {
                if (kart.GetAttachedPlayer() != player)
                    continue;

                var vagon = kart.GetComponentInParent<Vagon>();
                if (!vagon) continue;

                // Disable all cart colliders so raycasts pass through
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
            // Joint exists but the connected player was destroyed
            if (__instance.m_attachJoin != null && __instance.m_attachJoin.connectedBody == null)
            {
                __instance.Detach();
                return false;
            }

            return true;
        }
    }
}