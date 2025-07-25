using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Dokkaebi.Core.Data;
using Dokkaebi.Utilities;

namespace Dokkaebi.UI
{
    public class StatusEffectDisplay : MonoBehaviour
    {
        [SerializeField] private Image icon;
        [SerializeField] private TextMeshProUGUI durationText;
        [SerializeField] private TextMeshProUGUI stacksText;
        [SerializeField] private Image cooldownOverlay;

        private StatusEffectData effectData;

        public void Initialize(StatusEffectData effect)
        {
            effectData = effect;
            SmartLogger.Log($"[StatusEffectDisplay Initialize] Initializing effect: {effect?.effectName}, GameObject: {gameObject.name}, InstanceID: {GetInstanceID()}", LogCategory.UI, this);
            UpdateDisplay();
        }

        private void OnDestroy()
        {
            SmartLogger.Log($"[StatusEffectDisplay OnDestroy] Effect: {effectData?.effectName}, GameObject: {gameObject.name}, InstanceID: {GetInstanceID()}, Parent: {transform.parent?.name ?? "null"}, Active: {gameObject.activeSelf}, ActiveInHierarchy: {gameObject.activeInHierarchy}", LogCategory.UI, this);
            
            // Log additional component states
            if (icon != null)
                SmartLogger.Log($"[StatusEffectDisplay OnDestroy] Icon component enabled: {icon.enabled}, visible: {icon.IsActive()}", LogCategory.UI, this);
            if (durationText != null)
                SmartLogger.Log($"[StatusEffectDisplay OnDestroy] Duration text enabled: {durationText.enabled}, text: {durationText.text}", LogCategory.UI, this);
            if (cooldownOverlay != null)
                SmartLogger.Log($"[StatusEffectDisplay OnDestroy] Cooldown overlay enabled: {cooldownOverlay.enabled}, visible: {cooldownOverlay.IsActive()}", LogCategory.UI, this);
        }

        private void Update()
        {
            if (effectData != null)
            {
                UpdateDisplay();
            }
        }

        private void UpdateDisplay()
        {
            if (effectData == null) return;

            // Update icon
            if (icon != null)
            {
                icon.sprite = effectData.icon;
            }

            // Update duration
            if (durationText != null)
            {
                if (effectData.isPermanent)
                {
                    durationText.text = "∞";
                }
                else
                {
                    durationText.text = effectData.duration.ToString();
                }
            }

            // Update stacks
            if (stacksText != null)
            {
                if (effectData.isStackable && effectData.maxStacks > 1)
                {
                    stacksText.text = effectData.maxStacks.ToString();
                    stacksText.gameObject.SetActive(true);
                }
                else
                {
                    stacksText.gameObject.SetActive(false);
                }
            }

            // Update cooldown overlay
            if (cooldownOverlay != null)
            {
                cooldownOverlay.gameObject.SetActive(false);
            }
        }
    }
} 