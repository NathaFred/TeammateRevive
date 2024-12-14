using R2API;
using RoR2;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using TeammateRevive.Common;
using TeammateRevive.Localization;
using TeammateRevive.Logging;
using TeammateRevive.Resources;

namespace TeammateRevive.ProgressBar
{
    /// <summary>
    /// Controls adding and updating progress bar.
    /// </summary>
    public class ProgressBarController
    {
        private static string DefaultName => Language.GetString(LanguageConsts.TEAMMATE_REVIVAL_UI_PLAYER);
        private static string Reviving => Language.GetString(LanguageConsts.TEAMMATE_REVIVAL_UI_PROGRESS_BAR_REVIVING);
        public static ProgressBarController Instance;

        private TextMeshProUGUI textComponent;
        private string currentName = DefaultName;
        private readonly CharArrayBuilder charArrayBuilder;
        public bool showing = false;
        Slider progressBar;
        public float progress;
        RoR2.UI.HUD hudRef;

        RectTransform healthbarTransform;
        RectTransform barRootTransform;
        RectTransform progressBarTransform;

        public ProgressBarController()
        {
            Log.Debug("Init ResurrectController");
            On.RoR2.UI.HUD.Awake += HUDOnAwake;
            Instance = this;
            // NOTE: this string splitting is required so class can internally keep track of individual parts and update them efficiently
            charArrayBuilder = new CharArrayBuilder("Reviving ", DefaultName, "  -  ", "000.0", "%");
        }

        public void UpdateText(string name, float progress = 0)
        {
            name = string.IsNullOrEmpty(name) ? DefaultName : name;

            if (currentName != name)
            {
                charArrayBuilder.UpdatePart(1, name);
                currentName = name;
            }

            charArrayBuilder.SetPaddedPercentagePart(3, progress);
            textComponent.SetCharArray(charArrayBuilder.Buffer, 0, charArrayBuilder.Length);

            Show();
        }

        public void SetFraction(float fraction)
        {
            if (progressBar == null) return;
            progressBar.value = fraction;
            progress = fraction;
            Show();
        }

        public void SetColor(Color color)
        {
            if(progressBar == null) return;
            progressBar.fillRect.GetComponent<Image>().color = color;
            Show();
        }

        public (Vector3 bottomLeftOffset, Vector2 size) GetBarPositionAndSize()
        {
            //find and set parent to the center cluster
            if(progressBar.transform.parent.name != "BottomCenterCluster")
            {
                var cluster = hudRef.mainUIPanel.transform.Find("SpringCanvas/BottomCenterCluster");
                if(cluster != null)
                {
                    progressBar.transform.SetParent(cluster);
                }
                else
                {
                    //fallback to the main panel
                    progressBar.transform.SetParent(hudRef.mainUIPanel.transform);
                }
            }

            Vector2 parentSize = GetParentSize(progressBarTransform);

            //fallback values in case healthbar is not found
            Vector2 size = new Vector2(Screen.width/3.5f, Screen.height/27f);
            Vector3 bottomLeftOffset = new Vector3(parentSize.x/2 - size.x/2, size.y * 6, 0);

            var healthBarRoot = hudRef.mainUIPanel.transform.Find("SpringCanvas/BottomLeftCluster/BarRoots/HealthbarRoot");
            var barRoots = hudRef.mainUIPanel.transform.Find("SpringCanvas/BottomLeftCluster/BarRoots");

            if (healthBarRoot == null || barRoots == null)
            {
                return (bottomLeftOffset, size);
            }

            if(healthbarTransform == null || barRootTransform == null)
            {
                healthbarTransform = healthBarRoot.GetComponent<RectTransform>();
                barRootTransform = barRoots.GetComponent<RectTransform>();
            }

            size = new Vector2(parentSize.x * 0.8f, healthbarTransform.rect.height);

            //use law of sines to get the depth of the healthbar after 6 degrees of rotation
            float depthOffset = barRootTransform.rect.width
                    / Mathf.Sin(90 * Mathf.Deg2Rad) 
                    * Mathf.Sin(-barRoots.parent.rotation.eulerAngles.y * Mathf.Deg2Rad);
                    
            bottomLeftOffset = new Vector3(parentSize.x/2 - size.x/2, healthbarTransform.GetBottomLeftOffset().y, depthOffset);

            return (bottomLeftOffset, size);
        }

        public void UpdatePositionAndSize()
        {
            var (bottomLeftOffset, size) = GetBarPositionAndSize();

            progressBarTransform.SetSizeInPixels(size.x, size.y);
            progressBarTransform.SetBottomLeftOffset(bottomLeftOffset.x, bottomLeftOffset.y);
            progressBarTransform.localScale = Vector3.one;
            progressBarTransform.localPosition = progressBarTransform.localPosition.SetZ(bottomLeftOffset.z);
        }

        public void Hide()
        {
            currentName = DefaultName;
            progressBar.GetComponent<CanvasGroup>().alpha = 0;
            showing = false;
        }

        public void Show()
        {
            if (showing) return;

            UpdatePositionAndSize();
            showing = true;
            progressBar.GetComponent<CanvasGroup>().alpha = 1;
            charArrayBuilder.UpdatePart(0, Reviving);
        }

        public void AttachProgressBar(RoR2.UI.HUD hud)
        {
            Log.Debug("AttachProgressBar");

            hudRef = hud;
            progressBar = CustomResources.progressBarPrefab.InstantiateClone("Revival Progress Bar").GetComponent<Slider>();
            progressBarTransform = progressBar.GetComponent<RectTransform>();
            textComponent = progressBar.GetComponentInChildren<TextMeshProUGUI>();
            textComponent.font = RoR2.UI.HGTextMeshProUGUI.defaultLanguageFont;
            Hide();
        }

        public void Destroy()
        {
            if (progressBar?.gameObject)
            {
                Object.Destroy(progressBar.gameObject);
            }
        }

        private void HUDOnAwake(On.RoR2.UI.HUD.orig_Awake orig, RoR2.UI.HUD self)
        {
            Log.Debug("HUDOnAwake");
            orig(self);
            AttachProgressBar(self);
        }

        private Vector2 GetParentSize(RectTransform transform)
        {
            RectTransform parent = transform.parent as RectTransform;

            return parent == null ? Vector2.zero : parent.rect.size;
        }
    }
}
