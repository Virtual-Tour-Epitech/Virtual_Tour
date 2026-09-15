using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

[RequireComponent(typeof(ARTrackedImageManager))]
public class ImageTargetSpawner : MonoBehaviour
{
    public enum PersistenceMode
    {
        FollowImage,
        FollowAlways,
        PlaceOnce
    }

    [System.Serializable]
    public class ImageTargetEntry
    {
        public string referenceImageName;
        public GameObject prefab;
    }

    [SerializeField]
    List<ImageTargetEntry> m_Targets = new();

    [SerializeField]
    PersistenceMode m_Persistence = PersistenceMode.FollowImage;

    [SerializeField]
    Vector3 m_LocalOffset = Vector3.zero;

    ARTrackedImageManager m_Manager;
    ARAnchorManager m_AnchorManager;

    readonly Dictionary<TrackableId, GameObject> m_Spawned = new();
    readonly HashSet<string> m_AlreadyPlaced = new();

    void Awake()
    {
        m_Manager = GetComponent<ARTrackedImageManager>();

        m_AnchorManager = GetComponent<ARAnchorManager>();
        if (m_AnchorManager == null)
            m_AnchorManager = FindFirstObjectByType<ARAnchorManager>();
    }

    void OnEnable()
    {
        m_Manager.trackablesChanged.AddListener(OnTrackedImagesChanged);
    }

    void OnDisable()
    {
        m_Manager.trackablesChanged.RemoveListener(OnTrackedImagesChanged);
    }

    void OnTrackedImagesChanged(ARTrackablesChangedEventArgs<ARTrackedImage> args)
    {
        foreach (var trackedImage in args.added)
            Spawn(trackedImage);

        foreach (var trackedImage in args.updated)
            UpdateVisibility(trackedImage);

        foreach (var removed in args.removed)
            Despawn(removed.Key);
    }

    void Spawn(ARTrackedImage trackedImage)
    {
        var imageName = trackedImage.referenceImage.name;

        if (m_Persistence == PersistenceMode.PlaceOnce && m_AlreadyPlaced.Contains(imageName))
            return;

        var prefab = FindPrefab(imageName);

        if (prefab == null)
        {
            Debug.LogWarning(
                $"[ImageTargetSpawner] Image \"{imageName}\" detectee mais aucun prefab ne lui est " +
                "associe. Verifie que le champ 'Reference Image Name' de l'Inspector correspond " +
                "exactement au nom saisi dans la ReferenceImageLibrary.", this);
            return;
        }

        var instance = Instantiate(prefab, trackedImage.transform);
        instance.transform.localPosition = m_LocalOffset;
        instance.transform.localRotation = Quaternion.identity;

        m_Spawned[trackedImage.trackableId] = instance;

        UpdateVisibility(trackedImage);
    }

    void UpdateVisibility(ARTrackedImage trackedImage)
    {
        if (!m_Spawned.TryGetValue(trackedImage.trackableId, out var instance) || instance == null)
            return;

        var isTracking = trackedImage.trackingState == TrackingState.Tracking;

        switch (m_Persistence)
        {
            case PersistenceMode.FollowImage:
                if (instance.activeSelf != isTracking)
                    instance.SetActive(isTracking);
                break;

            case PersistenceMode.FollowAlways:
                break;

            case PersistenceMode.PlaceOnce:
                if (isTracking)
                    DetachAndAnchor(trackedImage, instance);
                break;
        }
    }

    void DetachAndAnchor(ARTrackedImage trackedImage, GameObject instance)
    {
        var imageName = trackedImage.referenceImage.name;

        instance.transform.SetParent(null, true);

        m_Spawned.Remove(trackedImage.trackableId);
        m_AlreadyPlaced.Add(imageName);

        if (m_AnchorManager == null)
        {
            Debug.Log(
                $"[ImageTargetSpawner] \"{imageName}\" pose sans ancrage (aucun ARAnchorManager " +
                "dans la scene). Ajoute-en un pour une position plus stable dans la duree.", this);
            return;
        }

        AnchorAsync(instance, imageName);
    }

    async void AnchorAsync(GameObject instance, string imageName)
    {
        var pose = new Pose(instance.transform.position, instance.transform.rotation);

        var result = await m_AnchorManager.TryAddAnchorAsync(pose);

        if (instance == null)
            return;

        if (!result.status.IsSuccess())
        {
            Debug.LogWarning(
                $"[ImageTargetSpawner] Echec de l'ancrage de \"{imageName}\". L'objet reste pose " +
                "mais derivera avec le temps.", this);
            return;
        }

        instance.transform.SetParent(result.value.transform, true);
    }

    void Despawn(TrackableId trackableId)
    {
        if (!m_Spawned.TryGetValue(trackableId, out var instance))
            return;

        if (instance != null)
        {
            if (m_Persistence == PersistenceMode.FollowImage)
                Destroy(instance);
            else
                instance.transform.SetParent(null, true);
        }

        m_Spawned.Remove(trackableId);
    }

    GameObject FindPrefab(string referenceImageName)
    {
        foreach (var entry in m_Targets)
        {
            if (entry != null && entry.prefab != null && entry.referenceImageName == referenceImageName)
                return entry.prefab;
        }

        return null;
    }
}
