using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

/// <summary>
/// Instancie un prefab 3D au-dessus de chaque image de reference detectee par la camera.
///
/// A placer sur le meme GameObject que l'ARTrackedImageManager (le XR Origin).
/// Chaque entree de la liste "Targets" associe le nom d'une image de la
/// ReferenceImageLibrary au prefab a afficher : plusieurs marqueurs peuvent donc
/// declencher des objets differents.
/// </summary>
[RequireComponent(typeof(ARTrackedImageManager))]
public class ImageTargetSpawner : MonoBehaviour
{
    /// <summary>
    /// Que devient l'objet quand la camera ne voit plus le marqueur.
    /// </summary>
    public enum PersistenceMode
    {
        /// <summary>
        /// L'objet suit le marqueur et disparait des qu'il n'est plus activement suivi.
        /// Comportement AR classique : rien ne flotte dans le vide.
        /// </summary>
        FollowImage,

        /// <summary>
        /// L'objet suit le marqueur mais reste toujours visible, fige a sa derniere pose connue
        /// quand le suivi est perdu. Attention : il peut sauter quand le suivi reprend, car la
        /// pose du marqueur est alors corrigee d'un coup.
        /// </summary>
        FollowAlways,

        /// <summary>
        /// L'objet est detache du marqueur des la premiere detection stable et reste pose dans
        /// le monde, meme si on ne revoit jamais le marqueur. Si un ARAnchorManager est present,
        /// un ancrage est cree pour que la position resiste a la derive du tracking.
        /// C'est le mode a choisir pour un parcours : on scanne une fois, le contenu reste en place.
        /// </summary>
        PlaceOnce
    }

    /// <summary>
    /// Association "nom de l'image de reference" -> "prefab a instancier".
    /// </summary>
    [System.Serializable]
    public class ImageTargetEntry
    {
        [Tooltip("Nom exact de l'image dans la ReferenceImageLibrary (sensible a la casse).")]
        public string referenceImageName;

        [Tooltip("Prefab 3D instancie au-dessus de cette image.")]
        public GameObject prefab;
    }

    [Header("Associations image -> objet 3D")]
    [SerializeField]
    List<ImageTargetEntry> m_Targets = new();

    [Header("Comportement")]
    [Tooltip("Que devient l'objet quand la camera ne voit plus le marqueur.")]
    [SerializeField]
    PersistenceMode m_Persistence = PersistenceMode.FollowImage;

    [Tooltip("Decalage local applique a l'objet instancie. +Y sort de la surface de l'image.")]
    [SerializeField]
    Vector3 m_LocalOffset = Vector3.zero;

    ARTrackedImageManager m_Manager;
    ARAnchorManager m_AnchorManager;

    // Une instance par image suivie : garantit qu'on n'instancie jamais deux fois le meme objet.
    readonly Dictionary<TrackableId, GameObject> m_Spawned = new();

    // En mode PlaceOnce, on retient les marqueurs deja poses PAR NOM : apres detachement,
    // un nouveau passage devant le meme marqueur genere un nouveau TrackableId, et sans ce
    // garde-fou on empilerait un deuxieme objet au meme endroit.
    readonly HashSet<string> m_AlreadyPlaced = new();

    void Awake()
    {
        m_Manager = GetComponent<ARTrackedImageManager>();

        // Optionnel : sert uniquement au mode PlaceOnce.
        m_AnchorManager = GetComponent<ARAnchorManager>();
        if (m_AnchorManager == null)
            m_AnchorManager = FindFirstObjectByType<ARAnchorManager>();
    }

