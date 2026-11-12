using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

using Assets;
using Assets.Code;

namespace kcRoofColor
{
    // Entry point. The loader attaches this component and calls Preload and SceneLoaded.
    public class kcRoofColorMod : MonoBehaviour
    {
        public const string kcModName = "RoofColorSelector";
        public const string kcSaveIdentifier = "RoofColorSelectorV1";
        public const string kcCopyName = "kcRoofCopy";
        public const string kcRegionSaveIdentifier = "RoofColorRegionsV1";

        public static KCModHelper kcHelper;
        public static kcRoofColorPanel kcPanel;
        public static Dictionary<string, float[]> kcSavedColors = new Dictionary<string, float[]>();
        public static List<kcRoofRegion> kcRegions = new List<kcRoofRegion>();

        private EventHandler<OnSaveEvent> kcSaveHandler;
        private EventHandler<OnLoadedEvent> kcLoadHandler;

        private void Preload(KCModHelper helper)
        {
            kcHelper = helper;
            helper.Log("RoofColorSelector: preload");
        }

        private void SceneLoaded(KCModHelper helper)
        {
            if (kcPanel != null)
                Destroy(kcPanel.gameObject);

            if (kcSaveHandler != null)
                Broadcast.OnSaveEvent.Unlisten(kcSaveHandler);
            if (kcLoadHandler != null)
                Broadcast.OnLoadedEvent.Unlisten(kcLoadHandler);

            GameObject root = new GameObject("kcRoofColorRoot");
            kcPanel = root.AddComponent<kcRoofColorPanel>();
            root.AddComponent<kcRegionPainter>();

            kcSaveHandler = Broadcast.OnSaveEvent.Listen(kcOnSave);
            kcLoadHandler = Broadcast.OnLoadedEvent.Listen(kcOnLoaded);
        }

        public void kcOnSave(object sender, OnSaveEvent saveEvent)
        {
            try
            {
                string json = JsonConvert.SerializeObject(kcSavedColors);
                LoadSave.SaveDataGeneric(kcModName, kcSaveIdentifier, json);
                LoadSave.SaveDataGeneric(kcModName, kcRegionSaveIdentifier, JsonConvert.SerializeObject(kcRegions));
            }
            catch (Exception ex)
            {
                if (kcHelper != null) kcHelper.Log(ex.ToString());
            }
        }

        public void kcOnLoaded(object sender, OnLoadedEvent loadedEvent)
        {
            try
            {
                kcSavedColors = new Dictionary<string, float[]>();
                string json = LoadSave.ReadDataGeneric(kcModName, kcSaveIdentifier);
                if (json != null)
                {
                    Dictionary<string, float[]> loaded = JsonConvert.DeserializeObject<Dictionary<string, float[]>>(json);
                    if (loaded != null)
                        kcSavedColors = loaded;
                }

                kcRegions = new List<kcRoofRegion>();
                string regionJson = LoadSave.ReadDataGeneric(kcModName, kcRegionSaveIdentifier);
                if (regionJson != null)
                {
                    List<kcRoofRegion> loadedRegions = JsonConvert.DeserializeObject<List<kcRoofRegion>>(regionJson);
                    if (loadedRegions != null)
                        kcRegions = loadedRegions;
                }

                foreach (KeyValuePair<string, float[]> entry in kcSavedColors)
                {
                    Building building = Player.inst.GetBuilding(new Guid(entry.Key));
                    if (building != null)
                        kcApplyRoofColor(building, kcToColor(entry.Value));
                }
            }
            catch (Exception ex)
            {
                if (kcHelper != null) kcHelper.Log(ex.ToString());
            }
        }

        // Index of the roof material slot, or -1 if the building has no renderer named like a roof.
        public static int kcFindRoofIndex(Building building)
        {
            if (building == null || building.fullMaterial == null || building.renderersWithBuildingShader == null)
                return -1;

            int count = Math.Min(building.fullMaterial.Count, building.renderersWithBuildingShader.Count);
            for (int i = 0; i < count; i++)
            {
                Renderer renderer = building.renderersWithBuildingShader[i];
                if (renderer != null && renderer.name.ToLower().Contains("roof"))
                    return i;
            }
            return -1;
        }

