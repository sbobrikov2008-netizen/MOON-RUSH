using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class MoonRushConstruction
{
    static readonly Color PowerOrange=new Color32(255,181,105,255);
    static readonly Color PowerRed=new Color32(255,119,127,255);
    readonly Dictionary<BuildingInstance,(Image icon,Vector3 position)> outageMarkers=
        new Dictionary<BuildingInstance,(Image,Vector3)>();
    readonly List<BuildingInstance> stalePowerMarkers=new List<BuildingInstance>();
    readonly Dictionary<string,int> shedNames=new Dictionary<string,int>();
    readonly StringBuilder powerListText=new StringBuilder();
    RectTransform gridWarningPanel;
    TextMeshProUGUI gridWarningTitle,gridWarningList;
    ScrollRect gridWarningScroll;

    static string PowerStatusText(BuildingInstance building)
    {
        switch(building.PowerStatus)
        {
            case BuildingPowerStatus.Powered:
                return building.RecentlyRestored?"<color=#7BECAF>● Электропитание восстановлено</color>":
                    "<color=#7BECAF>● Электропитание подключено</color>";
            case BuildingPowerStatus.Unpowered:
                return "<color=#FFB569>● Отключено из-за нехватки электричества</color>";
            case BuildingPowerStatus.SwitchedOff:
                return "<color=#FF777F>● Подача электричества отключена вручную</color>";
            default:
                return "<color=#FF777F>● Не подключено к электросети</color>";
        }
    }

    void CreateGridWarnings(Transform canvas)
    {
        Vector2 top=new Vector2(0f,1f);
        gridWarningPanel=Rect("Grid warning",canvas,top,top,new Vector2(18f,-156f),new Vector2(424f,120f));
        Fill(gridWarningPanel,Panel);
        gridWarningTitle=Label(Rect("Grid warning title",gridWarningPanel,top,top,new Vector2(11f,-7f),new Vector2(400f,26f)),
            "",15,PowerOrange,FontStyles.Bold);
        WrapText(gridWarningTitle);
        var viewport=Rect("Disabled loads viewport",gridWarningPanel,top,top,new Vector2(11f,-36f),new Vector2(402f,74f));
        viewport.gameObject.AddComponent<RectMask2D>();
        Fill(viewport,new Color(1f,1f,1f,0f));
        gridWarningScroll=viewport.gameObject.AddComponent<ScrollRect>();
        gridWarningScroll.viewport=viewport;
        gridWarningScroll.horizontal=false;
        gridWarningScroll.movementType=ScrollRect.MovementType.Clamped;
        var content=Rect("Disabled loads",viewport,top,top,Vector2.zero,new Vector2(397f,0f));
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
        gridWarningList=Label(content,"",14,Light);
        WrapText(gridWarningList);
        gridWarningScroll.content=content;
        gridWarningPanel.gameObject.SetActive(false);
    }

    void SyncGridWarnings(ColonyPowerNetwork.Balance balance)
    {
        if(gridWarningPanel==null) return;
        stalePowerMarkers.Clear();
        foreach(var item in outageMarkers)
            if(item.Key==null || item.Key.HasPower || !item.Key.isActiveAndEnabled) stalePowerMarkers.Add(item.Key);
        foreach(BuildingInstance item in stalePowerMarkers)
        {
            Destroy(outageMarkers[item].icon.gameObject);
            outageMarkers.Remove(item);
        }

        shedNames.Clear();
        foreach(BuildingInstance site in placed)
        {
            if(site==null || site.HasPower) continue;
            if(!outageMarkers.TryGetValue(site,out var marker))
            {
                Vector2 center=new Vector2(.5f,.5f);
                RectTransform rect=Rect("Power status marker",hudCanvas.transform,center,center,Vector2.zero,new Vector2(19f,19f));
                var icon=Fill(rect,Panel);
                icon.raycastTarget=false;
                var glyph=Label(Rect("Power warning glyph",rect,center,center,Vector2.zero,new Vector2(18f,18f)),"!",14,Panel,FontStyles.Bold);
                glyph.alignment=TextAlignmentOptions.Center;
                marker=(icon,OutagePosition(site));
                outageMarkers.Add(site,marker);
            }
            marker.icon.color=site.PowerStatus==BuildingPowerStatus.Unpowered?PowerOrange:PowerRed;
            if(site.PowerStatus!=BuildingPowerStatus.Unpowered) continue;
            string title=site.Definition.title;
            shedNames.TryGetValue(title,out int count);
            shedNames[title]=count+1;
        }
        if(shedNames.Count==0)
        {
            gridWarningPanel.gameObject.SetActive(false);
            return;
        }
        float shortfall=Mathf.Max(0f,balance.Demand-balance.Served);
        gridWarningTitle.text=$"Дефицит мощности · не подано {shortfall:F1} кВт";
        powerListText.Clear().AppendLine("Отключены:");
        foreach(var item in shedNames)
        {
            powerListText.Append("• ").Append(item.Key);
            if(item.Value>1) powerListText.Append(" ×").Append(item.Value);
            powerListText.AppendLine();
        }
        string text=powerListText.ToString().TrimEnd();
        if(gridWarningList.text!=text)
        {
            gridWarningList.text=text;
            gridWarningScroll.verticalNormalizedPosition=1f;
        }
        gridWarningPanel.gameObject.SetActive(true);
    }

    void PositionPowerMarkers()
    {
        if(outageMarkers.Count==0 || mapCamera==null || hudCanvas==null) return;
        var canvasRect=(RectTransform)hudCanvas.transform;
        foreach(var item in outageMarkers)
        {
            if(item.Key==null)
            {
                if(item.Value.icon.gameObject.activeSelf) item.Value.icon.gameObject.SetActive(false);
                continue;
            }
            Vector3 screen=mapCamera.WorldToScreenPoint(item.Value.position);
            bool visible=screen.z>0f && mapCamera.pixelRect.Contains(screen);
            if(item.Value.icon.gameObject.activeSelf!=visible) item.Value.icon.gameObject.SetActive(visible);
            if(visible && RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect,screen,null,out Vector2 local))
                item.Value.icon.rectTransform.anchoredPosition=local;
        }
    }
    static Vector3 OutagePosition(BuildingInstance site)
    {
        Vector3 position=site.transform.position+Vector3.up*3.5f;
        if(site.TryGetComponent(out PressureCorridor corridor) && corridor.Centerline!=null && corridor.Centerline.Count>0)
        {
            var midpoint=corridor.Centerline[corridor.Centerline.Count/2];
            return midpoint.point+Vector3.up*1.8f;
        }
        if(site.TryGetComponent(out MeshRenderer mesh) && mesh.enabled) position.y=mesh.bounds.max.y+.34f;
        return position;
    }
}
