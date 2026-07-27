using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Solo.MOST_IN_ONE
{
    [Serializable]
    public class ShopItem // Item Data / Settings
    {
        [HideInInspector] public Button itemButton;
        [HideInInspector] public GameObject LockedPart, SelectedPart, PointerPart;
        public int price;
        public bool isIAP, isFree, isPurchased;
        public UnityEvent OnSelect, OnDeselect, onPurchase;
        public List<GameObject> controlledGameObjects;
        public List<Material> materials;
    }

    public enum ShopType { GameObjectControl, CustomEffect }

    public class MOST_Shop : MonoBehaviour
    {
        public string ShopCode;
        public ShopType shopType;
        public List<ShopItem> shopItems;
        public int defaultElementIndex = -1;
        public MeshRenderer targetMeshRenderer;
        public Button buyButton;

        const string SaveKeyPurchasedItems = "PurchasedItems";
        const string SaveKeySelectedItem = "SelectedItem";
        int selectedItemIndex = -1, previewItemIndex = -1;

        void Awake()
        {
            LoadPurchasedItems();
            LoadSelectedItem();
            InitializeButtons();
            buyButton.gameObject.SetActive(false);
        }

        void InitializeButtons()
        {
            for (int i = 0; i < shopItems.Count; i++)
            {
                int index = i;
                shopItems[index].itemButton.onClick.AddListener(() => SelectItem(index));
            }
            buyButton.onClick.AddListener(PurchaseSelectedItem);
        }

        public void SelectItem(int itemIndex)
        {
            if (itemIndex < 0 || itemIndex >= shopItems.Count) return;

            ShopItem item = shopItems[itemIndex];
            if (previewItemIndex > 0) shopItems[previewItemIndex].OnDeselect?.Invoke();
            else shopItems[PlayerPrefs.GetInt(SaveKeySelectedItem)].OnDeselect?.Invoke();

            item.OnSelect?.Invoke();
            foreach (var it in shopItems) it.PointerPart.SetActive(false);
            item.PointerPart.SetActive(true);

            if (item.isPurchased)
            {
                selectedItemIndex = itemIndex;
                previewItemIndex = -1;
                SaveSelectedItem();
                ApplyShopEffect(itemIndex);
                buyButton.gameObject.SetActive(false);
            }
            else
            {
                previewItemIndex = itemIndex;
                ApplyShopEffect(itemIndex);
                buyButton.gameObject.SetActive(true);
            }
        }

        public void PurchaseSelectedItem()
        {
            if (previewItemIndex < 0 || previewItemIndex >= shopItems.Count) return;
            PurchaseItem(previewItemIndex);
        }

        public void PurchaseItem(int itemIndex)
        {
            if (itemIndex < 0 || itemIndex >= shopItems.Count) return;

            ShopItem item = shopItems[itemIndex];
            if (item.isPurchased) return;

            if (item.isFree || (item.isIAP || PlayerPrefs.GetInt("GemsCurrency") >= item.price))
            {
                item.isPurchased = true;
                item.onPurchase?.Invoke();
                selectedItemIndex = itemIndex;
                SaveSelectedItem();
                SavePurchasedItems();
                ApplyShopEffect(itemIndex);
                buyButton.gameObject.SetActive(false);
            }
            else Debug.Log("Not enough currency!");
        }

        void ApplyShopEffect(int itemIndex)
        {
            if (shopItems[itemIndex].isPurchased)
            {
                foreach (var item in shopItems) item.SelectedPart.SetActive(false);
                shopItems[itemIndex].SelectedPart.SetActive(true);
            }

            if (shopType == ShopType.GameObjectControl) EnableGameObject(itemIndex);
            else shopItems[itemIndex].OnSelect?.Invoke();
        }

        void EnableGameObject(int itemIndex)
        {
            foreach (var item in shopItems)
                if (item.controlledGameObjects != null)
                    foreach (var obj in item.controlledGameObjects)
                        if (obj != null) obj.SetActive(false);

            if (itemIndex >= 0 && itemIndex < shopItems.Count && shopItems[itemIndex].controlledGameObjects != null)
                foreach (var obj in shopItems[itemIndex].controlledGameObjects)
                    if (obj != null) obj.SetActive(true);
        }

        void SavePurchasedItems()
        {
            List<string> purchasedItems = new();
            for (int i = 0; i < shopItems.Count; i++)
                if (shopItems[i].isPurchased)
                {
                    purchasedItems.Add(i.ToString());
                    if (shopItems[i].LockedPart) shopItems[i].LockedPart.SetActive(false);
                }

            PlayerPrefs.SetString(SaveKeyPurchasedItems + ShopCode, string.Join(",", purchasedItems));
            PlayerPrefs.Save();
        }

        void LoadPurchasedItems()
        {
            if (PlayerPrefs.HasKey(SaveKeyPurchasedItems + ShopCode))
            {
                string[] purchasedItems = PlayerPrefs.GetString(SaveKeyPurchasedItems + ShopCode).Split(',');
                foreach (var num in purchasedItems)
                    if (int.TryParse(num, out int n) && n < shopItems.Count && shopItems[n] != null)
                    {
                        shopItems[n].isPurchased = true;
                        if (shopItems[n].LockedPart) shopItems[n].LockedPart.SetActive(false);
                    }
            }
        }

        void SaveSelectedItem() => PlayerPrefs.SetInt(SaveKeySelectedItem, selectedItemIndex);

        void LoadSelectedItem()
        {
            if (PlayerPrefs.HasKey(SaveKeySelectedItem))
            {
                selectedItemIndex = PlayerPrefs.GetInt(SaveKeySelectedItem);
                if (selectedItemIndex >= 0 && selectedItemIndex < shopItems.Count)
                {
                    ApplyShopEffect(selectedItemIndex);
                    shopItems[selectedItemIndex].OnSelect?.Invoke();
                }
            }
            else if (defaultElementIndex >= 0 && defaultElementIndex < shopItems.Count)
            {
                shopItems[defaultElementIndex].isPurchased = true;
                SavePurchasedItems();
                ApplyShopEffect(defaultElementIndex);
                selectedItemIndex = defaultElementIndex;
                SaveSelectedItem();
                shopItems[defaultElementIndex].OnSelect?.Invoke();
            }
        }

        public void CloseShop()
        {
            if (selectedItemIndex >= 0 && selectedItemIndex < shopItems.Count) ApplyShopEffect(selectedItemIndex);
            previewItemIndex = -1;
            buyButton.gameObject.SetActive(false);
        }
    }
}