        // Tints only the roof material of this one building. The first change makes a private copy
        // of the material, so other buildings that share the original are not affected.
        public static bool kcApplyRoofColor(Building building, Color color)
        {
            int index = kcFindRoofIndex(building);
            if (index < 0)
                return false;

            Material material = building.fullMaterial[index];
            if (material == null)
                return false;

            if (material.name != kcCopyName)
            {
                Material original = material;
                Material copy = new Material(original);
                copy.name = kcCopyName;
                building.fullMaterial[index] = copy;

                Renderer renderer = building.renderersWithBuildingShader[index];
                if (renderer != null)
                {
                    Material[] shared = renderer.sharedMaterials;
                    for (int j = 0; j < shared.Length; j++)
                    {
                        if (shared[j] == original)
                            shared[j] = copy;
                    }
                    renderer.sharedMaterials = shared;
                }
                material = copy;
            }

            material.color = color;
            return true;
        }

        // Re-applies a saved color to a building when it is selected or loaded.
        public static void kcApplySaved(Building building)
        {
            if (building == null)
                return;
            float[] saved;
            if (kcSavedColors.TryGetValue(building.guid.ToString(), out saved))
                kcApplyRoofColor(building, kcToColor(saved));
        }

        public static Color kcToColor(float[] rgb)
        {
            return new Color(rgb[0], rgb[1], rgb[2], 1f);
        }
    }

    // Small swatch bar shown only while a building with a roof is selected.
    public class kcRoofColorPanel : MonoBehaviour
    {
        // Fixed presets. Edit these to change the swatches.
        public static readonly Color[] kcPresets = new Color[]
        {
            new Color(0.91f, 0.91f, 0.89f),   // off-white
            new Color(0.29f, 0.35f, 0.41f),   // slate
            new Color(0.55f, 0.23f, 0.17f),   // brick red
            new Color(0.77f, 0.40f, 0.23f),   // terracotta
            new Color(0.18f, 0.42f, 0.23f),   // forest green
            new Color(0.12f, 0.23f, 0.41f),   // navy
            new Color(0.79f, 0.64f, 0.15f),   // ochre
            new Color(0.17f, 0.17f, 0.17f)    // charcoal
        };

        private GameObject kcBar;
        private Building kcSelected;

        private void Awake()
        {
            kcBuildCanvas();
            StartCoroutine(kcWatchSelection());
        }

        private void kcBuildCanvas()
        {
            GameObject canvasObject = new GameObject("kcRoofColorCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            kcBar = new GameObject("kcSwatchBar", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            kcBar.transform.SetParent(canvasObject.transform, false);

            RectTransform rect = kcBar.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 90f);
            rect.sizeDelta = new Vector2(kcPresets.Length * 38f + 12f, 44f);

            HorizontalLayoutGroup layout = kcBar.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 6f;
            layout.padding = new RectOffset(6, 6, 6, 6);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            for (int i = 0; i < kcPresets.Length; i++)
                kcMakeSwatch(kcPresets[i]);

            if (FindObjectOfType<EventSystem>() == null)
                new GameObject("kcEventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            kcBar.SetActive(false);
        }

        private void kcMakeSwatch(Color color)
        {
            GameObject swatch = new GameObject("kcSwatch", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            swatch.transform.SetParent(kcBar.transform, false);

            Image image = swatch.GetComponent<Image>();
            image.color = color;

            LayoutElement size = swatch.GetComponent<LayoutElement>();
            size.minWidth = 32f;
            size.preferredWidth = 32f;
            size.minHeight = 32f;
            size.preferredHeight = 32f;

            Button button = swatch.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => kcOnSwatchClicked(color));
        }

        // Polls the selection at a low rate instead of every frame.
        private IEnumerator kcWatchSelection()
        {
            WaitForSeconds wait = new WaitForSeconds(0.25f);
            while (true)
            {
                Building selected = null;
                if (GameUI.inst != null)
                    selected = GameUI.inst.GetBuildingSelected();

                if (selected != kcSelected)
                {
                    kcSelected = selected;
                    kcRefresh();
                }
                yield return wait;
            }
        }

        private void kcRefresh()
        {
            bool show = kcSelected != null && kcRoofColorMod.kcFindRoofIndex(kcSelected) >= 0;
            kcBar.SetActive(show);
            if (show)
                kcRoofColorMod.kcApplySaved(kcSelected);
        }

        private void kcOnSwatchClicked(Color color)
        {
            if (kcSelected == null)
                return;
            if (!kcRoofColorMod.kcApplyRoofColor(kcSelected, color))
                return;

            kcRoofColorMod.kcSavedColors[kcSelected.guid.ToString()] = new float[] { color.r, color.g, color.b };
        }
    }
}
