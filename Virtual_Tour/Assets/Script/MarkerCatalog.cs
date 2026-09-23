using System.Collections.Generic;
using UnityEngine;

public class MarkerCatalog : ScriptableObject
{
    [System.Serializable]
    public class Marker
    {
        public Texture2D image;

        public string imageName;

        public bool isProduction;

        public float screenWidthCm = 11.64f;

        public float screenHeightCm = 16.2f;

        public float printWidthCm = 21f;

        public float printHeightCm = 29.7f;

        public float ActiveWidthCm => isProduction ? printWidthCm : screenWidthCm;

        public float ActiveHeightCm => isProduction ? printHeightCm : screenHeightCm;

        public float WidthInMeters => ActiveWidthCm * 0.01f;

        public float HeightInMeters => ActiveHeightCm * 0.01f;

        public string ModeLabel => isProduction ? "papier" : "ecran";
    }

    [SerializeField]
    List<Marker> m_Markers = new();

    public IReadOnlyList<Marker> Markers => m_Markers;
}
