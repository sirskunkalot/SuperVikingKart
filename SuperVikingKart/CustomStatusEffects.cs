using System.Collections.Generic;
using Jotunn.Managers;
using UnityEngine;

namespace SuperVikingKart;

// ----- Buffs -----

// --- Puller ---

/// <summary>
/// Status effect that gives a short speed boost
/// </summary>
internal class SE_KartSpeedBoost : SE_Stats
{
    public void OnEnable()
    {
        name = "SuperVikingKart_SpeedBoost";
        m_ttl = 10f;
        m_icon = PrefabManager.Cache.GetPrefab<Sprite>("potion_hasty");
        var vfx = PrefabManager.Cache.GetPrefab<GameObject>("vfx_MeadHasty");
        var sfx = PrefabManager.Cache.GetPrefab<GameObject>("sfx_Potion_stamina_Start");
        if (vfx && sfx)
        {
            m_startEffects.m_effectPrefabs = new[]
            {
                new EffectList.EffectData { m_prefab = vfx, m_enabled = true, m_attach = true },
                new EffectList.EffectData { m_prefab = sfx, m_enabled = true, m_attach = true },
            };
        }
    }

    public override void Setup(Character character)
    {
        base.Setup(character);
        SuperVikingKart.DebugLog($"SE_KartSpeedBoost - Applied to {character.m_name}");
    }

    public override void ModifySpeed(float baseSpeed, ref float speed, Character character, Vector3 dir)
    {
        speed *= 1.5f;
    }

    public override void Stop()
    {
        SuperVikingKart.DebugLog($"SE_KartSpeedBoost - Stopped on {m_character?.m_name}");
        base.Stop();
    }
}

/// <summary>
/// Status effect that regens stamina for ten seconds
/// </summary>
internal class SE_KartStaminaRegen : SE_Stats
{
    public void OnEnable()
    {
        name = "SuperVikingKart_StaminaRegen";
        m_ttl = 10f;
        m_icon = PrefabManager.Cache.GetPrefab<Sprite>("potion_stamina_minor");
        var vfx = PrefabManager.Cache.GetPrefab<GameObject>("vfx_Potion_stamina_medium");
        var sfx = PrefabManager.Cache.GetPrefab<GameObject>("sfx_Potion_stamina_Start");
        if (vfx && sfx)
        {
            m_startEffects.m_effectPrefabs = new[]
            {
                new EffectList.EffectData { m_prefab = vfx, m_enabled = true, m_attach = true },
                new EffectList.EffectData { m_prefab = sfx, m_enabled = true, m_attach = true },
            };
        }

        m_staminaOverTime = 0.5f;
        m_staminaOverTimeIsFraction = true;
    }

    public override void Setup(Character character)
    {
        base.Setup(character);
        SuperVikingKart.DebugLog($"SE_KartStaminaRegen - Applied to {character.m_name}");
    }

    public override void Stop()
    {
        SuperVikingKart.DebugLog($"SE_KartStaminaRegen - Stopped on {m_character?.m_name}");
        base.Stop();
    }
}

/// <summary>
/// Status effect that gives full stamina once
/// </summary>
internal class SE_KartStaminaBurst : SE_Stats
{
    public void OnEnable()
    {
        name = "SuperVikingKart_StaminaBurst";
        m_ttl = 2f;
        var vfx = PrefabManager.Cache.GetPrefab<GameObject>("vfx_Potion_stamina_medium");
        var sfx = PrefabManager.Cache.GetPrefab<GameObject>("sfx_Potion_stamina_Start_lingering");
        if (vfx && sfx)
        {
            m_startEffects.m_effectPrefabs = new[]
            {
                new EffectList.EffectData { m_prefab = vfx, m_enabled = true, m_attach = true },
                new EffectList.EffectData { m_prefab = sfx, m_enabled = true, m_attach = true },
            };
        }
    }

    public override void Setup(Character character)
    {
        base.Setup(character);
        character.AddStamina(character.GetMaxStamina());
        SuperVikingKart.DebugLog($"SE_KartStaminaBurst - Refilled stamina for {character.m_name}");
    }
}

