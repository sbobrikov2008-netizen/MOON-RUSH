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
    [SerializeField, Min(10f)] float solarConnectionRadius = 120f;
    [SerializeField] BuildingDefinition[] buildings;

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
    readonly Dictionary<BuildingDefinition, (Button choose, Button image, Button title)> selectionButtons =
        new Dictionary<BuildingDefinition, (Button, Button, Button)>();
    readonly Dictionary<BuildingDefinition, Texture> catalogRenders = new Dictionary<BuildingDefinition, Texture>();
    readonly Dictionary<BuildingInstance, Texture> inspectionImages = new Dictionary<BuildingInstance, Texture>();
    readonly List<PressureCorridor> corridors = new List<PressureCorridor>();
    readonly HashSet<Transform> occupiedPorts = new HashSet<Transform>();
    readonly Dictionary<Transform, GameObject> portCaps = new Dictionary<Transform, GameObject>();
    PressureRoute.Anchor corridorFirst;
    PressureRoute.Anchor routeTarget;
    readonly Dictionary<Transform, (BuildingInstance building, Image marker)> portMarkers =
        new Dictionary<Transform, (BuildingInstance, Image)>();
    Vector3 routePointer;
    string routeFailure;
    Button removeBuildingButton;
    TextMeshProUGUI removeBuildingLabel;
    readonly Dictionary<BuildingInstance, SolarPowerCable> powerCables = new Dictionary<BuildingInstance, SolarPowerCable>();
    Vector3[] plannedSolarCable;
    LineRenderer solarZone;
    Material solarZoneMaterial;
    readonly List<PressureNetwork.Edge> networkEdges = new List<PressureNetwork.Edge>();
    Vector3[] proposedRoute;
    readonly List<PressureCorridor> previewCorridors = new List<PressureCorridor>();
    bool routePreviewDirty = true;
    int selectionFrame;
    TerrainFoundation previewFooting;
    Vector3 lastFootingPosition;
    Vector3 sampledHit, sampledOrigin;
    string sampledFailure;
    bool sampledPlacement, sampledValid;
    public IReadOnlyList<PressureCorridor> Connections => corridors;
    public IReadOnlyList<PressureNetwork.Edge> PressureGraph => networkEdges;
    public bool TryFindPressurePath(BuildingInstance from, BuildingInstance to, out Vector3[] waypoints) =>
        PressureNetwork.TryFindPath(from, to, networkEdges, out waypoints);
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
    Quaternion placementFacing, defaultPlacementFacing, lastFootingFacing;
    DockingInsertion plannedInsertion;
    PressureCorridor sampledCorridor;
    Button solarBreakerButton;
    TextMeshProUGUI solarBreakerLabel;
    float nextPowerCheck;

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
        if (Time.unscaledTime >= nextPowerCheck)
        {
            nextPowerCheck = Time.unscaledTime + .4f;
            RefreshPower();
        }
        if (inspectionPanel.gameObject.activeSelf) UpdateInspectionScale();
        if (selection == null)
        {
            if (Input.GetMouseButtonDown(0)) TryInspectBuilding();
            return;
        }
        if (Time.frameCount == selectionFrame) return;
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1))
        {
            Cancel();
            return;
        }

        if (selection.kind == BuildingKind.PressureCorridor)
        {
            UpdateCorridor();
            return;
        }

        Vector3 pointer = Input.mousePosition;
        Rect viewport = mapCamera.pixelRect;
        if (!viewport.Contains(pointer) || IsOverInterface(pointer) ||
            !surface.Raycast(mapCamera.ScreenPointToRay(pointer), out RaycastHit hit, mapCamera.farClipPlane))
        {
            preview.SetActive(false);
            feedback.text = "Выберите место на лунной поверхности   ·   Esc или ПКМ — отмена";
            return;
        }

        bool click = Input.GetMouseButtonDown(0);
        Vector3 placementPoint = hit.point;
        PressureCorridor insertionTarget = null;
        if (selection.kind == BuildingKind.DockingNode && Physics.Raycast(mapCamera.ScreenPointToRay(pointer),
            out RaycastHit corridorHit, mapCamera.farClipPlane, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            insertionTarget = corridorHit.collider.GetComponentInParent<PressureCorridor>();
            if (insertionTarget != null) placementPoint = corridorHit.point;
        }
        float sampleStep = selection.kind == BuildingKind.SolarStation ? .65f : .15f;
        if (!sampledPlacement || click || sampledCorridor != insertionTarget ||
            (sampledHit - placementPoint).sqrMagnitude > sampleStep * sampleStep)
        {
            plannedInsertion = null;
            placementFacing = defaultPlacementFacing;
            sampledOrigin = hit.point;
            if (insertionTarget == null)
                sampledValid = CanPlace(selection, hit.point, out sampledOrigin, out sampledFailure);
            else
            {
                sampledValid = DockingInsertion.TryPlan(terrain, selection, insertionTarget, placementPoint,
                    out DockingInsertion plan, out sampledFailure);
                if (sampledValid)
                {
                    placementFacing = plan.Facing;
                    sampledValid = CanPlace(selection, plan.Origin, out sampledOrigin, out sampledFailure, insertionTarget);
                    if (sampledValid) plannedInsertion = plan;
                }
            }
            sampledHit = placementPoint;
            sampledCorridor = insertionTarget;
            sampledPlacement = true;
        }
        bool valid = sampledValid;
        Vector3 origin = sampledOrigin;
        string reason = sampledFailure;
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
        preview.transform.SetPositionAndRotation(origin, placementFacing);
        preview.SetActive(true);
        if (previewFooting != null)
        {
            previewFooting.gameObject.SetActive(valid);
            if (valid && ((origin - lastFootingPosition).sqrMagnitude > .04f || lastFootingFacing != placementFacing))
            {
                previewFooting.Fit(terrain, selection.kind);
                lastFootingPosition = origin;
                lastFootingFacing = placementFacing;
            }
        }
        Color perimeter = valid ? Valid : Invalid;
        perimeter.a = .95f;
        tint.SetColor("_Tint", perimeter);
        feedback.text = valid
            ? (plannedInsertion == null ? "Левый клик — установить" : "Левый клик — вставить узел в коридор") +
                "    ·    Esc или ПКМ — отмена"
            : reason + "    ·    Esc или ПКМ — отмена";
        if (valid && click)
        {
            GameObject instance = Instantiate(selection.prefab, origin, placementFacing, transform);
            if (instance.TryGetComponent(out LunarLandingPad pad))
                pad.FitRamp(terrain, origin, placementFacing, true);
            if (selection.kind != BuildingKind.LandingPad)
                TerrainFoundation.Add(instance, instance.GetComponent<MeshRenderer>().sharedMaterials[2])
                    .Fit(terrain, selection.kind);
            instance.name = selection.title;
            var state = instance.AddComponent<BuildingInstance>();
            state.Initialize(selection, BuildingStatus.Built);
            placed.Add(state);
            AddPortCaps(state);
            if (selection.kind == BuildingKind.SolarStation)
                powerCables.Add(state, SolarPowerCable.Install(state, CommandPost(), plannedSolarCable));
            if (plannedInsertion != null) InsertDockingNode(state, plannedInsertion);
            RefreshPower();
            RefreshButtons();
            Cancel();
        }
    }

    void UpdateCorridor()
    {
        Vector3 pointer = Input.mousePosition;
        if (!mapCamera.pixelRect.Contains(pointer) || IsOverInterface(pointer))
        {
            ClearRoutePreview();
            routePreviewDirty = true;
            return;
        }
        bool click = Input.GetMouseButtonDown(0);
        bool hitWorld = Physics.Raycast(mapCamera.ScreenPointToRay(pointer), out RaycastHit hit,
            mapCamera.farClipPlane, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        BuildingInstance hovered = hitWorld ? hit.collider.GetComponentInParent<BuildingInstance>() : null;
        PressureRoute.Anchor target = PickPort(pointer);
        if (!corridorFirst.Exists)
        {
            feedback.text = "Выберите метку свободного шлюза · Esc или ПКМ — отмена";
            if (click && target.Exists) corridorFirst = target;
            else if (click && hovered == null) Cancel();
            return;
        }

        bool different = routeTarget.Port != target.Port;
        if (different || (click && target.Exists))
        {
            routeTarget = target;
            routePreviewDirty = true;
            proposedRoute = target.Exists
                ? PressureRoute.Find(terrain, corridorFirst, target, placed, corridors, out routeFailure)
                : null;
        }

        Vector3[] route = proposedRoute;
        if (route != null && routePreviewDirty)
        {
            ShowRoutePreview(route, true);
            routePreviewDirty = false;
        }
        if (route == null)
        {
            if (target.Exists || hitWorld)
            {
                Vector3 point = target.Exists ? target.Position : hit.point + Vector3.up * 2.35f;
                if (routePreviewDirty || previewCorridors.Count == 0 || (point - routePointer).sqrMagnitude > .64f)
                {
                    routePointer = point;
                    ClearRoutePreview();
                    if ((point - corridorFirst.Position).sqrMagnitude > 1f)
                        ShowRoutePreview(new[] { corridorFirst.Position, point }, false);
                    routePreviewDirty = false;
                }
            }
            else
            {
                ClearRoutePreview();
                routePreviewDirty = true;
            }
            feedback.text = target.Exists ? routeFailure :
                "Выберите второй шлюз. Для развилки установите соединительный узел.";
            return;
        }

        feedback.text = "Левый клик — построить переход · Esc или ПКМ — отмена";
        if (!click) return;

        ClearRoutePreview();
        InstallCorridor(selection, corridorFirst.Building, target.Building, corridorFirst.Port, target.Port, route);
        PressureNetwork.Rebuild(corridors, terrain, networkEdges);
        RefreshPower();
        Cancel();
    }

    void InstallCorridor(BuildingDefinition definition, BuildingInstance first, BuildingInstance second,
        Transform start, Transform end, Vector3[] route)
    {
        GameObject instance = Instantiate(definition.prefab, route[0], Quaternion.identity, transform);
        instance.name = definition.title;
        var corridor = instance.GetComponent<PressureCorridor>();
        corridor.Connect(first, second, route, start, end);
        var state = instance.AddComponent<BuildingInstance>();
        state.Initialize(definition, BuildingStatus.Built);
        corridors.Add(corridor);
        placed.Add(state);
        SetPortOccupied(start, true);
        SetPortOccupied(end, true);
    }

    void InsertDockingNode(BuildingInstance node, DockingInsertion plan)
    {
        PressureCorridor original = plan.Original;
        BuildingInstance old = original.GetComponent<BuildingInstance>();
        IReadOnlyList<Transform> ports = node.GetComponent<LunarBaseModule>().ConnectionPorts;
        InstallCorridor(old.Definition, original.First, node, original.StartPort, ports[2], plan.Before);
        InstallCorridor(old.Definition, node, original.Second, ports[0], original.EndPort, plan.After);
        corridors.Remove(original);
        placed.Remove(old);
        original.gameObject.SetActive(false);
        Destroy(original.gameObject);
        PressureNetwork.Rebuild(corridors, terrain, networkEdges);
    }

    void ShowPortMarkers()
    {
        foreach (BuildingInstance building in placed)
        {
            if (building == null || !building.TryGetComponent(out LunarBaseModule module)) continue;
            foreach (Transform port in module.ConnectionPorts)
            {
                if (port == null || occupiedPorts.Contains(port)) continue;
                var markerRect = Rect("Free pressure port", hudCanvas.transform, new Vector2(.5f, .5f),
                    new Vector2(.5f, .5f), Vector2.zero, new Vector2(13f, 13f));
                markerRect.localRotation = Quaternion.Euler(0f, 0f, 45f);
                var marker = Fill(markerRect, Accent);
                marker.raycastTarget = false;
                portMarkers.Add(port, (building, marker));
            }
        }
    }

    PressureRoute.Anchor PickPort(Vector2 pointer)
    {
        var canvasRect = (RectTransform)hudCanvas.transform;
        PressureRoute.Anchor selected = default;
        float nearest = 17f * 17f;
        foreach (var entry in portMarkers)
        {
            Vector3 screen = mapCamera.WorldToScreenPoint(entry.Key.position + Vector3.up * .48f);
            bool visible = screen.z > 0f && mapCamera.pixelRect.Contains(screen);
            entry.Value.marker.gameObject.SetActive(visible);
            if (!visible) continue;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out Vector2 position);
            entry.Value.marker.rectTransform.anchoredPosition = position;
            bool firstPort = entry.Key == corridorFirst.Port;
            entry.Value.marker.color = firstPort ? new Color(.99f, .70f, .30f) : Accent;
            float distance = (pointer - (Vector2)screen).sqrMagnitude;
            if (distance >= nearest || firstPort) continue;
            nearest = distance;
            selected = new PressureRoute.Anchor(entry.Value.building, entry.Key);
        }
        return selected;
    }

    void ShowRoutePreview(Vector3[] route, bool valid)
    {
        ClearRoutePreview();
        Color color = valid ? Valid : Invalid;
        color.a = .32f;
        tint.SetColor("_Tint", color);
        PressureCorridor ghost = CopyCorridor(route, corridorFirst.Port, routeTarget.Port, tint);
        if (!valid)
        {
            ghost.BeginGeometry();
            ghost.AddSpan(route[0], route[1], null, true, true);
            ghost.FinishGeometry();
            return;
        }

        PressureNetwork.Rebuild(previewCorridors, terrain, new List<PressureNetwork.Edge>());

        PressureCorridor CopyCorridor(IReadOnlyList<Vector3> path, Transform first, Transform last, Material material)
        {
            var copy = Instantiate(selection.prefab, path[0], Quaternion.identity, transform)
                .GetComponent<PressureCorridor>();
            copy.name = "Предварительный переход";
            copy.VisualOnly = true;
            copy.PreviewMaterial = material;
            var points = new Vector3[path.Count];
            for (int i = 0; i < path.Count; i++) points[i] = path[i];
            copy.Connect(null, null, points, first, last);
            previewCorridors.Add(copy);
            return copy;
        }
    }

    void ClearRoutePreview()
    {
        foreach (PressureCorridor corridor in previewCorridors)
        {
            corridor.gameObject.SetActive(false);
            Destroy(corridor.gameObject);
        }
        previewCorridors.Clear();
    }

    void SetPortOccupied(Transform port, bool used)
    {
        if (port == null) return;
        if (used) occupiedPorts.Add(port);
        else occupiedPorts.Remove(port);
        if (portCaps.TryGetValue(port, out GameObject cap)) cap.SetActive(!used);
    }

    void RemoveInspectedBuilding()
    {
        if (inspectedBuilding == null) return;
        if (inspectedBuilding.Definition.kind == BuildingKind.SolarStation)
        {
            BuildingInstance station = inspectedBuilding;
            CloseInspection();
            powerCables.Remove(station);
            placed.Remove(station);
            station.gameObject.SetActive(false);
            Destroy(station.gameObject);
            RefreshPower();
            RefreshButtons();
            return;
        }
        if (!inspectedBuilding.TryGetComponent(out PressureCorridor selected)) return;
        CloseInspection();
        SetPortOccupied(selected.StartPort, false);
        SetPortOccupied(selected.EndPort, false);
        corridors.Remove(selected);
        placed.Remove(selected.GetComponent<BuildingInstance>());
        selected.gameObject.SetActive(false);
        Destroy(selected.gameObject);
        PressureNetwork.Rebuild(corridors, terrain, networkEdges);
        RefreshPower();
    }

    void AddPortCaps(BuildingInstance building)
    {
        if (!building.TryGetComponent(out LunarBaseModule module)) return;
        foreach (Transform port in module.ConnectionPorts)
        {
            if (port == null) continue;
            GameObject cap = CreatePortCap(port, building.GetComponent<MeshRenderer>().sharedMaterials[1],
                building.Definition.kind == BuildingKind.DockingNode);
            portCaps.Add(port, cap);
        }
    }

    static GameObject CreatePortCap(Transform port, Material material, bool collidable)
    {
        var cap = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cap.name = "Sealed docking panel";
        if (!collidable)
        {
            cap.GetComponent<Collider>().enabled = false;
            Destroy(cap.GetComponent<Collider>());
        }
        cap.transform.SetParent(port, false);
        cap.transform.localPosition = new Vector3(0f, (PressureCorridor.Roof + PressureCorridor.Floor) * .5f, -.02f);
        cap.transform.localScale = new Vector3(PressureCorridor.HalfWidth * 2f,
            PressureCorridor.Roof - PressureCorridor.Floor, .09f);
        cap.GetComponent<Renderer>().sharedMaterial = material;
        return cap;
    }

    static void AddDisplayCaps(GameObject model)
    {
        Material material = model.GetComponent<MeshRenderer>().sharedMaterials[1];
        foreach (Transform port in model.GetComponent<LunarBaseModule>().ConnectionPorts)
            if (port != null) CreatePortCap(port, material, false);
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
        RefreshInspectionPower();
        inspectionCharacteristics.text = building.TryGetComponent(out PressureCorridor corridor)
            ? "Подключения:\n• " + (corridor.First != null ? corridor.First.Definition.title : "Узел сети переходов") +
                "\n• " + (corridor.Second != null ? corridor.Second.Definition.title : "Узел сети переходов")
            : definition.characteristics?.Trim() ?? string.Empty;
        bool solar = definition.kind == BuildingKind.SolarStation;
        removeBuildingButton.gameObject.SetActive(corridor != null || solar);
        removeBuildingLabel.text = solar ? "Удалить станцию" : "Удалить переход";
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
        if (corridor != null)
        {
            inspectionOutline.positionCount = corridor.Route.Count;
            inspectionOutline.loop = false;
            for (int i = 0; i < corridor.Route.Count; i++)
                inspectionOutline.SetPosition(i, corridor.Route[i] - building.transform.position + Vector3.up * 1.55f);
        }
        else inspectionOutline.loop = true;
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

    bool CanPlace(BuildingDefinition definition, Vector3 hit, out Vector3 origin, out string reason,
        PressureCorridor replacedCorridor = null)
    {
        BuildingInstance command = null;
        if (definition.kind == BuildingKind.SolarStation)
        {
            plannedSolarCable = null;
            command = CommandPost();
            origin = hit;
            if (command == null)
            {
                reason = "Сначала постройте командный пункт";
                return false;
            }
            Vector3 offset = hit - command.transform.position;
            if (offset.x * offset.x + offset.z * offset.z > solarConnectionRadius * solarConnectionRadius)
            {
                reason = "Слишком далеко от командного пункта";
                return false;
            }
        }
        float radius = definition.footprintRadius;
        Rect ramp = Extension(definition);
        if (!TerrainFoundation.Evaluate(terrain, definition, hit, placementFacing, ramp, out origin, out reason))
            return false;

        foreach (BuildingInstance item in placed)
        {
            if (item == null) continue;
            Vector3 delta = item.transform.position - origin;
            if (item.TryGetComponent(out PressureCorridor passage))
            {
                if (passage == replacedCorridor) continue;
                if (!passage.Occupies(origin, radius + 3.6f) &&
                    !RampCrossesCorridor(ramp, origin, passage)) continue;
            }
            else if (!Overlap(definition, placementFacing, item.Definition, item.transform.rotation, delta)) continue;
            reason = "Площадка пересекается с другим сооружением";
            return false;
        }
        foreach (SolarPowerCable cable in powerCables.Values)
        {
            if (cable == null) continue;
            if (!cable.Crosses(origin, radius) && !RampCrossesCable(ramp, origin, cable)) continue;
            reason = "Площадка пересекает электрический кабель";
            return false;
        }
        if (command != null)
        {
            plannedSolarCable = SolarPowerCable.Plan(terrain, origin, placementFacing, radius, command, placed, powerCables.Values);
            if (plannedSolarCable == null)
            {
                reason = "Нет свободного маршрута для электрического кабеля";
                return false;
            }
        }
        reason = string.Empty;
        return true;

    }

    void RefreshPower()
    {
        ColonyPowerNetwork.Recalculate(CommandPost(), placed, networkEdges, powerCables);
        RefreshInspectionPower();
    }

    void RefreshInspectionPower()
    {
        if (inspectedBuilding == null) return;
        string status = inspectedBuilding.PowerStatus switch
        {
            BuildingPowerStatus.Powered => "Электропитание подключено",
            BuildingPowerStatus.Unpowered => "Нет электропитания",
            _ => "Нет соединения с командным пунктом"
        };
        string description = inspectedBuilding.Definition.inspectionDescription?.Trim() ?? string.Empty;
        inspectionDescription.text = description + "\n\n" + status;
        bool solar = powerCables.TryGetValue(inspectedBuilding, out SolarPowerCable cable) && cable != null;
        solarBreakerButton.gameObject.SetActive(solar);
        if (solar) solarBreakerLabel.text = cable.IsBreakerOn ? "Отключить подачу" : "Включить подачу";
    }

    void ToggleSolarPower()
    {
        if (inspectedBuilding == null || !powerCables.TryGetValue(inspectedBuilding, out SolarPowerCable cable) ||
            cable == null) return;
        cable.ToggleBreaker();
        RefreshPower();
    }

    BuildingInstance CommandPost()
    {
        foreach (BuildingInstance building in placed)
            if (building != null && building.Definition.kind == BuildingKind.CommandPost) return building;
        return null;
    }

    void ShowSolarZone()
    {
        BuildingInstance command = CommandPost();
        if (command == null) return;
        if (solarZoneMaterial == null)
        {
            solarZoneMaterial = new Material(previewMaterial);
            solarZoneMaterial.SetColor("_Tint", new Color(.35f, .81f, .55f, .63f));
        }
        solarZone = CreateOutline("Solar connection zone", transform, solarZoneMaterial);
        solarZone.useWorldSpace = true;
        solarZone.widthMultiplier = .32f;
        const int samples = 200;
        var outline = new Vector3[samples];
        for (int i = 0; i < samples; i++)
        {
            float angle = i * Mathf.PI * 2f / samples;
            Vector3 point = command.transform.position +
                new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * solarConnectionRadius;
            point.y = terrain.transform.position.y + terrain.SampleHeight(point) + .18f;
            outline[i] = point;
        }
        solarZone.positionCount = samples;
        solarZone.SetPositions(outline);
    }

    bool RampCrossesCable(Rect ramp, Vector3 origin, SolarPowerCable cable)
    {
        if (ramp.width <= 0f) return false;
        for (float distance = ramp.yMin; distance <= ramp.yMax + .03f; distance += .60f)
        {
            Vector3 point = origin + placementFacing * new Vector3(0f, 0f, Mathf.Min(distance, ramp.yMax));
            if (cable.Crosses(point, ramp.width * .5f + .15f)) return true;
        }
        return false;
    }

    bool RampCrossesCorridor(Rect ramp, Vector3 origin, PressureCorridor passage)
    {
        if (ramp.width <= 0f) return false;
        for (float z = ramp.yMin; z <= ramp.yMax + .01f; z += 1f)
        {
            Vector3 point = origin + placementFacing * new Vector3(0f, 0f, Mathf.Min(z, ramp.yMax));
            if (passage.Occupies(point, ramp.width * .5f + 1.65f)) return true;
        }
        return false;
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
        selectionFrame = Time.frameCount;
        panel.gameObject.SetActive(false);
        if (definition.kind == BuildingKind.PressureCorridor)
        {
            ShowPortMarkers();
            feedback.transform.parent.gameObject.SetActive(true);
            return;
        }
        if (definition.kind == BuildingKind.SolarStation) ShowSolarZone();
        Vector3 towardPlayer = -mapCamera.transform.forward;
        towardPlayer.y = 0f;
        defaultPlacementFacing = towardPlayer.sqrMagnitude > .001f ? Quaternion.LookRotation(towardPlayer) : Quaternion.identity;
        placementFacing = defaultPlacementFacing;
        preview = Instantiate(definition.prefab, transform.position, placementFacing, transform);
        preview.name = "Превью строительства";
        preview.AddComponent<BuildingInstance>().Initialize(definition, BuildingStatus.Preview);
        if (definition.kind == BuildingKind.DockingNode) AddDisplayCaps(preview);
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
        if (definition.kind != BuildingKind.LandingPad)
        {
            previewFooting = TerrainFoundation.Add(preview, preview.GetComponent<MeshRenderer>().sharedMaterials[2]);
            lastFootingPosition = Vector3.one * float.PositiveInfinity;
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
        corridorFirst = default;
        routeTarget = default;
        foreach (var entry in portMarkers.Values)
        {
            entry.marker.gameObject.SetActive(false);
            Destroy(entry.marker.gameObject);
        }
        portMarkers.Clear();
        proposedRoute = null;
        previewFooting = null;
        sampledPlacement = false;
        sampledCorridor = null;
        plannedInsertion = null;
        plannedSolarCable = null;
        if (solarZone != null)
        {
            solarZone.gameObject.SetActive(false);
            Destroy(solarZone.gameObject);
            solarZone = null;
        }
        ClearRoutePreview();
        routePreviewDirty = true;
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
            entry.Value.choose.interactable = available;
            entry.Value.image.interactable = available;
            entry.Value.title.interactable = available;
            entry.Value.choose.GetComponentInChildren<TextMeshProUGUI>().text = available ? "Выбрать" : "Установлено";
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
        RectTransform breakerRow = Rect("Solar breaker", content, leftTop, leftTop, Vector2.zero, Vector2.zero);
        breakerRow.gameObject.AddComponent<LayoutElement>().preferredHeight = 36f;
        solarBreakerButton = MakeButton(breakerRow, Card, ToggleSolarPower);
        solarBreakerLabel = Label(Rect("Breaker label", breakerRow, leftTop, leftTop,
            new Vector2(10f, -5f), new Vector2(310f, 27f)), "Отключить подачу", 17, Light);
        solarBreakerLabel.raycastTarget = false;
        RectTransform removeRow = Rect("Remove corridor row", content, leftTop, leftTop, Vector2.zero, Vector2.zero);
        removeRow.gameObject.AddComponent<LayoutElement>().preferredHeight = 36f;
        removeBuildingButton = MakeButton(removeRow, Card, RemoveInspectedBuilding);
        removeBuildingLabel = Label(Rect("Remove label", removeRow, Vector2.zero, new Vector2(.5f, .5f),
            Vector2.zero, Vector2.zero), "Удалить переход", 17, Light);
        removeBuildingLabel.rectTransform.anchorMax = Vector2.one;
        removeBuildingLabel.rectTransform.offsetMin = Vector2.zero;
        removeBuildingLabel.rectTransform.offsetMax = Vector2.zero;
        removeBuildingLabel.alignment = TextAlignmentOptions.Center;
        removeBuildingLabel.raycastTarget = false;
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
        text.overflowMode = TextOverflowModes.Masking;
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
        thumbnail.raycastTarget = true;
        Button imageButton = thumbnail.gameObject.AddComponent<Button>();
        imageButton.targetGraphic = thumbnail;
        imageButton.transition = Selectable.Transition.None;
        imageButton.onClick.AddListener(() => Select(definition));
        StartCoroutine(RenderThumbnail(definition, shot =>
        {
            catalogRenders[definition] = shot;
            ShowThumbnail(thumbnail, shot);
            if (inspectedBuilding != null && inspectedBuilding.Definition == definition)
                RefreshInspectionThumbnail();
        }));
        RectTransform titleFrame = Rect("Building title", card, top, top, Vector2.zero, Vector2.zero);
        var title = Label(titleFrame, definition.title, 21, Light, FontStyles.Bold);
        WrapText(title);
        title.raycastTarget = true;
        var titleButton = titleFrame.gameObject.AddComponent<Button>();
        titleButton.targetGraphic = title;
        titleButton.transition = Selectable.Transition.None;
        titleButton.onClick.AddListener(() => Select(definition));
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
        selectionButtons.Add(definition, (choose, imageButton, titleButton));
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
        if (model.TryGetComponent(out PressureCorridor sample))
            sample.BuildCatalogSample();
        if (definition.kind == BuildingKind.DockingNode) AddDisplayCaps(model);
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
        if (solarZoneMaterial != null) Destroy(solarZoneMaterial);
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
