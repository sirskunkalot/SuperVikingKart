using System.Collections;
using HarmonyLib;
using Jotunn.Managers;
using UnityEngine;

namespace SuperVikingKart
{
    internal class SuperVikingKartComponent : MonoBehaviour, Hoverable, Interactable
    {
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
            _netView.Register("SuperVikingKart_RPC_Swap", RPC_Swap);

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
                var num = 10f / vagon.m_bodies.Length;
                foreach (var body in vagon.m_bodies)
                {
                    body.mass = num;
                }
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
        
        // --- Swap ---
        
        public void RequestSwap()
        {
            if (_netView && _netView.GetZDO() != null)
                _netView.InvokeRPC(ZNetView.Everybody, "SuperVikingKart_RPC_Swap");
        }

        private void RPC_Swap(long sender)
        {
            var localPlayer = Player.m_localPlayer;
            if (!localPlayer)
                return;

            var vagon = GetComponentInParent<Vagon>();
            if (!vagon)
                return;

            var isPuller = vagon.IsAttached(localPlayer);
            var isRider = _attachedPlayerLocal == localPlayer;

            if (!isPuller && !isRider)
                return;

            if (isPuller)
            {
                SuperVikingKart.DebugLog("RPC_Swap - Puller detaching from cart");
                vagon.Detach();
            }
            else
            {
                SuperVikingKart.DebugLog("RPC_Swap - Rider detaching from seat");
                Detach();
            }

            StartCoroutine(DelayedSwap(localPlayer, isPuller));
        }

        private IEnumerator DelayedSwap(Player player, bool wasPuller)
        {
            yield return new WaitForSeconds(1f);

            var vagon = GetComponentInParent<Vagon>();
            if (!vagon)
            {
                SuperVikingKart.DebugLog("DelayedSwap - No vagon found");
                yield break;
            }

            if (wasPuller)
            {
                SuperVikingKart.DebugLog("DelayedSwap - Old puller becoming rider");
                Attach(player);
            }
            else
            {
                SuperVikingKart.DebugLog($"DelayedSwap - Old rider becoming puller, current owner: {vagon.m_nview.IsOwner()}, InUse: {vagon.InUse()}, IsAttached: {vagon.IsAttached()}");

                player.transform.position = vagon.m_attachPoint.position - vagon.m_attachOffset;
                player.m_body.position = player.transform.position;
                player.m_body.velocity = Vector3.zero;

                yield return new WaitForFixedUpdate();

                SuperVikingKart.DebugLog($"DelayedSwap - CanAttach: {vagon.CanAttach(player.gameObject)}");

                vagon.m_nview.ClaimOwnership();

                SuperVikingKart.DebugLog($"DelayedSwap - After claim, owner: {vagon.m_nview.IsOwner()}");

                if (vagon.m_nview.IsOwner())
                {
                    SuperVikingKart.DebugLog("DelayedSwap - Got ownership, attaching");
                    vagon.AttachTo(player.gameObject);
                }
                else
                {
                    SuperVikingKart.DebugLog("DelayedSwap - Failed to get ownership");
                }
            }
        }

        // --- State ---

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

    [HarmonyPatch(typeof(WearNTear), nameof(WearNTear.Destroy))]
    internal class KartRespawnPatch
    {
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

            SuperVikingKart.DebugLog($"KartRespawn - Kart destroyed at {position}, scheduling respawn");
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
}