// --- Rider ---

/// <summary>
/// Status effect that gives the rider ooze bombs
/// </summary>
internal class SE_KartOozeBombs : SE_Stats
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
        if (!player) return;
        var prefab = ZNetScene.instance.GetPrefab("BombOoze");
        if (!prefab) return;
        SuperVikingKart.DebugLog($"SE_KartOozeBombs - Adding ooze bombs to {player.GetPlayerName()}");
        player.GetInventory().AddItem(prefab, 5);
    }
}

/// <summary>
/// Status effect that gives the rider bile bombs
/// </summary>
internal class SE_KartBileBombs : SE_Stats
{
    public void OnEnable()
    {
        name = "SuperVikingKart_BileBombs";
        m_ttl = 0.1f;
    }

    public override void Setup(Character character)
    {
        base.Setup(character);
        var player = character as Player;
        if (!player) return;
        var prefab = ZNetScene.instance.GetPrefab("BombBile");
        if (!prefab) return;
        SuperVikingKart.DebugLog($"SE_KartBileBombs - Adding bile bombs to {player.GetPlayerName()}");
        player.GetInventory().AddItem(prefab, 2);
    }
}

/// <summary>
/// Status effect that gives the rider smoke bombs
/// </summary>
internal class SE_KartSmokeBombs : SE_Stats
{
    public void OnEnable()
    {
        name = "SuperVikingKart_SmokeBombs";
        m_ttl = 0.1f;
    }

    public override void Setup(Character character)
    {
        base.Setup(character);
        var player = character as Player;
        if (!player) return;
        var prefab = ZNetScene.instance.GetPrefab("BombSmoke");
        if (!prefab) return;
        SuperVikingKart.DebugLog($"SE_KartSmokeBombs - Adding smoke bombs to {player.GetPlayerName()}");
        player.GetInventory().AddItem(prefab, 5);
    }
}

/// <summary>
/// Status effect that gives the rider fire arrows
/// </summary>
internal class SE_KartFireArrows : SE_Stats
{
    public void OnEnable()
    {
        name = "SuperVikingKart_FireArrows";
        m_ttl = 0.1f;
    }

    public override void Setup(Character character)
    {
        base.Setup(character);
        var player = character as Player;
        if (!player) return;
        var arrowPrefab = ZNetScene.instance.GetPrefab("ArrowFire");
        if (!arrowPrefab) return;
        // Add bow if not already in there
        var bowPrefab = ZNetScene.instance.GetPrefab("BowFineWood");
        if (bowPrefab)
        {
            if (!player.GetInventory().ContainsItemByName("$item_bow_finewood"))
                player.GetInventory().AddItem(bowPrefab, 1);
        }

        // Add arrows
        SuperVikingKart.DebugLog($"SE_KartFireArrows - Adding fire arrows to {player.GetPlayerName()}");
        player.GetInventory().AddItem(arrowPrefab, 20);
    }
}

/// <summary>
/// Status effect that gives the rider a harpoon
/// </summary>
internal class SE_KartHarpoon : SE_Stats
{
    public void OnEnable()
    {
        name = "SuperVikingKart_Harpoon";
        m_ttl = 0.1f;
    }

    public override void Setup(Character character)
    {
        base.Setup(character);
        var player = character as Player;
        if (!player) return;
        var prefab = ZNetScene.instance.GetPrefab("SpearChitin");
        if (!prefab) return;
        var exists = player.GetInventory().ContainsItemByName("$item_spear_chitin");
        if (exists) return;
        SuperVikingKart.DebugLog($"SE_KartHarpoon - Adding harpoon to {player.GetPlayerName()}");
        player.GetInventory().AddItem(prefab, 1);
    }
}

/// <summary>
/// Status effect that massively increases damage output
/// </summary>
internal class SE_KartBerserk : SE_Stats
{
    public void OnEnable()
    {
        name = "SuperVikingKart_Berserk";
        m_ttl = 30f;
        m_icon = PrefabManager.Cache.GetPrefab<Sprite>("potion_bzerker");
        var effect = PrefabManager.Cache.GetPrefab<GameObject>("vfx_MeadBzerker");
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
        SuperVikingKart.DebugLog($"SE_KartBerserk - Applied to {character.m_name}");
    }

