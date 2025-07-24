using UnityEngine;
using UnityEditor;
using Dokkaebi.UI;
using Dokkaebi.Core;
using System.Collections.Generic;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using System.Reflection;

namespace Dokkaebi.Editor
{
    public class UISetupHelper : MonoBehaviour
    {
        // Auto-disable A* update checker to prevent insecure connection errors
        [InitializeOnLoadMethod]
        static void DisableAstarUpdateCheckerAutomatic()
        {
            if (!EditorPrefs.GetBool("AstarDisableUpdateCheck", false))
            {
                EditorPrefs.SetBool("AstarDisableUpdateCheck", true);
                Debug.Log("[UISetupHelper] Auto-disabled A* Pathfinding Project update checker to prevent insecure connection errors.");
            }
        }
        [MenuItem("Dokkaebi/Fix UI Connections")]
        public static void FixUIConnections()
        {
            // Find UIManager
            UIManager uiManager = FindObjectOfType<UIManager>();
            if (uiManager == null)
            {
                Debug.LogError("UIManager not found in scene!");
                return;
            }

            // Find or create panels
            Canvas mainCanvas = FindObjectOfType<Canvas>();
            if (mainCanvas == null)
            {
                Debug.LogError("No Canvas found in scene!");
                return;
            }

            // Try to find existing panels by name
            Transform hudPanel = mainCanvas.transform.Find("HudPanel");
            Transform unitInfoPanel = mainCanvas.transform.Find("UnitInfoPanel");
            Transform abilitySelectionPanel = mainCanvas.transform.Find("AbilitySelectionPanel");
            Transform turnPhasePanel = mainCanvas.transform.Find("TurnPhasePanel");
            Transform playerResourcePanel = mainCanvas.transform.Find("PlayerResourcePanel");

            // Create missing panels
            if (hudPanel == null)
            {
                hudPanel = CreateUIPanel(mainCanvas.transform, "HudPanel");
                Debug.Log("Created HudPanel");
            }

            if (abilitySelectionPanel == null)
            {
                abilitySelectionPanel = CreateUIPanel(mainCanvas.transform, "AbilitySelectionPanel");
                Debug.Log("Created AbilitySelectionPanel");
            }

            if (turnPhasePanel == null)
            {
                turnPhasePanel = CreateUIPanel(mainCanvas.transform, "TurnPhasePanel");
                Debug.Log("Created TurnPhasePanel");
            }

            if (playerResourcePanel == null)
            {
                playerResourcePanel = CreateUIPanel(mainCanvas.transform, "PlayerResourcePanel");
                Debug.Log("Created PlayerResourcePanel");
            }

            // Connect references using reflection to set private fields
            var uiManagerType = typeof(UIManager);
            
            SetSerializedField(uiManager, "hudPanel", hudPanel?.gameObject);
            SetSerializedField(uiManager, "unitInfoPanel", unitInfoPanel?.gameObject);
            SetSerializedField(uiManager, "abilitySelectionPanel", abilitySelectionPanel?.gameObject);
            SetSerializedField(uiManager, "turnPhasePanel", turnPhasePanel?.gameObject);
            SetSerializedField(uiManager, "playerResourcePanel", playerResourcePanel?.gameObject);

            // Find turn system
            DokkaebiTurnSystemCore turnSystem = FindObjectOfType<DokkaebiTurnSystemCore>();
            if (turnSystem != null)
            {
                SetSerializedField(uiManager, "turnSystem", turnSystem);
            }

            // Mark dirty for saving
            EditorUtility.SetDirty(uiManager);
            
            Debug.Log("UI connections fixed! Check UIManager component.");
        }

        [MenuItem("Dokkaebi/Disable A* Update Checker")]
        public static void DisableAstarUpdateChecker()
        {
            EditorPrefs.SetBool("AstarDisableUpdateCheck", true);
            Debug.Log("A* Pathfinding Project update checker disabled to prevent insecure connection errors.");
        }

