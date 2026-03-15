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
    
    /// <summary>
    /// Status effect that staggers the player on impact
    /// </summary>
    internal class SE_KartStagger : SE_Stats
    {
        public void OnEnable()
        {
            name = "SuperVikingKart_Stagger";
            m_ttl = 0.1f;
        }

        public override void Setup(Character character)
        {
            base.Setup(character);
            character.Stagger(character.transform.forward * -1f);
            SuperVikingKart.DebugLog($"SE_KartStagger - Staggered {character.m_name}");
        }
    }

    /// <summary>
    /// Status effect that slows movement like being covered in tar
    /// </summary>
    internal class SE_KartTarred : SE_Stats
    {
        public void OnEnable()
        {
            name = "SuperVikingKart_Tarred";
            m_ttl = 8f;
            m_icon = PrefabManager.Cache.GetPrefab<Sprite>("Tared");

            var effect = PrefabManager.Cache.GetPrefab<GameObject>("vfx_Tared");
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
            SuperVikingKart.DebugLog($"SE_KartTarred - Applied to {character.m_name}");
        }

        public override void ModifySpeed(float baseSpeed, ref float speed, Character character, Vector3 dir)
        {
            speed *= 0.3f;
        }

        public override void Stop()
        {
            SuperVikingKart.DebugLog($"SE_KartTarred - Stopped on {m_character?.m_name}");
            base.Stop();
        }
    }
    
    /// <summary>
    /// Status effect that blinds the player with a screen overlay
    /// </summary>
    internal class SE_KartBlind : SE_Stats
    {
        private GameObject _overlay;

        public void OnEnable()
        {
            name = "SuperVikingKart_Blind";
            m_ttl = 5f;
            m_icon = PrefabManager.Cache.GetPrefab<Sprite>("Smoked");

            var effect = PrefabManager.Cache.GetPrefab<GameObject>("vfx_Smoked");
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
            SuperVikingKart.DebugLog($"SE_KartBlind - Applied to {character.m_name}");

            if (character != Player.m_localPlayer)
                return;

            _overlay = new GameObject("BlindOverlay");
            var canvas = _overlay.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 999;

            var image = new GameObject("BlindImage");
            image.transform.SetParent(_overlay.transform, false);

            var rectTransform = image.AddComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;

            var img = image.AddComponent<UnityEngine.UI.Image>();
            img.color = new Color(0.15f, 0.15f, 0.15f, 0.98f);
        }

        public override void Stop()
        {
            SuperVikingKart.DebugLog($"SE_KartBlind - Stopped on {m_character?.m_name}");

            if (_overlay)
                Object.Destroy(_overlay);

            base.Stop();
        }
    }
}