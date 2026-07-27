using UnityEngine;
using UnityEngine.UI;
namespace Solo.MOST_IN_ONE
{
    public class SharedMaterialControl : MonoBehaviour
    {
        /// <summary>
        /// A Live Material Control (The edit will  affect the shared material from Project file)
        /// </summary>
        [Tooltip("Target Material")] public Material SharedMaterial;
        [Tooltip("Red Slider (Make sure the limits 0-255)")] public Slider RedSlider;
        [Tooltip("Red UI value Text Display")] public Text RedValue;
        [Tooltip("Green Slider (Make sure the limits 0-255)")] public Slider GreenSlider;
        [Tooltip("Green UI value Text Display")] public Text GreenValue;
        [Tooltip("Blue Slider (Make sure the limits 0-255)")] public Slider BlueSlider;
        [Tooltip("Blue UI value Text Display")] public Text BlueValue;

        void Start()
        {
            RedSlider.value = SharedMaterial.color.r * 255;
            RedValue.text = RedSlider.value.ToString();

            GreenSlider.value = SharedMaterial.color.g * 255;
            GreenValue.text = GreenSlider.value.ToString();

            BlueSlider.value = SharedMaterial.color.b * 255;
            BlueValue.text = BlueSlider.value.ToString();

            RedSlider.onValueChanged.AddListener(ChangeR);
            GreenSlider.onValueChanged.AddListener(ChangeG);
            BlueSlider.onValueChanged.AddListener(ChangeB);
        }

        public void ChangeR(float red)
        {
            Color cl = SharedMaterial.color; cl.r = red / 255;
            SharedMaterial.color = cl;
            RedValue.text = red.ToString();
        }
        public void ChangeG(float green)
        {
            Color cl = SharedMaterial.color; cl.g = green / 255;
            SharedMaterial.color = cl;
            GreenValue.text = green.ToString();
        }
        public void ChangeB(float blue)
        {
            Color cl = SharedMaterial.color; cl.b = blue / 255;
            SharedMaterial.color = cl;
            BlueValue.text = blue.ToString();
        }
    }
}