using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

public sealed partial class MoonRushConstruction
{
    readonly ColonyIndustry industry = new ColonyIndustry();
    readonly Dictionary<(BuildingInstance a, BuildingInstance b), SolarPowerCable> solarPeers =
        new Dictionary<(BuildingInstance, BuildingInstance), SolarPowerCable>();
    readonly HashSet<BuildingInstance> disabledSolar = new HashSet<BuildingInstance>();
    readonly RaycastHit[] sunlightHits = new RaycastHit[32];
    readonly List<Vector3> deposits = new List<Vector3>();
    readonly List<LineRenderer> coverageRings = new List<LineRenderer>();
    Coroutine wiringJob;
    BuildingInstance pipeStart;
    bool pipeMode;
    GameObject depositMap;
    Material depositMaterial;
    readonly Dictionary<ColonyIndustry.Resource, Material> pipeMaterials =
        new Dictionary<ColonyIndustry.Resource, Material>();
    TextMeshProUGUI gridStatus, scienceStatus, researchLabel;
    float lastIndustryTick;

    void CreateIndustryUI(Transform canvas)
    {
        Vector2 top = new Vector2(0f, 1f);
        RectTransform desk = Rect("Colony systems", canvas, top, top, new Vector2(24f,-24f), new Vector2(400f,146f));
        Fill(desk, Panel).raycastTarget = false;
        gridStatus = Label(Rect("Grid balance",desk,top,top,new Vector2(13f,-9f),new Vector2(374f,48f)), "",17,Light);
        WrapText(gridStatus);
        scienceStatus = Label(Rect("Survey status",desk,top,top,new Vector2(13f,-59f),new Vector2(374f,42f)), "",15,Muted);
        WrapText(scienceStatus);
        Control("Месторождения", 252f, ToggleDeposits);
        Control("Трубопровод", 132f, BeginPipe);
        researchLabel = Control("Исследовать", 12f, Research);

        TextMeshProUGUI Control(string caption,float x,UnityEngine.Events.UnityAction action)
        {
            var row=Rect(caption,desk,top,top,new Vector2(x,-108f),new Vector2(116f,27f));
            MakeButton(row,Card,action);
            var label=Label(Rect(caption,row,top,top,new Vector2(3f,-2f),new Vector2(110f,23f)),caption,13,Light);
            label.alignment=TextAlignmentOptions.Center;
            WrapText(label);
            return label;
        }
    }

    void UpdateIndustry()
    {
        float now = Time.time;
        float elapsed = lastIndustryTick > 0f ? Mathf.Min(now-lastIndustryTick,1f) : 0f;
        lastIndustryTick = now;
        int before = industry.Surveys;
        industry.Tick(elapsed,placed);
        if (industry.Surveys > before) RevealDeposit(industry.Surveys);

        bool lab = false;
        foreach (BuildingInstance item in placed)
            if (item != null && item.Kind == BuildingKind.ScienceLab && item.isActiveAndEnabled) lab=true;
        researchLabel.text = lab ? "Исследовать" : "На Землю";
        if (industry.ResearchRemaining > 0f)
            scienceStatus.text = $"Образцы: {industry.Samples} · {(industry.EarthResearch ? "Анализ на Земле" : "Анализ в лаборатории")}: {Mathf.CeilToInt(industry.ResearchRemaining)} с";
        else if (string.IsNullOrEmpty(scienceStatus.text) || Time.unscaledTime >= researchNoticeUntil)
            scienceStatus.text = $"Образцы: {industry.Samples} · Открыто месторождений: {industry.Surveys}";
    }

    float researchNoticeUntil;
    void Research()
    {
        bool lab = false, powered = false;
        foreach (BuildingInstance item in placed)
            if (item != null && item.Kind == BuildingKind.ScienceLab && item.isActiveAndEnabled)
            {
                lab = true;
                powered |= item.HasPower;
            }
        bool pad = LandingPad() != null;
        if (industry.BeginResearch(powered,lab,pad)) return;
        scienceStatus.text = industry.ResearchRemaining > 0f ? "Исследование уже начато" :
            industry.Samples == 0 ? "Нужен образец добытого реголита" :
            lab ? "Лаборатории требуется питание" : "Для отправки нужна посадочная площадка";
        researchNoticeUntil = Time.unscaledTime + 4f;
    }

