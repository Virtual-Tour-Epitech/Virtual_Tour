using TMPro;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;

public class ImageTargetLogger : MonoBehaviour
{
    const string k_Idle = "Rien";

    [SerializeField]
    TextMeshProUGUI m_DetectionOutput;

    [SerializeField]
    TextMeshProUGUI m_ActionOutput;

    void Start()
    {
        Set(m_DetectionOutput, k_Idle);
        Set(m_ActionOutput, k_Idle);
    }

    public void LogDetection(string imageName, TrackingState state)
    {
        Set(m_DetectionOutput, $"{imageName} ({Describe(state)})");
    }

    public void LogNoDetection()
    {
        Set(m_DetectionOutput, k_Idle);
    }

    public void LogAction(string action)
    {
        Set(m_ActionOutput, action);
    }

    static void Set(TextMeshProUGUI output, string value)
    {
        // UpdateVisibility tourne a chaque frame pour chaque image suivie : sans ce test,
        // on reconstruirait le maillage du texte 60 fois par seconde pour rien.
        if (output != null && output.text != value)
            output.text = value;
    }

    static string Describe(TrackingState state)
    {
        return state switch
        {
            TrackingState.Tracking => "suivi",
            TrackingState.Limited => "suivi limite",
            _ => "perdu"
        };
    }
}
