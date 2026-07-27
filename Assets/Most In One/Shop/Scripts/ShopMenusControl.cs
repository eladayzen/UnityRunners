using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
namespace Solo.MOST_IN_ONE
{
    public class ShopMenusControl : MonoBehaviour
    {
        /// <summary>
        /// Generally it controls the currency, saving shop prefs and  manage shop main buttons
        /// Dependeing on your game and shop system... edit or add custom commands or functions...
        /// </summary>

        [ReadOnly]public string TestCurrency = "GemsCurrency";
        public Text TestCurrencyText;

        public GameObject ShopUI;
        public GameObject[] DisabledWhenOpen;
        public MOST_Shop[] MenuList;
        public Button[] MenuButtons;
        public Button OpenButton;
        public Button ExitButton;
        public bool SaveLastMenu;
        public bool DisableAtStart;

        Vector3 startPosition;
        void Start()
        {
            if (!PlayerPrefs.HasKey(TestCurrency))
            {
                PlayerPrefs.SetInt(TestCurrency, 0);
            }
            TestCurrencyText.text = PlayerPrefs.GetInt(TestCurrency).ToString();

            startPosition = MenuList[0].gameObject.transform.position;
            if (SaveLastMenu)
            {
                if (!PlayerPrefs.HasKey("SavedMenu" + SceneManager.GetActiveScene().name))
                {
                    PlayerPrefs.SetInt("SavedMenu" + SceneManager.GetActiveScene().name, 0);
                }
            }

            for (int i = 0; i < MenuButtons.Length; i++)
            {
                int index = i;
                MenuButtons[index].onClick.AddListener(delegate { MenuJump(index); });
            }
            OpenButton.onClick.AddListener(OpenShop);
            ExitButton.onClick.AddListener(CloseShop);

            if (DisableAtStart) ShopUI.SetActive(false);
        }

        void MenuJump(int index)
        {
            foreach (MOST_Shop menu in MenuList) menu.gameObject.SetActive(false);
            foreach (Button button in MenuButtons) button.gameObject.SetActive(true);

            if(MenuList.Length > index) MenuList[index].gameObject.SetActive(true);
            if (MenuList.Length > index) MenuList[index].gameObject.transform.position = startPosition;

            if (MenuButtons.Length > index) MenuButtons[index].gameObject.SetActive(false);
        }
        void CloseShop()
        {
            foreach (MOST_Shop menu in MenuList) menu.gameObject.SetActive(true);
            foreach (MOST_Shop menu in MenuList) menu.CloseShop();
            foreach (GameObject obj in DisabledWhenOpen) obj.SetActive(true);
            ShopUI.SetActive(false);
        }
        void OpenShop()
        {
            ShopUI.SetActive(true);
            foreach (GameObject obj in DisabledWhenOpen) obj.SetActive(false);
            if (SaveLastMenu && PlayerPrefs.GetInt("SavedMenu" + SceneManager.GetActiveScene().name) <= MenuList.Length)
                MenuJump(PlayerPrefs.GetInt("SavedMenu" + SceneManager.GetActiveScene().name));
            else MenuJump(0); ;
        }
        public void UpdateCurrency(int amount)
        {
            PlayerPrefs.SetInt(TestCurrency, amount + PlayerPrefs.GetInt(TestCurrency));
            TestCurrencyText.text = PlayerPrefs.GetInt(TestCurrency).ToString();
        }
    }
}