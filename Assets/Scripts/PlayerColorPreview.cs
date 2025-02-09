using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class PlayerColorPreview : MonoBehaviour
{
    private Slider _redSlider;
    private Slider _greenSlider;
    private Slider _blueSlider;
    private Image _previewImage;

    // Start is called before the first frame update
    void Start()
    {
        var colorSliders = GetComponentsInChildren<Slider>();
        _redSlider = colorSliders.First(slider => slider.gameObject.name.Contains("Red"));
        _blueSlider = colorSliders.First(slider => slider.gameObject.name.Contains("Blue"));
        _greenSlider = colorSliders.First(slider => slider.gameObject.name.Contains("Green"));

        System.Random random = new System.Random();
        _redSlider.value = random.Next(0, 256);
        _blueSlider.value = random.Next(0, 256);
        _greenSlider.value = random.Next(0, 256);

        _previewImage = GetComponentsInChildren<Image>().First(image => image.gameObject.name == "Preview Image");
    }

    // Update is called once per frame
    void Update()
    {
        _previewImage.color = new Color(_redSlider.value/255f, _greenSlider.value/255f, _blueSlider.value/255f);
    }
}
