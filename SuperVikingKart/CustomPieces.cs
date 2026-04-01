using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SuperVikingKart;

internal static class KartPiece
{
    public static void CloneCart()
    {
        var cart = new CustomPiece(SuperVikingKart.KartPrefabName, "Cart", new PieceConfig
        {
            Name = "Super Viking Kart",
            Description = "Mountable cart. Get ready to race.",
            PieceTable = PieceTables.Hammer,
            Category = PieceCategories.Misc,
            Requirements = new[]
            {
                new RequirementConfig("Wood", 4)
            }
        });

        cart.Piece.m_canBeRemoved = true;
        PieceManager.Instance.AddPiece(cart);

        var tf = cart.PiecePrefab.transform;
        Object.DestroyImmediate(tf.Find("load").gameObject);

        var attach = new GameObject("AttachPointPlayer");
        attach.transform.SetParent(tf, false);
        attach.transform.SetAsFirstSibling();
        attach.transform.localPosition = Vector3.up * 0.5f;

        var container = tf.Find("Container").gameObject;
        Object.DestroyImmediate(container.GetComponent<Container>());

        var chair = container.AddComponent<SuperVikingKartComponent>();
        chair.AttachPoint = attach.transform;

        SuperVikingKart.DebugLog("SuperVikingKart prefab registered");
    }
}

internal static class BuffBlockPieces
{
    public static void CreateBuffBlock()
    {
        CreateBlock(SuperVikingKart.BuffBlockPrefabName, "Buff Block",
            "Drive through for a random buff!",
            CreateBuffBlockMaterial(), BlockType.Buff);

        SuperVikingKart.DebugLog("BuffBlock prefab registered");
    }

    public static void CreateDebuffBlock()
    {
        CreateBlock(SuperVikingKart.DebuffBlockPrefabName, "Debuff Block",
            "Drive through for a random debuff!",
            CreateDebuffBlockMaterial(), BlockType.Debuff);

        SuperVikingKart.DebugLog("DebuffBlock prefab registered");
    }

    public static void CreateMysteryBlock()
    {
        CreateBlock(SuperVikingKart.MysteryBlockPrefabName, "Mystery Block",
            "Drive through for a mystery effect!",
            CreateMysteryBlockMaterial(), BlockType.Mystery);

        SuperVikingKart.DebugLog("MysteryBlock prefab registered");
    }

    private static void CreateBlock(string prefabName, string displayName, string description, Material material,
        BlockType blockType)
    {
        var prefab = new GameObject(prefabName);
        prefab.layer = LayerMask.NameToLayer("piece_nonsolid");
        prefab.SetActive(false);

        var netView = prefab.AddComponent<ZNetView>();
        netView.m_persistent = true;
        netView.m_syncInitialScale = true;

        var piece = prefab.AddComponent<Piece>();
        piece.m_canBeRemoved = true;
        piece.m_craftingStation = PrefabManager.Cache.GetPrefab<CraftingStation>(CraftingStations.Workbench);

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

    private static Material CreateBuffBlockMaterial() =>
        CreateBlockMaterial(
            new Color(0.85f, 0.85f, 0f),
            new Color(0.6f, 0.5f, 0f),
            Color.black); //new Color(0.2f, 0.15f, 0f));

    private static Material CreateDebuffBlockMaterial() =>
        CreateBlockMaterial(
            new Color(0.8f, 0.2f, 0.2f),
            new Color(0.6f, 0.1f, 0.1f),
            Color.black); //new Color(0.3f, 0.05f, 0.05f));

    private static Material CreateMysteryBlockMaterial() =>
        CreateBlockMaterial(
            new Color(0.6f, 0.2f, 0.8f),
            new Color(0.4f, 0.1f, 0.5f),
            Color.black); //new Color(0.15f, 0.05f, 0.3f));

    private static Material CreateBlockMaterial(Color bgColor, Color borderColor, Color markColor)
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

        var shader = PrefabManager.Cache.GetPrefab<Shader>("Custom/Piece");
        var mat = new Material(shader)
        {
            mainTexture = texture,
            color = new Color(0.8f, 0.8f, 0.8f)
        };

        // Kill vertex noise to remove flickering
        mat.SetFloat("_RippleDistance", 0f);
        mat.SetFloat("_ValueNoise", 0f);
        mat.SetFloat("_ValueNoiseVertex", 0f);

        // Slight emission so it looks bright regardless of lighting angle
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", bgColor * 0.3f);

        return mat;
    }
}