    void ToggleDeposits()
    {
        if (depositMap == null)
        {
            depositMap = new GameObject("Surveyed mineral deposits");
            depositMap.transform.SetParent(transform,false);
        }
        else depositMap.SetActive(!depositMap.activeSelf);
        if (deposits.Count == 0)
        {
            scienceStatus.text = "Исследуйте образец, чтобы открыть месторождение";
            researchNoticeUntil = Time.unscaledTime + 4f;
        }
    }

    void RevealDeposit(int index)
    {
        if (depositMap == null)
        {
            depositMap = new GameObject("Surveyed mineral deposits");
            depositMap.transform.SetParent(transform,false);
            depositMap.SetActive(false);
        }
        if (depositMaterial == null)
        {
            depositMaterial = new Material(previewMaterial);
            depositMaterial.SetColor("_Tint",new Color(.26f,.91f,.78f,.93f));
        }
        Vector3 origin = terrain.transform.position;
        Vector3 size = terrain.terrainData.size;
        var random = new System.Random(73519 + index * 127);
        Vector3 point = origin + new Vector3(size.x*.5f,0f,size.z*.5f);
        for (int attempt=0;attempt<48;attempt++)
        {
            float u=.19f+(float)random.NextDouble()*.62f, v=.19f+(float)random.NextDouble()*.62f;
            Vector3 candidate=origin+new Vector3(size.x*u,0f,size.z*v);
            if (terrain.terrainData.GetSteepness(u,v) > 10f ||
                deposits.Exists(p => (p-candidate).sqrMagnitude < 1600f)) continue;
            point=candidate;
            break;
        }
        deposits.Add(point);
        foreach (BuildingInstance item in placed)
            if (item != null) ShowDepositsForMining(item);
        var ring=CreateOutline($"Месторождение {index}",depositMap.transform,depositMaterial);
        PlaceRing(ring,point,18f,72,.52f);
    }

    void ShowDepositsForMining(BuildingInstance item)
    {
        if (item.Kind != BuildingKind.MaterialsStation) return;
        Vector3 local=item.transform.position;
        local.y=terrain.transform.position.y;
        item.ProductionMultiplier=deposits.Exists(p => (p-local).sqrMagnitude < 18f*18f) ? 1.5f : 1f;
    }

    void BeginPipe()
    {
        Cancel();
        CloseInspection();
        panel.gameObject.SetActive(false);
        pipeMode=true;
        feedback.transform.parent.gameObject.SetActive(true);
        feedback.text="Трубопровод · Выберите добывающую станцию или перерабатывающий цех";
    }