        [MenuItem("Dokkaebi/Fix Missing Prefab References")]
        public static void FixMissingPrefabReferences()
        {
            Debug.Log("Fixing missing prefab references...");
            
            // Fix TeamStatusUI missing prefab reference
            TeamStatusUI teamStatusUI = FindObjectOfType<TeamStatusUI>();
            if (teamStatusUI != null)
            {
                GameObject unitStatusPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/UnitStatusOverlayItem_Source.prefab");
                if (unitStatusPrefab != null)
                {
                    SetSerializedField(teamStatusUI, "unitStatusItemPrefab", unitStatusPrefab);
                    EditorUtility.SetDirty(teamStatusUI);
                    Debug.Log("Fixed TeamStatusUI unitStatusItemPrefab reference");
                }
                else
                {
                    Debug.LogError("Could not find UnitStatusOverlayItem_Source.prefab!");
                }
            }
            
            // Fix other missing UI prefab references
            var unitInfoPanels = FindObjectsOfType<UnitInfoPanel>();
            foreach (var panel in unitInfoPanels)
            {
                GameObject statusEffectPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/StatusEffectIcon_Prefab_Base.prefab");
                if (statusEffectPrefab != null)
                {
                    SetSerializedField(panel, "statusEffectPrefab", statusEffectPrefab);
                    EditorUtility.SetDirty(panel);
                    Debug.Log($"Fixed UnitInfoPanel statusEffectPrefab reference on {panel.name}");
                }
            }
            
            // Fix UnitStatusUI missing prefab references
            var unitStatusUIs = FindObjectsOfType<UnitStatusUI>();
            foreach (var ui in unitStatusUIs)
            {
                GameObject statusEffectIconPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/StatusEffectIcon_Prefab_Base.prefab");
                GameObject cooldownDisplayPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/CooldownIconPrefabBase.prefab");
                
                if (statusEffectIconPrefab != null)
                {
                    SetSerializedField(ui, "statusEffectIconPrefab", statusEffectIconPrefab);
                }
                
                if (cooldownDisplayPrefab != null)
                {
                    SetSerializedField(ui, "cooldownDisplayPrefab", cooldownDisplayPrefab);
                }
                
                EditorUtility.SetDirty(ui);
                Debug.Log($"Fixed UnitStatusUI prefab references on {ui.name}");
            }
            
            Debug.Log("Finished fixing missing prefab references!");
        }

        [MenuItem("Dokkaebi/Create AbilityUI")]
        public static void CreateAbilityUI()
        {
            // Find the main Canvas or create one if it doesn't exist
            Canvas mainCanvas = FindObjectOfType<Canvas>();
            if (mainCanvas == null)
            {
                GameObject canvasObj = new GameObject("Canvas");
                mainCanvas = canvasObj.AddComponent<Canvas>();
                mainCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasObj.AddComponent<CanvasScaler>();
                canvasObj.AddComponent<GraphicRaycaster>();
                Debug.Log("Created main Canvas for UI.");
            }

            // Create AbilitySelectionUI GameObject
            GameObject abilityUIObj = new GameObject("AbilitySelectionUI");
            abilityUIObj.transform.SetParent(mainCanvas.transform, false);

            // Add RectTransform and position it
            RectTransform rectTransform = abilityUIObj.AddComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0, 0);
            rectTransform.anchorMax = new Vector2(1, 0);
            rectTransform.anchoredPosition = new Vector2(0, 100);
            rectTransform.sizeDelta = new Vector2(-100, 150);

            // Add background panel
            Image backgroundImage = abilityUIObj.AddComponent<Image>();
            backgroundImage.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);

            // Add AbilitySelectionUI component
            AbilitySelectionUI abilityUI = abilityUIObj.AddComponent<AbilitySelectionUI>();

            // Create Unit Name Text
            GameObject unitNameObj = new GameObject("UnitNameText");
            unitNameObj.transform.SetParent(abilityUIObj.transform, false);
            RectTransform unitNameRect = unitNameObj.AddComponent<RectTransform>();
            unitNameRect.anchorMin = new Vector2(0.05f, 0.7f);
            unitNameRect.anchorMax = new Vector2(0.95f, 0.95f);
            unitNameRect.offsetMin = Vector2.zero;
            unitNameRect.offsetMax = Vector2.zero;
            
            TextMeshProUGUI unitNameText = unitNameObj.AddComponent<TextMeshProUGUI>();
            unitNameText.text = "Select Unit";
            unitNameText.fontSize = 18;
            unitNameText.alignment = TextAlignmentOptions.Center;

            // Create ability buttons container
            GameObject buttonsContainer = new GameObject("AbilityButtonsContainer");
            buttonsContainer.transform.SetParent(abilityUIObj.transform, false);
            RectTransform buttonsRect = buttonsContainer.AddComponent<RectTransform>();
            buttonsRect.anchorMin = new Vector2(0.05f, 0.1f);
            buttonsRect.anchorMax = new Vector2(0.95f, 0.65f);
            buttonsRect.offsetMin = Vector2.zero;
            buttonsRect.offsetMax = Vector2.zero;

