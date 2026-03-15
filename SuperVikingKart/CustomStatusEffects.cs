using Jotunn.Managers;
using UnityEngine;

namespace SuperVikingKart
{
    /// <summary>
    /// Status effect that gives a player ooze bombs
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
    /// Status effect that reduces the cart mass for faster pulling
    /// </summary>
    internal class SE_LightKart : SE_Stats
    {
        private Vagon _vagon;

        public void OnEnable()
        {
            name = "SuperVikingKart_LightKart";
            m_ttl = 10f;
            m_icon = PrefabManager.Cache.GetPrefab<Sprite>("SlowFall");
        }

        public override void Setup(Character character)
        {
            base.Setup(character);
            var player = character as Player;
            if (!player) return;

            foreach (var v in Vagon.m_instances)
            {
                if (v.IsAttached(player))
                {
                    _vagon = v;
                    var newMass = _vagon.m_baseMass * 0.2f;
                    _vagon.SetMass(newMass);
                    SuperVikingKart.DebugLog($"SE_LightKart - Set mass to {newMass} (base: {_vagon.m_baseMass})");
                    break;
                }
            }
        }

        public override void Stop()
        {
            if (_vagon)
            {
                _vagon.SetMass(_vagon.m_baseMass);
                SuperVikingKart.DebugLog($"SE_LightKart - Restored mass to {_vagon.m_baseMass}");
            }
            base.Stop();
        }
    }

    /// <summary>
    /// Status effect that increases the cart mass for slower pulling
    /// </summary>
    internal class SE_HeavyKart : SE_Stats
    {
        private Vagon _vagon;

        public void OnEnable()
        {
            name = "SuperVikingKart_HeavyCart";
            m_ttl = 10f;
            m_icon = PrefabManager.Cache.GetPrefab<Sprite>("Encumbered");
        }

        public override void Setup(Character character)
        {
            base.Setup(character);
            var player = character as Player;
            if (!player) return;

            foreach (var v in Vagon.m_instances)
            {
                if (v.IsAttached(player))
                {
                    _vagon = v;
                    var newMass = _vagon.m_baseMass * 5f;
                    _vagon.SetMass(newMass);
                    SuperVikingKart.DebugLog($"SE_HeavyCart - Set mass to {newMass} (base: {_vagon.m_baseMass})");
                    break;
                }
            }
        }

        public override void Stop()
        {
            if (_vagon)
            {
                _vagon.SetMass(_vagon.m_baseMass);
                SuperVikingKart.DebugLog($"SE_HeavyCart - Restored mass to {_vagon.m_baseMass}");
            }
            base.Stop();
        }
    }
    
    /// <summary>
    /// Status effect that deals poison damage over time - low damage, long duration
    /// </summary>
    internal class SE_KartPoison : SE_Stats
    {
        private float _timer;

        public void OnEnable()
        {
            name = "SuperVikingKart_Poison";
            m_ttl = 10f;
            m_icon = PrefabManager.Cache.GetPrefab<Sprite>("Poison");

            var effect = PrefabManager.Cache.GetPrefab<GameObject>("vfx_Poison");
            if (effect)
            {
                m_startEffects.m_effectPrefabs = new[]
                {
                    new EffectList.EffectData { m_prefab = effect, m_enabled = true, m_attach = true }
                };
            }
        }

        public override void Setup(Character character)
        {
            base.Setup(character);
            SuperVikingKart.DebugLog($"SE_KartPoison - Applied to {character.m_name}");
        }

        public override void UpdateStatusEffect(float dt)
        {
            base.UpdateStatusEffect(dt);
            _timer -= dt;
            if (_timer > 0f) return;

            _timer = 1f;
            var hit = new HitData();
            hit.m_point = m_character.GetCenterPoint();
            hit.m_damage.m_poison = 2f;
            hit.m_hitType = HitData.HitType.Poisoned;
            m_character.ApplyDamage(hit, true, false);
        }

        public override void Stop()
        {
            SuperVikingKart.DebugLog($"SE_KartPoison - Stopped on {m_character?.m_name}");
            base.Stop();
        }
    }