    void UpdatePipes()
    {
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1))
        {
            Cancel();
            return;
        }
        Vector3 pointer=Input.mousePosition;
        if (!Input.GetMouseButtonDown(0) || !mapCamera.pixelRect.Contains(pointer) || IsOverInterface(pointer)) return;
        var sourceRay=mapCamera.ScreenPointToRay(pointer);
        BuildingInstance hit=Physics.Raycast(sourceRay,out RaycastHit result,mapCamera.farClipPlane,
            Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore) ?
            result.collider.GetComponentInParent<BuildingInstance>() : null;
        if (pipeStart == null)
        {
            if (hit == null || !ColonyIndustry.HasOutput(hit.Kind))
            {
                feedback.text="Выберите станцию добычи реголита, воды или перерабатывающий цех";
                return;
            }
            pipeStart=hit;
            feedback.text=$"Выберите получателя для «{hit.Definition.title}»";
            return;
        }
        if (hit == null || !ColonyIndustry.TryCargo(pipeStart.Kind,hit.Kind,out var cargo))
        {
            feedback.text="Несовместимые разъёмы. Реголит → переработка · концентрат и вода → топливо";
            return;
        }
        if (!industry.Free(pipeStart,hit,cargo))
        {
            feedback.text="Промышленный разъём уже занят";
            return;
        }
        Vector3[] route=IndustrialPipeRoute.Plan(terrain,pipeStart,hit,cargo,placed);
        if (route == null)
        {
            feedback.text="Трубопровод не проходит между сооружениями";
            return;
        }
        Material material=PipeMaterial(cargo);
        var root=new GameObject($"Промышленная труба · {pipeStart.Definition.title} → {hit.Definition.title}",typeof(LineRenderer));
        root.transform.SetParent(transform,false);
        var pipe=root.GetComponent<LineRenderer>();
        pipe.sharedMaterial=material;
        pipe.useWorldSpace=true;
        pipe.widthMultiplier=.31f;
        pipe.numCornerVertices=3;
        pipe.numCapVertices=5;
        pipe.positionCount=route.Length;
        pipe.SetPositions(route);
        industry.Add(pipeStart,hit,cargo,root,route);
        RefreshCableCrossings();
        Cancel();
    }

    void AddIndustrySockets(BuildingInstance item)
    {
        BuildingKind kind = item.Kind;
        if (ColonyIndustry.HasOutput(kind))
        {
            var type = kind == BuildingKind.WaterStation ? ColonyIndustry.Resource.Water : kind == BuildingKind.Refinery ?
                ColonyIndustry.Resource.Refined : ColonyIndustry.Resource.Ore;
            Socket(true,type,1f);
        }
        if (kind == BuildingKind.Refinery) Socket(false,ColonyIndustry.Resource.Ore,-1f);
        if (kind == BuildingKind.FuelWorks)
        {
            Socket(false,ColonyIndustry.Resource.Refined,-1f);
            Socket(false,ColonyIndustry.Resource.Water,1f);
        }

        void Socket(bool output,ColonyIndustry.Resource type,float side)
        {
            Vector3 at=item.transform.TransformPoint(ColonyIndustry.Socket(kind,output,type));
            Coupling(item.transform,at,at+item.transform.right*side,PipeMaterial(type));
        }
    }

    Material PipeMaterial(ColonyIndustry.Resource cargo)
    {
        if (pipeMaterials.TryGetValue(cargo,out Material result)) return result;
        result=new Material(previewMaterial);
        result.SetColor("_Tint",cargo==ColonyIndustry.Resource.Water?new Color(.20f,.63f,.91f,1f):
            cargo==ColonyIndustry.Resource.Refined?new Color(.73f,.72f,.66f,1f):new Color(.96f,.56f,.19f,1f));
        pipeMaterials.Add(cargo,result);
        return result;
    }

    static void Coupling(Transform root,Vector3 endpoint,Vector3 along,Material material)
    {
        var fitting=GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        fitting.name="Промышленная муфта";
        fitting.transform.SetParent(root,false);
        fitting.transform.SetPositionAndRotation(endpoint,Quaternion.FromToRotation(Vector3.up,along-endpoint));
        fitting.transform.localScale=new Vector3(.67f,.19f,.67f);
        fitting.GetComponent<Renderer>().sharedMaterial=material;
        fitting.GetComponent<Collider>().enabled=false;
        Destroy(fitting.GetComponent<Collider>());
    }

    void QueueSolarWiring()
    {
        if (wiringJob != null) StopCoroutine(wiringJob);
        wiringJob = StartCoroutine(WireSolar());
    }

    IEnumerator WireSolar()
    {
        yield return null;
        var obsolete = new List<(BuildingInstance,BuildingInstance)>();
        foreach (var entry in solarPeers)
            if (entry.Key.a == null || entry.Key.b == null || !placed.Contains(entry.Key.a) || !placed.Contains(entry.Key.b))
            {
                if (entry.Value != null) Destroy(entry.Value.gameObject);
                obsolete.Add(entry.Key);
            }
        foreach (var key in obsolete) solarPeers.Remove(key);
        var stations = new List<BuildingInstance>();
        foreach (BuildingInstance item in placed)
            if (item != null && item.Kind == BuildingKind.SolarStation) stations.Add(item);
        BuildingInstance command=CommandPost();
        for (int a=0;a<stations.Count;a++)
        {
            BuildingInstance station=stations[a];
            if (command != null && WithinSolarRange(station,command) && !powerCables.ContainsKey(station))
            {
                Vector3[] direct = SolarPowerCable.Plan(terrain,station.transform.position,station.transform.rotation,
                    station.Definition.footprintRadius,command,placed,powerCables.Values,station,searchLimit: 500);
                if (direct != null)
                {
                    powerCables.Add(station,SolarPowerCable.Install(station,command,direct));
                    RefreshCableCrossings();
                }
                yield return null;
            }
            for(int b=a+1;b<stations.Count;b++)
            {
                BuildingInstance neighbor=stations[b];
                if (!WithinSolarRange(station,neighbor) || solarPeers.ContainsKey((station,neighbor))) continue;
                Vector3[] route = SolarPowerCable.Plan(terrain,station.transform.position,station.transform.rotation,
                    station.Definition.footprintRadius,neighbor,placed,solarPeers.Values,station,searchLimit: 500);
                BuildingInstance emitter = station, receiver = neighbor;
                if (route == null)
                {
                    yield return null;
                    emitter = neighbor;
                    receiver = station;
                    route = SolarPowerCable.Plan(terrain,neighbor.transform.position,neighbor.transform.rotation,
                        neighbor.Definition.footprintRadius,station,placed,solarPeers.Values,neighbor,searchLimit: 500);
                }
                if (route != null)
                {
                    solarPeers.Add((station,neighbor),SolarPowerCable.Install(emitter,receiver,route));
                    RefreshCableCrossings();
                }
                yield return null;
            }
        }
        wiringJob=null;
        RefreshPower();
    }

    bool WithinSolarRange(BuildingInstance a,BuildingInstance b)
    {
        Vector3 delta=a.transform.position-b.transform.position;
        return delta.x*delta.x+delta.z*delta.z <= solarConnectionRadius*solarConnectionRadius;
    }

    IEnumerable<(BuildingInstance a,BuildingInstance b)> LiveSolarPeers()
    {
        foreach (var entry in solarPeers)
            if (entry.Value != null && entry.Value.IsTransmitting) yield return entry.Key;
    }

    float DirectSunlight(BuildingInstance station)
    {
        Light sun=RenderSettings.sun;
        if (sun == null || !sun.isActiveAndEnabled || sun.type != LightType.Directional || sun.intensity <= 0f)
            return 0f;
        Vector3 direction=-sun.transform.forward;
        Vector3 face=station.transform.rotation*Quaternion.Euler(21f,0f,0f)*Vector3.up;
        float incidence=direction.y > .001f ? Mathf.Max(0f,Vector3.Dot(direction,face)) : 0f;
        if (incidence <= 0f) return 0f;
        int exposed=0;
        for (int row=-1;row<=1;row++)
        for (int column=-1;column<=1;column+=2)
        foreach (float across in new[]{-2.35f,2.35f})
        {
            Vector3 point=station.transform.TransformPoint(new Vector3(column*5.73f+across,4.3f,row*8.17f-.62f+column*.57f));
            int hits=Physics.RaycastNonAlloc(point+direction*.12f,direction,sunlightHits,
                12000f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
            bool shaded=hits == sunlightHits.Length;
            for(int i=0;i<hits;i++)
            {
                Collider collider=sunlightHits[i].collider;
                if (collider == null || collider.transform.IsChildOf(station.transform)) continue;
                shaded=true;
                break;
            }
            if (!shaded) exposed++;
        }
        return incidence*exposed/12f;
    }

    void PlaceRing(LineRenderer ring,Vector3 center,float radius,int samples,float width)
    {
        ring.useWorldSpace=true;
        ring.widthMultiplier=width;
        var locations=new Vector3[samples];
        for (int i=0;i<samples;i++)
        {
            float angle=i*Mathf.PI*2f/samples;
            Vector3 point=center+new Vector3(Mathf.Cos(angle),0f,Mathf.Sin(angle))*radius;
            point.y=terrain.transform.position.y+terrain.SampleHeight(point)+.31f;
            locations[i]=point;
        }
        ring.positionCount=samples;
        ring.SetPositions(locations);
    }
}