internal static class RaceBoardPiece
{
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

    public static void CreateRaceBoard()
    {
        var prefab = new GameObject(SuperVikingKart.RaceBoardPrefabName);
        prefab.layer = LayerMask.NameToLayer("piece");
        prefab.SetActive(false);

        var netView = prefab.AddComponent<ZNetView>();
        netView.m_persistent = true;
        netView.m_syncInitialScale = true;

        var piece = prefab.AddComponent<Piece>();
        piece.m_canBeRemoved = true;
        piece.m_craftingStation = PrefabManager.Cache.GetPrefab<CraftingStation>(CraftingStations.Workbench);

        // BoardVisual
        var boardVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        boardVisual.name = "BoardVisual";
        boardVisual.transform.SetParent(prefab.transform, false);
        boardVisual.transform.localPosition = new Vector3(0f, 2.6f, 0f);
        boardVisual.transform.localScale = new Vector3(2.4f, 2.8f, 0.1f);
        Object.DestroyImmediate(boardVisual.GetComponent<BoxCollider>());
        boardVisual.GetComponent<MeshRenderer>().material = CreateRaceBoardMaterial();

        // StatusDisplay
        var displayGo = new GameObject("StatusDisplay");
        displayGo.transform.SetParent(prefab.transform, false);
        displayGo.transform.localRotation = Quaternion.identity;
        displayGo.transform.localScale = Vector3.one;

        var tmp = displayGo.AddComponent<TextMeshPro>();
        tmp.font = PrefabManager.Cache.GetPrefab<TMP_FontAsset>("Valheim-AveriaSansLibre");
        tmp.alignment = TextAlignmentOptions.TopLeft;
        tmp.fontSize = 1f;
        tmp.color = Color.white;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.overflowMode = TextOverflowModes.Overflow;

        tmp.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        tmp.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        tmp.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        tmp.rectTransform.sizeDelta = new Vector2(2.1f, 2.7f);
        tmp.rectTransform.anchoredPosition3D = new Vector3(0.05f, 2.4f, -0.06f);

        // Buttons
        var registerButtonGo = CreateButtonObject("RegisterButton", prefab.transform,
            new Vector3(-0.9f, 1.25f, -0.06f), "Register");
        var startButtonGo = CreateButtonObject("StartButton", prefab.transform,
            new Vector3(-0.3f, 1.25f, -0.06f), "Start");
        var resetButtonGo = CreateButtonObject("ResetButton", prefab.transform,
            new Vector3(0.3f, 1.25f, -0.06f), "Reset");
        var adminButtonGo = CreateButtonObject("AdminButton", prefab.transform,
            new Vector3(0.9f, 1.25f, -0.06f), "Admin");

        // Collider
        var rootCollider = prefab.AddComponent<BoxCollider>();
        rootCollider.center = new Vector3(0f, 2f, 0f);
        rootCollider.size = new Vector3(2.5f, 4f, 0.2f);

        // RaceBoardComponent
        var board = prefab.AddComponent<RaceBoardComponent>();
        board.StatusDisplay = tmp;
        board.RegisterButton = WireButton(registerButtonGo, RaceBoardButtonType.Register, board);
        board.StartButton = WireButton(startButtonGo, RaceBoardButtonType.Start, board);
        board.ResetButton = WireButton(resetButtonGo, RaceBoardButtonType.Reset, board);
        board.AdminButton = WireButton(adminButtonGo, RaceBoardButtonType.Admin, board);

        // Register
        var icon = RenderManager.Instance.Render(prefab, RenderManager.IsometricRotation);

        PieceManager.Instance.AddPiece(new CustomPiece(prefab, false, new PieceConfig
        {
            Name = "Race Board",
            Description = "Place to configure and manage a race. Shows you race statistics in real time.",
            PieceTable = PieceTables.Hammer,
            Category = PieceCategories.Misc,
            Icon = icon,
            Requirements = new[]
            {
                new RequirementConfig("Wood", 4),
                new RequirementConfig("Stone", 2)
            }
        }));

        prefab.SetActive(true);
        SuperVikingKart.DebugLog("RaceBoard prefab registered");
    }