    /// <summary>
    /// Status effect that deals fire damage over time - high damage, short duration
    /// </summary>
    internal class SE_KartBurn : SE_Stats
    {
        private float _timer;

        public void OnEnable()
        {
            name = "SuperVikingKart_Burn";
            m_ttl = 3f;
            m_icon = PrefabManager.Cache.GetPrefab<Sprite>("Burning");

            var effect = PrefabManager.Cache.GetPrefab<GameObject>("vfx_Burning");
            if (effect)
            {
                m_startEffects.m_effectPrefabs = new[]
                {
                    new EffectList.EffectData { m_prefab = effect, m_enabled = true, m_attach = true }
                };
            }
        }

        public override void Setup(Character character)
        {
            base.Setup(character);
            SuperVikingKart.DebugLog($"SE_KartBurn - Applied to {character.m_name}");
        }

        public override void UpdateStatusEffect(float dt)
        {
            base.UpdateStatusEffect(dt);
            _timer -= dt;
            if (_timer > 0f) return;

            _timer = 1f;
            var hit = new HitData();
            hit.m_point = m_character.GetCenterPoint();
            hit.m_damage.m_fire = 10f;
            hit.m_hitType = HitData.HitType.Burning;
            m_character.ApplyDamage(hit, true, false);
        }

        public override void Stop()
        {
            SuperVikingKart.DebugLog($"SE_KartBurn - Stopped on {m_character?.m_name}");
            base.Stop();
        }
    }

    /// <summary>
    /// Status effect that deals lightning damage and slows movement - medium damage with slow
    /// </summary>
    internal class SE_KartShock : SE_Stats
    {
        private float _timer;

        public void OnEnable()
        {
            name = "SuperVikingKart_Shock";
            m_ttl = 5f;
            m_icon = PrefabManager.Cache.GetPrefab<Sprite>("Lightning");

            var effect = PrefabManager.Cache.GetPrefab<GameObject>("vfx_FireWork_ThunderStone");
            if (effect)
            {
                m_startEffects.m_effectPrefabs = new[]
                {
                    new EffectList.EffectData { m_prefab = effect, m_enabled = true, m_attach = true }
                };
            }
        }

        public override void Setup(Character character)
        {
            base.Setup(character);
            SuperVikingKart.DebugLog($"SE_KartShock - Applied to {character.m_name}");
        }

        public override void UpdateStatusEffect(float dt)
        {
            base.UpdateStatusEffect(dt);
            _timer -= dt;
            if (_timer > 0f) return;

            _timer = 1f;
            var hit = new HitData();
            hit.m_point = m_character.GetCenterPoint();
            hit.m_damage.m_lightning = 5f;
            m_character.ApplyDamage(hit, true, false);
        }

        public override void ModifySpeed(float baseSpeed, ref float speed, Character character, Vector3 dir)
        {
            speed *= 0.7f;
        }

        public override void Stop()
        {
            SuperVikingKart.DebugLog($"SE_KartShock - Stopped on {m_character?.m_name}");
            base.Stop();
        }
    }

    /// <summary>
    /// Status effect that slows movement speed
    /// </summary>
    internal class SE_KartFrost : SE_Stats
    {
        public void OnEnable()
        {
            name = "SuperVikingKart_Frost";
            m_ttl = 8f;
            m_icon = PrefabManager.Cache.GetPrefab<Sprite>("Frost");

            var effect = PrefabManager.Cache.GetPrefab<GameObject>("vfx_Frost");
            if (effect)
            {
                m_startEffects.m_effectPrefabs = new[]
                {
                    new EffectList.EffectData { m_prefab = effect, m_enabled = true, m_attach = true }
                };
            }
        }

        public override void Setup(Character character)
        {
            base.Setup(character);
            SuperVikingKart.DebugLog($"SE_KartFrost - Applied to {character.m_name}");
        }

        public override void ModifySpeed(float baseSpeed, ref float speed, Character character, Vector3 dir)
        {
            speed *= 0.5f;
        }

        public override void Stop()
        {
            SuperVikingKart.DebugLog($"SE_KartFrost - Stopped on {m_character?.m_name}");
            base.Stop();
        }
    }
}