    void OnEnable()
    {
        // AR Foundation 6.x : trackablesChanged est un UnityEvent (propriete en lecture seule),
        // on s'y abonne avec AddListener. L'ancien evenement trackedImagesChanged est deprecie.
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

    /// <summary>
    /// Instancie le prefab associe a l'image qui vient d'etre detectee.
    /// </summary>
    void Spawn(ARTrackedImage trackedImage)
    {
        var imageName = trackedImage.referenceImage.name;

        // Deja pose definitivement lors d'un passage precedent : on ne recommence pas.
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

        // On instancie en ENFANT du transform de l'ARTrackedImage : AR Foundation met ce transform
        // a jour a chaque frame, donc l'objet suit l'image sans code de poursuite dans Update().
        var instance = Instantiate(prefab, trackedImage.transform);
        instance.transform.localPosition = m_LocalOffset;
        instance.transform.localRotation = Quaternion.identity;

        m_Spawned[trackedImage.trackableId] = instance;

        // On applique tout de suite l'etat de visibilite : une image peut etre ajoutee
        // avec un tracking state "Limited".
        UpdateVisibility(trackedImage);
    }

    /// <summary>
    /// Applique le mode de persistance a chaque mise a jour du marqueur.
    /// </summary>
    /// <remarks>
    /// Le test sur trackingState est indispensable : ARKit et ARCore ne suppriment quasiment
    /// jamais une image detectee. Quand le marqueur sort du champ, l'image passe simplement en
    /// TrackingState.Limited, et "removed" n'est pas appele.
    /// </remarks>
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
                // Rien a faire : l'objet reste visible et fige a sa derniere pose connue.
                break;

            case PersistenceMode.PlaceOnce:
                // On attend une detection franche avant de figer la position : detacher sur un
                // etat "Limited" fixerait l'objet sur une pose approximative.
                if (isTracking)
                    DetachAndAnchor(trackedImage, instance);
                break;
        }
    }

    /// <summary>
    /// Detache l'objet du marqueur et le laisse dans le monde, ancre si possible.
    /// </summary>
    void DetachAndAnchor(ARTrackedImage trackedImage, GameObject instance)
    {
        var imageName = trackedImage.referenceImage.name;

        // worldPositionStays: true -> l'objet ne bouge pas d'un pixel au moment du detachement.
        instance.transform.SetParent(null, true);

        m_Spawned.Remove(trackedImage.trackableId);
        m_AlreadyPlaced.Add(imageName);

        if (m_AnchorManager == null)
        {
            // Sans ancrage, l'objet reste a des coordonnees fixes de la session. Ca suffit sur
            // une courte duree, mais il derivera lentement par rapport au monde reel.
            Debug.Log(
                $"[ImageTargetSpawner] \"{imageName}\" pose sans ancrage (aucun ARAnchorManager " +
                "dans la scene). Ajoute-en un pour une position plus stable dans la duree.", this);
            return;
        }

        AnchorAsync(instance, imageName);
    }

    /// <summary>
    /// Cree un ARAnchor a la pose de l'objet et parente l'objet dessus.
    /// </summary>
    async void AnchorAsync(GameObject instance, string imageName)
    {
        var pose = new Pose(instance.transform.position, instance.transform.rotation);

        var result = await m_AnchorManager.TryAddAnchorAsync(pose);

        // L'objet a pu etre detruit pendant l'await (changement de scene, quit).
        if (instance == null)
            return;

        if (!result.status.IsSuccess())
        {
            Debug.LogWarning(
                $"[ImageTargetSpawner] Echec de l'ancrage de \"{imageName}\". L'objet reste pose " +
                "mais derivera avec le temps.", this);
            return;
        }

        // Parente a l'ancre : c'est elle que le systeme AR repositionne quand il affine
        // sa comprehension de l'environnement, et l'objet suit.
        instance.transform.SetParent(result.value.transform, true);
    }

    /// <summary>
    /// Traite une image que le systeme ne suit plus du tout.
    /// </summary>
    void Despawn(TrackableId trackableId)
    {
        if (!m_Spawned.TryGetValue(trackableId, out var instance))
            return;

        if (instance != null)
        {
            if (m_Persistence == PersistenceMode.FollowImage)
            {
                Destroy(instance);
            }
            else
            {
                // Le GameObject de l'ARTrackedImage va etre detruit et emporterait son enfant :
                // on detache pour que l'objet survive.
                instance.transform.SetParent(null, true);
            }
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
