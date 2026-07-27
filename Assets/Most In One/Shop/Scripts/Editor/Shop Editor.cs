using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Solo.MOST_IN_ONE
{
    [CustomEditor(typeof(MOST_Shop))]
    public class MOST_Shop_Editor : Editor
    {
        SerializedProperty ShopCode, shopType, buyButton, shopItems, defaultElementIndex, targetMeshRenderer;
        ReorderableList _list;

        void OnEnable()
        {
            ShopCode = serializedObject.FindProperty("ShopCode");
            shopType = serializedObject.FindProperty("shopType");
            buyButton = serializedObject.FindProperty("buyButton");
            shopItems = serializedObject.FindProperty("shopItems");
            defaultElementIndex = serializedObject.FindProperty("defaultElementIndex");
            targetMeshRenderer = serializedObject.FindProperty("targetMeshRenderer");

            _list = new(serializedObject, shopItems, true, true, true, true)
            {
                drawHeaderCallback = r =>
                {
                    EditorGUI.LabelField(r, "Shop Items");
                },

                elementHeightCallback = index =>
                {
                    var element = shopItems.GetArrayElementAtIndex(index);
                    float h = EditorGUIUtility.singleLineHeight + 6f; // header

                    if (!element.isExpanded) return h;

                    float v = EditorGUIUtility.standardVerticalSpacing;

                    var isFree = element.FindPropertyRelative("isFree");
                    var price = element.FindPropertyRelative("price");
                    var itemButton = element.FindPropertyRelative("itemButton");
                    var selectedPart = element.FindPropertyRelative("SelectedPart");
                    var pointerPart = element.FindPropertyRelative("PointerPart");
                    var lockedPart = element.FindPropertyRelative("LockedPart");
                    var isIAP = element.FindPropertyRelative("isIAP");
                    var isPurchased = element.FindPropertyRelative("isPurchased");
                    var onSelect = element.FindPropertyRelative("OnSelect");
                    var onDeselect = element.FindPropertyRelative("OnDeselect");
                    var onPurchase = element.FindPropertyRelative("onPurchase");
                    var controlled = element.FindPropertyRelative("controlledGameObjects");
                    var materials = element.FindPropertyRelative("materials");

                    // sum expanded property heights
                    h += EditorGUI.GetPropertyHeight(isFree) + v;
                    h += EditorGUI.GetPropertyHeight(itemButton) + v;
                    h += EditorGUI.GetPropertyHeight(selectedPart) + v;
                    h += EditorGUI.GetPropertyHeight(pointerPart) + v;

                    if (!isFree.boolValue)
                    {
                        h += EditorGUI.GetPropertyHeight(lockedPart) + v;
                        h += EditorGUI.GetPropertyHeight(price) + v;
                    }

                    h += EditorGUI.GetPropertyHeight(isIAP) + v;
                    h += EditorGUI.GetPropertyHeight(isPurchased) + v;

                    h += EditorGUI.GetPropertyHeight(onSelect, true) + v;
                    h += EditorGUI.GetPropertyHeight(onDeselect, true) + v;
                    h += EditorGUI.GetPropertyHeight(onPurchase, true) + v;

                    var type = (ShopType)shopType.enumValueIndex;
                    if (type == ShopType.GameObjectControl)
                        h += EditorGUI.GetPropertyHeight(controlled, true) + v;
                    else
                        h += EditorGUI.GetPropertyHeight(materials, true) + v;

                    return h + 6f;
                },

                drawElementCallback = (rect, index, isActive, isFocused) =>
                {
                    var element = shopItems.GetArrayElementAtIndex(index);

                    // HEADER (gray with foldout)
                    var headerRect = new Rect(rect.x, rect.y + 2, rect.width, EditorGUIUtility.singleLineHeight);
                    var headerBg = EditorGUIUtility.isProSkin ? new Color(0.23f, 0.23f, 0.23f, 1f) : new Color(0.86f, 0.86f, 0.86f, 1f);
                    EditorGUI.DrawRect(headerRect, headerBg);

                    // space for label + foldout + quick toggles on the right
                    var labelRect = headerRect; labelRect.width -= 180f;

                    var isFree = element.FindPropertyRelative("isFree");
                    var isIAP = element.FindPropertyRelative("isIAP");
                    var isPurchased = element.FindPropertyRelative("isPurchased");
                    var price = element.FindPropertyRelative("price");

                    string priceText = isFree.boolValue ? "FREE" : $"Price: {price.intValue}";
                    string flags = (isPurchased.boolValue ? "✓Purchased  " : "") + (isIAP.boolValue ? "IAP" : "");
                    string label = $"Item {index + 1}  —  {priceText}  {flags}";

                    element.isExpanded = EditorGUI.Foldout(labelRect, element.isExpanded, label, true, EditorStyles.foldoutHeader);

                    // quick toggles (right side)
                    var r = new Rect(rect.xMax - 60, headerRect.y, 60, headerRect.height);
                    EditorGUI.PropertyField(r, isPurchased, GUIContent.none); r.x -= 60;
                    EditorGUI.PropertyField(r, isIAP, GUIContent.none); r.x -= 60;
                    EditorGUI.PropertyField(r, isFree, GUIContent.none);

                    if (!element.isExpanded) return;

                    // BODY (expanded fields)
                    float y = headerRect.yMax + 4f;
                    float v = EditorGUIUtility.standardVerticalSpacing;

                    void Draw(SerializedProperty p, string labelOverride = null, bool children = false)
                    {
                        var h = EditorGUI.GetPropertyHeight(p, children);
                        var fr = new Rect(rect.x, y, rect.width, h);
                        if (labelOverride == null) EditorGUI.PropertyField(fr, p, children);
                        else EditorGUI.PropertyField(fr, p, new GUIContent(labelOverride), children);
                        y += h + v;
                    }

                    var itemButton = element.FindPropertyRelative("itemButton");
                    var selectedPart = element.FindPropertyRelative("SelectedPart");
                    var pointerPart = element.FindPropertyRelative("PointerPart");
                    var lockedPart = element.FindPropertyRelative("LockedPart");
                    var onSelect = element.FindPropertyRelative("OnSelect");
                    var onDeselect = element.FindPropertyRelative("OnDeselect");
                    var onPurchase = element.FindPropertyRelative("onPurchase");
                    var controlled = element.FindPropertyRelative("controlledGameObjects");
                    var materials = element.FindPropertyRelative("materials");

                    Draw(isFree);
                    Draw(itemButton, "Item Button");
                    Draw(selectedPart, "Selected Part");
                    Draw(pointerPart, "Pointer Part");

                    if (!isFree.boolValue)
                    {
                        Draw(lockedPart, "Locked Part");
                        Draw(price);
                    }

                    Draw(isIAP);
                    Draw(isPurchased);
                    Draw(onSelect, "On Select", true);
                    Draw(onDeselect, "On Deselect", true);
                    Draw(onPurchase, "On Purchase", true);

                    if ((ShopType)shopType.enumValueIndex == ShopType.GameObjectControl)
                        Draw(controlled, "Controlled GameObjects", true);
                    else
                        Draw(materials, "Materials", true);
                },

                onAddCallback = l =>
                {
                    shopItems.arraySize++;
                    var newItem = shopItems.GetArrayElementAtIndex(shopItems.arraySize - 1);
                    newItem.isExpanded = true;

                    newItem.FindPropertyRelative("price").intValue = 0;
                    newItem.FindPropertyRelative("isIAP").boolValue = false;
                    newItem.FindPropertyRelative("isFree").boolValue = false;
                    newItem.FindPropertyRelative("isPurchased").boolValue = false;

                    var controlled = newItem.FindPropertyRelative("controlledGameObjects");
                    controlled?.ClearArray();
                    var mats = newItem.FindPropertyRelative("materials");
                    mats?.ClearArray();

                    serializedObject.ApplyModifiedProperties();
                }
            };
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(ShopCode);
            EditorGUILayout.PropertyField(shopType);
            if ((ShopType)shopType.enumValueIndex == ShopType.CustomEffect)
                EditorGUILayout.PropertyField(targetMeshRenderer);
            EditorGUILayout.PropertyField(buyButton);
            EditorGUILayout.PropertyField(defaultElementIndex);

            EditorGUILayout.Space();
            _list.DoLayoutList();

            serializedObject.ApplyModifiedProperties();
        }
    }
}
