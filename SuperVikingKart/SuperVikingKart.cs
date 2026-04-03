using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Jotunn.Entities;
using Jotunn.Managers;
using Jotunn.Utils;
using System;
using Jotunn;
using Jotunn.Configs;
using UnityEngine;

namespace SuperVikingKart;

[BepInPlugin(PluginGUID, PluginName, PluginVersion)]
[BepInDependency(Jotunn.Main.ModGuid)]
[NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
internal class SuperVikingKart : BaseUnityPlugin
{
    public const string PluginGUID = "de.sirskunkalot.SuperVikingKart";
    public const string PluginName = "SuperVikingKart";
    public const string PluginVersion = "0.0.1";

    public const string KartPrefabName = "SuperVikingKart";
    public static readonly int KartPrefabHash = KartPrefabName.GetStableHashCode();

    public const string BuffBlockPrefabName = "BuffBlock";
    public static readonly int BuffBlockPrefabHash = BuffBlockPrefabName.GetStableHashCode();

    public const string DebuffBlockPrefabName = "DebuffBlock";
    public static readonly int DebuffBlockPrefabHash = DebuffBlockPrefabName.GetStableHashCode();

    public const string MysteryBlockPrefabName = "MysteryBlock";
    public static readonly int MysteryBlockPrefabHash = MysteryBlockPrefabName.GetStableHashCode();

    public const string RaceBoardPrefabName = "RaceBoard";
    public static readonly int RaceBoardPrefabHash = RaceBoardPrefabName.GetStableHashCode();

    public const string RaceLinePrefabName = "RaceLine";
    public static readonly int RaceLinePrefabHash = RaceLinePrefabName.GetStableHashCode();

    public static SuperVikingKart Instance;
    public static CustomLocalization Localization = LocalizationManager.Instance.GetLocalization();

    public static ConfigEntry<int> CartRespawnTimeConfig;
    public static ConfigEntry<int> BuffBlockRespawnTimeConfig;
    public static ConfigEntry<bool> DebugLogConfig;

    private Harmony _harmony;

    private void Awake()
    {
        Instance = this;

        DebugLogConfig = Config.Bind("General", "Debug", false, "Enable debug logging");
        CartRespawnTimeConfig = Config.Bind("General", "CartRespawnTime", 10,
            new ConfigDescription("Time in seconds before a destroyed cart respawns. Server synced value.",
                new AcceptableValueRange<int>(2, 20),
                new ConfigurationManagerAttributes { IsAdminOnly = true }));
        BuffBlockRespawnTimeConfig = Config.Bind("General", "BuffBlockRespawnTime", 10,
            new ConfigDescription("Time in seconds before a collected buff block reappears. Server synced value.",
                new AcceptableValueRange<int>(2, 20),
                new ConfigurationManagerAttributes { IsAdminOnly = true }));

        _harmony = new Harmony(PluginGUID);
        _harmony.PatchAll();

        // Create Objects once after starting the game and loading into the menu
        PrefabManager.OnVanillaPrefabsAvailable += RegisterCustomPieces;
        PrefabManager.OnVanillaPrefabsAvailable += RegisterCustomStatusEffects;

        // (Re-)Initialize the RaceManager on every connect to a server / start of a local game
        PrefabManager.OnPrefabsRegistered += RaceManager.Init;

        // Request a race data sync from the server everytime an initial config sync was sent
        SynchronizationManager.OnConfigurationSynchronized += (_, args) =>
        {
            if (ZNet.instance.IsServer() || !args.InitialSynchronization) return;
            DebugLog("RaceManager - Requesting sync");
            ZRoutedRpc.instance.InvokeRoutedRPC(
                ZRoutedRpc.instance.GetServerPeerID(),
                "SuperVikingKart_Race_RequestSync");
        };

        // (Re-)Build the RaceBoard Admin GUI on every GUI creation
        GUIManager.OnCustomGUIAvailable += RaceBoardAdminGui.Build;

        // (Re-)Build the RaceLine Admin GUI on every GUI creation
        GUIManager.OnCustomGUIAvailable += RaceLineAdminGui.Build;

        // Create the commands once directly on mod Awake
        Commands.Register();
    }

    private void OnDestroy()
    {
        _harmony?.UnpatchSelf();
    }

    public static void DebugLog(string message)
    {
        if (DebugLogConfig.Value)
            Jotunn.Logger.LogDebug(message);
    }

    private static void RegisterCustomPieces()
    {
        try
        {
            KartPiece.CloneCart();
            BuffBlockPieces.CreateBuffBlock();
            BuffBlockPieces.CreateDebuffBlock();
            BuffBlockPieces.CreateMysteryBlock();
            RaceBoardPiece.CreateRaceBoard();
            RaceLinePiece.CreateRaceLine();

            DebugLog("Custom pieces registered");
        }
        catch (Exception ex)
        {
            Jotunn.Logger.LogWarning($"Caught exception while creating pieces: {ex}");
        }
        finally
        {
            PrefabManager.OnVanillaPrefabsAvailable -= RegisterCustomPieces;
        }
    }

    private static void RegisterCustomStatusEffects()
    {
        try
        {
            // --- Buffs ---

            // Puller
            ItemManager.Instance.AddStatusEffect(
                new CustomStatusEffect(ScriptableObject.CreateInstance<SE_KartSpeedBoost>(), fixReference: false));
            ItemManager.Instance.AddStatusEffect(
                new CustomStatusEffect(ScriptableObject.CreateInstance<SE_KartStaminaRegen>(),
                    fixReference: false));
            ItemManager.Instance.AddStatusEffect(
                new CustomStatusEffect(ScriptableObject.CreateInstance<SE_KartStaminaBurst>(),
                    fixReference: false));

            // Rider
            ItemManager.Instance.AddStatusEffect(
                new CustomStatusEffect(ScriptableObject.CreateInstance<SE_KartOozeBombs>(), fixReference: false));
            ItemManager.Instance.AddStatusEffect(
                new CustomStatusEffect(ScriptableObject.CreateInstance<SE_KartBileBombs>(), fixReference: false));
            ItemManager.Instance.AddStatusEffect(
                new CustomStatusEffect(ScriptableObject.CreateInstance<SE_KartSmokeBombs>(), fixReference: false));
            ItemManager.Instance.AddStatusEffect(
                new CustomStatusEffect(ScriptableObject.CreateInstance<SE_KartFireArrows>(), fixReference: false));
            ItemManager.Instance.AddStatusEffect(
                new CustomStatusEffect(ScriptableObject.CreateInstance<SE_KartHarpoon>(), fixReference: false));
            ItemManager.Instance.AddStatusEffect(
                new CustomStatusEffect(ScriptableObject.CreateInstance<SE_KartBerserk>(), fixReference: false));

            // Both
            ItemManager.Instance.AddStatusEffect(
                new CustomStatusEffect(ScriptableObject.CreateInstance<SE_KartShield>(), fixReference: false));
            ItemManager.Instance.AddStatusEffect(
                new CustomStatusEffect(ScriptableObject.CreateInstance<SE_KartHealthRegen>(), fixReference: false));
            ItemManager.Instance.AddStatusEffect(
                new CustomStatusEffect(ScriptableObject.CreateInstance<SE_KartHealthBurst>(), fixReference: false));
            ItemManager.Instance.AddStatusEffect(
                new CustomStatusEffect(ScriptableObject.CreateInstance<SE_KartLivingDead>(), fixReference: false));

            // --- Debuffs ---

            // Puller
            ItemManager.Instance.AddStatusEffect(
                new CustomStatusEffect(ScriptableObject.CreateInstance<SE_KartFrost>(), fixReference: false));
            ItemManager.Instance.AddStatusEffect(
                new CustomStatusEffect(ScriptableObject.CreateInstance<SE_KartTarred>(), fixReference: false));
            ItemManager.Instance.AddStatusEffect(
                new CustomStatusEffect(ScriptableObject.CreateInstance<SE_KartBounce>(), fixReference: false));

            // Rider
            ItemManager.Instance.AddStatusEffect(
                new CustomStatusEffect(ScriptableObject.CreateInstance<SE_KartPoison>(), fixReference: false));
            ItemManager.Instance.AddStatusEffect(
                new CustomStatusEffect(ScriptableObject.CreateInstance<SE_KartBurn>(), fixReference: false));
            ItemManager.Instance.AddStatusEffect(
                new CustomStatusEffect(ScriptableObject.CreateInstance<SE_KartStagger>(), fixReference: false));
            ItemManager.Instance.AddStatusEffect(
                new CustomStatusEffect(ScriptableObject.CreateInstance<SE_KartDisarm>(), fixReference: false));

            // Both
            ItemManager.Instance.AddStatusEffect(
                new CustomStatusEffect(ScriptableObject.CreateInstance<SE_KartWeak>(), fixReference: false));
            ItemManager.Instance.AddStatusEffect(
                new CustomStatusEffect(ScriptableObject.CreateInstance<SE_KartBlind>(), fixReference: false));
            ItemManager.Instance.AddStatusEffect(
                new CustomStatusEffect(ScriptableObject.CreateInstance<SE_KartShock>(), fixReference: false));

            DebugLog("Custom status effects registered");
        }
        catch (Exception ex)
        {
            Jotunn.Logger.LogWarning($"Caught exception while creating status effects: {ex}");
        }
        finally
        {
            PrefabManager.OnVanillaPrefabsAvailable -= RegisterCustomStatusEffects;
        }
    }
}