    public override void ModifyAttack(Skills.SkillType skill, ref HitData hitData)
    {
        hitData.m_damage.m_damage *= 3f;
        hitData.m_damage.m_blunt *= 3f;
        hitData.m_damage.m_slash *= 3f;
        hitData.m_damage.m_pierce *= 3f;
    }

    public override void Stop()
    {
        SuperVikingKart.DebugLog($"SE_KartBerserk - Stopped on {m_character?.m_name}");
        base.Stop();
    }
}

// --- Both ---

/// <summary>
/// Status effect that grants temporary resistance to physical damage
/// </summary>
internal class SE_KartShield : SE_Stats
{
    public void OnEnable()
    {
        name = "SuperVikingKart_Shield";
        m_ttl = 30f;
        m_icon = PrefabManager.Cache.GetPrefab<Sprite>("TrophyBonemass");
        var effect = PrefabManager.Cache.GetPrefab<GameObject>("fx_GP_Activation");
        if (effect)
        {
            m_startEffects.m_effectPrefabs = new[]
            {
                new EffectList.EffectData { m_prefab = effect, m_enabled = true, m_attach = true }
            };
        }

        m_mods = new List<HitData.DamageModPair>
        {
            new() { m_type = HitData.DamageType.Blunt, m_modifier = HitData.DamageModifier.Resistant },
            new() { m_type = HitData.DamageType.Slash, m_modifier = HitData.DamageModifier.Resistant },
            new() { m_type = HitData.DamageType.Pierce, m_modifier = HitData.DamageModifier.Resistant },
        };
    }

    public override void Setup(Character character)
    {
        base.Setup(character);
        SuperVikingKart.DebugLog($"SE_KartShield - Applied to {character.m_name}");
    }

    public override void Stop()
    {
        SuperVikingKart.DebugLog($"SE_KartShield - Stopped on {m_character?.m_name}");
        base.Stop();
    }
}

/// <summary>
/// Status effect that regens health for ten seconds
/// </summary>
internal class SE_KartHealthRegen : SE_Stats
{
    public void OnEnable()
    {
        name = "SuperVikingKart_HealthRegen";
        m_ttl = 10f;
        m_icon = PrefabManager.Cache.GetPrefab<Sprite>("potion_health_minor");
        var vfx = PrefabManager.Cache.GetPrefab<GameObject>("vfx_Potion_health_medium");
        var sfx = PrefabManager.Cache.GetPrefab<GameObject>("sfx_Potion_health_minor");
        if (vfx && sfx)
        {
            m_startEffects.m_effectPrefabs = new[]
            {
                new EffectList.EffectData { m_prefab = vfx, m_enabled = true, m_attach = true },
                new EffectList.EffectData { m_prefab = sfx, m_enabled = true, m_attach = true },
            };
        }

        m_healthOverTime = 100f;
        m_healthOverTimeInterval = 2f;
    }

    public override void Setup(Character character)
    {
        base.Setup(character);
        SuperVikingKart.DebugLog($"SE_KartStaminaRegen - Applied to {character.m_name}");
    }

    public override void Stop()
    {
        SuperVikingKart.DebugLog($"SE_KartStaminaRegen - Stopped on {m_character?.m_name}");
        base.Stop();
    }
}

/// <summary>
/// Status effect that fully heals the player once
/// </summary>
internal class SE_KartHealthBurst : SE_Stats
{
    public void OnEnable()
    {
        name = "SuperVikingKart_HealthBurst";
        m_ttl = 2f;
        var vfx = PrefabManager.Cache.GetPrefab<GameObject>("vfx_Potion_health_medium");
        var sfx = PrefabManager.Cache.GetPrefab<GameObject>("sfx_Potion_health_large");
        if (vfx && sfx)
        {
            m_startEffects.m_effectPrefabs = new[]
            {
                new EffectList.EffectData { m_prefab = vfx, m_enabled = true, m_attach = true },
                new EffectList.EffectData { m_prefab = sfx, m_enabled = true, m_attach = true },
            };
        }
    }

