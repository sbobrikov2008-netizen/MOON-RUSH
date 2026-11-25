using System.Collections.Generic;
using TMPro;
using UnityEngine;

public sealed partial class MoonRushConstruction
{
    readonly Dictionary<string, Material> operationsMaterials = new Dictionary<string, Material>();
    ColonyIndustry.Pipe inspectedPipe;
    bool removalPending;
    LunarColonyOperations operations;

    Material OperationsMaterial(string name, Color color)
    {
        if (!operationsMaterials.TryGetValue(name, out Material material))
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name, color = color };
            material.SetFloat("_Smoothness", .37f);
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

    void InspectPipe(ColonyIndustry.Pipe pipe)
    {
        CloseInspection();
        inspectedPipe = pipe;
        panel.gameObject.SetActive(false);
        inspectionTitle.text = "Промышленный трубопровод";
        inspectionDescription.text = "Перевозит материалы между промышленными сооружениями.";
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
