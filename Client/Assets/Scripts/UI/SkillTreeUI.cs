using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SkillTreeUI : MonoBehaviour
{
    private GameObject panel;
    private bool isVisible = false;
    private Text unspentPointsText;
    private Text bonusSummaryText;

    private struct SkillRow
    {
        public string SkillName;
        public string DisplayName;
        public string BonusDescription;
        public Text ValueText;
        public Button AddButton;
        public Button RemoveButton;
    }

    private List<SkillRow> skillRows = new List<SkillRow>();

    private void Start()
    {
        ClientPlayerStats.OnSkillsUpdated += OnSkillsUpdated;
        NetworkManager.OnSkillAllocationResponse += OnSkillAllocationResponse;
        BuildUI();
        panel.SetActive(false);
    }

    private void OnDestroy()
    {
        ClientPlayerStats.OnSkillsUpdated -= OnSkillsUpdated;
        NetworkManager.OnSkillAllocationResponse -= OnSkillAllocationResponse;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.K))
        {
            TogglePanel();
        }
    }

    public void TogglePanel()
    {
        isVisible = !isVisible;
        panel.SetActive(isVisible);
        if (isVisible)
        {
            RefreshUI();
        }
    }

    private void BuildUI()
    {
        // Find Canvas
        Canvas mainCanvas = null;
        foreach (Canvas c in FindObjectsOfType<Canvas>())
        {
            if (c.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                mainCanvas = c;
                break;
            }
        }
        if (mainCanvas == null)
        {
            Debug.LogError("[SkillTreeUI] No Canvas found!");
            return;
        }

        // Create panel
        panel = new GameObject("SkillTreePanel");
        panel.transform.SetParent(mainCanvas.transform, false);

        var panelRect = panel.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(420, 480);
        panelRect.anchoredPosition = Vector2.zero;

        var panelImage = panel.AddComponent<Image>();
        panelImage.color = new Color(0.1f, 0.1f, 0.15f, 0.95f);

        // Title
        CreateText(panel.transform, "SkillTreeTitle", "SKILL TREE (K)",
            new Vector2(0, 210), new Vector2(400, 30), 18, Color.white, TextAnchor.MiddleCenter);

        // Unspent points
        unspentPointsText = CreateText(panel.transform, "UnspentPoints", "Unspent Points: 0",
            new Vector2(0, 180), new Vector2(400, 25), 16, Color.yellow, TextAnchor.MiddleCenter);

        // Divider line
        var divider = new GameObject("Divider");
        divider.transform.SetParent(panel.transform, false);
        var divRect = divider.AddComponent<RectTransform>();
        divRect.anchorMin = new Vector2(0.5f, 0.5f);
        divRect.anchorMax = new Vector2(0.5f, 0.5f);
        divRect.sizeDelta = new Vector2(380, 2);
        divRect.anchoredPosition = new Vector2(0, 165);
        var divImg = divider.AddComponent<Image>();
        divImg.color = new Color(0.4f, 0.4f, 0.5f, 1f);

        // Skill rows
        string[][] skills = new string[][]
        {
            new[] { "Strength", "Strength", "+1 melee ATK per point" },
            new[] { "RangedSkill", "Ranged Skill", "+1 ranged ATK per point" },
            new[] { "MagicPower", "Magic Power", "+1 grenade/AoE dmg per point" },
            new[] { "Health", "Health", "+10 max HP per point" },
            new[] { "MovementSpeed", "Move Speed", "+0.5 speed per point" },
            new[] { "AttackSpeed", "Attack Speed", "+0.05 atk/sec per point" },
            new[] { "Intelligence", "Intelligence", "+2% XP, +1% gold per point" },
        };

        float startY = 140f;
        float rowHeight = 38f;

        for (int i = 0; i < skills.Length; i++)
        {
            float y = startY - (i * rowHeight);
            var row = CreateSkillRow(panel.transform, skills[i][0], skills[i][1], skills[i][2], y);
            skillRows.Add(row);
        }

        // Bonus summary at bottom
        bonusSummaryText = CreateText(panel.transform, "BonusSummary", "",
            new Vector2(0, -155), new Vector2(380, 60), 12, new Color(0.7f, 0.8f, 0.7f), TextAnchor.MiddleCenter);

        // Close button
        CreateButton(panel.transform, "CloseBtn", "X",
            new Vector2(190, 220), new Vector2(30, 30),
            new Color(0.6f, 0.2f, 0.2f), () => { isVisible = false; panel.SetActive(false); });
    }

    private SkillRow CreateSkillRow(Transform parent, string skillName, string displayName, string bonusDesc, float yPos)
    {
        var row = new SkillRow();
        row.SkillName = skillName;
        row.DisplayName = displayName;
        row.BonusDescription = bonusDesc;

        // Label
        CreateText(parent, $"Label_{skillName}", displayName,
            new Vector2(-120, yPos), new Vector2(150, 30), 14, Color.white, TextAnchor.MiddleLeft);

        // Bonus description
        CreateText(parent, $"Desc_{skillName}", bonusDesc,
            new Vector2(-120, yPos - 12), new Vector2(150, 20), 10, new Color(0.6f, 0.6f, 0.6f), TextAnchor.MiddleLeft);

        // Value display
        row.ValueText = CreateText(parent, $"Value_{skillName}", "0",
            new Vector2(80, yPos), new Vector2(50, 30), 16, Color.cyan, TextAnchor.MiddleCenter);

        // [-] button
        row.RemoveButton = CreateButton(parent, $"Remove_{skillName}", "-",
            new Vector2(40, yPos), new Vector2(30, 30),
            new Color(0.5f, 0.2f, 0.2f), () => OnRemoveClicked(skillName));

        // [+] button
        row.AddButton = CreateButton(parent, $"Add_{skillName}", "+",
            new Vector2(120, yPos), new Vector2(30, 30),
            new Color(0.2f, 0.5f, 0.2f), () => OnAddClicked(skillName));

        return row;
    }

    private Text CreateText(Transform parent, string name, string content, Vector2 position, Vector2 size, int fontSize, Color color, TextAnchor alignment)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        var rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        var text = obj.AddComponent<Text>();
        text.text = content;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (text.font == null) text.font = Font.CreateDynamicFontFromOSFont("Arial", fontSize);
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = alignment;
        return text;
    }

    private Button CreateButton(Transform parent, string name, string label, Vector2 position, Vector2 size, Color bgColor, Action onClick)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        var rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        var img = obj.AddComponent<Image>();
        img.color = bgColor;
        var btn = obj.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(() => onClick());

        // Button text
        var textObj = new GameObject("Text");
        textObj.transform.SetParent(obj.transform, false);
        var textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        var text = textObj.AddComponent<Text>();
        text.text = label;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (text.font == null) text.font = Font.CreateDynamicFontFromOSFont("Arial", 14);
        text.fontSize = 14;
        text.color = Color.white;
        text.alignment = TextAnchor.MiddleCenter;

        return btn;
    }

    private void OnAddClicked(string skillName)
    {
        var networkManager = GameManager.Instance?.NetworkManager;
        if (networkManager != null)
        {
            _ = networkManager.SendSkillAllocationRequest(skillName, 1, false);
        }
    }

    private void OnRemoveClicked(string skillName)
    {
        var networkManager = GameManager.Instance?.NetworkManager;
        if (networkManager != null)
        {
            _ = networkManager.SendSkillAllocationRequest(skillName, 1, true);
        }
    }

    private void OnSkillsUpdated(NetworkMessages.SkillTreeData skills)
    {
        if (isVisible)
        {
            RefreshUI();
        }
    }

    private void OnSkillAllocationResponse(NetworkMessages.SkillAllocationResponseMessage response)
    {
        if (!response.Success)
        {
            Debug.Log($"[SkillTreeUI] Allocation failed: {response.Message}");
            var uiManager = GameManager.Instance?.UIManager;
            if (uiManager != null)
            {
                uiManager.ShowNotification(response.Message, Color.red);
            }
        }
    }

    private void RefreshUI()
    {
        var stats = FindObjectOfType<ClientPlayerStats>();
        if (stats == null) return;

        unspentPointsText.text = $"Unspent Points: {stats.SkillPoints}";
        bool hasPoints = stats.SkillPoints > 0;

        int[] values = new int[]
        {
            stats.SkillStrength,
            stats.SkillRangedSkill,
            stats.SkillMagicPower,
            stats.SkillHealth,
            stats.SkillMovementSpeed,
            stats.SkillAttackSpeed,
            stats.SkillIntelligence
        };

        for (int i = 0; i < skillRows.Count && i < values.Length; i++)
        {
            skillRows[i].ValueText.text = values[i].ToString();
            skillRows[i].AddButton.interactable = hasPoints;
            skillRows[i].RemoveButton.interactable = values[i] > 0;
        }

        // Bonus summary
        string summary = "";
        if (stats.SkillStrength > 0) summary += $"+{stats.SkillStrength} Melee ATK  ";
        if (stats.SkillRangedSkill > 0) summary += $"+{stats.SkillRangedSkill} Ranged ATK  ";
        if (stats.SkillMagicPower > 0) summary += $"+{stats.SkillMagicPower} Magic DMG  ";
        if (stats.SkillHealth > 0) summary += $"+{stats.SkillHealth * 10} Max HP  ";
        if (stats.SkillMovementSpeed > 0) summary += $"+{stats.SkillMovementSpeed * 0.5f:F1} Speed  ";
        if (stats.SkillAttackSpeed > 0) summary += $"+{stats.SkillAttackSpeed * 0.05f:F2} Atk/s  ";
        if (stats.SkillIntelligence > 0) summary += $"+{stats.SkillIntelligence * 2}% XP, +{stats.SkillIntelligence}% Gold";

        bonusSummaryText.text = summary.Length > 0 ? summary : "No skill bonuses allocated yet";
    }
}
