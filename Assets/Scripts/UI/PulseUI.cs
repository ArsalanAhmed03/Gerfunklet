using UnityEngine;
using UnityEngine.UI;

public class PulseUI : MonoBehaviour
{
    [SerializeField] private float pulseSpeed = 3f;
    [SerializeField] private float minAlpha = 0.25f;
    [SerializeField] private float maxAlpha = 1f;

    private CanvasGroup _group;
    private Graphic _graphic;
    private Color _baseColor;

    private void Awake()
    {
        _group = GetComponent<CanvasGroup>();
        _graphic = GetComponent<Graphic>();
        if (_graphic != null)
            _baseColor = _graphic.color;
    }

    private void Update()
    {
        float t = (Mathf.Sin(Time.unscaledTime * pulseSpeed) + 1f) * 0.5f;
        float alpha = Mathf.Lerp(minAlpha, maxAlpha, t);

        if (_group != null)
        {
            _group.alpha = alpha;
            return;
        }

        if (_graphic != null)
        {
            var c = _baseColor;
            c.a = alpha;
            _graphic.color = c;
        }
    }
}