            // Add HorizontalLayoutGroup for ability buttons
            HorizontalLayoutGroup layoutGroup = buttonsContainer.AddComponent<HorizontalLayoutGroup>();
            layoutGroup.spacing = 10;
            layoutGroup.childControlWidth = false;
            layoutGroup.childControlHeight = false;
            layoutGroup.childForceExpandWidth = false;
            layoutGroup.childForceExpandHeight = false;

            // Create ability buttons (typically 4-6 abilities)
            var abilityButtons = new List<AbilitySelectionUI.AbilityButton>();
            for (int i = 0; i < 6; i++)
            {
                GameObject buttonObj = new GameObject($"AbilityButton_{i}");
                buttonObj.transform.SetParent(buttonsContainer.transform, false);

                // Set up RectTransform
                RectTransform buttonRect = buttonObj.AddComponent<RectTransform>();
                buttonRect.sizeDelta = new Vector2(80, 60);

                // Add Button component
                Button button = buttonObj.AddComponent<Button>();
                Image buttonImage = buttonObj.AddComponent<Image>();
                buttonImage.color = Color.white;

                // Create ability icon
                GameObject iconObj = new GameObject("Icon");
                iconObj.transform.SetParent(buttonObj.transform, false);
                RectTransform iconRect = iconObj.AddComponent<RectTransform>();
                iconRect.anchorMin = new Vector2(0.1f, 0.3f);
                iconRect.anchorMax = new Vector2(0.9f, 0.9f);
                iconRect.offsetMin = Vector2.zero;
                iconRect.offsetMax = Vector2.zero;
                Image iconImage = iconObj.AddComponent<Image>();
                iconImage.preserveAspect = true;

                // Create cooldown overlay
                GameObject cooldownObj = new GameObject("CooldownOverlay");
                cooldownObj.transform.SetParent(buttonObj.transform, false);
                RectTransform cooldownRect = cooldownObj.AddComponent<RectTransform>();
                cooldownRect.anchorMin = Vector2.zero;
                cooldownRect.anchorMax = Vector2.one;
                cooldownRect.offsetMin = Vector2.zero;
                cooldownRect.offsetMax = Vector2.zero;
                Image cooldownOverlay = cooldownObj.AddComponent<Image>();
                cooldownOverlay.color = new Color(0.5f, 0.5f, 0.5f, 0.7f);
                cooldownObj.SetActive(false);

                // Create cooldown text
                GameObject cooldownTextObj = new GameObject("CooldownText");
                cooldownTextObj.transform.SetParent(cooldownObj.transform, false);
                RectTransform cooldownTextRect = cooldownTextObj.AddComponent<RectTransform>();
                cooldownTextRect.anchorMin = Vector2.zero;
                cooldownTextRect.anchorMax = Vector2.one;
                cooldownTextRect.offsetMin = Vector2.zero;
                cooldownTextRect.offsetMax = Vector2.zero;
                TextMeshProUGUI cooldownText = cooldownTextObj.AddComponent<TextMeshProUGUI>();
                cooldownText.text = "0";
                cooldownText.fontSize = 20;
                cooldownText.alignment = TextAlignmentOptions.Center;
                cooldownText.color = Color.white;

                // Create aura cost text
                GameObject costTextObj = new GameObject("AuraCostText");
                costTextObj.transform.SetParent(buttonObj.transform, false);
                RectTransform costTextRect = costTextObj.AddComponent<RectTransform>();
                costTextRect.anchorMin = new Vector2(0, 0);
                costTextRect.anchorMax = new Vector2(1, 0.3f);
                costTextRect.offsetMin = Vector2.zero;
                costTextRect.offsetMax = Vector2.zero;
                TextMeshProUGUI costText = costTextObj.AddComponent<TextMeshProUGUI>();
                costText.text = "0";
                costText.fontSize = 12;
                costText.alignment = TextAlignmentOptions.Center;
                costText.color = Color.cyan;

                // Create AbilityButton struct
                var abilityButton = new AbilitySelectionUI.AbilityButton
                {
                    button = button,
                    icon = iconImage,
                    cooldownOverlay = cooldownOverlay,
                    cooldownText = cooldownText,
                    auraCostText = costText,
                    abilityIndex = i
                };
                abilityButtons.Add(abilityButton);

                // Initially hide buttons
                buttonObj.SetActive(false);
            }

