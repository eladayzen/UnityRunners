using UnityEngine;
namespace Solo.MOST_IN_ONE
{
    public class CustomShopEdits : MonoBehaviour
    {
        /// <summary>
        /// Dummy... Dependeing on your game and shop system... add custom commands or functions...
        /// </summary>
        public MOST_FreeMovement editedCS;
        public GameObject[] DisabledList;
        public GameObject[] EnabledList;

        public void UpdateData(GameObject model)
        {
            editedCS.AnimationControl = model.GetComponent<Animator>();
        }

        public void OnShopOpen()
        {
            editedCS.EnableState(false);
            editedCS.OnInputReleased(false);
            foreach (GameObject obj in EnabledList) obj.SetActive(true);
            foreach (GameObject obj in DisabledList) obj.SetActive(false);
        }
        public void OnShopClosed()
        {
            editedCS.EnableState(true);
            editedCS.OnInputReleased(false);
            foreach (GameObject obj in EnabledList) obj.SetActive(false);
            foreach (GameObject obj in DisabledList) obj.SetActive(true);
        }
    }
}