    public override void Setup(Character character)
    {
        base.Setup(character);
        character.Heal(character.GetMaxHealth());
        SuperVikingKart.DebugLog($"SE_KartHealthBurst - Fully healed {character.m_name}");
    }
}

/// <summary>
/// Status effect that prevents the player from dying once, leaving them at 1 HP instead
/// </summary>
internal class SE_KartLivingDead : SE_Stats
{
    private bool _triggered;

    public void OnEnable()
    {
        name = "SuperVikingKart_LivingDead";
        m_ttl = 20f;
        m_icon = PrefabManager.Cache.GetPrefab<Sprite>("CorpseRun");
        var effect = PrefabManager.Cache.GetPrefab<GameObject>("vfx_corpse_destruction_medium");
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
        _triggered = false;
        base.Setup(character);
        SuperVikingKart.DebugLog($"SE_KartLivingDead - Applied to {character.m_name}");
    }

    public override void OnDamaged(HitData hit, Character attacker)
    {
        if (_triggered) return;
        if (m_character.GetHealth() - hit.GetTotalDamage() <= 0f)
        {
            _triggered = true;
            hit.m_damage = new HitData.DamageTypes();
            m_character.SetHealth(1f);
            m_character.Message(MessageHud.MessageType.Center, "Living Dead saved you!");
            SuperVikingKart.DebugLog($"SE_KartLivingDead - Blocked death for {m_character.m_name}");
            Stop();
        }
    }

    public override void Stop()
    {
        SuperVikingKart.DebugLog($"SE_KartLivingDead - Stopped on {m_character?.m_name}");
        base.Stop();
    }
}

// ----- Debuffs -----

// --- Puller ---

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
/// Status effect that launches the cart upward
/// </summary>
internal class SE_KartBounce : SE_Stats
{
    public void OnEnable()
    {
        name = "SuperVikingKart_Bounce";
        m_ttl = 0.1f;
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
                foreach (var body in v.m_bodies)
                    body.AddForce(Vector3.up * 40f, ForceMode.Impulse);
                SuperVikingKart.DebugLog("SE_KartBounce - Launched cart");
                break;
            }
        }
    }
}

// --- Rider ---

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
/// Status effect that unequips and drops the rider's weapon
/// </summary>
internal class SE_KartDisarm : SE_Stats
{
    public void OnEnable()
    {
        name = "SuperVikingKart_Disarm";
        m_ttl = 0.1f;
    }

    public override void Setup(Character character)
    {
        base.Setup(character);
        var player = character as Player;
        if (!player) return;

        var rightItem = player.GetRightItem();
        if (rightItem != null)
        {
            SuperVikingKart.DebugLog(
                $"SE_KartDisarm - Dropping {rightItem.m_shared.m_name} from {player.GetPlayerName()}");
            player.DropItem(player.GetInventory(), rightItem, 100);
        }

        var leftItem = player.GetLeftItem();
        if (leftItem != null)
        {
            SuperVikingKart.DebugLog(
                $"SE_KartDisarm - Dropping {leftItem.m_shared.m_name} from {player.GetPlayerName()}");
            player.DropItem(player.GetInventory(), leftItem, 100);
        }
    }
}

// --- Both ---

/// <summary>
/// Status effect that reduces health and stamina regen
/// </summary>
internal class SE_KartWeak : SE_Stats
{
    private float _timer;

    public void OnEnable()
    {
        name = "SuperVikingKart_Weak";
        m_ttl = 30f;
        m_icon = PrefabManager.Cache.GetPrefab<Sprite>("Wet");
        var effect = PrefabManager.Cache.GetPrefab<GameObject>("vfx_Wet");
        if (effect)
        {
            m_startEffects.m_effectPrefabs = new[]
            {
                new EffectList.EffectData { m_prefab = effect, m_enabled = true, m_attach = true }
            };
        }

        m_healthRegenMultiplier = 0.75f;
        m_staminaRegenMultiplier = 0.85f;
        m_eitrRegenMultiplier = 0.85f;
    }

