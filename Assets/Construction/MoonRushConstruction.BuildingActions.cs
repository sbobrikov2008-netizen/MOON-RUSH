using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class MoonRushConstruction
{
    RectTransform actionsPanel;
    TextMeshProUGUI actionsHint, deleteActionText, mapActionText, researchActionText;
    Button researchAction, depositAction, mapAction, deleteAction, infoAction;

    void CreateBuildingActions(Transform canvas)
    {
        actionsPanel=Rect("Selected building actions",canvas,Bottom,Bottom,new Vector2(0f,26f),new Vector2(851f,91f));
        Round(Fill(actionsPanel,HudSurface));
        actionsHint=Label(Rect("Action status",actionsPanel,TopLeft,TopLeft,new Vector2(12f,-6f),new Vector2(824f,27f)),
            "",15,Muted);
        FitHudText(actionsHint,12f,15f);
        researchAction=Action("Исследовать образцы",0f,()=>Research(false),out researchActionText);
        depositAction=Action("Найти месторождения",1f,()=>Research(true),out _);
        mapAction=Action("Показать месторождения",2f,ToggleDeposits,out mapActionText);
        infoAction=Action("Информация",3f,()=>
        {
            inspectionPanel.gameObject.SetActive(!inspectionPanel.gameObject.activeSelf);
            if(inspectionPanel.gameObject.activeSelf) UpdateInspectionScale();
        },out _);
        deleteAction=Action("Удалить",4f,()=>
        {
            RemoveInspectedBuilding();
            RefreshBuildingActions();
        },out deleteActionText);
        actionsPanel.gameObject.SetActive(false);

        Button Action(string caption,float slot,UnityEngine.Events.UnityAction callback,out TextMeshProUGUI label)
        {
            var tile=Rect("Building action",actionsPanel,TopLeft,TopLeft,new Vector2(10f+slot*166f,-38f),new Vector2(159f,43f));
            var button=HudButton(tile,callback);
            label=Centered(tile,caption,15,Light);
            return button;
        }
    }

    bool HasResearchLab()
    {
        foreach(var building in placed)
            if(building!=null && building.Kind==BuildingKind.ScienceLab) return true;
        return false;
    }

    void SetBuildingActions(BuildingInstance selection)
    {
        if(actionsPanel==null) return;
        bool show=selection!=null && (selection.Kind==BuildingKind.ScienceLab ||
            (selection.Kind==BuildingKind.LandingPad && !HasResearchLab()));
        actionsPanel.gameObject.SetActive(show);
    }

    void RefreshBuildingActions()
    {
        if(actionsPanel==null || !actionsPanel.gameObject.activeSelf || inspectedBuilding==null) return;
        bool lab=inspectedBuilding.Kind==BuildingKind.ScienceLab;
        bool power=!lab || inspectedBuilding.HasPower;
        bool busy=industry.ResearchRemaining>0f;
        bool ready=power && !busy && industry.Samples>0 && (lab || !HasResearchLab());
        researchAction.interactable=ready;
        depositAction.interactable=ready;
        mapAction.interactable=lab && power && deposits.Count>0;
        depositAction.gameObject.SetActive(lab);
        mapAction.gameObject.SetActive(lab);
        infoAction.gameObject.SetActive(lab);
        deleteAction.gameObject.SetActive(lab);
        researchActionText.text=lab?"Исследовать образцы":"Отправить образцы на Землю";
        bool mapVisible=depositMap!=null && depositMap.activeSelf;
        mapAction.targetGraphic.color=mapVisible?(Color)new Color32(32,96,105,255):HudTile;
        mapActionText.text=mapVisible?"Скрыть месторождения":"Показать месторождения";
        deleteActionText.text=removalPending?"Подтвердить удаление":"Удалить";
        actionsHint.text=!power ? "Лаборатория обесточена. Восстановите питание для научных действий" :
            busy ? $"Исследование образца · осталось {Mathf.CeilToInt(industry.ResearchRemaining)} с" :
            $"Образцы: {industry.Samples}    ·    Открытые месторождения: {deposits.Count}" +
                (industry.Samples==0?"    ·    Для исследования нужен образец реголита":"");
    }
}
