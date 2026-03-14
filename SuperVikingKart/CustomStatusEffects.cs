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
}