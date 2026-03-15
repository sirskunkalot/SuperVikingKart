using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using Jotunn.Utils;
using System;
using UnityEngine;

namespace SuperVikingKart
{
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
        
        public static SuperVikingKart Instance;
        public static CustomLocalization Localization = LocalizationManager.Instance.GetLocalization();

        public static ConfigEntry<int> CartRespawnTimeConfig;
        public static ConfigEntry<int> BuffBlockRespawnTimeConfig;
        public static ConfigEntry<int> KartMassConfig;
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
            KartMassConfig = Config.Bind("General", "KartMass", 10,
                new ConfigDescription("Total mass of the kart. Lower is faster. Server synced value.",
                    new AcceptableValueRange<int>(1, 100),
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));

            KartMassConfig.SettingChanged += (_, _) =>
            {
                foreach (var vagon in Vagon.m_instances)
                {
                    var kart = vagon.GetComponentInChildren<SuperVikingKartComponent>();
                    if (!kart) continue;

                    vagon.m_baseMass = (float)KartMassConfig.Value;
                    vagon.SetMass(vagon.m_baseMass);
                    DebugLog($"KartMass updated to {KartMassConfig.Value}");
                }
            };

            _harmony = new Harmony(PluginGUID);
            _harmony.PatchAll();

            PrefabManager.OnVanillaPrefabsAvailable += CloneCart;
            PrefabManager.OnVanillaPrefabsAvailable += CreateBuffBlock;
            PrefabManager.OnVanillaPrefabsAvailable += CreateDebuffBlock;
            PrefabManager.OnVanillaPrefabsAvailable += CreateMysteryBlock;
            PrefabManager.OnVanillaPrefabsAvailable += RegisterCustomStatusEffects;
            
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

        private void CloneCart()
        {
            try
            {
                var cart = new CustomPiece(KartPrefabName, "Cart", new PieceConfig
                {
                    Name = "SuperVikingKart",
                    Description = "Mountable cart. Get ready to race.",
                    PieceTable = PieceTables.Hammer,
                    Category = PieceCategories.Misc,
                    Requirements = new[]
                    {
                        new RequirementConfig("Wood", 1)
                    }
                });
                
                //TODO: with or without station?
                cart.Piece.m_craftingStation = null; 
                cart.Piece.m_canBeRemoved = true;
                PieceManager.Instance.AddPiece(cart);

                var tf = cart.PiecePrefab.transform;
                DestroyImmediate(tf.Find("load").gameObject);

                var attach = new GameObject("AttachPointPlayer");
                attach.transform.SetParent(tf, false);
                attach.transform.SetAsFirstSibling();
                attach.transform.localPosition = Vector3.up * 0.5f;

                var container = tf.Find("Container").gameObject;
                DestroyImmediate(container.GetComponent<Container>());

                var chair = container.AddComponent<SuperVikingKartComponent>();
                chair.AttachPoint = attach.transform;
            }
            catch (Exception ex)
            {
                Jotunn.Logger.LogWarning($"Caught exception while creating cart: {ex}");
            }
            finally
            {
                PrefabManager.OnVanillaPrefabsAvailable -= CloneCart;
            }
        }

        private void CreateBuffBlock()
        {
            try
            {
                CreateBlock(BuffBlockPrefabName, "BuffBlock",
                    "Drive through for a random buff!",
                    CreateBuffBlockMaterial(), BlockType.Buff);
            }
            catch (Exception ex)
            {
                Jotunn.Logger.LogWarning($"Caught exception while creating buff block: {ex}");
            }
            finally
            {
                PrefabManager.OnVanillaPrefabsAvailable -= CreateBuffBlock;
            }
        }

        private void CreateDebuffBlock()
        {
            try
            {
                CreateBlock(DebuffBlockPrefabName, "DebuffBlock",
                    "Drive through for a random debuff!",
                    CreateDebuffBlockMaterial(), BlockType.Debuff);
            }
            catch (Exception ex)
            {
                Jotunn.Logger.LogWarning($"Caught exception while creating debuff block: {ex}");
            }
            finally
            {
                PrefabManager.OnVanillaPrefabsAvailable -= CreateDebuffBlock;
            }
        }

