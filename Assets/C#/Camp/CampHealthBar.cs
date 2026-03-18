using UnityEngine;
using UnityEngine.UI;

public class CampHealthBar : MonoBehaviour
{
    public Image fillImage;
    private Camp camp;

    private void Awake()
    {
        camp = GetComponentInParent<Camp>();
    }

    private void Update()
    {
        if (camp != null && camp.healthmax > 0)
            UpdateBar((float)camp.health / camp.healthmax);
    }

    private void LateUpdate()
    {
        // 只修正旋转，抵消父物体（营地）的旋转影响
        // 位置完全由 Inspector 里的 localPosition 控制，不在代码里干预
        transform.rotation = Quaternion.Euler(20f, 0f, 0f);
    }

    public void UpdateBar(float ratio)
    {
        if (fillImage != null)
            fillImage.fillAmount = ratio;
    }

    public void Hide()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
            canvas.gameObject.SetActive(false);
    }
}
