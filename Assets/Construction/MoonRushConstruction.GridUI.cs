using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class MoonRushConstruction
{
    enum NoticeKind { Emergency, Warning, Positive }
    static readonly Color NoticeRed=new Color32(255,125,137,255);
    static readonly Color NoticeYellow=new Color32(255,202,115,255);
    static readonly Color NoticeGreen=new Color32(128,235,177,255);

    sealed class NoticeRow
    {
        internal RectTransform Rect;
        internal CanvasGroup Fade;
        internal TextMeshProUGUI Text, Icon;
        internal Image AccentBar;
        internal string Message;
        internal float Started;
    }

    readonly NoticeRow[] noticeRows=new NoticeRow[3];
    readonly Queue<string> emergencyNotices=new Queue<string>();
    readonly Queue<(NoticeKind kind,string message)> pendingNotices=new Queue<(NoticeKind,string)>();
    readonly HashSet<string> postedNotices=new HashSet<string>();
    readonly Dictionary<BuildingInstance,BuildingPowerStatus> observedPower=new Dictionary<BuildingInstance,BuildingPowerStatus>();
    readonly List<BuildingInstance> removedPowerSites=new List<BuildingInstance>();
    readonly List<string> newlyShed=new List<string>(), recovered=new List<string>(), connectedNotices=new List<string>(), disconnectedNotices=new List<string>();
    bool observedDeficit;
    TextMeshProUGUI gridStatus;
    Image powerBackground;
    readonly List<Image> powerIcon=new List<Image>();

    void CreateColonyHud(Transform canvas)
    {
        var meter=Rect("Power production and demand",canvas,TopLeft,TopLeft,new Vector2(18f,-18f),new Vector2(272f,42f));
        powerBackground=Fill(meter,HudSurface);
        Round(powerBackground);
        powerBackground.raycastTarget=false;
        var meterCaption=Label(Stretch("Power heading",meter,41f,22f,8f,4f),"Выработка / потребность",11,Muted);
        meterCaption.alignment=TextAlignmentOptions.MidlineLeft;
        gridStatus=Label(Stretch("Power reading",meter,41f,4f,8f,20f),"0,0 / 0,0 кВт",16,Light,FontStyles.Bold);
        gridStatus.alignment=TextAlignmentOptions.MidlineLeft;
        FitHudText(gridStatus,13f,16f);
        Vector2[] bolt={new Vector2(13f,17f),new Vector2(3f,8f),new Vector2(10f,8f),
            new Vector2(7f,1f),new Vector2(18f,11f),new Vector2(11f,11f),new Vector2(13f,17f)};
        for(int i=1;i<bolt.Length;i++)
        {
            Vector2 vector=bolt[i]-bolt[i-1];
            var stroke=Rect("Power glyph",meter,new Vector2(0f,.5f),Middle,
                new Vector2(13f,-9f)+(bolt[i]+bolt[i-1])*.5f,new Vector2(vector.magnitude,1.9f));
            stroke.localRotation=Quaternion.Euler(0f,0f,Mathf.Atan2(vector.y,vector.x)*Mathf.Rad2Deg);
            Image fill=Fill(stroke,Accent);
            fill.raycastTarget=false;
            powerIcon.Add(fill);
        }
        Vector2 topCenter=new Vector2(.5f,1f);
        for(int i=0;i<noticeRows.Length;i++)
        {
            var rect=Rect("Colony event",canvas,topCenter,topCenter,new Vector2(0f,-19f),new Vector2(640f,60f));
            var surface=Fill(rect,HudSurface);
            Round(surface);
            surface.raycastTarget=false;
            var fade=rect.gameObject.AddComponent<CanvasGroup>();
            fade.blocksRaycasts=false;
            var rail=Fill(Stretch("Event accent",rect,5f,7f,626f,7f),Accent);
            rail.raycastTarget=false;
            Round(rail);
            var icon=Label(Rect("Event icon",rect,new Vector2(0f,.5f),new Vector2(0f,.5f),
                new Vector2(17f,0f),new Vector2(29f,30f)),"!",21,Light,FontStyles.Bold);
            icon.alignment=TextAlignmentOptions.Center;
            var caption=Label(Stretch("Event message",rect,53f,6f,13f,6f),"",16,Light,FontStyles.Bold);
            caption.alignment=TextAlignmentOptions.MidlineLeft;
            FitHudText(caption,13f,16f);
            noticeRows[i]=new NoticeRow{Rect=rect,Fade=fade,Text=caption,Icon=icon,AccentBar=rail};
            rect.gameObject.SetActive(false);
        }
        CreateBuildingActions(canvas);
    }

    void UpdatePowerMeter(ColonyPowerNetwork.Balance balance)
    {
        bool shortage=balance.Demand-balance.Served>.005f;
        gridStatus.text=$"{balance.Generation:F1} / {balance.Demand:F1} кВт";
        gridStatus.color=shortage?NoticeRed:Light;
        powerBackground.color=shortage?(Color)new Color32(57,24,37,245):HudSurface;
        foreach(Image stroke in powerIcon) stroke.color=shortage?NoticeRed:Accent;
    }

    static string PowerStatusText(BuildingInstance building)
    {
        switch(building.PowerStatus)
        {
            case BuildingPowerStatus.Powered:
                return building.RecentlyRestored?"<color=#80EBB1>● Электропитание восстановлено</color>":
                    "<color=#80EBB1>● Электропитание подключено</color>";
            case BuildingPowerStatus.Unpowered:
                return "<color=#FF7D89>● Отключено из-за нехватки электричества</color>";
            case BuildingPowerStatus.SwitchedOff:
                return "<color=#FFCA73>● Подача электричества отключена вручную</color>";
            default:
                return "<color=#FFCA73>● Не подключено к электросети</color>";
        }
    }

    void SyncGridWarnings(ColonyPowerNetwork.Balance balance)
    {
        bool deficit=balance.Demand-balance.Served>.005f;
        newlyShed.Clear(); recovered.Clear(); connectedNotices.Clear(); disconnectedNotices.Clear(); removedPowerSites.Clear();
        foreach(var item in observedPower)
            if(item.Key==null || !item.Key.isActiveAndEnabled) removedPowerSites.Add(item.Key);
        foreach(var site in removedPowerSites) observedPower.Remove(site);
        foreach(var site in placed)
        {
            if(site==null || !site.isActiveAndEnabled || site.Kind==BuildingKind.LandingPad) continue;
            bool known=observedPower.TryGetValue(site,out var old);
            var state=site.PowerStatus;
            observedPower[site]=state;
            if(deficit && state==BuildingPowerStatus.Unpowered && (!observedDeficit || !known || old!=state))
                newlyShed.Add(site.Definition.title);
            if(known && old==state) continue;
            if(state==BuildingPowerStatus.Disconnected)
                disconnectedNotices.Add(site.Definition.title);
            if(state==BuildingPowerStatus.Powered)
            {
                if(known && (old==BuildingPowerStatus.Unpowered || site.RecentlyRestored)) recovered.Add(site.Definition.title);
                else connectedNotices.Add(site.Definition.title);
            }
        }
        if(newlyShed.Count>0) NotifySites(NoticeKind.Emergency,"Недостаточно электроэнергии. Отключены: ",newlyShed);
        else if(deficit && !observedDeficit) Notify(NoticeKind.Emergency,"Недостаточно электроэнергии");
        NotifySites(NoticeKind.Warning,"Не подключены к электросети: ",disconnectedNotices);
        NotifySites(NoticeKind.Positive,"Электроснабжение восстановлено: ",recovered);
        NotifySites(NoticeKind.Positive,"Подключены к электросети: ",connectedNotices);
        observedDeficit=deficit;
    }

    void NotifySites(NoticeKind kind,string prefix,List<string> names)
    {
        if(names.Count==0) return;
        var counts=new Dictionary<string,int>();
        foreach(string name in names)
            counts[name]=counts.TryGetValue(name,out int count)?count+1:1;
        string page=prefix;
        foreach(var item in counts)
        {
            string entry=item.Key+(item.Value>1?$" ×{item.Value}":"");
            if(page.Length>prefix.Length && page.Length+entry.Length>132)
            {
                Notify(kind,page);
                page=prefix;
            }
            if(page.Length>prefix.Length) page+=", ";
            page+=entry;
        }
        if(page.Length>prefix.Length) Notify(kind,page);
    }

    void Notify(NoticeKind kind,string message)
    {
        if(!postedNotices.Add(message)) return;
        if(kind==NoticeKind.Emergency) emergencyNotices.Enqueue(message);
        else pendingNotices.Enqueue((kind,message));
    }

    void TickHud()
    {
        if(landingWarningRequested && !landingNoticeActive) Notify(NoticeKind.Warning,LandingWarningText);
        landingNoticeActive=landingWarningRequested;
        if(panel.gameObject.activeSelf)
        {
            catalogFade.alpha=Mathf.MoveTowards(catalogFade.alpha,catalogOpen?1f:0f,Time.unscaledDeltaTime*6.8f);
            panel.anchoredPosition=new Vector2(0f,96f-15f*(1f-catalogFade.alpha));
            if(!catalogOpen && catalogFade.alpha<=0f) panel.gameObject.SetActive(false);
        }
        float time=Time.unscaledTime;
        foreach(NoticeRow row in noticeRows)
        {
            if(row.Message!=null && time-row.Started>=5.9f)
            {
                postedNotices.Remove(row.Message);
                row.Message=null;
                row.Rect.gameObject.SetActive(false);
            }
            if(row.Message==null && (emergencyNotices.Count>0 || pendingNotices.Count>0))
            {
                (NoticeKind kind,string message) next=emergencyNotices.Count>0 ?
                    (NoticeKind.Emergency,emergencyNotices.Dequeue()) : pendingNotices.Dequeue();
                row.Message=row.Text.text=next.message;
                row.Started=time;
                row.Icon.text=next.kind==NoticeKind.Positive?"+":"!";
                row.Icon.color=row.AccentBar.color=next.kind==NoticeKind.Positive?NoticeGreen:
                    next.kind==NoticeKind.Emergency?NoticeRed:NoticeYellow;
                row.Rect.gameObject.SetActive(true);
            }
        }
        int depth=0;
        foreach(NoticeRow row in noticeRows)
        {
            if(row.Message==null) continue;
            float age=time-row.Started;
            float enter=Mathf.Clamp01(age/.22f);
            row.Fade.alpha=enter*Mathf.Clamp01((5.9f-age)/.7f);
            row.Rect.anchoredPosition=new Vector2(0f,-19f-66f*depth++ + 8f*(1f-enter));
        }
    }
}
