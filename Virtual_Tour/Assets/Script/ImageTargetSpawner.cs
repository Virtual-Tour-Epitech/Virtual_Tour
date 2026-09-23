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
        public GameObject sceneObject;
    }

    [SerializeField]
    List<ImageTargetEntry> m_Targets = new();

    [SerializeField]
    PersistenceMode m_Persistence = PersistenceMode.FollowImage;

    [SerializeField]
    Vector3 m_LocalOffset = Vector3.zero;

    [SerializeField]
    ImageTargetLogger m_Logger;

    ARTrackedImageManager m_Manager;
    ARAnchorManager m_AnchorManager;

    readonly Dictionary<TrackableId, GameObject> m_Spawned = new();
    readonly HashSet<string> m_AlreadyPlaced = new();
    readonly Dictionary<GameObject, Transform> m_OriginalParents = new();

    void Awake()
    {
        m_Manager = GetComponent<ARTrackedImageManager>();

        m_AnchorManager = GetComponent<ARAnchorManager>();
        if (m_AnchorManager == null)
            m_AnchorManager = FindAnyObjectByType<ARAnchorManager>();

        if (m_Logger == null)
            m_Logger = FindAnyObjectByType<ImageTargetLogger>();

        foreach (var entry in m_Targets)
        {
            if (entry == null || entry.sceneObject == null)
                continue;

            m_OriginalParents[entry.sceneObject] = entry.sceneObject.transform.parent;
            entry.sceneObject.SetActive(false);
        }
    }

    public void ClearAll()
    {
        foreach (var entry in m_Targets)
        {
            if (entry == null || entry.sceneObject == null)
                continue;

            if (m_OriginalParents.TryGetValue(entry.sceneObject, out var parent))
                entry.sceneObject.transform.SetParent(parent, false);

            entry.sceneObject.SetActive(false);
        }

        m_Spawned.Clear();

        m_AlreadyPlaced.Clear();

        LogAction("Objets masques");
        LogNoDetection();
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

        Log(imageName, trackedImage.trackingState);

        if (m_Persistence == PersistenceMode.PlaceOnce && m_AlreadyPlaced.Contains(imageName))
        {
            LogAction("Deja pose, ignore");
            return;
        }

        var target = FindSceneObject(imageName);

        if (target == null)
        {
            LogAction("Aucun objet associe");
            return;
        }

        target.transform.SetParent(trackedImage.transform, false);
        target.transform.localPosition = m_LocalOffset;
        target.transform.localRotation = Quaternion.identity;
        target.transform.localScale = Vector3.one;
        target.SetActive(true);

        m_Spawned[trackedImage.trackableId] = target;

        LogAction($"\"{target.name}\" affiche");

        UpdateVisibility(trackedImage);
    }

    void UpdateVisibility(ARTrackedImage trackedImage)
    {
        Log(trackedImage.referenceImage.name, trackedImage.trackingState);

        if (!m_Spawned.TryGetValue(trackedImage.trackableId, out var instance) || instance == null)
            return;

        var isTracking = trackedImage.trackingState == TrackingState.Tracking;

        switch (m_Persistence)
        {
            case PersistenceMode.FollowImage:
                if (instance.activeSelf != isTracking)
                {
                    instance.SetActive(isTracking);
                    LogAction(isTracking ? $"\"{instance.name}\" affiche" : $"\"{instance.name}\" masque");
                }
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
            LogAction($"\"{instance.name}\" pose sans ancrage");
            return;
        }

        LogAction($"\"{instance.name}\" pose, ancrage en cours");

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
            LogAction($"\"{instance.name}\" pose, ancrage echoue");
            return;
        }

        instance.transform.SetParent(result.value.transform, true);

        LogAction($"\"{instance.name}\" pose et ancre");
    }

    void Despawn(TrackableId trackableId)
    {
        if (!m_Spawned.TryGetValue(trackableId, out var instance))
            return;

        if (instance != null)
        {
            instance.transform.SetParent(null, true);

            if (m_Persistence == PersistenceMode.FollowImage)
                instance.SetActive(false);

            LogAction($"\"{instance.name}\" retire (suivi perdu)");
        }

        m_Spawned.Remove(trackableId);

        if (m_Spawned.Count == 0)
            LogNoDetection();
    }

    void Log(string imageName, TrackingState state)
    {
        if (m_Logger != null)
            m_Logger.LogDetection(imageName, state);
    }

    void LogAction(string action)
    {
        if (m_Logger != null)
            m_Logger.LogAction(action);
    }

    void LogNoDetection()
    {
        if (m_Logger != null)
            m_Logger.LogNoDetection();
    }

    GameObject FindSceneObject(string referenceImageName)
    {
        foreach (var entry in m_Targets)
        {
            if (entry != null && entry.sceneObject != null && entry.referenceImageName == referenceImageName)
                return entry.sceneObject;
        }

        return null;
    }
}