        private void CreateMysteryBlock()
        {
            try
            {
                CreateBlock(MysteryBlockPrefabName, "MysteryBlock",
                    "Drive through for a mystery effect!",
                    CreateMysteryBlockMaterial(), BlockType.Mystery);
            }
            catch (Exception ex)
            {
                Jotunn.Logger.LogWarning($"Caught exception while creating mystery block: {ex}");
            }
            finally
            {
                PrefabManager.OnVanillaPrefabsAvailable -= CreateMysteryBlock;
            }
        }

        private void CreateBlock(string prefabName, string displayName, string description, Material material, BlockType blockType)
        {
            var prefab = new GameObject(prefabName);
            prefab.layer = LayerMask.NameToLayer("piece_nonsolid");
            prefab.SetActive(false);

            var netView = prefab.AddComponent<ZNetView>();
            netView.m_persistent = true;

            var piece = prefab.AddComponent<Piece>();
            piece.m_canBeRemoved = true;
            //TODO: with or without station?
            //piece.m_craftingStation = PrefabManager.Cache.GetPrefab<CraftingStation>(CraftingStations.Workbench);

            var collider = prefab.AddComponent<BoxCollider>();
            collider.center = Vector3.up * 1f;
            collider.size = Vector3.one * 0.8f;

            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "Visual";
            visual.transform.SetParent(prefab.transform, false);
            visual.transform.localPosition = Vector3.up * 1.5f;
            visual.transform.localScale = Vector3.one * 0.8f;

            var renderer = visual.GetComponent<MeshRenderer>();
            renderer.material = material;

            var trigger = visual.GetComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = Vector3.one * 1.5f;

            var rb = visual.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            visual.AddComponent<BuffBlockSpin>();

            var buffBlock = prefab.AddComponent<BuffBlockComponent>();
            buffBlock.Visual = visual;
            buffBlock.BlockType = blockType;

            var collectEffect = PrefabManager.Instance.GetPrefab("vfx_Place_chest");
            buffBlock.CollectEffectPrefab = collectEffect;

            var triggerRelay = visual.AddComponent<BuffBlockTrigger>();
            triggerRelay.BuffBlock = buffBlock;
            
            var icon = RenderManager.Instance.Render(prefab, RenderManager.IsometricRotation);

            var customPiece = new CustomPiece(prefab, false, new PieceConfig
            {
                Name = displayName,
                Description = description,
                PieceTable = PieceTables.Hammer,
                Category = PieceCategories.Misc,
                Icon = icon,
                Requirements = new[]
                {
                    new RequirementConfig("Wood", 1)
                }
            });

            PieceManager.Instance.AddPiece(customPiece);
            prefab.SetActive(true);
        }

        private Material CreateBuffBlockMaterial() =>
            CreateBlockMaterial(
                new Color(1f, 0.85f, 0f),
                new Color(0.6f, 0.5f, 0f),
                new Color(0.2f, 0.15f, 0f));

        private Material CreateDebuffBlockMaterial() =>
            CreateBlockMaterial(
                new Color(1f, 0.2f, 0.2f),
                new Color(0.7f, 0.1f, 0.1f),
                new Color(0.3f, 0.05f, 0.05f));

        private Material CreateMysteryBlockMaterial() =>
            CreateBlockMaterial(
                new Color(0.5f, 0.2f, 0.8f),
                new Color(0.3f, 0.1f, 0.5f),
                new Color(0.15f, 0.05f, 0.3f));
        
