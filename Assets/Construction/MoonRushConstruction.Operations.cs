using System.Collections.Generic;
using TMPro;
using UnityEngine;

public sealed partial class MoonRushConstruction
{
    readonly Dictionary<string, Material> operationsMaterials = new Dictionary<string, Material>();
    ColonyIndustry.Pipe inspectedPipe;
    bool removalPending;
    LunarColonyOperations operations;

    readonly Dictionary<BuildingInstance,float> irradiance = new Dictionary<BuildingInstance,float>();
    readonly List<(float distance, Vector3 location, Quaternion facing)> corridorLights = new List<(float,Vector3,Quaternion)>();
    readonly Light[] lampPool = new Light[18];
    bool gridDirty = true, crossingDirty, hangarWiringDirty;
    int solarCursor, solarScanRemaining;
    float nextSolarScan;
    float nextLightUpdate;
    System.Collections.IEnumerator crossingWork, hangarWork;

    float CachedSunlight(BuildingInstance station) => irradiance.TryGetValue(station,out float value) ? value : 0f;

    void UpdateGridAndLights()
    {
        if (crossingDirty && crossingWork == null)
        {
            crossingDirty = false;
            crossingWork = RefitConnections();
        }
        if (crossingWork != null && !crossingWork.MoveNext()) crossingWork = null;
        if (hangarWiringDirty && hangarWork == null)
        {
            hangarWiringDirty = false;
            hangarWork = WireHangars();
        }
        if (hangarWork != null && !hangarWork.MoveNext()) hangarWork = null;
        // Spread a shadow refresh across frames; a settled grid needs no rebuild.
        if (solarScanRemaining==0 && Time.unscaledTime>=nextSolarScan)
        {
            solarScanRemaining=placed.Count;
            nextSolarScan=Time.unscaledTime+.60f;
        }
        if(placed.Count==0) solarScanRemaining=0;
        for(int checkedStations=0;solarScanRemaining>0 && checkedStations<2;solarScanRemaining--)
        {
            if(solarCursor>=placed.Count) solarCursor=0;
            BuildingInstance candidate=placed[solarCursor++];
            if(candidate==null || candidate.Kind!=BuildingKind.SolarStation) continue;
            checkedStations++;
            float value=DirectSunlight(candidate);
            if(irradiance.TryGetValue(candidate,out float previous) && Mathf.Abs(value-previous)<.005f) continue;
            irradiance[candidate]=value;
            gridDirty=true;
        }
        if (gridDirty)
        {
            gridDirty=false;
            ApplyPowerBalance();
            nextLightUpdate=0f;
        }
        if (Time.unscaledTime < nextLightUpdate || mapCamera==null) return;
        nextLightUpdate=Time.unscaledTime+.3f;
        corridorLights.Clear();
        // Rank lights around the map focus. Emissive fixtures remain visible throughout the base.
        var viewRay=mapCamera.ViewportPointToRay(new Vector3(.5f,.5f,0f));
        Vector3 focus=surface.Raycast(viewRay,out RaycastHit hit,mapCamera.farClipPlane) ?
            hit.point : mapCamera.transform.position;
        foreach (PressureCorridor corridor in corridors)
            if (corridor.IsPowered)
                foreach (var fixture in corridor.Fixtures)
                    corridorLights.Add(((fixture.position-focus).sqrMagnitude,fixture.position,fixture.facing));
        corridorLights.Sort((a,b)=>a.distance.CompareTo(b.distance));
        for (int i=0;i<lampPool.Length;i++)
        {
            if (i>=corridorLights.Count)
            {
                if (lampPool[i]!=null) lampPool[i].enabled=false;
                continue;
            }
            Light lamp=lampPool[i];
            if (lamp==null)
            {
                var source=new GameObject("Pooled white corridor lamp",typeof(Light));
                source.transform.SetParent(transform,false);
                lamp=lampPool[i]=source.GetComponent<Light>();
                lamp.type=LightType.Spot;
                lamp.spotAngle=119f;
                lamp.innerSpotAngle=72f;
                lamp.color=new Color(1f,.94f,.86f);
                lamp.range=5.4f;
                lamp.intensity=5.6f;
                lamp.shadows=LightShadows.None;
            }
            var fitting=corridorLights[i];
            lamp.transform.SetPositionAndRotation(fitting.location,fitting.facing);
            lamp.enabled=true;
        }
    }

    Material OperationsMaterial(string name, Color color)
    {
        if (!operationsMaterials.TryGetValue(name, out Material material))
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name, color = color };
            material.SetFloat("_Smoothness", .37f);
            material.enableInstancing = true;
            operationsMaterials.Add(name, material);
        }
        return material;
    }

    void TickOperations()
    {
        if (operations == null)
        {
            operations = gameObject.AddComponent<LunarColonyOperations>();
            operations.Initialize(terrain, placed, corridors, OperationsMaterial, industry.CollectSample);
        }
    }

    void ClearRemovalSelection()
    {
        inspectedPipe = null;
        removalPending = false;
        if (removeBuildingLabel != null) removeBuildingLabel.text = "Удалить";
    }

    void FitIndustryLink(ColonyIndustry.Pipe link,IReadOnlyList<(Vector3 from, Vector3 to)> spans,
        IReadOnlyList<(Vector3 center, float radius)> modules)
    {
        link.Visual.GetComponent<IndustrialPipeVisual>().SetRoute(
            SolarPowerCable.FitCrossings(terrain,link.Path,spans,modules,link.Cargo==ColonyIndustry.Resource.Ore?1.15f:.49f));
    }

    void InspectPipe(ColonyIndustry.Pipe pipe)
    {
        if(pipe.Visual==null || pipe.Source==null || pipe.Target==null) return;
        CloseInspection();
        inspectedPipe = pipe;
        SetCatalogOpen(false);
        bool belt=pipe.Cargo==ColonyIndustry.Resource.Ore;
        inspectionTitle.text = belt ? "Герметичный конвейер" : "Промышленный трубопровод";
        inspectionDescription.text = belt ? "Закрытая лента перевозки лунной породы." :
            "Перевозит материалы между промышленными сооружениями.";
        inspectionCharacteristics.text = pipe.Source.Definition.title + " → " + pipe.Target.Definition.title;
        inspectionImage.enabled = false;
        solarBreakerButton.gameObject.SetActive(false);
        surfaceDoorButton.gameObject.SetActive(false);
        removeBuildingButton.gameObject.SetActive(true);
        inspectionPanel.gameObject.SetActive(true);
        UpdateInspectionScale();
        inspectionScroll.StopMovement();
        inspectionScroll.verticalNormalizedPosition = 1f;
        inspectionOutline.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
        inspectionOutline.loop = false;
        inspectionOutline.positionCount = pipe.Path.Length;
        for (int i = 0; i < pipe.Path.Length; i++)
            inspectionOutline.SetPosition(i,pipe.Path[i]+Vector3.up*.42f);
        inspectionOutline.gameObject.SetActive(true);
    }
}
