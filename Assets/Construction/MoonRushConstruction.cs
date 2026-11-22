using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.UI;

public sealed class MoonRushConstruction : MonoBehaviour
{
    [SerializeField] Camera mapCamera;
    [SerializeField] Terrain terrain;
    [SerializeField] Font font;
    [SerializeField] Texture2D hammerIcon;
    [SerializeField] Material previewMaterial;
    [SerializeField] BuildingDefinition[] buildings;
    [SerializeField] Vector2 zoneHalfSize = new Vector2(175f, 122f);

    static readonly Color Panel = new Color32(16, 24, 35, 249);
    static readonly Color Card = new Color32(24, 35, 48, 255);
    static readonly Color Accent = new Color32(104, 232, 213, 255);
    static readonly Color Light = new Color32(232, 242, 247, 255);
    static readonly Color Muted = new Color32(161, 181, 195, 255);
    static readonly Color Invalid = new Color(.96f, .26f, .26f, .58f);
    static readonly Color Valid = new Color(.16f, .86f, .57f, .52f);

    readonly List<PlacedBuilding> placed = new List<PlacedBuilding>();
    readonly List<RaycastResult> uiHits = new List<RaycastResult>();
    readonly Dictionary<BuildingDefinition, Button> selectionButtons = new Dictionary<BuildingDefinition, Button>();
    TerrainCollider surface;
    GraphicRaycaster raycaster;
    RectTransform panel;
    Text feedback;
    Material tint;
    BuildingDefinition selection;
    GameObject preview;

    struct PlacedBuilding
    {
        public BuildingDefinition Definition;
        public GameObject Instance;
    }

    void Awake()
    {
        if (mapCamera == null || terrain == null || font == null || previewMaterial == null ||
            !terrain.TryGetComponent(out surface)) return;
        tint = new Material(previewMaterial);
        CreateInterface();
    }

