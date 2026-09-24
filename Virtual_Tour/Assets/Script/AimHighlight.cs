using UnityEngine;

public class AimHighlight : MonoBehaviour
{
    static readonly int k_BaseColor = Shader.PropertyToID("_BaseColor");

    [SerializeField]
    Color m_HighlightColor = Color.yellow;

    Renderer[] m_Renderers;
    Color[] m_OriginalColors;
    bool m_Aimed;

    void Awake()
    {
        m_Renderers = GetComponentsInChildren<Renderer>(true);
        m_OriginalColors = new Color[m_Renderers.Length];

        for (var i = 0; i < m_Renderers.Length; i++)
        {
            var material = m_Renderers[i].material;
            m_OriginalColors[i] = material.HasProperty(k_BaseColor) ? material.GetColor(k_BaseColor) : Color.white;
        }

        if (m_Renderers.Length == 0)
            Debug.LogError("[AimHighlight] Aucun Renderer sous cet objet : la surbrillance restera invisible.", this);
    }

    public void OnAimEnter()
    {
        if (m_Aimed)
            return;

        m_Aimed = true;
        ApplyColor(true);
    }

    public void OnAimExit()
    {
        if (!m_Aimed)
            return;

        m_Aimed = false;
        ApplyColor(false);
    }

    void ApplyColor(bool aimed)
    {
        for (var i = 0; i < m_Renderers.Length; i++)
        {
            var material = m_Renderers[i].material;

            if (material.HasProperty(k_BaseColor))
                material.SetColor(k_BaseColor, aimed ? m_HighlightColor : m_OriginalColors[i]);
        }
    }
}
