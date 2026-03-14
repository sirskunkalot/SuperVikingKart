using Jotunn.Managers;

namespace SuperVikingKart
{
    /// <summary>
    /// Status effect that gives a player ooze bombs for 30 seconds
    /// </summary>
    internal class SE_OozeBombs : SE_Stats
    {
        public void OnEnable()
        {
            name = "SuperVikingKart_OozeBombs";
            m_ttl = 0.1f;
        }

        public override void Setup(Character character)
        {
            base.Setup(character);
            
            var player = character as Player;
            if (!player)
                return;

            var prefab = PrefabManager.Instance.GetPrefab("BombOoze");
            if (!prefab)
                return;

            SuperVikingKart.DebugLog($"SE_OozeBombs - Adding ooze bombs to {player.GetPlayerName()}");
            player.GetInventory().AddItem(prefab, 5);
        }
    }
    
    /// <summary>
    /// Status effect that gives a player full stamina once
    /// </summary>
    internal class SE_StaminaBurst : SE_Stats
    {
        public void OnEnable()
        {
            name = "SuperVikingKart_StaminaBurst";
            m_ttl = 0.1f;
        }

        public override void Setup(Character character)
        {
            base.Setup(character);
            character.AddStamina(character.GetMaxStamina());
            SuperVikingKart.DebugLog($"SE_StaminaBurst - Refilled stamina for {character.m_name}");
        }
    }
    
    /// <summary>
    /// Status effect to swap rider and puller on a cart. needs to be given to the puller
    /// </summary>
    internal class SE_Swap : SE_Stats
    {
        public void OnEnable()
        {
            name = "SuperVikingKart_Swap";
            m_ttl = 0.1f;
        }

        public override void Setup(Character character)
        {
            base.Setup(character);

            var player = character as Player;
            if (!player)
                return;

            Vagon vagon = null;
            foreach (var v in Vagon.m_instances)
            {
                if (v.IsAttached(player))
                {
                    vagon = v;
                    break;
                }
            }

            if (!vagon)
                return;

            var cart = vagon.GetComponentInChildren<SuperVikingKartComponent>();
            if (!cart)
                return;

            var rider = cart.GetAttachedPlayer();
            if (!rider)
            {
                SuperVikingKart.DebugLog("SE_Swap - No rider to swap with");
                return;
            }

            SuperVikingKart.DebugLog($"SE_Swap - Requesting swap");
            cart.RequestSwap();
        }
    }
}