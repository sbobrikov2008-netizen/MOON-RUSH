using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

public sealed class MoonRushConstruction : MonoBehaviour
{
    [SerializeField] Camera mapCamera;
    [SerializeField] Terrain terrain;
    [SerializeField] Font font;
    [SerializeField] Shader fontShader;
    [SerializeField] Texture2D hammerIcon;
    [SerializeField] Material previewMaterial;
    [SerializeField] BuildingDefinition[] buildings;
    [SerializeField, Range(.1f, .45f)] float placementExtent = .40f;

    static readonly Color Panel = new Color32(16, 24, 35, 249);
    static readonly Color Card = new Color32(24, 35, 48, 255);
    static readonly Color Accent = new Color32(104, 232, 213, 255);
    static readonly Color Light = new Color32(232, 242, 247, 255);
    static readonly Color Muted = new Color32(161, 181, 195, 255);
    static readonly Color Invalid = new Color(.96f, .26f, .26f, .58f);
    static readonly Color Valid = new Color(.16f, .86f, .57f, .52f);
    const float InspectionWidth = 428f;
    const float InspectionHeight = 476f;

    readonly List<BuildingInstance> placed = new List<BuildingInstance>();
    readonly List<RaycastResult> uiHits = new List<RaycastResult>();
    readonly Dictionary<BuildingDefinition, Button> selectionButtons = new Dictionary<BuildingDefinition, Button>();
    readonly Dictionary<BuildingDefinition, Texture> catalogRenders = new Dictionary<BuildingDefinition, Texture>();
    readonly Dictionary<BuildingInstance, Texture> inspectionImages = new Dictionary<BuildingInstance, Texture>();
    TerrainCollider surface;
    GraphicRaycaster raycaster;
    Canvas hudCanvas;
    RectTransform panel;
    RectTransform inspectionPanel;
    ScrollRect inspectionScroll;
    TextMeshProUGUI inspectionTitle;
    TextMeshProUGUI inspectionDescription;
    TextMeshProUGUI inspectionCharacteristics;
    RawImage inspectionImage;
    BuildingInstance inspectedBuilding;
    LineRenderer inspectionOutline;
    Material inspectionTint;
    int thumbnailStageIndex;
    TextMeshProUGUI feedback;
    TMP_FontAsset uiFont;
    readonly List<Material> ghostMaterials = new List<Material>();
    readonly List<RenderTexture> thumbnails = new List<RenderTexture>();
    Material tint;
    BuildingDefinition selection;
    GameObject preview;
    LineRenderer previewOutline;
    Quaternion placementFacing;

    public int TotalHousingCapacity
    {
        get
        {
            int total = 0;
            foreach (BuildingInstance building in placed)
                if (building != null && building.Status == BuildingStatus.Built) total += building.Capacity;
            return total;
        }
    }