    public override void Setup(Character character)
    {
        base.Setup(character);
        SuperVikingKart.DebugLog($"SE_KartWeak - Applied to {character.m_name}");
    }

    public override void Stop()
    {
        SuperVikingKart.DebugLog($"SE_KartWeak - Stopped on {m_character?.m_name}");
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
        var effect1 = PrefabManager.Cache.GetPrefab<GameObject>("vfx_FireWork_ThunderStone");
        var effect2 = PrefabManager.Cache.GetPrefab<GameObject>("fx_Lightning");
        if (effect1 && effect2)
        {
            m_startEffects.m_effectPrefabs = new[]
            {
                new EffectList.EffectData { m_prefab = effect1, m_enabled = true, m_attach = true },
                new EffectList.EffectData { m_prefab = effect2, m_enabled = true, m_attach = true }
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
/// Status effect that blinds the player with a screen overlay
/// </summary>
internal class SE_KartBlind : SE_Stats
{
    private GameObject _overlay;
    private float _elapsed;

    private const float TarAlpha = 0.99f;
    private const float VignetteAlpha = 0.98f;
    private const float PulseSpeed = 0.6f;
    private const float PulseStrength = 0.06f;

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
        if (character != Player.m_localPlayer) return;

        // Root canvas
        _overlay = new GameObject("BlindOverlay");
        var canvas = _overlay.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999;
        _overlay.AddComponent<UnityEngine.UI.CanvasScaler>();
        _overlay.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        // Dark tar base layer (near-opaque brownish-black)
        CreateFullscreenImage(_overlay.transform, "TarBase",
            new Color(0.05f, 0.03f, 0.01f, TarAlpha));

        // Vignette layer using a baked radial gradient sprite
        var vignetteGo = CreateFullscreenImage(_overlay.transform, "Vignette",
            new Color(0f, 0f, 0f, VignetteAlpha));
        vignetteGo.GetComponent<UnityEngine.UI.Image>().sprite =
            CreateVignetteSprite(256);

        // Subtle tar sheen (dark green tint, pulsed in UpdateStatusEffect)
        CreateFullscreenImage(_overlay.transform, "TarSheen",
            new Color(0.02f, 0.06f, 0.01f, 0.75f));

        _elapsed = 0f;

        SuperVikingKart.DebugLog($"SE_KartBlind - Applied to {character.m_name}");
    }

    public override void UpdateStatusEffect(float dt)
    {
        base.UpdateStatusEffect(dt);
        if (_overlay == null) return;

        _elapsed += dt;

        // Pulse the sheen layer alpha for a slimy organic feel
        var sheen = _overlay.transform.Find("TarSheen");
        if (sheen)
        {
            var img = sheen.GetComponent<UnityEngine.UI.Image>();
            float pulse = Mathf.Sin(_elapsed * PulseSpeed * Mathf.PI * 2f) * PulseStrength;
            var c = img.color;
            c.a = Mathf.Clamp01(0.25f + pulse);
            img.color = c;
        }
    }

    public override void Stop()
    {
        SuperVikingKart.DebugLog($"SE_KartBlind - Stopped on {m_character?.m_name}");
        if (_overlay)
            Object.Destroy(_overlay);
        base.Stop();
    }

    // Creates a fullscreen UI image parented to the given transform
    private static GameObject CreateFullscreenImage(Transform parent, string goName, Color color)
    {
        var go = new GameObject(goName);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        var img = go.AddComponent<UnityEngine.UI.Image>();
        img.color = color;
        return go;
    }

    // Bakes a radial gradient texture: transparent centre, dark opaque edges.
    // Used as the vignette sprite so no external assets are needed.
    private static Sprite CreateVignetteSprite(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        var pixels = new Color32[size * size];
        float half = size * 0.5f;

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = (x - half) / half;
            float dy = (y - half) / half;
            float dist = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy));

            // Smooth-step so the centre stays transparent and edges go solid
            float alpha = Mathf.SmoothStep(0.3f, 1.0f, dist);
            pixels[y * size + x] = new Color32(0, 0, 0, (byte)(alpha * 255));
        }

        tex.SetPixels32(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }
}