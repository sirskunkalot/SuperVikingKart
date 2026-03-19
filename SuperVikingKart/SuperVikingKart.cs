using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using Jotunn.Utils;
using System;
using TMPro;
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
            PrefabManager.OnVanillaPrefabsAvailable += CloneCart;
            PrefabManager.OnVanillaPrefabsAvailable += CreateBuffBlock;
            PrefabManager.OnVanillaPrefabsAvailable += CreateDebuffBlock;
            PrefabManager.OnVanillaPrefabsAvailable += CreateMysteryBlock;
            PrefabManager.OnVanillaPrefabsAvailable += RegisterCustomStatusEffects;
            PrefabManager.OnVanillaPrefabsAvailable += CreateRaceBoard;
            PrefabManager.OnVanillaPrefabsAvailable += CreateRaceLine;
            
            // (Re-)Initialize the RaceManager on every connect to a server / start of a local game
            MinimapManager.OnVanillaMapDataLoaded += RaceManager.Init;

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

        private void CloneCart()
        {
            try
            {
                var cart = new CustomPiece(KartPrefabName, "Cart", new PieceConfig
                {
                    Name = "Super Viking Kart",
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
                CreateBlock(BuffBlockPrefabName, "Buff Block",
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
                CreateBlock(DebuffBlockPrefabName, "Debuff Block",
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
                CreateBlock(MysteryBlockPrefabName, "Mystery Block",
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
        
        /*private void CreateRaceBoard()
        {
            try
            {
                var assetBundle = AssetUtils.LoadAssetBundleFromResources("supervikingkart");
                var prefab = assetBundle.LoadAsset<GameObject>("RaceBoard");
                prefab.SetActive(false);

                // Root — add coordinator only, ZNetView/Piece/WearNTear come from Unity
                var board = prefab.AddComponent<RaceBoardComponent>();

                // StatusDisplay child — add TextMeshPro
                var displayGo = prefab.transform.Find("StatusDisplay").gameObject;
                var tmp = displayGo.AddComponent<TMPro.TextMeshPro>();
                tmp.alignment = TMPro.TextAlignmentOptions.TopLeft;
                tmp.fontSize = 3f;
                tmp.color = Color.white;
                board.StatusDisplay = tmp;

                // Wire up buttons
                board.RegisterButton = WireButton(prefab, "RegisterButton", RaceBoardButtonType.Register, board);
                board.StartButton    = WireButton(prefab, "StartButton",    RaceBoardButtonType.Start,    board);
                board.ResetButton    = WireButton(prefab, "ResetButton",    RaceBoardButtonType.Reset,    board);
                board.AdminButton    = WireButton(prefab, "AdminButton",    RaceBoardButtonType.Admin,    board);
                
                // Rgister into KitbashManeger to apply the kitbash
                KitbashManager.Instance.AddKitbash(prefab, new KitbashConfig { 
                    Layer = "piece",
                    KitbashSources = new List<KitbashSourceConfig>
                    {
                        new()
                        {
                            Name = "eye_1",
                            SourcePrefab = "Ruby",
                            SourcePath = "attach/model",
                            Position = new Vector3(0.528f, 0.1613345f, -0.253f),
                            Rotation = Quaternion.Euler(0, 180, 0f),
                            Scale = new Vector3(0.02473f, 0.05063999f, 0.05064f)
                        },
                        new()
                        {
                            Name = "eye_2",
                            SourcePrefab = "Ruby",
                            SourcePath = "attach/model",
                            Position = new Vector3(0.528f, 0.1613345f, 0.253f),
                            Rotation = Quaternion.Euler(0, 180, 0f),
                            Scale = new Vector3(0.02473f, 0.05063999f, 0.05064f)
                        },
                        new()
                        {
                            Name = "mouth",
                            SourcePrefab = "draugr_bow",
                            SourcePath = "attach/bow",
                            Position = new Vector3(0.53336f, -0.315f, -0.001953f),
                            Rotation = Quaternion.Euler(-0.06500001f, -2.213f, -272.086f),
                            Scale = new Vector3(0.41221f, 0.41221f, 0.41221f)
                        }
                    }
                }); 

                var icon = RenderManager.Instance.Render(prefab, RenderManager.IsometricRotation);

                var customPiece = new CustomPiece(prefab, true, new PieceConfig
                {
                    Name = "Race Board",
                    Description = "Place to configure and manage a race.",
                    PieceTable = PieceTables.Hammer,
                    Category = PieceCategories.Misc,
                    Icon = icon,
                    Requirements = new[]
                    {
                        new RequirementConfig("Wood", 4),
                        new RequirementConfig("Stone", 2)
                    }
                });

                PieceManager.Instance.AddPiece(customPiece);
                prefab.SetActive(true);
                DebugLog("RaceBoard prefab registered");
            }
            catch (Exception ex)
            {
                Jotunn.Logger.LogWarning($"Caught exception while creating race board: {ex}");
            }
            finally
            {
                PrefabManager.OnVanillaPrefabsAvailable -= CreateRaceBoard;
            }
            
            RaceBoardButton WireButton(GameObject prefab, string childName,
                RaceBoardButtonType type, RaceBoardComponent board)
            {
                var go = prefab.transform.Find(childName).gameObject;
                var button = go.AddComponent<RaceBoardButton>();
                button.ButtonType = type;
                button.Board = board;
                return button;
            }
        }*/
        
        private void CreateRaceBoard()
        {
            try
            {
                var prefab = new GameObject(RaceBoardPrefabName);
                prefab.layer = LayerMask.NameToLayer("piece");
                prefab.SetActive(false);

                var netView = prefab.AddComponent<ZNetView>();
                netView.m_persistent = true;

                var piece = prefab.AddComponent<Piece>();
                piece.m_canBeRemoved = true;

                // BoardVisual
                var boardVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
                boardVisual.name = "BoardVisual";
                boardVisual.transform.SetParent(prefab.transform, false);
                boardVisual.transform.localPosition = new Vector3(0f, 2.6f, 0f);
                boardVisual.transform.localScale    = new Vector3(2.4f, 2.8f, 0.1f);
                DestroyImmediate(boardVisual.GetComponent<BoxCollider>());
                boardVisual.GetComponent<MeshRenderer>().material = CreateRaceBoardMaterial();

                // StatusDisplay
                var displayGo = new GameObject("StatusDisplay");
                displayGo.transform.SetParent(prefab.transform, false);
                displayGo.transform.localRotation = Quaternion.identity;
                displayGo.transform.localScale    = Vector3.one;

                var tmp = displayGo.AddComponent<TMPro.TextMeshPro>();
                tmp.font               = PrefabManager.Cache.GetPrefab<TMP_FontAsset>("Valheim-AveriaSansLibre");
                tmp.alignment          = TMPro.TextAlignmentOptions.TopLeft;
                tmp.fontSize           = 1f;
                tmp.color              = Color.white;
                tmp.textWrappingMode   = TextWrappingModes.Normal;
                tmp.overflowMode       = TMPro.TextOverflowModes.Overflow;

                tmp.rectTransform.pivot              = new Vector2(0.5f, 0.5f);
                tmp.rectTransform.anchorMin          = new Vector2(0.5f, 0.5f);
                tmp.rectTransform.anchorMax          = new Vector2(0.5f, 0.5f);
                tmp.rectTransform.sizeDelta          = new Vector2(2.1f, 2.7f);
                tmp.rectTransform.anchoredPosition3D = new Vector3(0.05f, 2.4f, -0.06f);

                // Buttons
                var registerButtonGo = CreateButtonObject("RegisterButton", prefab.transform, new Vector3(-0.9f,  1.25f, -0.06f), "Register");
                var startButtonGo    = CreateButtonObject("StartButton",    prefab.transform, new Vector3(-0.3f,  1.25f, -0.06f), "Start");
                var resetButtonGo    = CreateButtonObject("ResetButton",    prefab.transform, new Vector3( 0.3f,  1.25f, -0.06f), "Reset");
                var adminButtonGo    = CreateButtonObject("AdminButton",    prefab.transform, new Vector3( 0.9f,  1.25f, -0.06f), "Admin");

                // Collider
                var rootCollider    = prefab.AddComponent<BoxCollider>();
                rootCollider.center = new Vector3(0f, 2f, 0f);
                rootCollider.size   = new Vector3(2.5f, 4f, 0.2f);

                // RaceBoardComponent
                var board            = prefab.AddComponent<RaceBoardComponent>();
                board.StatusDisplay  = tmp;
                board.RegisterButton = WireButton(registerButtonGo, RaceBoardButtonType.Register, board);
                board.StartButton    = WireButton(startButtonGo,    RaceBoardButtonType.Start,    board);
                board.ResetButton    = WireButton(resetButtonGo,    RaceBoardButtonType.Reset,    board);
                board.AdminButton    = WireButton(adminButtonGo,    RaceBoardButtonType.Admin,    board);

                // Register
                var icon = RenderManager.Instance.Render(prefab, RenderManager.IsometricRotation);

                PieceManager.Instance.AddPiece(new CustomPiece(prefab, false, new PieceConfig
                {
                    Name         = "Race Board",
                    Description  = "Place to configure and manage a race. Shows you race statistics in real time.",
                    PieceTable   = PieceTables.Hammer,
                    Category     = PieceCategories.Misc,
                    Icon         = icon,
                    Requirements = new[]
                    {
                        new RequirementConfig("Wood", 4),
                        new RequirementConfig("Stone", 2)
                    }
                }));

                prefab.SetActive(true);
                DebugLog("RaceBoard prefab registered");
            }
            catch (Exception ex)
            {
                Jotunn.Logger.LogWarning($"Caught exception while creating race board: {ex}");
            }
            finally
            {
                PrefabManager.OnVanillaPrefabsAvailable -= CreateRaceBoard;
            }
        }

        private Material CreateRaceBoardMaterial()
        {
            var texture = new Texture2D(64, 64);
            var colors  = new Color[64 * 64];
            var bg      = new Color(0.15f, 0.1f, 0.05f);
            var border  = new Color(0.6f,  0.4f, 0.1f);

            for (var i = 0; i < colors.Length; i++)
                colors[i] = bg;

            for (var x = 0; x < 64; x++)
            for (var y = 0; y < 64; y++)
                if (x < 3 || x >= 61 || y < 3 || y >= 61)
                    colors[y * 64 + x] = border;

            texture.SetPixels(colors);
            texture.Apply();
            texture.filterMode = FilterMode.Point;

            var torchMat = PrefabManager.Instance.GetPrefab("Torch")
                .GetComponentInChildren<MeshRenderer>().material;

            return new Material(torchMat) { mainTexture = texture, color = Color.white };
        }

        private GameObject CreateButtonObject(string name, Transform parent, Vector3 localPos, string label)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale    = Vector3.one;

            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "Visual";
            visual.transform.SetParent(go.transform, false);
            visual.transform.localScale = new Vector3(0.48f, 0.18f, 0.06f);
            DestroyImmediate(visual.GetComponent<BoxCollider>());
            visual.GetComponent<MeshRenderer>().material = CreateButtonMaterial();

            var collider  = go.AddComponent<BoxCollider>();
            collider.size = new Vector3(0.48f, 0.18f, 0.12f);

            // Label parented to root (no inherited scale from visual cube)
            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(go.transform, false);
            labelGo.transform.localPosition = new Vector3(0f, 0f, -0.04f);
            labelGo.transform.localScale    = Vector3.one;

            var labelTmp = labelGo.AddComponent<TMPro.TextMeshPro>();
            labelTmp.font               = PrefabManager.Cache.GetPrefab<TMP_FontAsset>("Valheim-AveriaSansLibre");
            labelTmp.text               = label;
            labelTmp.alignment          = TMPro.TextAlignmentOptions.Center;
            labelTmp.fontSize           = 1f;
            labelTmp.color              = Color.white;
            labelTmp.textWrappingMode   = TextWrappingModes.Normal;
            labelTmp.rectTransform.sizeDelta       = new Vector2(0.44f, 0.16f);
            labelTmp.rectTransform.pivot           = new Vector2(0.5f, 0.5f);
            labelTmp.rectTransform.anchorMin       = new Vector2(0.5f, 0.5f);
            labelTmp.rectTransform.anchorMax       = new Vector2(0.5f, 0.5f);
            labelTmp.rectTransform.anchoredPosition = Vector2.zero;

            return go;
        }

        private Material CreateButtonMaterial()
        {
            var texture = new Texture2D(32, 16);
            var colors  = new Color[32 * 16];
            var bg      = new Color(0.20f, 0.12f, 0.04f);
            var border  = new Color(0.55f, 0.35f, 0.08f);

            for (var i = 0; i < colors.Length; i++)
                colors[i] = bg;

            for (var x = 0; x < 32; x++)
            for (var y = 0; y < 16; y++)
                if (x < 2 || x >= 30 || y < 2 || y >= 14)
                    colors[y * 32 + x] = border;

            texture.SetPixels(colors);
            texture.Apply();
            texture.filterMode = FilterMode.Point;

            var torchMat = PrefabManager.Instance.GetPrefab("Torch")
                .GetComponentInChildren<MeshRenderer>().material;

            return new Material(torchMat) { mainTexture = texture, color = Color.white };
        }

        private RaceBoardButton WireButton(GameObject go, RaceBoardButtonType type, RaceBoardComponent board)
        {
            var btn = go.AddComponent<RaceBoardButton>();
            btn.ButtonType = type;
            btn.Board      = board;
            return btn;
        }
    
        private void CreateRaceLine()
        {
            try
            {
                // Root
                var prefab = new GameObject(RaceLinePrefabName);
                prefab.layer = LayerMask.NameToLayer("piece");
                prefab.SetActive(false);

                var netView = prefab.AddComponent<ZNetView>();
                netView.m_persistent = true;

                var piece = prefab.AddComponent<Piece>();
                piece.m_canBeRemoved = true;

                // Place collider for placement
                var placeGo = new GameObject("PlaceCollider");
                placeGo.transform.SetParent(prefab.transform, false);
                var snapCol        = placeGo.AddComponent<BoxCollider>();
                snapCol.center     = new Vector3(3f, 0f, 0f);
                snapCol.size       = new Vector3(6f, 0.001f, 0.001f);

                // Trigger Collider
                var triggerGo = new GameObject("TriggerCollider");
                triggerGo.transform.SetParent(prefab.transform, false);
                triggerGo.transform.localPosition = new Vector3(0f, 0f, 0f);

                // Kinematic Rigidbody required for trigger callbacks
                var rb           = triggerGo.AddComponent<Rigidbody>();
                rb.isKinematic   = true;
                rb.useGravity    = false;

                var triggerCol      = triggerGo.AddComponent<BoxCollider>();
                triggerCol.isTrigger = true;
                triggerCol.size      = new Vector3(6f, 3f, 1f); // wide, tall, thin
                
                // Gate posts
                CreatePost("PostLeft",  prefab.transform, new Vector3(-3f, 1.5f, 0f));
                CreatePost("PostRight", prefab.transform, new Vector3( 3f, 1.5f, 0f));

                // Ground quad (chequered)
                var groundQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                groundQuad.name = "GroundQuad";
                groundQuad.transform.SetParent(prefab.transform, false);
                groundQuad.transform.localPosition = new Vector3(0f, 0.02f, 0f);
                groundQuad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                groundQuad.transform.localScale    = new Vector3(6f, 1f, 1f);
                DestroyImmediate(groundQuad.GetComponent<MeshCollider>());
                var groundMat = CreateChequeredMaterial();
                groundMat.mainTextureScale = new Vector2(
                    groundQuad.transform.localScale.x / groundQuad.transform.localScale.z,
                    1f
                );
                groundQuad.GetComponent<MeshRenderer>().material = groundMat;

                // Direction arrow
                var arrowQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                arrowQuad.name = "DirectionArrow";
                arrowQuad.transform.SetParent(prefab.transform, false);
                arrowQuad.transform.localPosition = new Vector3(0f, 0.03f, 0.6f); // slightly in front of line
                arrowQuad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                arrowQuad.transform.localScale    = new Vector3(1f, 1f, 1f);
                DestroyImmediate(arrowQuad.GetComponent<MeshCollider>());
                arrowQuad.GetComponent<MeshRenderer>().material = CreateArrowMaterial();

                // World-space label
                var labelGo = new GameObject("Label");
                labelGo.transform.SetParent(prefab.transform, false);
                labelGo.transform.localPosition = Vector3.zero;
                labelGo.transform.localScale    = Vector3.one;

                var tmp = labelGo.AddComponent<TextMeshPro>();
                tmp.font             = PrefabManager.Cache.GetPrefab<TMP_FontAsset>("Valheim-Norse");
                tmp.alignment        = TextAlignmentOptions.Center;
                tmp.fontSize         = 3f;
                tmp.color            = Color.black;
                tmp.fontStyle        = FontStyles.Bold;
                tmp.textWrappingMode = TextWrappingModes.Normal;
                tmp.overflowMode     = TextOverflowModes.Overflow;
                tmp.rectTransform.sizeDelta          = new Vector2(6f, 1.5f);
                tmp.rectTransform.pivot              = new Vector2(0.5f, 0.5f);
                tmp.rectTransform.anchorMin          = new Vector2(0.5f, 0.5f);
                tmp.rectTransform.anchorMax          = new Vector2(0.5f, 0.5f);
                tmp.rectTransform.anchoredPosition3D = new Vector3(0f, 3f, -0.05f);

                // Banner between posts
                var banner = GameObject.CreatePrimitive(PrimitiveType.Cube);
                banner.name = "Banner";
                banner.transform.SetParent(prefab.transform, false);
                banner.transform.localPosition = new Vector3(0f, 3f, 0f);
                banner.transform.localScale    = new Vector3(6f, 0.4f, 0.01f);
                banner.GetComponent<MeshRenderer>().material = CreateBannerMaterial();
                
                // RaceLineComponent on root
                var raceLine = prefab.AddComponent<RaceLineComponent>();
                raceLine.Label = tmp;

                // Trigger relay component
                var relay = triggerGo.AddComponent<RaceLineTrigger>();
                relay.Line = raceLine;
                
                // Global scale
                prefab.transform.localScale = new Vector3(1.2f, 1.2f, 1f);

                // Register
                var icon = RenderManager.Instance.Render(prefab, RenderManager.IsometricRotation);

                PieceManager.Instance.AddPiece(new CustomPiece(prefab, false, new PieceConfig
                {
                    Name        = "Race Line",
                    Description = "Start and/or finish line for a race. Place the arrow facing the direction of travel.",
                    PieceTable  = PieceTables.Hammer,
                    Category    = PieceCategories.Misc,
                    Icon        = icon,
                    Requirements = new[]
                    {
                        new RequirementConfig("Wood", 2)
                    }
                }));

                prefab.SetActive(true);
                DebugLog("RaceLine prefab registered");
            }
            catch (Exception ex)
            {
                Jotunn.Logger.LogWarning($"Caught exception while creating race line: {ex}");
            }
            finally
            {
                PrefabManager.OnVanillaPrefabsAvailable -= CreateRaceLine;
            }
        }

        // ---- Prefab helpers -------------------------------------------------

        private void CreatePost(string name, Transform parent, Vector3 localPos)
        {
            var post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            post.name = name;
            post.transform.SetParent(parent, false);
            post.transform.localPosition = localPos;
            post.transform.localScale    = new Vector3(0.15f, 1.5f, 0.15f); // height = 3 units total
            //DestroyImmediate(post.GetComponent<CapsuleCollider>());
            post.GetComponent<MeshRenderer>().material = CreatePostMaterial();
        }

        private Material CreateChequeredMaterial()
        {
            const int size = 64;
            var texture = new Texture2D(size, size);
            var colors  = new Color[size * size];

            for (var x = 0; x < size; x++)
            for (var y = 0; y < size; y++)
            {
                // 8x8 chequered squares
                var cx = x / 8;
                var cy = y / 8;
                colors[y * size + x] = ((cx + cy) % 2 == 0) ? Color.white : Color.black;
            }

            texture.SetPixels(colors);
            texture.Apply();
            texture.filterMode = FilterMode.Point;

            var torchMat = PrefabManager.Instance.GetPrefab("Torch")
                .GetComponentInChildren<MeshRenderer>().material;
            return new Material(torchMat) { mainTexture = texture, color = Color.white };
        }

        private Material CreateArrowMaterial()
        {
            const int size = 32;
            var texture = new Texture2D(size, size);
            var colors  = new Color[size * size];

            // Transparent background
            for (var i = 0; i < colors.Length; i++)
                colors[i] = new Color(1f, 0.6f, 0f, 0f);

            // Paint a simple right-pointing arrow in orange (pointing toward +Z = forward)
            // Arrow body
            for (var x = 4; x < 20; x++)
            for (var y = 12; y < 20; y++)
                colors[y * size + x] = new Color(1f, 0.6f, 0f, 1f);

            // Arrow head (triangle pointing right)
            for (var x = 20; x < 30; x++)
            {
                var halfWidth = (30 - x);
                var yMin = 16 - halfWidth;
                var yMax = 16 + halfWidth;
                for (var y = yMin; y <= yMax; y++)
                    if (y >= 0 && y < size)
                        colors[y * size + x] = new Color(1f, 0.6f, 0f, 1f);
            }

            texture.SetPixels(colors);
            texture.Apply();
            texture.filterMode = FilterMode.Point;

            var torchMat = PrefabManager.Instance.GetPrefab("Torch")
                .GetComponentInChildren<MeshRenderer>().material;
            var mat = new Material(torchMat)
            {
                mainTexture = texture,
                color       = Color.white
            };
            return mat;
        }

        private Material CreatePostMaterial()
        {
            var texture = new Texture2D(4, 4);
            var colors  = new Color[16];
            for (var i = 0; i < 16; i++)
                colors[i] = new Color(0.9f, 0.85f, 0.1f); // bright yellow
            texture.SetPixels(colors);
            texture.Apply();

            var torchMat = PrefabManager.Instance.GetPrefab("Torch")
                .GetComponentInChildren<MeshRenderer>().material;
            return new Material(torchMat) { mainTexture = texture, color = Color.white };
        }

        private Material CreateBannerMaterial()
        {
            const int w = 64, h = 16;
            var texture = new Texture2D(w, h);
            var colors  = new Color[w * h];

            // Alternating red/white stripes
            for (var x = 0; x < w; x++)
            for (var y = 0; y < h; y++)
                colors[y * w + x] = (x / 8 % 2 == 0) ? Color.red : Color.white;

            texture.SetPixels(colors);
            texture.Apply();
            texture.filterMode = FilterMode.Point;

            var torchMat = PrefabManager.Instance.GetPrefab("Torch")
                .GetComponentInChildren<MeshRenderer>().material;
            return new Material(torchMat) { mainTexture = texture, color = Color.white };
        }
    }
}