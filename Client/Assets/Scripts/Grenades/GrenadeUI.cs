using UnityEngine;
using UnityEngine.UI;
using System.Collections;

namespace CombatMechanix.Unity
{
    /// <summary>
    /// UI component for grenade system
    /// </summary>
    public class GrenadeUI : MonoBehaviour
    {
        [Header("Grenade Count UI")]
        public Text fragGrenadeCountText;
        public Text smokeGrenadeCountText;
        public Text flashGrenadeCountText;

        [Header("Grenade Selection UI")]
        public Image fragGrenadeIcon;
        public Image smokeGrenadeIcon;
        public Image flashGrenadeIcon;

        [Header("Aiming UI")]
        public GameObject aimingPanel;
        public Text aimingInstructionText;
        public Image crosshair;

        [Header("Message System")]
        public GameObject messagePanel;
        public Text messageText;

        [Header("Colors")]
        public Color selectedColor = Color.yellow;
        public Color unselectedColor = Color.white;
        public Color lowCountColor = Color.red;
        public Color normalCountColor = Color.white;

        private string currentSelectedType = "frag_grenade";
        private Coroutine messageCoroutine;

        void Start()
        {
            // Initialize UI state
            SetSelectedGrenadeType("frag_grenade");
            SetAimingMode(false, "");

            if (messagePanel != null)
            {
                messagePanel.SetActive(false);
            }
        }

        /// <summary>
        /// Update grenade count displays
        /// </summary>
        public void UpdateGrenadeCounts(int fragCount, int smokeCount, int flashCount)
        {
            // Update count texts
            if (fragGrenadeCountText != null)
            {
                fragGrenadeCountText.text = fragCount.ToString();
                fragGrenadeCountText.color = fragCount <= 0 ? lowCountColor : normalCountColor;
            }

            if (smokeGrenadeCountText != null)
            {
                smokeGrenadeCountText.text = smokeCount.ToString();
                smokeGrenadeCountText.color = smokeCount <= 0 ? lowCountColor : normalCountColor;
            }

            if (flashGrenadeCountText != null)
            {
                flashGrenadeCountText.text = flashCount.ToString();
                flashGrenadeCountText.color = flashCount <= 0 ? lowCountColor : normalCountColor;
            }

            // Update icon opacity based on availability
            UpdateIconOpacity(fragGrenadeIcon, fragCount > 0);
            UpdateIconOpacity(smokeGrenadeIcon, smokeCount > 0);
            UpdateIconOpacity(flashGrenadeIcon, flashCount > 0);
        }

        /// <summary>
        /// Set the selected grenade type
        /// </summary>
        public void SetSelectedGrenadeType(string grenadeType)
        {
            currentSelectedType = grenadeType;

            // Reset all icons to unselected
            SetIconColor(fragGrenadeIcon, unselectedColor);
            SetIconColor(smokeGrenadeIcon, unselectedColor);
            SetIconColor(flashGrenadeIcon, unselectedColor);

            // Highlight selected icon
            switch (grenadeType)
            {
                case "frag_grenade":
                    SetIconColor(fragGrenadeIcon, selectedColor);
                    break;
                case "smoke_grenade":
                    SetIconColor(smokeGrenadeIcon, selectedColor);
                    break;
                case "flash_grenade":
                    SetIconColor(flashGrenadeIcon, selectedColor);
                    break;
            }
        }

        /// <summary>
        /// Set aiming mode UI state
        /// </summary>
        public void SetAimingMode(bool isAiming, string grenadeType)
        {
            if (aimingPanel != null)
            {
                aimingPanel.SetActive(isAiming);
            }

            if (crosshair != null)
            {
                crosshair.gameObject.SetActive(isAiming);
            }

            if (aimingInstructionText != null && isAiming)
            {
                string grenadeName = GetGrenadeDisplayName(grenadeType);
                aimingInstructionText.text = $"Aiming {grenadeName}\nLeft click to throw, Right click to cancel";
            }
        }

        /// <summary>
        /// Show a temporary message
        /// </summary>
        public void ShowMessage(string message, float duration = 3f)
        {
            if (messageText != null)
            {
                messageText.text = message;
            }

            if (messagePanel != null)
            {
                messagePanel.SetActive(true);
            }

            // Stop previous message coroutine if running
            if (messageCoroutine != null)
            {
                StopCoroutine(messageCoroutine);
            }

            messageCoroutine = StartCoroutine(HideMessageAfterDelay(duration));
        }

        private void UpdateIconOpacity(Image icon, bool available)
        {
            if (icon == null) return;

            Color color = icon.color;
            color.a = available ? 1f : 0.3f;
            icon.color = color;
        }

        private void SetIconColor(Image icon, Color color)
        {
            if (icon == null) return;

            Color newColor = color;
            newColor.a = icon.color.a; // Preserve opacity
            icon.color = newColor;
        }

        private string GetGrenadeDisplayName(string grenadeType)
        {
            return grenadeType switch
            {
                "frag_grenade" => "Frag Grenade",
                "smoke_grenade" => "Smoke Grenade",
                "flash_grenade" => "Flash Grenade",
                _ => "Unknown Grenade"
            };
        }

        private IEnumerator HideMessageAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);

            if (messagePanel != null)
            {
                messagePanel.SetActive(false);
            }

            messageCoroutine = null;
        }

        // Button click handlers for UI interaction
        public void OnFragGrenadeClicked()
        {
            var grenadeInput = FindObjectOfType<GrenadeInputHandler>();
            if (grenadeInput != null)
            {
                // Simulate key press for grenade type 1
                SetSelectedGrenadeType("frag_grenade");
            }
        }

        public void OnSmokeGrenadeClicked()
        {
            var grenadeInput = FindObjectOfType<GrenadeInputHandler>();
            if (grenadeInput != null)
            {
                SetSelectedGrenadeType("smoke_grenade");
            }
        }

        public void OnFlashGrenadeClicked()
        {
            var grenadeInput = FindObjectOfType<GrenadeInputHandler>();
            if (grenadeInput != null)
            {
                SetSelectedGrenadeType("flash_grenade");
            }
        }
    }
}