    void Update()
    {
        if (selection == null) return;
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
        preview.transform.position = origin;
        preview.SetActive(true);
        tint.SetColor("_Tint", valid ? Valid : Invalid);
        feedback.text = valid ? "Левый клик — установить    ·    Esc — отмена" : reason + "    ·    Esc — отмена";
        if (valid && Input.GetMouseButtonDown(0))
        {
            GameObject instance = Instantiate(selection.prefab, origin, Quaternion.identity, transform);
            instance.name = selection.title;
            placed.Add(new PlacedBuilding { Definition = selection, Instance = instance });
            RefreshButtons();
            Cancel();
        }
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
        bool slope = false;
        // Evaluate both the perimeter and the middle of the footprint so crater walls
        // and bowls cannot pass just because the center of the pad would be level.
        for (int ring = 0; ring <= 3; ring++)
        {
            int count = ring == 0 ? 1 : 24;
            for (int index = 0; index < count; index++)
            {
                float angle = index * Mathf.PI * 2f / count;
                float distance = radius * ring / 3f;
                float x = hit.x + Mathf.Cos(angle) * distance;
                float z = hit.z + Mathf.Sin(angle) * distance;
                Vector2 zone = new Vector2((x - center.x) / zoneHalfSize.x, (z - center.z) / zoneHalfSize.y);
                float u = (x - terrainOrigin.x) / data.size.x;
                float v = (z - terrainOrigin.z) / data.size.z;
                if (zone.sqrMagnitude > 1f || u < 0f || u > 1f || v < 0f || v > 1f)
                {
                    reason = "За пределами строительной зоны";
                    return false;
                }

                float elevation = terrainOrigin.y + data.GetInterpolatedHeight(u, v);
                high = Mathf.Max(high, elevation);
                low = Mathf.Min(low, elevation);
                slope |= data.GetSteepness(u, v) > definition.maximumSlope;
            }
        }
        origin.y = high;
        if (slope || high - low > definition.maximumRelief)
        {
            reason = "Необходима более ровная поверхность";
            return false;
        }

        foreach (PlacedBuilding item in placed)
        {
            if (item.Instance == null) continue;
            Vector3 delta = item.Instance.transform.position - origin;
            float clearance = radius + item.Definition.footprintRadius;
            if (delta.x * delta.x + delta.z * delta.z >= clearance * clearance) continue;
            reason = "Площадка пересекается с другим сооружением";
            return false;
        }
        reason = string.Empty;
        return true;
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
        selection = definition;
        panel.gameObject.SetActive(false);
        preview = Instantiate(definition.prefab, transform);
        preview.name = "Превью строительства";
        foreach (Collider collider in preview.GetComponentsInChildren<Collider>())
            collider.enabled = false;
        foreach (Renderer renderer in preview.GetComponentsInChildren<Renderer>())
        {
            Material[] materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++) materials[i] = tint;
            renderer.sharedMaterials = materials;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }
        preview.SetActive(false);
        feedback.transform.parent.gameObject.SetActive(true);
    }

    void Cancel()
    {
        selection = null;
        if (preview != null)
        {
            preview.SetActive(false);
            Destroy(preview);
            preview = null;
        }
        feedback.transform.parent.gameObject.SetActive(false);
    }

    bool Available(BuildingDefinition definition)
    {
        int count = 0;
        foreach (PlacedBuilding item in placed)
            if (item.Definition == definition && item.Instance != null) count++;
        return count < definition.maximumCount;
    }

    void RefreshButtons()
    {
        foreach (var entry in selectionButtons)
        {
            bool available = Available(entry.Key);
            entry.Value.interactable = available;
            entry.Value.GetComponentInChildren<Text>().text = available ? "Выбрать" : "Установлено";
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
        Canvas layer = canvas.GetComponent<Canvas>();
        layer.renderMode = RenderMode.ScreenSpaceOverlay;
        layer.sortingOrder = 20;
        var scaler = canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1440f, 900f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        raycaster = canvas.GetComponent<GraphicRaycaster>();

        RectTransform toggle = Rect("Open construction", canvas.transform, new Vector2(1f, 0f), new Vector2(1f, 0f),
            new Vector2(-30f, 28f), new Vector2(216f, 56f));
        Button toggleButton = MakeButton(toggle, Accent, () => panel.gameObject.SetActive(!panel.gameObject.activeSelf));
        toggleButton.targetGraphic.color = Accent;
        var icon = Picture(Rect("Hammer", toggle, new Vector2(0f, .5f), new Vector2(0f, .5f),
            new Vector2(15f, 0f), new Vector2(28f, 28f)), hammerIcon);
        icon.color = Panel;
        Label(Rect("Title", toggle, new Vector2(0f, .5f), new Vector2(0f, .5f),
            new Vector2(51f, 0f), new Vector2(155f, 32f)), "Строительство", 20, Panel, FontStyle.Bold);

        panel = Rect("Construction panel", canvas.transform, new Vector2(1f, 0f), new Vector2(1f, 0f),
            new Vector2(-30f, 100f), new Vector2(400f, 349f));
        Fill(panel, Panel);
        Fill(Rect("Accent", panel, new Vector2(0f, 1f), new Vector2(0f, 1f),
            Vector2.zero, new Vector2(400f, 3f)) , Accent).raycastTarget = false;
        Label(Rect("Heading", panel, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(20f, -14f), new Vector2(300f, 31f)), "Строительство", 24, Light, FontStyle.Bold);
        Button close = MakeButton(Rect("Close", panel, Vector2.one, Vector2.one,
            new Vector2(-15f, -13f), new Vector2(32f, 30f)), Card, () => panel.gameObject.SetActive(false));
        Text closeText = Label(Rect("X", close.transform, new Vector2(.5f, .5f), new Vector2(.5f, .5f),
            Vector2.zero, new Vector2(30f, 28f)), "×", 23, Light);
        closeText.alignment = TextAnchor.MiddleCenter;

        RectTransform viewport = Rect("Buildings", panel, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(16f, -59f), new Vector2(368f, 271f));
        Fill(viewport, Panel);
        viewport.gameObject.AddComponent<RectMask2D>();
        ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        RectTransform content = Rect("Catalog", viewport, new Vector2(0f, 1f), new Vector2(0f, 1f),
            Vector2.zero, new Vector2(368f, 271f));
        scroll.content = content;
        int row = 0;
        foreach (BuildingDefinition definition in buildings)
        {
            if (definition == null || definition.prefab == null || selectionButtons.ContainsKey(definition)) continue;
            CreateCard(content, definition, row++);
        }
        content.sizeDelta = new Vector2(368f, Mathf.Max(271f, 272f * row - 8f));
        panel.gameObject.SetActive(false);

        RectTransform help = Rect("Placement hint", canvas.transform, new Vector2(.5f, 0f), new Vector2(.5f, 0f),
            new Vector2(0f, 104f), new Vector2(555f, 41f));
        Fill(help, Panel);
        feedback = Label(Rect("Placement message", help, new Vector2(.5f, .5f), new Vector2(.5f, .5f),
            Vector2.zero, new Vector2(530f, 32f)), "", 16, Light);
        feedback.alignment = TextAnchor.MiddleCenter;
        help.gameObject.SetActive(false);
    }

    void CreateCard(RectTransform content, BuildingDefinition definition, int row)
    {
        RectTransform card = Rect(definition.title, content, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(0f, -272f * row), new Vector2(368f, 264f));
        Fill(card, Card);
        Picture(Rect("Preview image", card, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(10f, -10f), new Vector2(348f, 118f)), definition.thumbnail);
        Label(Rect("Building title", card, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(13f, -138f), new Vector2(340f, 24f)), definition.title, 19, Light, FontStyle.Bold);
        Text description = Label(Rect("Description", card, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(13f, -166f), new Vector2(342f, 47f)), definition.description, 14, Muted);
        description.alignment = TextAnchor.UpperLeft;
        Button choose = MakeButton(Rect("Choose", card, new Vector2(1f, 0f), new Vector2(1f, 0f),
            new Vector2(-12f, 11f), new Vector2(127f, 35f)), Accent, () => Select(definition));
        Text text = Label(Rect("Choose label", choose.transform, new Vector2(.5f, .5f), new Vector2(.5f, .5f),
            Vector2.zero, new Vector2(120f, 31f)), "Выбрать", 15, Panel, FontStyle.Bold);
        text.alignment = TextAnchor.MiddleCenter;
        selectionButtons.Add(definition, choose);
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

    Text Label(RectTransform rect, string content, int size, Color color, FontStyle style = FontStyle.Normal)
    {
        var text = rect.gameObject.AddComponent<Text>();
        text.font = font;
        text.text = content;
        text.fontSize = size;
        text.fontStyle = style;
        text.color = color;
        text.alignment = TextAnchor.MiddleLeft;
        text.raycastTarget = false;
        return text;
    }

    void OnDestroy()
    {
        if (tint != null) Destroy(tint);
    }
}