    void Awake()
    {
        if (mapCamera == null || terrain == null || font == null || fontShader == null || previewMaterial == null ||
            !terrain.TryGetComponent(out surface)) return;
        uiFont = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024);
        if (uiFont == null) return;
        tint = new Material(previewMaterial);
        CreateInterface();
    }

    void Update()
    {
        if (raycaster == null) return;
        if (inspectionPanel.gameObject.activeSelf) UpdateInspectionScale();
        if (selection == null)
        {
            if (Input.GetMouseButtonDown(0)) TryInspectBuilding();
            return;
        }
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cancel();
            return;
        }

        Vector3 pointer = Input.mousePosition;
        Rect viewport = mapCamera.pixelRect;
        if (!viewport.Contains(pointer) || IsOverInterface(pointer) ||
            !surface.Raycast(mapCamera.ScreenPointToRay(pointer), out RaycastHit hit, mapCamera.farClipPlane))
        {
            preview.SetActive(false);
            feedback.text = "Выберите место на лунной поверхности   ·   Esc — отмена";
            return;
        }

        bool valid = CanPlace(selection, hit.point, out Vector3 origin, out string reason);
        if (preview.TryGetComponent(out LunarLandingPad previewPad))
        {
            if (!previewPad.FitRamp(terrain, origin, placementFacing, false) && valid)
            {
                valid = false;
                reason = "Спуску мешает уклон или неровность грунта";
            }
            Rect ramp = LunarLandingPad.RampFootprint;
            Vector2 toes = previewPad.RampToeHeights;
            previewOutline.SetPosition(0, new Vector3(ramp.xMin, toes.x + .16f, ramp.yMax));
            previewOutline.SetPosition(1, new Vector3(ramp.xMax, toes.y + .16f, ramp.yMax));
        }
        preview.transform.position = origin;
        preview.SetActive(true);
        Color perimeter = valid ? Valid : Invalid;
        perimeter.a = .95f;
        tint.SetColor("_Tint", perimeter);
        feedback.text = valid ? "Левый клик — установить    ·    Esc — отмена" : reason + "    ·    Esc — отмена";
        if (valid && Input.GetMouseButtonDown(0))
        {
            GameObject instance = Instantiate(selection.prefab, origin, placementFacing, transform);
            if (instance.TryGetComponent(out LunarLandingPad pad))
                pad.FitRamp(terrain, origin, placementFacing, true);
            instance.name = selection.title;
            var state = instance.AddComponent<BuildingInstance>();
            state.Initialize(selection, BuildingStatus.Built);
            placed.Add(state);
            RefreshButtons();
            Cancel();
        }
    }

    void TryInspectBuilding()
    {
        Vector3 pointer = Input.mousePosition;
        if (!mapCamera.pixelRect.Contains(pointer) || IsOverInterface(pointer)) return;
        Ray ray = mapCamera.ScreenPointToRay(pointer);
        BuildingInstance building = Physics.Raycast(ray, out RaycastHit hit, mapCamera.farClipPlane,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
            ? hit.collider.GetComponentInParent<BuildingInstance>() : null;
        if (building == null || building.Status != BuildingStatus.Built)
        {
            CloseInspection();
            return;
        }
        if (building == inspectedBuilding) return;
        inspectedBuilding = building;
        panel.gameObject.SetActive(false);
        BuildingDefinition definition = building.Definition;
        inspectionTitle.text = definition.title?.Trim() ?? string.Empty;
        inspectionDescription.text = definition.inspectionDescription?.Trim() ?? string.Empty;
        inspectionCharacteristics.text = definition.characteristics?.Trim() ?? string.Empty;
        RefreshInspectionThumbnail();
        if (!inspectionImages.ContainsKey(building) && building.TryGetComponent<LunarLandingPad>(out _))
            StartCoroutine(RenderThumbnail(definition, shot =>
            {
                inspectionImages[building] = shot;
                if (inspectedBuilding == building) RefreshInspectionThumbnail();
            }, building));

        Vector3[] perimeter = GetPerimeter(definition);
        if (building.TryGetComponent(out LunarLandingPad pad))
        {
            Vector2 toes = pad.RampToeHeights;
            perimeter[0].y = toes.x + .16f;
            perimeter[1].y = toes.y + .16f;
        }
        inspectionOutline.transform.SetPositionAndRotation(building.transform.position, building.transform.rotation);
        inspectionOutline.positionCount = perimeter.Length;
        inspectionOutline.SetPositions(perimeter);
        inspectionOutline.gameObject.SetActive(true);
        inspectionPanel.gameObject.SetActive(true);
        UpdateInspectionScale();
        LayoutRebuilder.ForceRebuildLayoutImmediate(inspectionScroll.content);
        inspectionScroll.StopMovement();
        inspectionScroll.verticalNormalizedPosition = 1f;
    }

    void RefreshInspectionThumbnail()
    {
        if (inspectedBuilding == null) return;
        BuildingDefinition definition = inspectedBuilding.Definition;
        if (!inspectionImages.TryGetValue(inspectedBuilding, out Texture image) &&
            !catalogRenders.TryGetValue(definition, out image))
            image = definition.thumbnail;
        ShowThumbnail(inspectionImage, image);
    }

    void CloseInspection()
    {
        inspectedBuilding = null;
        inspectionPanel.gameObject.SetActive(false);
        inspectionOutline.gameObject.SetActive(false);
    }

    bool CanPlace(BuildingDefinition definition, Vector3 hit, out Vector3 origin, out string reason)
    {
        origin = hit;
        TerrainData data = terrain.terrainData;
        Vector3 terrainOrigin = terrain.transform.position;
        Vector3 center = terrainOrigin + data.size * .5f;
        float radius = definition.footprintRadius;
        float high = hit.y;
        float low = hit.y;
        Rect ramp = Extension(definition);
        if (ramp.width > 0f)
        {
            for (int corner = 0; corner < 4; corner++)
            {
                Vector3 point = hit + placementFacing * new Vector3(
                    (corner & 1) == 0 ? ramp.xMin : ramp.xMax, 0f,
                    (corner & 2) == 0 ? ramp.yMin : ramp.yMax);
                if (InsideZone(point.x, point.z)) continue;
                reason = "Спуск выходит за пределы строительной зоны";
                return false;
            }
        }

        int slopedSamples = 0;
        int samples = 0;
        bool steepDrop = false;
        // Evaluate both the perimeter and the middle of the footprint so crater walls
        // and bowls cannot pass just because the center of the pad would be level.
        for (int ring = 0; ring <= 5; ring++)
        {
            int count = ring == 0 ? 1 : 24;
            for (int index = 0; index < count; index++)
            {
                float angle = index * Mathf.PI * 2f / count;
                float distance = radius * ring / 5f;
                float x = hit.x + Mathf.Cos(angle) * distance;
                float z = hit.z + Mathf.Sin(angle) * distance;
                float u = (x - terrainOrigin.x) / data.size.x;
                float v = (z - terrainOrigin.z) / data.size.z;
                if (!InsideZone(x, z))
                {
                    reason = "За пределами строительной зоны";
                    return false;
                }

                float elevation = terrainOrigin.y + data.GetInterpolatedHeight(u, v);
                high = Mathf.Max(high, elevation);
                low = Mathf.Min(low, elevation);
                float steepness = data.GetSteepness(u, v);
                if (steepness > definition.maximumSlope) slopedSamples++;
                steepDrop |= steepness > definition.maximumSlope + 8f;
                samples++;
            }
        }
        origin.y = high;
        if (steepDrop || slopedSamples * 4 > samples)
        {
            reason = "Слишком крутой склон под основанием";
            return false;
        }
        if (high - low > definition.maximumRelief)
        {
            reason = "Слишком большой перепад высот под основанием";
            return false;
        }

        foreach (BuildingInstance item in placed)
        {
            if (item == null) continue;
            Vector3 delta = item.transform.position - origin;
            if (!Overlap(definition, placementFacing, item.Definition, item.transform.rotation, delta)) continue;
            reason = "Площадка пересекается с другим сооружением";
            return false;
        }
        reason = string.Empty;
        return true;

        bool InsideZone(float x, float z)
        {
            float halfSize = Mathf.Min(data.size.x, data.size.z) * placementExtent;
            return Mathf.Abs(x - center.x) <= halfSize && Mathf.Abs(z - center.z) <= halfSize &&
                x >= terrainOrigin.x && x <= terrainOrigin.x + data.size.x &&
                z >= terrainOrigin.z && z <= terrainOrigin.z + data.size.z;
        }
    }

    static Rect Extension(BuildingDefinition definition) =>
        definition.prefab.GetComponent<LunarLandingPad>() != null ? LunarLandingPad.RampFootprint : default;

    static bool Overlap(BuildingDefinition first, Quaternion firstFacing, BuildingDefinition second,
        Quaternion secondFacing, Vector3 offset)
    {
        offset.y = 0f;
        float combined = first.footprintRadius + second.footprintRadius;
        if (offset.sqrMagnitude < combined * combined) return true;
        Rect a = Extension(first);
        Rect b = Extension(second);
        if (DiskIntersects(a, Quaternion.Inverse(firstFacing) * offset, second.footprintRadius) ||
            DiskIntersects(b, Quaternion.Inverse(secondFacing) * -offset, first.footprintRadius)) return true;
        if (a.width <= 0f || b.width <= 0f) return false;

        Vector3 ax = firstFacing * Vector3.right, az = firstFacing * Vector3.forward;
        Vector3 bx = secondFacing * Vector3.right, bz = secondFacing * Vector3.forward;
        Vector3 between = offset + b.center.x * bx + b.center.y * bz - a.center.x * ax - a.center.y * az;
        return !Separates(ax) && !Separates(az) && !Separates(bx) && !Separates(bz);

        bool Separates(Vector3 axis)
        {
            float span = (Mathf.Abs(Vector3.Dot(ax, axis)) * a.width + Mathf.Abs(Vector3.Dot(az, axis)) * a.height +
                Mathf.Abs(Vector3.Dot(bx, axis)) * b.width + Mathf.Abs(Vector3.Dot(bz, axis)) * b.height) * .5f;
            return Mathf.Abs(Vector3.Dot(between, axis)) >= span;
        }

        static bool DiskIntersects(Rect rect, Vector3 center, float radius)
        {
            if (rect.width <= 0f) return false;
            var nearest = new Vector2(Mathf.Clamp(center.x, rect.xMin, rect.xMax),
                Mathf.Clamp(center.z, rect.yMin, rect.yMax));
            return (nearest - new Vector2(center.x, center.z)).sqrMagnitude < radius * radius;
        }
    }

    bool IsOverInterface(Vector2 pointer)
    {
        if (EventSystem.current == null) return false;
        uiHits.Clear();
        raycaster.Raycast(new PointerEventData(EventSystem.current) { position = pointer }, uiHits);
        return uiHits.Count != 0;
    }

    void Select(BuildingDefinition definition)
    {
        if (!Available(definition)) return;
        Cancel();
        CloseInspection();
        selection = definition;
        panel.gameObject.SetActive(false);
        Vector3 towardPlayer = -mapCamera.transform.forward;
        towardPlayer.y = 0f;
        placementFacing = towardPlayer.sqrMagnitude > .001f ? Quaternion.LookRotation(towardPlayer) : Quaternion.identity;
        preview = Instantiate(definition.prefab, transform.position, placementFacing, transform);
        preview.name = "Превью строительства";
        preview.AddComponent<BuildingInstance>().Initialize(definition, BuildingStatus.Preview);
        foreach (Collider collider in preview.GetComponentsInChildren<Collider>())
            collider.enabled = false;
        foreach (Light light in preview.GetComponentsInChildren<Light>())
            light.enabled = false;
        foreach (Renderer renderer in preview.GetComponentsInChildren<Renderer>())
        {
            Material[] materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                // The cabin glass already has its own transparent pass.
                if (materials[i].GetTag("RenderType", false) == "Transparent") continue;
                var ghost = new Material(materials[i]);
                Color color = ghost.GetColor("_BaseColor");
                color.a = .48f;
                ghost.SetColor("_BaseColor", color);
                ghost.SetFloat("_Surface", 1f);
                ghost.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                ghost.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                ghost.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
                ghost.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
                ghost.SetFloat("_ZWrite", 1f);
                ghost.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                ghost.SetOverrideTag("RenderType", "Transparent");
                ghost.renderQueue = (int)RenderQueue.Transparent;
                materials[i] = ghost;
                ghostMaterials.Add(ghost);
            }
            renderer.sharedMaterials = materials;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }
        previewOutline = CreateOutline("Placement perimeter", preview.transform, tint);
        Vector3[] perimeter = GetPerimeter(definition);
        previewOutline.positionCount = perimeter.Length;
        previewOutline.SetPositions(perimeter);
        preview.SetActive(false);
        feedback.transform.parent.gameObject.SetActive(true);
    }

    static Vector3[] GetPerimeter(BuildingDefinition definition)
    {
        Rect ramp = Extension(definition);
        const int samples = 64;
        var perimeter = new List<Vector3>(samples + 4);
        float angleCut = ramp.width > 0f ? Mathf.Asin(ramp.xMax / definition.footprintRadius) : 0f;
        if (ramp.width > 0f)
        {
            perimeter.Add(new Vector3(ramp.xMin, .72f, ramp.yMax));
            perimeter.Add(new Vector3(ramp.xMax, .72f, ramp.yMax));
        }
        for (int i = 0; i <= samples; i++)
        {
            float angle = Mathf.Lerp(angleCut, Mathf.PI * 2f - angleCut, i / (float)samples);
            perimeter.Add(new Vector3(Mathf.Sin(angle) * definition.footprintRadius, .72f,
                Mathf.Cos(angle) * definition.footprintRadius));
        }
        return perimeter.ToArray();
    }

    static LineRenderer CreateOutline(string name, Transform parent, Material material)
    {
        var outline = new GameObject(name, typeof(LineRenderer));
        outline.transform.SetParent(parent, false);
        var line = outline.GetComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.loop = true;
        line.widthMultiplier = .18f;
        line.sharedMaterial = material;
        line.shadowCastingMode = ShadowCastingMode.Off;
        return line;
    }

    void Cancel()
    {
        selection = null;
        if (preview != null)
        {
            preview.SetActive(false);
            Destroy(preview);
            preview = null;
            previewOutline = null;
        }
        foreach (Material material in ghostMaterials) Destroy(material);
        ghostMaterials.Clear();
        feedback.transform.parent.gameObject.SetActive(false);
    }

    bool Available(BuildingDefinition definition)
    {
        if (definition.maximumCount == 0) return true;
        int count = 0;
        foreach (BuildingInstance item in placed)
            if (item != null && item.Definition == definition) count++;
        return count < definition.maximumCount;
    }

    void RefreshButtons()
    {
        foreach (var entry in selectionButtons)
        {
            bool available = Available(entry.Key);
            entry.Value.interactable = available;
            entry.Value.GetComponentInChildren<TextMeshProUGUI>().text = available ? "Выбрать" : "Установлено";
        }
    }

    void CreateInterface()
    {
        if (EventSystem.current == null && FindFirstObjectByType<EventSystem>() == null)
        {
            var events = new GameObject("Construction events", typeof(EventSystem), typeof(StandaloneInputModule));
            events.transform.SetParent(transform, false);
        }

        var canvas = new GameObject("Construction HUD", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvas.transform.SetParent(transform, false);
        hudCanvas = canvas.GetComponent<Canvas>();
        hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        hudCanvas.sortingOrder = 20;
        var scaler = canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1440f, 900f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        scaler.referencePixelsPerUnit = 100f;
        raycaster = canvas.GetComponent<GraphicRaycaster>();

        RectTransform toggle = Rect("Open construction", canvas.transform, new Vector2(1f, 0f), new Vector2(1f, 0f),
            new Vector2(-30f, 28f), new Vector2(216f, 56f));
        Button toggleButton = MakeButton(toggle, Accent, () =>
        {
            CloseInspection();
            panel.gameObject.SetActive(!panel.gameObject.activeSelf);
        });
        toggleButton.targetGraphic.color = Accent;
        var icon = Picture(Rect("Hammer", toggle, new Vector2(0f, .5f), new Vector2(0f, .5f),
            new Vector2(15f, 0f), new Vector2(28f, 28f)), hammerIcon);
        icon.color = Panel;
        Label(Rect("Title", toggle, new Vector2(0f, .5f), new Vector2(0f, .5f),
            new Vector2(51f, 0f), new Vector2(155f, 32f)), "Строительство", 20, Panel, FontStyles.Bold);

        panel = Rect("Construction panel", canvas.transform, new Vector2(1f, 0f), new Vector2(1f, 0f),
            new Vector2(-30f, 100f), new Vector2(428f, 435f));
        Fill(panel, Panel);
        Fill(Rect("Accent", panel, new Vector2(0f, 1f), new Vector2(0f, 1f),
            Vector2.zero, new Vector2(InspectionWidth, 3f)) , Accent).raycastTarget = false;
        Label(Rect("Heading", panel, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(20f, -14f), new Vector2(300f, 31f)), "Строительство", 24, Light, FontStyles.Bold);
        Button close = MakeButton(Rect("Close", panel, Vector2.one, Vector2.one,
            new Vector2(-15f, -13f), new Vector2(32f, 30f)), Card, () => panel.gameObject.SetActive(false));
        TextMeshProUGUI closeText = Label(Rect("X", close.transform, new Vector2(.5f, .5f), new Vector2(.5f, .5f),
            Vector2.zero, new Vector2(30f, 28f)), "×", 23, Light);
        closeText.alignment = TextAlignmentOptions.Center;

        RectTransform viewport = Rect("Buildings", panel, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(16f, -59f), new Vector2(396f, 361f));
        Fill(viewport, Panel);
        viewport.gameObject.AddComponent<RectMask2D>();
        ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.scrollSensitivity = 28f;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        RectTransform content = Rect("Catalog", viewport, new Vector2(0f, 1f), new Vector2(0f, 1f),
            Vector2.zero, new Vector2(396f, 361f));
        scroll.content = content;
        AddVerticalFlow(content, 0, 8f);
        int row = 0;
        foreach (BuildingDefinition definition in buildings)
        {
            if (definition == null || definition.prefab == null || selectionButtons.ContainsKey(definition)) continue;
            CreateCard(content, definition);
            row++;
        }
        if (row > 1) AddScrollbar(scroll, panel, -59f, 361f);
        panel.gameObject.SetActive(false);

        RectTransform help = Rect("Placement hint", canvas.transform, new Vector2(.5f, 0f), new Vector2(.5f, 0f),
            new Vector2(0f, 104f), new Vector2(555f, 41f));
        Fill(help, Panel);
        feedback = Label(Rect("Placement message", help, new Vector2(.5f, .5f), new Vector2(.5f, .5f),
            Vector2.zero, new Vector2(530f, 32f)), "", 16, Light);
        feedback.alignment = TextAlignmentOptions.Center;
        help.gameObject.SetActive(false);
        CreateInspectionPanel(canvas.transform);
    }

    void CreateInspectionPanel(Transform canvas)
    {
        Color cyan = new Color32(125, 202, 255, 255);
        inspectionTint = new Material(previewMaterial);
        inspectionTint.SetColor("_Tint", cyan);
        inspectionOutline = CreateOutline("Inspected building perimeter", transform, inspectionTint);
        inspectionOutline.widthMultiplier = .15f;

        const float viewportHeight = 432f;
        inspectionPanel = Rect("Building information", canvas, new Vector2(1f, 0f), new Vector2(1f, 0f),
            new Vector2(-30f, 100f), new Vector2(InspectionWidth, InspectionHeight));
        Fill(inspectionPanel, Panel);
        Vector2 leftTop = new Vector2(0f, 1f);
        Fill(Rect("Inspection accent", inspectionPanel, leftTop, leftTop,
            Vector2.zero, new Vector2(InspectionWidth, 3f)), cyan).raycastTarget = false;
        Button close = MakeButton(Rect("Close information", inspectionPanel, Vector2.one, Vector2.one,
            new Vector2(-10f, -12f), new Vector2(30f, 30f)), Card, CloseInspection);
        TextMeshProUGUI closeLabel = Label(Rect("Close label", close.transform, new Vector2(.5f, .5f),
            new Vector2(.5f, .5f), Vector2.zero, new Vector2(28f, 28f)), "×", 22, Light);
        closeLabel.alignment = TextAlignmentOptions.Center;

        RectTransform viewport = Rect("Information viewport", inspectionPanel, leftTop, leftTop,
            new Vector2(16f, -16f), new Vector2(368f, viewportHeight));
        Fill(viewport, Panel);
        viewport.gameObject.AddComponent<RectMask2D>();
        inspectionScroll = viewport.gameObject.AddComponent<ScrollRect>();
        inspectionScroll.viewport = viewport;
        inspectionScroll.horizontal = false;
        inspectionScroll.vertical = true;
        inspectionScroll.movementType = ScrollRect.MovementType.Clamped;
        inspectionScroll.scrollSensitivity = 27f;

        RectTransform content = Rect("Information flow", viewport, leftTop, leftTop, Vector2.zero, Vector2.zero);
        content.anchorMax = Vector2.one;
        inspectionScroll.content = content;
        AddVerticalFlow(content, 0, 12f);

        inspectionTitle = InspectionText("Building name", 24, Light, FontStyles.Bold);
        RectTransform frame = Rect("Image frame", content, leftTop, leftTop, Vector2.zero, Vector2.zero);
        frame.gameObject.AddComponent<LayoutElement>().preferredHeight = 184f;
        inspectionImage = Thumbnail(frame, null);
        inspectionDescription = InspectionText("Purpose", 17, Light);
        RectTransform divider = Rect("Characteristics divider", content, leftTop, leftTop, Vector2.zero, Vector2.zero);
        divider.gameObject.AddComponent<LayoutElement>().preferredHeight = 1f;
        Fill(divider, Card).raycastTarget = false;
        inspectionCharacteristics = InspectionText("Characteristics", 17, Muted);
        AddScrollbar(inspectionScroll, inspectionPanel, -56f, viewportHeight - 40f);
        CloseInspection();

        TextMeshProUGUI InspectionText(string name, int size, Color color, FontStyles style = FontStyles.Normal)
        {
            var text = Label(Rect(name, content, leftTop, leftTop, Vector2.zero, Vector2.zero), "", size, color, style);
            text.alignment = TextAlignmentOptions.TopLeft;
            WrapText(text);
            return text;
        }
    }

    void UpdateInspectionScale()
    {
        // Keep type usable in a narrow Game window while reserving space below the
        // panel for the construction button and above it for the margin.
        if (Screen.width < 1 || Screen.height < 1) return;
        float canvasScale = Mathf.Max(.01f, hudCanvas.scaleFactor);
        float widthFraction = Screen.width < Screen.height ? .90f : .52f;
        float widthLimit = Screen.width * widthFraction / InspectionWidth;
        float heightLimit = (Screen.height - 116f * canvasScale) / InspectionHeight;
        float physicalScale = Mathf.Min(Mathf.Max(.78f, canvasScale), Mathf.Min(widthLimit, heightLimit));
        float localScale = physicalScale / canvasScale;
        if (!Mathf.Approximately(inspectionPanel.localScale.x, localScale))
            inspectionPanel.localScale = Vector3.one * localScale;
    }

    static void AddScrollbar(ScrollRect scroll, Transform parent, float y, float height)
    {
        RectTransform track = Rect("Scrollbar", parent, Vector2.one, Vector2.one,
            new Vector2(-6f, y), new Vector2(7f, height));
        Fill(track, Card);
        var scrollbar = track.gameObject.AddComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        RectTransform thumb = Rect("Scrollbar thumb", track, Vector2.zero, Vector2.zero,
            Vector2.zero, Vector2.zero);
        scrollbar.handleRect = thumb;
        scrollbar.targetGraphic = Fill(thumb, Accent);
        scrollbar.value = 1f;
        scroll.verticalScrollbar = scrollbar;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
    }

    static void AddVerticalFlow(RectTransform root, int padding, float spacing, bool fitHeight = true)
    {
        var layout = root.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(padding, padding, padding, padding);
        layout.spacing = spacing;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        if (fitHeight) root.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    static void WrapText(TextMeshProUGUI text)
    {
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Overflow;
        text.margin = new Vector4(2f, 0f, 6f, 0f);
    }

    void CreateCard(RectTransform content, BuildingDefinition definition)
    {
        Vector2 top = new Vector2(0f, 1f);
        RectTransform card = Rect(definition.title, content, top, top, Vector2.zero, Vector2.zero);
        Fill(card, Card);
        AddVerticalFlow(card, 12, 9f, false);
        RectTransform frame = Rect("Preview image", card, top, top, Vector2.zero, Vector2.zero);
        frame.gameObject.AddComponent<LayoutElement>().preferredHeight = 186f;
        RawImage thumbnail = Thumbnail(frame, definition.thumbnail);
        StartCoroutine(RenderThumbnail(definition, shot =>
        {
            catalogRenders[definition] = shot;
            ShowThumbnail(thumbnail, shot);
            if (inspectedBuilding != null && inspectedBuilding.Definition == definition)
                RefreshInspectionThumbnail();
        }));
        WrapText(Label(Rect("Building title", card, top, top, Vector2.zero, Vector2.zero),
            definition.title, 21, Light, FontStyles.Bold));
        var description = Label(Rect("Description", card, top, top, Vector2.zero, Vector2.zero),
            definition.capacity > 0 ? definition.description + "\nВместимость: " + definition.capacity : definition.description,
            17, Muted);
        description.alignment = TextAlignmentOptions.TopLeft;
        WrapText(description);
        RectTransform actions = Rect("Actions", card, top, top, Vector2.zero, Vector2.zero);
        actions.gameObject.AddComponent<LayoutElement>().preferredHeight = 31f;
        var right = new Vector2(1f, 0f);
        Button choose = MakeButton(Rect("Choose", actions, right, right, Vector2.zero, new Vector2(127f, 31f)),
            Accent, () => Select(definition));
        TextMeshProUGUI text = Label(Rect("Choose label", choose.transform, new Vector2(.5f, .5f), new Vector2(.5f, .5f),
            Vector2.zero, new Vector2(120f, 31f)), "Выбрать", 15, Panel, FontStyles.Bold);
        text.alignment = TextAlignmentOptions.Center;
        selectionButtons.Add(definition, choose);
    }

    IEnumerator RenderThumbnail(BuildingDefinition definition, System.Action<Texture> complete, BuildingInstance source = null)
    {
        // Wait until the active URP renderer is initialized. The stage is outside the world
        // camera's far plane and contributes just one cached render per building in the catalog.
        yield return null;
        if (!ReferenceEquals(source, null) && (source == null || inspectedBuilding != source)) yield break;
        const int studioLayer = 29;
        // Destroy is deferred until the end of the frame. Separate simultaneous requests
        // spatially as well so a stage can never render another building on the same layer.
        Vector3 stage = new Vector3(thumbnailStageIndex++ * 512f, -24000f, 0f);
        GameObject studio = new GameObject("Building thumbnail studio");
        studio.transform.SetParent(transform, false);
        var model = Instantiate(definition.prefab, stage, Quaternion.identity, studio.transform);
        if (source != null && model.TryGetComponent(out LunarLandingPad pad))
            pad.FitRamp(terrain, source.transform.position, source.transform.rotation, false);
        foreach (Transform item in model.GetComponentsInChildren<Transform>()) item.gameObject.layer = studioLayer;
        foreach (Collider collider in model.GetComponentsInChildren<Collider>()) collider.enabled = false;

        var capture = new GameObject("Catalog camera", typeof(Camera)).GetComponent<Camera>();
        capture.transform.SetParent(studio.transform, false);
        capture.enabled = false;
        capture.cullingMask = 1 << studioLayer;
        capture.clearFlags = CameraClearFlags.SolidColor;
        capture.backgroundColor = new Color32(20, 35, 49, 255);
        capture.orthographic = true;
        capture.aspect = 2f;
        capture.nearClipPlane = .3f;
        capture.farClipPlane = 200f;
        var bounds = new Bounds(stage, Vector3.zero);
        foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>())
            bounds.Encapsulate(renderer.bounds);
        capture.transform.position = bounds.center + new Vector3(-1f, .86f, 1.32f) * definition.footprintRadius * 2.4f;
        capture.transform.LookAt(bounds.center);
        float halfFrame = 0f;
        for (int corner = 0; corner < 8; corner++)
        {
            Vector3 offset = Vector3.Scale(bounds.extents, new Vector3(
                (corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f));
            halfFrame = Mathf.Max(halfFrame, Mathf.Abs(Vector3.Dot(capture.transform.up, offset)),
                Mathf.Abs(Vector3.Dot(capture.transform.right, offset)) / capture.aspect);
        }
        capture.orthographicSize = halfFrame * 1.07f;
        capture.GetUniversalAdditionalCameraData().renderShadows = false;

        var key = new GameObject("Catalog key light", typeof(Light)).GetComponent<Light>();
        key.transform.SetParent(studio.transform, false);
        key.type = LightType.Point;
        key.transform.position = stage + new Vector3(-20f, 38f, -24f);
        key.cullingMask = 1 << studioLayer;
        key.color = new Color(1f, .95f, .86f);
        key.intensity = 650f;
        key.range = 120f;

        var shot = new RenderTexture(768, 384, 24, RenderTextureFormat.ARGB32)
        {
            name = definition.name + " catalog thumbnail",
            antiAliasing = 2
        };
        var request = new UniversalRenderPipeline.SingleCameraRequest { destination = shot };
        if (RenderPipeline.SupportsRenderRequest(capture, request) && shot.Create())
        {
            RenderPipeline.SubmitRenderRequest(capture, request);
            thumbnails.Add(shot);
            complete(shot);
        }
        else
        {
            Destroy(shot);
        }
        studio.SetActive(false);
        Destroy(studio);
    }

    static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 offset, Vector2 size)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = offset;
        rect.sizeDelta = size;
        return rect;
    }

    static Image Fill(RectTransform rect, Color color)
    {
        var image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        return image;
    }

    static RawImage Thumbnail(RectTransform frame, Texture texture)
    {
        Vector2 center = new Vector2(.5f, .5f);
        var image = Picture(Rect("Thumbnail", frame, center, center, Vector2.zero, Vector2.zero), null);
        image.gameObject.AddComponent<AspectRatioFitter>().aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        ShowThumbnail(image, texture);
        return image;
    }

    static void ShowThumbnail(RawImage image, Texture texture)
    {
        image.texture = texture;
        var fit = image.GetComponent<AspectRatioFitter>();
        fit.aspectRatio = texture != null ? texture.width / (float)texture.height : 2f;
    }

    static RawImage Picture(RectTransform rect, Texture2D texture)
    {
        var image = rect.gameObject.AddComponent<RawImage>();
        image.texture = texture;
        image.raycastTarget = false;
        return image;
    }

    static Button MakeButton(RectTransform rect, Color color, UnityEngine.Events.UnityAction action)
    {
        var image = Fill(rect, color);
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        ColorBlock states = button.colors;
        states.highlightedColor = new Color(.9f, .97f, 1f);
        states.pressedColor = new Color(.67f, .78f, .84f);
        states.disabledColor = new Color(.55f, .62f, .65f);
        button.colors = states;
        button.onClick.AddListener(action);
        return button;
    }

    TextMeshProUGUI Label(RectTransform rect, string content, int size, Color color, FontStyles style = FontStyles.Normal)
    {
        rect.gameObject.SetActive(false);
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = uiFont;
        text.text = content;
        text.fontSize = size;
        text.fontStyle = style;
        text.color = color;
        text.alignment = TextAlignmentOptions.Left;
        text.raycastTarget = false;
        rect.gameObject.SetActive(true);
        return text;
    }

    void OnDestroy()
    {
        if (tint != null) Destroy(tint);
        if (inspectionTint != null) Destroy(inspectionTint);
        foreach (Material material in ghostMaterials) Destroy(material);
        foreach (RenderTexture thumbnail in thumbnails)
        {
            thumbnail.Release();
            Destroy(thumbnail);
        }
        if (uiFont == null) return;
        foreach (Texture2D atlas in uiFont.atlasTextures)
            if (atlas != null) Destroy(atlas);
        Destroy(uiFont.material);
        Destroy(uiFont);
    }
}