        private Material CreateBlockMaterial(Color bgColor, Color borderColor, Color markColor)
        {
            var texture = new Texture2D(64, 64);
            var colors = new Color[64 * 64];

            for (var i = 0; i < colors.Length; i++)
                colors[i] = bgColor;

            for (var x = 0; x < 64; x++)
            for (var y = 0; y < 64; y++)
                if (x < 4 || x >= 60 || y < 4 || y >= 60)
                    colors[y * 64 + x] = borderColor;

            // Question mark
            for (var x = 22; x < 42; x++)
            for (var y = 44; y < 52; y++)
                colors[y * 64 + x] = markColor;
            for (var x = 36; x < 42; x++)
            for (var y = 36; y < 44; y++)
                colors[y * 64 + x] = markColor;
            for (var x = 28; x < 42; x++)
            for (var y = 28; y < 36; y++)
                colors[y * 64 + x] = markColor;
            for (var x = 28; x < 36; x++)
            for (var y = 20; y < 28; y++)
                colors[y * 64 + x] = markColor;
            for (var x = 28; x < 36; x++)
            for (var y = 10; y < 18; y++)
                colors[y * 64 + x] = markColor;

            texture.SetPixels(colors);
            texture.Apply();
            texture.filterMode = FilterMode.Point;

            var torchPrefab = PrefabManager.Instance.GetPrefab("Torch");
            var torchMat = torchPrefab.GetComponentInChildren<MeshRenderer>().material;

            return new Material(torchMat)
            {
                mainTexture = texture,
                color = Color.white
            };
        }

        private void RegisterCustomStatusEffects()
        {
            var oozeBombs = ScriptableObject.CreateInstance<SE_OozeBombs>();
            ItemManager.Instance.AddStatusEffect(new CustomStatusEffect(oozeBombs, fixReference: false));

            var staminaBurst = ScriptableObject.CreateInstance<SE_StaminaBurst>();
            ItemManager.Instance.AddStatusEffect(new CustomStatusEffect(staminaBurst, fixReference: false));
            
            var kartPoison = ScriptableObject.CreateInstance<SE_KartPoison>();
            ItemManager.Instance.AddStatusEffect(new CustomStatusEffect(kartPoison, fixReference: false));

            var kartBurn = ScriptableObject.CreateInstance<SE_KartBurn>();
            ItemManager.Instance.AddStatusEffect(new CustomStatusEffect(kartBurn, fixReference: false));

            var kartFrost = ScriptableObject.CreateInstance<SE_KartFrost>();
            ItemManager.Instance.AddStatusEffect(new CustomStatusEffect(kartFrost, fixReference: false));
            
            var kartShock = ScriptableObject.CreateInstance<SE_KartShock>();
            ItemManager.Instance.AddStatusEffect(new CustomStatusEffect(kartShock, fixReference: false));
            
            var kartStagger = ScriptableObject.CreateInstance<SE_KartStagger>();
            ItemManager.Instance.AddStatusEffect(new CustomStatusEffect(kartStagger, fixReference: false));

            var kartTarred = ScriptableObject.CreateInstance<SE_KartTarred>();
            ItemManager.Instance.AddStatusEffect(new CustomStatusEffect(kartTarred, fixReference: false));
            
            var kartBlind = ScriptableObject.CreateInstance<SE_KartBlind>();
            ItemManager.Instance.AddStatusEffect(new CustomStatusEffect(kartBlind, fixReference: false));
            
            var kartBounce = ScriptableObject.CreateInstance<SE_KartBounce>();
            ItemManager.Instance.AddStatusEffect(new CustomStatusEffect(kartBounce, fixReference: false));
            
            var fireArrows = ScriptableObject.CreateInstance<SE_FireArrows>();
            ItemManager.Instance.AddStatusEffect(new CustomStatusEffect(fireArrows, fixReference: false));

            var bileBombs = ScriptableObject.CreateInstance<SE_BileBombs>();
            ItemManager.Instance.AddStatusEffect(new CustomStatusEffect(bileBombs, fixReference: false));

            var kartBerserk = ScriptableObject.CreateInstance<SE_KartBerserk>();
            ItemManager.Instance.AddStatusEffect(new CustomStatusEffect(kartBerserk, fixReference: false));

            var kartDisarm = ScriptableObject.CreateInstance<SE_KartDisarm>();
            ItemManager.Instance.AddStatusEffect(new CustomStatusEffect(kartDisarm, fixReference: false));
            
            var speedBoost = ScriptableObject.CreateInstance<SE_KartSpeedBoost>();
            ItemManager.Instance.AddStatusEffect(new CustomStatusEffect(speedBoost, fixReference: false));
            
            DebugLog("Custom status effects registered");

            PrefabManager.OnVanillaPrefabsAvailable -= RegisterCustomStatusEffects;
        }
    }
}