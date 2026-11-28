using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class MoonRushConstruction
{
    static readonly BuildingKind[] ColonyCatalog =
    {
        BuildingKind.LandingPad, BuildingKind.CommandPost, BuildingKind.SolarStation,
        BuildingKind.Habitat, BuildingKind.WaterStation, BuildingKind.OxygenStation, BuildingKind.Greenhouse,
        BuildingKind.CargoHangar, BuildingKind.AirlockCapsule, BuildingKind.PressureCorridor, BuildingKind.DockingNode
    };

    static readonly Vector2 TopLeft=new Vector2(0f,1f), Middle=new Vector2(.5f,.5f), Bottom=new Vector2(.5f,0f);
    static readonly Color HudSurface=new Color32(14,24,37,241);
    static readonly Color HudTile=new Color32(28,44,61,250);
    readonly RectTransform[] catalogPages=new RectTransform[2];
    readonly Button[] catalogTabs=new Button[2];
    readonly TextMeshProUGUI[] catalogTabTitles=new TextMeshProUGUI[2];
    ScrollRect catalogScroll;
    CanvasGroup catalogFade;
    Sprite cornerSprite;
    bool catalogOpen;

    void CreateCatalog(Transform canvas)
    {
        panel=Rect("Construction catalog",canvas,Bottom,Bottom,new Vector2(0f,96f),new Vector2(-56f,382f));
        panel.anchorMin=new Vector2(0f,0f);
        panel.anchorMax=new Vector2(1f,0f);
        Round(Fill(panel,HudSurface));
        catalogFade=panel.gameObject.AddComponent<CanvasGroup>();
        catalogFade.alpha=0f;
        catalogFade.blocksRaycasts=false;
        catalogFade.interactable=false;
        panel.gameObject.SetActive(false);

        Label(Rect("Catalog title",panel,TopLeft,TopLeft,new Vector2(22f,-14f),new Vector2(240f,34f)),
            "Строительство",24,Light,FontStyles.Bold);
        for(int i=0;i<catalogTabs.Length;i++)
        {
            int page=i;
            var tab=Rect("Catalog tab",panel,TopLeft,TopLeft,new Vector2(268f+i*154f,-12f),new Vector2(142f,39f));
            catalogTabs[i]=HudButton(tab,()=>SetCatalogTab(page));
            catalogTabTitles[i]=Centered(tab,i==0?"Колония":"Добыча",18,Light);
        }
        var close=Rect("Close catalog",panel,Vector2.one,Vector2.one,new Vector2(-17f,-13f),new Vector2(40f,38f));
        HudButton(close,()=>SetCatalogOpen(false));
        Centered(close,"×",26,Light);

        var viewport=Stretch("Catalog viewport",panel,18f,36f,18f,66f);
        Fill(viewport,Color.clear);
        viewport.gameObject.AddComponent<RectMask2D>();
        catalogScroll=viewport.gameObject.AddComponent<ScrollRect>();
        catalogScroll.viewport=viewport;
        catalogScroll.horizontal=true;
        catalogScroll.vertical=false;
        catalogScroll.movementType=ScrollRect.MovementType.Clamped;
        catalogScroll.scrollSensitivity=42f;
        var track=Stretch("Catalog scrollbar",panel,21f,12f,21f,0f);
        track.anchorMax=new Vector2(1f,0f);
        track.sizeDelta=new Vector2(-42f,9f);
        track.anchoredPosition=new Vector2(0f,18f);
        Round(Fill(track,new Color32(42,65,83,255)));
        var thumb=Stretch("Catalog scrollbar handle",track,1f,1f,1f,1f);
        var thumbImage=Fill(thumb,Accent);
        Round(thumbImage);
        var scrollBar=track.gameObject.AddComponent<Scrollbar>();
        scrollBar.handleRect=thumb;
        scrollBar.targetGraphic=thumbImage;
        catalogScroll.horizontalScrollbar=scrollBar;

        for(int i=0;i<catalogPages.Length;i++)
        {
            var row=Rect("Catalog card row",viewport,TopLeft,TopLeft,Vector2.zero,new Vector2(0f,278f));
            var layout=row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing=13f;
            layout.childControlWidth=false;
            layout.childControlHeight=false;
            layout.childForceExpandWidth=false;
            layout.childForceExpandHeight=false;
            row.gameObject.AddComponent<ContentSizeFitter>().horizontalFit=ContentSizeFitter.FitMode.PreferredSize;
            catalogPages[i]=row;
        }

        var byKind=new Dictionary<BuildingKind,BuildingDefinition>();
        foreach(BuildingDefinition definition in buildings)
            if(definition!=null && definition.prefab!=null && !byKind.ContainsKey(definition.kind))
                byKind.Add(definition.kind,definition);
        foreach(BuildingKind kind in ColonyCatalog) Add(kind,0);
        Add(BuildingKind.ScienceLab,1);
        Add(BuildingKind.MaterialsStation,1);
        AddTool(true);
        Add(BuildingKind.Refinery,1);
        AddTool(false);
        Add(BuildingKind.FuelWorks,1);
        RefreshButtons();
        SetCatalogTab(0);

        void Add(BuildingKind kind,int page)
        {
            if(!byKind.TryGetValue(kind,out var definition)) return;
            string caption=definition.title;
            if(kind==BuildingKind.MaterialsStation) caption="Станция добычи ископаемых";
            if(kind==BuildingKind.FuelWorks) caption="Цех производства ракетного топлива";
            string description=definition.description;
            if(definition.capacity>0) description+=$" Вместимость — {definition.capacity} чел.";
            var button=CreateCatalogCard(catalogPages[page],caption,description,definition.thumbnail,
                ()=>Select(definition),out var status,out var photo);
            selectionButtons.Add(definition,(button,status));
            if(definition.thumbnail==null)
                StartCoroutine(RenderThumbnail(definition,shot=>
                {
                    catalogRenders[definition]=shot;
                    ShowThumbnail(photo,shot);
                }));
        }

        void AddTool(bool conveyor)
        {
            string title=conveyor?"Герметичный промышленный конвейер":"Промышленный трубопровод";
            string purpose=conveyor?"Перевозит реголит от добывающей станции в перерабатывающий цех.":
                "Подаёт воду и концентрат в цех производства ракетного топлива.";
            var thumbnail=Resources.Load<Texture2D>(conveyor?"ConveyorCatalog":"PipelineCatalog");
            CreateCatalogCard(catalogPages[1],title,purpose,thumbnail,()=>BeginPipe(conveyor),out _,out _);
        }
    }

    Button CreateCatalogCard(Transform parent,string title,string description,Texture image,
        UnityEngine.Events.UnityAction select,out TextMeshProUGUI status,out RawImage photo)
    {
        var card=Rect(title,parent,TopLeft,TopLeft,Vector2.zero,new Vector2(282f,274f));
        var button=HudButton(card,select);
        var frame=Rect("Building illustration",card,TopLeft,TopLeft,new Vector2(10f,-10f),new Vector2(262f,131f));
        photo=Thumbnail(frame,image);
        var heading=Label(Rect("Building title",card,TopLeft,TopLeft,new Vector2(12f,-148f),new Vector2(258f,46f)),
            title,19,Light,FontStyles.Bold);
        FitHudText(heading,15f,19f);
        var details=Label(Rect("Building purpose",card,TopLeft,TopLeft,new Vector2(12f,-195f),new Vector2(258f,49f)),
            description,14,Muted);
        FitHudText(details,11f,14f);
        status=Label(Rect("Availability",card,TopLeft,TopLeft,new Vector2(12f,-249f),new Vector2(255f,19f)),
            "Доступно",12,Accent,FontStyles.Bold);
        return button;
    }

    void SetCatalogTab(int page)
    {
        for(int i=0;i<catalogPages.Length;i++)
        {
            bool current=i==page;
            catalogPages[i].gameObject.SetActive(current);
            catalogTabs[i].targetGraphic.color=current?Accent:HudTile;
            catalogTabTitles[i].color=current?Panel:Light;
        }
        catalogScroll.content=catalogPages[page];
        catalogScroll.StopMovement();
        catalogPages[page].anchoredPosition=Vector2.zero;
        catalogScroll.horizontalNormalizedPosition=0f;
    }

    void SetCatalogOpen(bool open)
    {
        if(panel==null || catalogOpen==open) return;
        catalogOpen=open;
        if(open)
        {
            Cancel();
            SetCatalogTab(0);
            panel.gameObject.SetActive(true);
        }
        catalogFade.blocksRaycasts=open;
        catalogFade.interactable=open;
    }

    Button HudButton(RectTransform rect,UnityEngine.Events.UnityAction action)
    {
        var button=MakeButton(rect,HudTile,action);
        Round((Image)button.targetGraphic);
        return button;
    }

    TextMeshProUGUI Centered(RectTransform parent,string text,int size,Color tint)
    {
        var label=Label(Stretch("Button label",parent,5f,1f,5f,1f),text,size,tint,FontStyles.Bold);
        label.alignment=TextAlignmentOptions.Center;
        FitHudText(label,size-3,size);
        return label;
    }

    static void FitHudText(TextMeshProUGUI label,float min,float max)
    {
        WrapText(label);
        label.enableAutoSizing=true;
        label.fontSizeMin=min;
        label.fontSizeMax=max;
    }

    static RectTransform Stretch(string name,Transform parent,float left,float bottom,float right,float top)
    {
        var rect=Rect(name,parent,Vector2.zero,Middle,Vector2.zero,Vector2.zero);
        rect.anchorMax=Vector2.one;
        rect.offsetMin=new Vector2(left,bottom);
        rect.offsetMax=new Vector2(-right,-top);
        return rect;
    }

    void Round(Image image)
    {
        if(cornerSprite==null)
        {
            const int size=32;
            var texture=new Texture2D(size,size,TextureFormat.RGBA32,false){name="HUD rounded corners"};
            texture.wrapMode=TextureWrapMode.Clamp;
            var pixels=new Color32[size*size];
            for(int y=0;y<size;y++)
            for(int x=0;x<size;x++)
            {
                var corner=new Vector2(Mathf.Max(0f,Mathf.Abs(x-15.5f)-6f),Mathf.Max(0f,Mathf.Abs(y-15.5f)-6f));
                pixels[y*size+x]=new Color32(255,255,255,(byte)(255f*Mathf.Clamp01(9.5f-corner.magnitude)));
            }
            texture.SetPixels32(pixels);
            texture.Apply(false,true);
            cornerSprite=Sprite.Create(texture,new Rect(0f,0f,size,size),Middle,100f,0,SpriteMeshType.FullRect,
                new Vector4(11f,11f,11f,11f));
        }
        image.sprite=cornerSprite;
        image.type=Image.Type.Sliced;
    }
}
