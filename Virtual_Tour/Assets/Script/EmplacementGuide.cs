using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

public class EmplacementGuide : MonoBehaviour
{
    public enum Emplacement
    {
        Salon,
        Chambre,
        Cuisine,
        Toilette
    }

    [System.Serializable]
    public class EmplacementPoint
    {
        public Emplacement emplacement;
        public Transform point;
    }

    [SerializeField]
    List<EmplacementPoint> m_Points = new();

    [SerializeField]
    Emplacement m_FirstEmplacement = Emplacement.Chambre;

    [SerializeField]
    Emplacement m_GoToEmplacement = Emplacement.Salon;

    [SerializeField]
    LineRenderer m_Line;

    [SerializeField]
    LineRenderer m_ArrowHead;

    [SerializeField]
    float m_HeightOffset = 0.1f;

    [SerializeField]
    float m_ArrowLength = 0.4f;

    [SerializeField]
    float m_ArrowWidth = 0.25f;

    [SerializeField]
    float m_SampleDistance = 1f;

    [SerializeField]
    NavMeshSurface m_Surface;

    bool m_PendingGenerate;

    public LineRenderer Line => m_Line;

    public LineRenderer ArrowHead => m_ArrowHead;

    void OnEnable()
    {
        if (!m_PendingGenerate)
            return;

        m_PendingGenerate = false;
        Generate();
    }

    public void SetRoute(Emplacement first, Emplacement goTo)
    {
        m_FirstEmplacement = first;
        m_GoToEmplacement = goTo;

        if (isActiveAndEnabled)
            Generate();
        else
            m_PendingGenerate = true;
    }

    public void Generate()
    {
        if (m_Line == null || m_ArrowHead == null)
        {
            Debug.LogError("[EmplacementGuide] Line ou ArrowHead non assigne.", this);
            return;
        }

        if (m_FirstEmplacement == m_GoToEmplacement)
        {
            Debug.LogError($"[EmplacementGuide] Depart et destination identiques ({m_FirstEmplacement}).", this);
            return;
        }

        var start = FindPoint(m_FirstEmplacement);
        var end = FindPoint(m_GoToEmplacement);

        if (start == null || end == null)
        {
            Debug.LogError(
                $"[EmplacementGuide] Point manquant pour {(start == null ? m_FirstEmplacement : m_GoToEmplacement)} dans la liste Points.",
                this);
            return;
        }

        if (m_Surface == null)
            m_Surface = GetComponentInParent<NavMeshSurface>(true);

        if (m_Surface == null)
        {
            Debug.LogError("[EmplacementGuide] Aucun NavMeshSurface assigne ni trouve dans les parents.", this);
            return;
        }

        m_Surface.RemoveData();
        m_Surface.AddData();

        if (!NavMesh.SamplePosition(start.position, out var startHit, m_SampleDistance, NavMesh.AllAreas) ||
            !NavMesh.SamplePosition(end.position, out var endHit, m_SampleDistance, NavMesh.AllAreas))
        {
            Debug.LogError(
                $"[EmplacementGuide] Aucun NavMesh a moins de {m_SampleDistance} m d'un des points. " +
                "Le NavMesh est-il bake et les points poses au sol ?",
                this);
            return;
        }

        var path = new NavMeshPath();

        if (!NavMesh.CalculatePath(startHit.position, endHit.position, NavMesh.AllAreas, path) ||
            path.status != NavMeshPathStatus.PathComplete ||
            path.corners.Length < 2)
        {
            Debug.LogError(
                $"[EmplacementGuide] Aucun chemin complet de {m_FirstEmplacement} vers {m_GoToEmplacement} " +
                "(escalier ou passage non couvert par le NavMesh ?).",
                this);
            return;
        }

        DrawPath(path.corners);
    }

    public void Clear()
    {
        if (m_Line != null)
            m_Line.positionCount = 0;

        if (m_ArrowHead != null)
            m_ArrowHead.positionCount = 0;
    }

    void DrawPath(Vector3[] corners)
    {
        var offset = Vector3.up * m_HeightOffset;

        m_Line.useWorldSpace = false;
        m_Line.positionCount = corners.Length;

        for (var i = 0; i < corners.Length; i++)
            m_Line.SetPosition(i, m_Line.transform.InverseTransformPoint(corners[i] + offset));

        var tip = corners[^1] + offset;
        var previous = corners[^2] + offset;
        var direction = (tip - previous).normalized;
        var length = Mathf.Min(m_ArrowLength, Vector3.Distance(tip, previous));

        m_ArrowHead.useWorldSpace = false;
        m_ArrowHead.positionCount = 2;
        m_ArrowHead.SetPosition(0, m_ArrowHead.transform.InverseTransformPoint(tip - direction * length));
        m_ArrowHead.SetPosition(1, m_ArrowHead.transform.InverseTransformPoint(tip));
        m_ArrowHead.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
        m_ArrowHead.widthMultiplier = m_ArrowWidth;
    }

    Transform FindPoint(Emplacement emplacement)
    {
        foreach (var entry in m_Points)
        {
            if (entry != null && entry.point != null && entry.emplacement == emplacement)
                return entry.point;
        }

        return null;
    }
}