    private static Material CreateRaceBoardMaterial()
    {
        var texture = new Texture2D(64, 64);
        var colors = new Color[64 * 64];
        var bg = new Color(0.15f, 0.1f, 0.05f);
        var border = new Color(0.6f, 0.4f, 0.1f);

        for (var i = 0; i < colors.Length; i++)
            colors[i] = bg;

        for (var x = 0; x < 64; x++)
        for (var y = 0; y < 64; y++)
            if (x < 3 || x >= 61 || y < 3 || y >= 61)
                colors[y * 64 + x] = border;

        texture.SetPixels(colors);
        texture.Apply();
        texture.filterMode = FilterMode.Point;

        var shader = PrefabManager.Cache.GetPrefab<Shader>("Custom/Piece");
        var mat = new Material(shader)
        {
            mainTexture = texture,
            color = Color.white
        };
        return mat;
    }

    private static GameObject CreateButtonObject(string name, Transform parent, Vector3 localPos, string label)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = Vector3.one;

        var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        visual.name = "Visual";
        visual.transform.SetParent(go.transform, false);
        visual.transform.localScale = new Vector3(0.58f, 0.18f, 0.06f);
        Object.DestroyImmediate(visual.GetComponent<BoxCollider>());
        visual.GetComponent<MeshRenderer>().material = CreateButtonMaterial();

        var collider = go.AddComponent<BoxCollider>();
        collider.size = new Vector3(0.58f, 0.18f, 0.12f);

        // Label parented to root (no inherited scale from visual cube)
        var labelGo = new GameObject("Label");
        labelGo.transform.SetParent(go.transform, false);
        labelGo.transform.localPosition = new Vector3(0f, 0f, -0.04f);
        labelGo.transform.localScale = Vector3.one;

        var labelTmp = labelGo.AddComponent<TMPro.TextMeshPro>();
        labelTmp.font = PrefabManager.Cache.GetPrefab<TMP_FontAsset>("Valheim-AveriaSansLibre");
        labelTmp.text = label;
        labelTmp.alignment = TMPro.TextAlignmentOptions.Center;
        labelTmp.fontSize = 1f;
        labelTmp.color = Color.white;
        labelTmp.textWrappingMode = TextWrappingModes.Normal;
        labelTmp.rectTransform.sizeDelta = new Vector2(0.54f, 0.16f);
        labelTmp.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        labelTmp.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        labelTmp.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        labelTmp.rectTransform.anchoredPosition = Vector2.zero;

        return go;
    }

    private static Material CreateButtonMaterial()
    {
        var texture = new Texture2D(32, 16);
        var colors = new Color[32 * 16];
        var bg = new Color(0.20f, 0.12f, 0.04f);
        var border = new Color(0.55f, 0.35f, 0.08f);

        for (var i = 0; i < colors.Length; i++)
            colors[i] = bg;

        for (var x = 0; x < 32; x++)
        for (var y = 0; y < 16; y++)
            if (x < 2 || x >= 30 || y < 2 || y >= 14)
                colors[y * 32 + x] = border;

        texture.SetPixels(colors);
        texture.Apply();
        texture.filterMode = FilterMode.Point;

        var shader = PrefabManager.Cache.GetPrefab<Shader>("Custom/Piece");
        var mat = new Material(shader)
        {
            mainTexture = texture,
            color = Color.white
        };
        return mat;
    }

    private static RaceBoardButton WireButton(GameObject go, RaceBoardButtonType type, RaceBoardComponent board)
    {
        var btn = go.AddComponent<RaceBoardButton>();
        btn.ButtonType = type;
        btn.Board = board;
        return btn;
    }
}

