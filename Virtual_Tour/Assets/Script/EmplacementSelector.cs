using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class EmplacementSelector : MonoBehaviour
{
    [System.Serializable]
    public class EmplacementButton
    {
        public EmplacementGuide.Emplacement emplacement;
        public Button button;
    }

    [SerializeField]
    List<EmplacementButton> m_StartButtons = new();

    [SerializeField]
    List<EmplacementButton> m_EndButtons = new();

    [SerializeField]
    EmplacementGuide m_Guide;

    [SerializeField]
    Color m_SelectedColor = new(0f, 0.85f, 1f, 1f);

    EmplacementGuide.Emplacement? m_Start;
    EmplacementGuide.Emplacement? m_End;

    readonly Dictionary<Button, Color> m_IdleColors = new();

    void Awake()
    {
        if (m_Guide == null)
            m_Guide = FindAnyObjectByType<EmplacementGuide>(FindObjectsInactive.Include);

        if (m_Guide == null)
            Debug.LogError("[EmplacementSelector] Aucun EmplacementGuide dans la scene : aucune ligne ne sera generee.", this);

        foreach (var entry in m_StartButtons)
        {
            if (entry == null || entry.button == null)
                continue;

            var emplacement = entry.emplacement;
            RememberIdleColor(entry.button);
            entry.button.onClick.AddListener(() => SelectStart(emplacement));
        }

        foreach (var entry in m_EndButtons)
        {
            if (entry == null || entry.button == null)
                continue;

            var emplacement = entry.emplacement;
            RememberIdleColor(entry.button);
            entry.button.onClick.AddListener(() => SelectEnd(emplacement));
        }
    }

    public void SelectStart(EmplacementGuide.Emplacement emplacement)
    {
        m_Start = emplacement;
        Highlight(m_StartButtons, emplacement);
        TryGenerate();
    }

    public void SelectEnd(EmplacementGuide.Emplacement emplacement)
    {
        m_End = emplacement;
        Highlight(m_EndButtons, emplacement);
        TryGenerate();
    }

    void TryGenerate()
    {
        if (m_Guide == null || m_Start == null || m_End == null)
            return;

        if (m_Start == m_End)
        {
            m_Guide.Clear();
            return;
        }

        m_Guide.SetRoute(m_Start.Value, m_End.Value);
    }

    void RememberIdleColor(Button button)
    {
        if (button.targetGraphic != null)
            m_IdleColors[button] = button.targetGraphic.color;
    }

    void Highlight(List<EmplacementButton> column, EmplacementGuide.Emplacement? selected)
    {
        foreach (var entry in column)
        {
            if (entry == null || entry.button == null || entry.button.targetGraphic == null)
                continue;

            if (entry.emplacement == selected)
                entry.button.targetGraphic.color = m_SelectedColor;
            else if (m_IdleColors.TryGetValue(entry.button, out var idle))
                entry.button.targetGraphic.color = idle;
        }
    }
}
