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

    [SerializeField]
    TextMeshProUGUI m_AimOutput;

    void Start()
    {
        Set(m_DetectionOutput, k_Idle);
        Set(m_ActionOutput, k_Idle);
        Set(m_AimOutput, k_Idle);
    }

    public void LogAim(string objectName)
    {
        Set(m_AimOutput, string.IsNullOrEmpty(objectName) ? k_Idle : objectName);
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