            // Create targeting indicator
            GameObject targetingObj = new GameObject("TargetingIndicator");
            targetingObj.transform.SetParent(abilityUIObj.transform, false);
            RectTransform targetingRect = targetingObj.AddComponent<RectTransform>();
            targetingRect.anchorMin = new Vector2(0, 0.65f);
            targetingRect.anchorMax = new Vector2(1, 0.7f);
            targetingRect.offsetMin = Vector2.zero;
            targetingRect.offsetMax = Vector2.zero;
            
            Image targetingImage = targetingObj.AddComponent<Image>();
            targetingImage.color = new Color(1f, 1f, 0f, 0.3f);
            targetingObj.SetActive(false);

            // Create targeting instructions text
            GameObject instructionsObj = new GameObject("TargetingInstructionsText");
            instructionsObj.transform.SetParent(abilityUIObj.transform, false);
            RectTransform instructionsRect = instructionsObj.AddComponent<RectTransform>();
            instructionsRect.anchorMin = new Vector2(0.05f, 0.7f);
            instructionsRect.anchorMax = new Vector2(0.95f, 0.95f);
            instructionsRect.offsetMin = Vector2.zero;
            instructionsRect.offsetMax = Vector2.zero;
            
            TextMeshProUGUI instructionsText = instructionsObj.AddComponent<TextMeshProUGUI>();
            instructionsText.text = "Select an ability target";
            instructionsText.fontSize = 14;
            instructionsText.alignment = TextAlignmentOptions.Center;
            instructionsText.color = Color.yellow;
            instructionsObj.SetActive(false);

            // Use reflection to set the private fields
            var abilityButtonsField = typeof(AbilitySelectionUI).GetField("abilityButtons", BindingFlags.NonPublic | BindingFlags.Instance);
            var targetingIndicatorField = typeof(AbilitySelectionUI).GetField("targetingIndicator", BindingFlags.NonPublic | BindingFlags.Instance);
            var targetingInstructionsTextField = typeof(AbilitySelectionUI).GetField("targetingInstructionsText", BindingFlags.NonPublic | BindingFlags.Instance);
            var unitNameTextField = typeof(AbilitySelectionUI).GetField("unitNameText", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);

            if (abilityButtonsField != null)
                abilityButtonsField.SetValue(abilityUI, abilityButtons.ToArray());
            
            if (targetingIndicatorField != null)
                targetingIndicatorField.SetValue(abilityUI, targetingObj);
                
            if (targetingInstructionsTextField != null)
                targetingInstructionsTextField.SetValue(abilityUI, instructionsText);
                
            if (unitNameTextField != null)
                unitNameTextField.SetValue(abilityUI, unitNameText);

            // Find and connect to UnitSelectionController
            UnitSelectionController unitSelectionController = FindObjectOfType<UnitSelectionController>();
            if (unitSelectionController != null)
            {
                var abilityUIField = typeof(UnitSelectionController).GetField("abilityUI", BindingFlags.NonPublic | BindingFlags.Instance);
                if (abilityUIField != null)
                {
                    abilityUIField.SetValue(unitSelectionController, abilityUI);
                    Debug.Log("Connected AbilitySelectionUI to UnitSelectionController!");
                }
                else
                {
                    Debug.LogWarning("Could not find abilityUI field in UnitSelectionController. You may need to assign it manually in the Inspector.");
                }
            }

            // Initially hide the UI
            abilityUIObj.SetActive(false);

            Debug.Log("AbilitySelectionUI created and configured! The UI will appear when you select a unit.");
        }

        private static Transform CreateUIPanel(Transform parent, string name)
        {
            GameObject panel = new GameObject(name);
            panel.transform.SetParent(parent);
            
            RectTransform rectTransform = panel.AddComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            
            panel.AddComponent<CanvasGroup>();
            
            // Start panels inactive
            panel.SetActive(false);
            
            return panel.transform;
        }
        
        private static void SetSerializedField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, 
                System.Reflection.BindingFlags.NonPublic | 
                System.Reflection.BindingFlags.Instance);
            
            if (field != null)
            {
                field.SetValue(target, value);
            }
            else
            {
                Debug.LogWarning($"Field '{fieldName}' not found on type {target.GetType().Name}");
            }
        }
    }
} 