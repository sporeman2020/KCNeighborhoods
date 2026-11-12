using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

using Assets;
using Assets.Code;

namespace kcRoofColor
{
    // One saved region: a name, a color, and the buildings whose roofs it covers.
    public class kcRoofRegion
    {
        public string name;
        public float[] color;
        public List<string> guids = new List<string>();
    }

    // "Regions" window. Paint mode lets you drag a rectangle over the map. Roofs inside it are
    // collected, then you name the group, pick a color, and save it as a region.
    public class kcRegionPainter : MonoBehaviour
    {
        private const float kcMinDragPixels = 8f;

        private Font kcFont;
        private GameObject kcBlocker;
        private GameObject kcSelectionBox;
        private RectTransform kcBoxRect;
        private GameObject kcWindow;
        private RectTransform kcWindowRect;
        private RectTransform kcListRoot;
        private InputField kcNameField;
        private Text kcStatus;
        private Text kcPaintLabel;

        private bool kcPainting;
        private bool kcDragging;
        private Vector2 kcDragStart;
        private Color kcChosenColor;
        private List<Building> kcPending = new List<Building>();

        private void Awake()
        {
            kcFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
            kcChosenColor = kcRoofColorPanel.kcPresets[0];
            kcBuildCanvas();
            kcRefreshList();
        }

        private void Update()
        {
            if (!kcPainting || kcWindowRect == null)
                return;

            Vector2 mouse = Input.mousePosition;

            // Start a drag only outside the window, so clicking its buttons does not start one.
            if (Input.GetMouseButtonDown(0) && !RectTransformUtility.RectangleContainsScreenPoint(kcWindowRect, mouse, null))
            {
                kcDragging = true;
                kcDragStart = mouse;
                kcSelectionBox.SetActive(true);
            }

            if (kcDragging)
            {
                kcSetBox(kcDragStart, mouse);
                if (Input.GetMouseButtonUp(0))
                {
                    kcDragging = false;
                    kcSelectionBox.SetActive(false);
                    if (Vector2.Distance(kcDragStart, mouse) >= kcMinDragPixels)
                        kcCollect(kcDragStart, mouse);
                }
            }
        }

