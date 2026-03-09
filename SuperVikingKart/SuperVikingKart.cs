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

        public static SuperVikingKart Instance;
        public static CustomLocalization Localization = LocalizationManager.Instance.GetLocalization();
        public static ConfigEntry<int> CartRespawnTimeConfig;
        public static ConfigEntry<int> BuffBlockRespawnTimeConfig;
        private static ConfigEntry<bool> _debugConfig;
        private Harmony _harmony;

        private void Awake()
        {
            Instance = this;
            _debugConfig = Config.Bind("General", "Debug", false, "Enable debug logging");
            CartRespawnTimeConfig = Config.Bind("General", "CartRespawnTime", 10,
                new ConfigDescription("Time in seconds before a destroyed cart respawns. Server synced value.",
                    new AcceptableValueRange<int>(0, 300),
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));
            BuffBlockRespawnTimeConfig = Config.Bind("General", "BuffBlockRespawnTime", 10,
                new ConfigDescription("Time in seconds before a collected buff block reappears. Server synced value.",
                    new AcceptableValueRange<int>(0, 300),
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));

            _harmony = new Harmony(PluginGUID);
            _harmony.PatchAll();

            PrefabManager.OnVanillaPrefabsAvailable += CloneCart;
            PrefabManager.OnVanillaPrefabsAvailable += CreateBuffBlock;
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
        }

        public static void DebugLog(string message)
        {
            if (_debugConfig.Value)
                Jotunn.Logger.LogDebug(message);
        }

        private void CloneCart()
        {
            try
            {
                var cart = new CustomPiece("SuperVikingKart", "Cart", new PieceConfig
                {
                    Name = "SuperVikingKart",
                    Description = "Mountable kart. Get ready to race.",
                    PieceTable = PieceTables.Hammer,
                    Category = PieceCategories.Misc,
                    Requirements = new[]
                    {
                        new RequirementConfig("Wood", 1)
                    }
                });

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
                // Base prefab with components
                var prefab = new GameObject("BuffBlock");
                prefab.layer = LayerMask.NameToLayer("piece_nonsolid");
                // IMPORTANT: Needs to be inactive while setting up or the ZNetView gets wrecked
                prefab.SetActive(false); 

                var netView = prefab.AddComponent<ZNetView>();
                netView.m_persistent = true;

                var piece = prefab.AddComponent<Piece>();
                piece.m_canBeRemoved = true;

                var collider = prefab.AddComponent<BoxCollider>();
                collider.center = Vector3.up * 1f;
                collider.size = Vector3.one * 0.8f;
                
                // Visual part
                var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
                visual.name = "Visual";
                visual.transform.SetParent(prefab.transform, false);
                visual.transform.localPosition = Vector3.up * 1.5f;
                visual.transform.localScale = Vector3.one * 0.8f;

                var renderer = visual.GetComponent<MeshRenderer>();
                renderer.material = CreateBuffBlockMaterial();

                var trigger = visual.GetComponent<BoxCollider>();
                trigger.isTrigger = true;
                trigger.size = Vector3.one * 1.5f;

                var rb = visual.AddComponent<Rigidbody>();
                rb.isKinematic = true;
                rb.useGravity = false;

                visual.AddComponent<BuffBlockSpin>();
                
                // Add main component to prefab and wire visual
                var buffBlock = prefab.AddComponent<BuffBlockComponent>();
                buffBlock.Visual = visual;
                
                // Add trigger relay to visual and wire main component
                var triggerRelay = visual.AddComponent<BuffBlockTrigger>();
                triggerRelay.BuffBlock = buffBlock;

                var icon = RenderManager.Instance.Render(prefab, RenderManager.IsometricRotation);

                var customPiece = new CustomPiece(prefab, false, new PieceConfig
                {
                    Name = "BuffBlock",
                    Description = "Drive through for a random buff!",
                    PieceTable = PieceTables.Hammer,
                    Category = PieceCategories.Misc,
                    Icon = icon,
                    Requirements = new[]
                    {
                        new RequirementConfig("Wood", 1)
                    }
                });

                PieceManager.Instance.AddPiece(customPiece);

                // IMPORTANT: Needs to be enabled again to actually be in game.
                // After AddPiece it lives under a disabled parent in Jötunn. 
                // Took me waaaay too much time figuring it out / remembering :D
                prefab.SetActive(true);
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

        private Material CreateBuffBlockMaterial()
        {
            var texture = new Texture2D(64, 64);
            var colors = new Color[64 * 64];

            var bgColor = new Color(1f, 0.85f, 0f);
            for (var i = 0; i < colors.Length; i++)
                colors[i] = bgColor;

            var borderColor = new Color(0.6f, 0.5f, 0f);
            for (var x = 0; x < 64; x++)
            {
                for (var y = 0; y < 64; y++)
                {
                    if (x < 4 || x >= 60 || y < 4 || y >= 60)
                        colors[y * 64 + x] = borderColor;
                }
            }

            var markColor = new Color(0.2f, 0.15f, 0f);

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

            var material = new Material(torchMat)
            {
                mainTexture = texture,
                color = Color.white
            };

            return material;
        }
    }
}