internal static class RaceLinePiece
{
    public static void CreateRaceLine()
    {
        // Root
        var prefab = new GameObject(SuperVikingKart.RaceLinePrefabName);
        prefab.layer = LayerMask.NameToLayer("piece");
        prefab.SetActive(false);

        var netView = prefab.AddComponent<ZNetView>();
        netView.m_persistent = true;
        netView.m_syncInitialScale = true;

        var piece = prefab.AddComponent<Piece>();
        piece.m_canBeRemoved = true;
        piece.m_craftingStation = PrefabManager.Cache.GetPrefab<CraftingStation>(CraftingStations.Workbench);

        // Place collider for placement
        var placeGo = new GameObject("PlaceCollider");
        placeGo.transform.SetParent(prefab.transform, false);
        var snapCol = placeGo.AddComponent<BoxCollider>();
        snapCol.center = new Vector3(3f, 0f, 0f);
        snapCol.size = new Vector3(6f, 0.001f, 0.001f);

        // Trigger collider
        var triggerGo = new GameObject("TriggerCollider");
        triggerGo.transform.SetParent(prefab.transform, false);
        triggerGo.transform.localPosition = new Vector3(0f, 0f, 0f);

        var triggerCol = triggerGo.AddComponent<BoxCollider>();
        triggerCol.isTrigger = true;
        triggerCol.size = new Vector3(6f, 6f, 1f); // wide, tall, thin

        var triggerRig = triggerGo.AddComponent<Rigidbody>();
        triggerRig.isKinematic = true;
        triggerRig.useGravity = false;

        // Gate posts
        CreatePost("PostLeft", prefab.transform, new Vector3(-3f, 1.5f, 0f));
        CreatePost("PostRight", prefab.transform, new Vector3(3f, 1.5f, 0f));

        // Ground quad (chequered)
        var groundQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        groundQuad.name = "GroundQuad";
        groundQuad.transform.SetParent(prefab.transform, false);
        groundQuad.transform.localPosition = new Vector3(0f, 0.02f, 0f);
        groundQuad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        groundQuad.transform.localScale = new Vector3(6f, 1f, 1f);
        Object.DestroyImmediate(groundQuad.GetComponent<MeshCollider>());
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
        arrowQuad.transform.localRotation = Quaternion.Euler(90f, 270f, 0f);
        arrowQuad.transform.localScale = new Vector3(1f, 1f, 1f);
        Object.DestroyImmediate(arrowQuad.GetComponent<MeshCollider>());
        arrowQuad.GetComponent<MeshRenderer>().material = CreateArrowMaterial();

        // World-space label
        var labelGo = new GameObject("Label");
        labelGo.transform.SetParent(prefab.transform, false);
        labelGo.transform.localPosition = Vector3.zero;
        labelGo.transform.localScale = Vector3.one;

        var tmp = labelGo.AddComponent<TextMeshPro>();
        tmp.font = PrefabManager.Cache.GetPrefab<TMP_FontAsset>("Valheim-Norse");
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontSize = 3f;
        tmp.color = Color.black;
        tmp.fontStyle = FontStyles.Bold;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.rectTransform.sizeDelta = new Vector2(6f, 1.5f);
        tmp.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        tmp.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        tmp.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        tmp.rectTransform.anchoredPosition3D = new Vector3(0f, 3f, -0.05f);

        // Banner between posts
        var banner = GameObject.CreatePrimitive(PrimitiveType.Cube);
        banner.name = "Banner";
        banner.transform.SetParent(prefab.transform, false);
        banner.transform.localPosition = new Vector3(0f, 3f, 0f);
        banner.transform.localScale = new Vector3(6f, 0.4f, 0.01f);
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
            Name = "Race Line",
            Description =
                "Start and/or finish line for a race. Place the arrow facing the direction of travel.",
            PieceTable = PieceTables.Hammer,
            Category = PieceCategories.Misc,
            Icon = icon,
            Requirements = new[]
            {
                new RequirementConfig("Wood", 2)
            }
        }));

        prefab.SetActive(true);
        SuperVikingKart.DebugLog("RaceLine prefab registered");
    }

    private static void CreatePost(string name, Transform parent, Vector3 localPos)
    {
        var post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        post.name = name;
        post.transform.SetParent(parent, false);
        post.transform.localPosition = localPos;
        post.transform.localScale = new Vector3(0.15f, 1.5f, 0.15f); // height = 3 units total
        //DestroyImmediate(post.GetComponent<CapsuleCollider>());
        post.GetComponent<MeshRenderer>().material = CreatePostMaterial();
    }

    private static Material CreateChequeredMaterial()
    {
        const int size = 64;
        var texture = new Texture2D(size, size);
        var colors = new Color[size * size];

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

        var shader = PrefabManager.Cache.GetPrefab<Shader>("Custom/Piece");
        var mat = new Material(shader)
        {
            mainTexture = texture,
            color = new Color(0.6f, 0.6f, 0.6f)
        };
        return mat;
    }

    private static Material CreateArrowMaterial()
    {
        const int size = 32;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var colors = new Color[size * size];

        // Fully transparent background
        for (var i = 0; i < colors.Length; i++)
            colors[i] = new Color(1f, 0.6f, 0f, 0f);

        // Arrow body — centred vertically (rows 12–19)
        for (var x = 2; x <= 17; x++)
        for (var y = 12; y <= 19; y++)
            colors[y * size + x] = new Color(1f, 0.6f, 0f, 1f);

        // Arrow head — tip at x=29, base at x=18, centre at y=15.5
        for (var x = 18; x <= 29; x++)
        {
            var halfWidth = 29 - x;
            var yMin = Mathf.RoundToInt(15.5f - halfWidth);
            var yMax = Mathf.RoundToInt(15.5f + halfWidth);
            for (var y = yMin; y <= yMax; y++)
                if (y >= 0 && y < size)
                    colors[y * size + x] = new Color(1f, 0.6f, 0f, 1f);
        }

        texture.SetPixels(colors);
        texture.Apply();
        texture.filterMode = FilterMode.Point;

        var shader = PrefabManager.Cache.GetPrefab<Shader>("Custom/Piece");
        var mat = new Material(shader)
        {
            mainTexture = texture,
            color = new Color(0.6f, 0.6f, 0.6f)
        };
        mat.SetFloat("_Cutoff", 0.1f);
        return mat;
    }

    private static Material CreatePostMaterial()
    {
        var texture = new Texture2D(4, 4);
        var colors = new Color[16];
        for (var i = 0; i < 16; i++)
            colors[i] = new Color(0.9f, 0.85f, 0.1f); // bright yellow
        texture.SetPixels(colors);
        texture.Apply();

        var shader = PrefabManager.Cache.GetPrefab<Shader>("Custom/Piece");
        var mat = new Material(shader)
        {
            mainTexture = texture,
            color = new Color(0.6f, 0.6f, 0.6f)
        };
        return mat;
    }

    private static Material CreateBannerMaterial()
    {
        const int w = 64, h = 16;
        var texture = new Texture2D(w, h);
        var colors = new Color[w * h];

        // Alternating red/white stripes
        for (var x = 0; x < w; x++)
        for (var y = 0; y < h; y++)
            colors[y * w + x] = (x / 8 % 2 == 0) ? Color.red : Color.white;

        texture.SetPixels(colors);
        texture.Apply();
        texture.filterMode = FilterMode.Point;

        var shader = PrefabManager.Cache.GetPrefab<Shader>("Custom/Piece");
        var mat = new Material(shader)
        {
            mainTexture = texture,
            color = new Color(0.6f, 0.6f, 0.6f)
        };
        return mat;
    }
}