        private void kcBuildCanvas()
        {
            GameObject canvasObject = new GameObject("kcRegionCanvas", typeof(Canvas), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 501;
            Transform root = canvasObject.transform;

            if (FindObjectOfType<EventSystem>() == null)
                new GameObject("kcEventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            // Blocker: created first so later elements draw above it. Active only while painting,
            // so the drag does not also click the map or buttons.
            kcBlocker = kcNewObject(root, "kcBlocker", typeof(RectTransform), typeof(Image));
            kcStretch(kcBlocker);
            kcBlocker.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
            kcBlocker.SetActive(false);

            // Rectangle shown while dragging. Anchored bottom-left so its position is in screen pixels.
            kcSelectionBox = kcNewObject(root, "kcSelectionBox", typeof(RectTransform), typeof(Image));
            Image boxImage = kcSelectionBox.GetComponent<Image>();
            boxImage.color = new Color(1f, 0.85f, 0.2f, 0.25f);
            boxImage.raycastTarget = false;
            kcBoxRect = kcSelectionBox.GetComponent<RectTransform>();
            kcBoxRect.anchorMin = Vector2.zero;
            kcBoxRect.anchorMax = Vector2.zero;
            kcBoxRect.pivot = Vector2.zero;
            kcSelectionBox.SetActive(false);

            // Toggle button, top-left.
            Button toggle = kcMakeButton(root, "Regions", kcOnToggleWindow);
            RectTransform toggleRect = toggle.GetComponent<RectTransform>();
            toggleRect.anchorMin = new Vector2(0f, 1f);
            toggleRect.anchorMax = new Vector2(0f, 1f);
            toggleRect.pivot = new Vector2(0f, 1f);
            toggleRect.anchoredPosition = new Vector2(10f, -10f);
            toggleRect.sizeDelta = new Vector2(140f, 36f);

            // Window, stacked vertically under the toggle.
            kcWindow = kcNewObject(root, "kcRegionWindow", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            kcWindow.GetComponent<Image>().color = new Color(0.08f, 0.09f, 0.11f, 0.92f);
            kcWindowRect = kcWindow.GetComponent<RectTransform>();
            kcWindowRect.anchorMin = new Vector2(0f, 1f);
            kcWindowRect.anchorMax = new Vector2(0f, 1f);
            kcWindowRect.pivot = new Vector2(0f, 1f);
            kcWindowRect.anchoredPosition = new Vector2(10f, -56f);
            kcWindowRect.sizeDelta = new Vector2(290f, 0f);

            VerticalLayoutGroup vlg = kcWindow.GetComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(10, 10, 10, 10);
            vlg.spacing = 6f;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            kcWindow.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            Text title = kcMakeText(kcWindow.transform, "Regions", 20, TextAnchor.MiddleLeft);
            kcLayout(title.gameObject, 0f, 28f);

            Button paint = kcMakeButton(kcWindow.transform, "Paint: off", kcOnTogglePaint);
            kcLayout(paint.gameObject, 0f, 32f);
            kcPaintLabel = paint.GetComponentInChildren<Text>();

            kcNameField = kcMakeInputField(kcWindow.transform);
            kcLayout(kcNameField.gameObject, 0f, 30f);

            // Color choice row, reusing the roof presets.
            GameObject swatchRow = kcNewObject(kcWindow.transform, "kcColorRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            HorizontalLayoutGroup hl = swatchRow.GetComponent<HorizontalLayoutGroup>();
            hl.spacing = 4f;
            hl.childControlWidth = true;
            hl.childControlHeight = true;
            hl.childForceExpandWidth = false;
            hl.childForceExpandHeight = false;
            kcLayout(swatchRow, 0f, 28f);
            for (int i = 0; i < kcRoofColorPanel.kcPresets.Length; i++)
                kcMakeSwatch(swatchRow.transform, kcRoofColorPanel.kcPresets[i]);

            kcStatus = kcMakeText(kcWindow.transform, "Press Paint, then drag a rectangle over the roofs.", 14, TextAnchor.UpperLeft);
            kcLayout(kcStatus.gameObject, 0f, 44f);

            Button save = kcMakeButton(kcWindow.transform, "Save region", kcSaveRegion);
            kcLayout(save.gameObject, 0f, 32f);

            GameObject list = kcNewObject(kcWindow.transform, "kcRegionList", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            VerticalLayoutGroup lv = list.GetComponent<VerticalLayoutGroup>();
            lv.spacing = 4f;
            lv.childControlWidth = true;
            lv.childControlHeight = true;
            lv.childForceExpandWidth = true;
            lv.childForceExpandHeight = false;
            list.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            kcListRoot = list.GetComponent<RectTransform>();

            kcWindow.SetActive(false);
        }

        private void kcOnToggleWindow()
        {
            bool open = !kcWindow.activeSelf;
            kcWindow.SetActive(open);
            if (open)
                kcRefreshList();
            else if (kcPainting)
                kcOnTogglePaint();
        }

        private void kcOnTogglePaint()
        {
            kcPainting = !kcPainting;
            kcDragging = false;
            kcSelectionBox.SetActive(false);
            kcBlocker.SetActive(kcPainting);
            kcPaintLabel.text = kcPainting ? "Paint: on" : "Paint: off";
            kcStatus.text = kcPainting ? "Drag a rectangle over the roofs to select them." : "Paint is off.";
        }

        private void kcSetBox(Vector2 a, Vector2 b)
        {
            Vector2 min = Vector2.Min(a, b);
            Vector2 max = Vector2.Max(a, b);
            kcBoxRect.anchoredPosition = min;
            kcBoxRect.sizeDelta = max - min;
        }

        // Collects roofs whose building origin projects inside the dragged screen rectangle.
        private void kcCollect(Vector2 a, Vector2 b)
        {
            Camera cam = Camera.main;
            if (cam == null)
                cam = FindObjectOfType<Camera>();
            if (cam == null)
            {
                kcStatus.text = "No camera found.";
                return;
            }

            Vector2 min = Vector2.Min(a, b);
            Vector2 max = Vector2.Max(a, b);
            kcPending.Clear();

            Building[] all = FindObjectsOfType<Building>();
            for (int i = 0; i < all.Length; i++)
            {
                Building building = all[i];
                if (building == null)
                    continue;

                Vector3 screen = cam.WorldToScreenPoint(building.transform.position);
                if (screen.z < 0f)
                    continue;
                if (screen.x < min.x || screen.x > max.x || screen.y < min.y || screen.y > max.y)
                    continue;
                if (kcRoofColorMod.kcFindRoofIndex(building) < 0)
                    continue;

                kcPending.Add(building);
            }

            kcStatus.text = kcPending.Count + " roofs selected. Name the region and press Save.";
        }

        private void kcSaveRegion()
        {
            if (kcPending.Count == 0)
            {
                kcStatus.text = "Drag a rectangle over some roofs first.";
                return;
            }

            string name = kcNameField.text == null ? "" : kcNameField.text.Trim();
            if (name.Length == 0)
                name = "Region " + (kcRoofColorMod.kcRegions.Count + 1);

            kcRoofRegion region = new kcRoofRegion();
            region.name = name;
            region.color = kcToFloats(kcChosenColor);

            for (int i = 0; i < kcPending.Count; i++)
            {
                Building building = kcPending[i];
                if (building == null)
                    continue;
                if (kcRoofColorMod.kcApplyRoofColor(building, kcChosenColor))
                {
                    string key = building.guid.ToString();
                    region.guids.Add(key);
                    kcRoofColorMod.kcSavedColors[key] = kcToFloats(kcChosenColor);
                }
            }

            kcRoofColorMod.kcRegions.Add(region);
            kcPending.Clear();
            kcNameField.text = "";
            kcStatus.text = "Saved '" + name + "' with " + region.guids.Count + " roofs.";
            kcRefreshList();
        }

        private void kcApplyRegion(kcRoofRegion region)
        {
            Color color = kcRoofColorMod.kcToColor(region.color);
            int applied = 0;

            for (int i = 0; i < region.guids.Count; i++)
            {
                string key = region.guids[i];
                Building building = Player.inst == null ? null : Player.inst.GetBuilding(new Guid(key));
                if (building == null)
                    continue;
                if (kcRoofColorMod.kcApplyRoofColor(building, color))
                {
                    kcRoofColorMod.kcSavedColors[key] = kcToFloats(color);
                    applied++;
                }
            }

            kcStatus.text = "Applied '" + region.name + "' to " + applied + " roofs.";
        }

        private void kcDeleteRegion(kcRoofRegion region)
        {
            kcRoofColorMod.kcRegions.Remove(region);
            kcStatus.text = "Deleted '" + region.name + "'. Roof colors already applied stay as they are.";
            kcRefreshList();
        }

        private void kcRefreshList()
        {
            if (kcListRoot == null)
                return;

            for (int i = kcListRoot.childCount - 1; i >= 0; i--)
                Destroy(kcListRoot.GetChild(i).gameObject);

            for (int i = 0; i < kcRoofColorMod.kcRegions.Count; i++)
                kcAddRegionRow(kcRoofColorMod.kcRegions[i]);
        }

        private void kcAddRegionRow(kcRoofRegion region)
        {
            GameObject row = kcNewObject(kcListRoot, "kcRegionRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            HorizontalLayoutGroup hl = row.GetComponent<HorizontalLayoutGroup>();
            hl.spacing = 4f;
            hl.childControlWidth = true;
            hl.childControlHeight = true;
            hl.childForceExpandWidth = false;
            hl.childForceExpandHeight = true;
            kcLayout(row, 0f, 30f);

            GameObject chip = kcNewObject(row.transform, "kcChip", typeof(RectTransform), typeof(Image));
            chip.GetComponent<Image>().color = kcRoofColorMod.kcToColor(region.color);
            kcLayout(chip, 24f, 24f);

            Text label = kcMakeText(row.transform, region.name, 15, TextAnchor.MiddleLeft);
            LayoutElement labelSize = kcLayout(label.gameObject, 0f, 24f);
            labelSize.flexibleWidth = 1f;

            Button apply = kcMakeButton(row.transform, "Apply", () => kcApplyRegion(region));
            kcLayout(apply.gameObject, 56f, 26f);

            Button delete = kcMakeButton(row.transform, "Delete", () => kcDeleteRegion(region));
            kcLayout(delete.gameObject, 56f, 26f);
        }

        private void kcMakeSwatch(Transform parent, Color color)
        {
            GameObject go = kcNewObject(parent, "kcSwatch", typeof(RectTransform), typeof(Image), typeof(Button));
            Image image = go.GetComponent<Image>();
            image.color = color;
            kcLayout(go, 24f, 24f);

            Button button = go.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => kcChooseColor(color));
        }

        private void kcChooseColor(Color color)
        {
            kcChosenColor = color;
            kcStatus.text = "Color chosen. Drag over roofs, then Save region.";
        }

        private InputField kcMakeInputField(Transform parent)
        {
            GameObject go = kcNewObject(parent, "kcNameField", typeof(RectTransform), typeof(Image), typeof(InputField));
            Image image = go.GetComponent<Image>();
            image.color = new Color(0.16f, 0.18f, 0.22f, 1f);

            Text text = kcMakeText(go.transform, "", 16, TextAnchor.MiddleLeft);
            kcStretch(text.gameObject);

            InputField field = go.GetComponent<InputField>();
            field.targetGraphic = image;
            field.textComponent = text;
            field.text = "";
            return field;
        }

        private Button kcMakeButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick)
        {
            GameObject go = kcNewObject(parent, "kcButton", typeof(RectTransform), typeof(Image), typeof(Button));
            Image image = go.GetComponent<Image>();
            image.color = new Color(0.22f, 0.26f, 0.32f, 1f);

            Button button = go.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);

            Text text = kcMakeText(go.transform, label, 16, TextAnchor.MiddleCenter);
            text.raycastTarget = false;
            kcStretch(text.gameObject);
            return button;
        }

        private Text kcMakeText(Transform parent, string content, int size, TextAnchor alignment)
        {
            GameObject go = kcNewObject(parent, "kcText", typeof(RectTransform), typeof(Text));
            Text text = go.GetComponent<Text>();
            text.font = kcFont;
            text.fontSize = size;
            text.color = Color.white;
            text.alignment = alignment;
            text.text = content;
            return text;
        }

        private static GameObject kcNewObject(Transform parent, string name, params Type[] parts)
        {
            GameObject go = new GameObject(name, parts);
            go.transform.SetParent(parent, false);
            return go;
        }

        private static LayoutElement kcLayout(GameObject go, float width, float height)
        {
            LayoutElement element = go.GetComponent<LayoutElement>();
            if (element == null)
                element = go.AddComponent<LayoutElement>();
            if (width > 0f)
            {
                element.preferredWidth = width;
                element.minWidth = width;
            }
            if (height > 0f)
            {
                element.preferredHeight = height;
                element.minHeight = height;
            }
            return element;
        }

        private static void kcStretch(GameObject go)
        {
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static float[] kcToFloats(Color color)
        {
            return new float[] { color.r, color.g, color.b };
        }
    }
}
