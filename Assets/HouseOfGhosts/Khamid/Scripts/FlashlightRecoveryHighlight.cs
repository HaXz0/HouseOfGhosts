using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[DisallowMultipleComponent]
public class FlashlightRecoveryHighlight : MonoBehaviour
{
    [Header("Distance & Wall Occlusion Settings")]
    [Tooltip("Horizontal distance (in meters) at which a dropped flashlight begins showing its X-Ray outline.")]
    [SerializeField] private float highlightStartDistance = 3.5f;

    [Tooltip("Horizontal distance (in meters) at which the X-Ray outline reaches full strength.")]
    [SerializeField] private float fullHighlightDistance = 5.0f;

    [Tooltip("If true, the X-Ray outline also activates earlier (at wallOccludedMinDistance) whenever a solid wall/furniture blocks your view of the dropped flashlight.")]
    [SerializeField] private bool highlightWhenBehindWall = true;

    [Tooltip("Minimum distance (in meters) before wall occlusion triggers the X-Ray outline.")]
    [SerializeField] private float wallOccludedMinDistance = 1.8f;

    [Tooltip("Layers that count as walls/furniture blocking your view of the dropped flashlight.")]
    [SerializeField] private LayerMask wallLayers = ~0;

    [Header("X-Ray Outline Appearance")]
    [Tooltip("Color of the through-wall X-Ray outline and Fresnel rim silhouette.")]
    [SerializeField] private Color outlineColor = new Color(0.15f, 0.92f, 1.0f, 1.0f);

    [Tooltip("Thickness of the outer silhouette outline (in meters).")]
    [Range(0.001f, 0.012f)]
    [SerializeField] private float outlineWidth = 0.0035f;

    [Tooltip("Sharpness of the inner 3D contour Fresnel rim (higher = thinner edge lines).")]
    [Range(1.0f, 6.0f)]
    [SerializeField] private float fresnelRimSharpness = 3.2f;

    [Tooltip("Brightness of the inner 3D contour Fresnel rim.")]
    [Range(0.0f, 3.0f)]
    [SerializeField] private float fresnelRimIntensity = 1.25f;

    [Tooltip("Subtle breathing pulse speed of the outline.")]
    [SerializeField] private float pulseFrequency = 2.2f;

    [Header("Shader Reference")]
    [Tooltip("Automatically links to 'HouseOfGhosts/FlashlightXRayOutline'.")]
    [SerializeField] private Shader xrayOutlineShader;

    [Header("Current State (Read Only)")]
    [SerializeField] private bool isHighlighted = false;
    [SerializeField] private bool isBehindWall = false;
    [SerializeField] private float currentHorizontalDistance = 0f;

    [Header("References")]
    [Tooltip("Player VR Headset / Camera. Automatically finds Camera.main if left empty.")]
    [SerializeField] private Transform playerHead;

    private XRGrabInteractable m_GrabInteractable;
    private Material m_OutlineMaterialInstance;
    private readonly List<MeshRenderer> m_OverlayRenderers = new List<MeshRenderer>();
    private Collider[] m_SelfColliders;

    private float m_CurrentHighlightBlend = 0f;

    private static readonly int s_OutlineColorId = Shader.PropertyToID("_OutlineColor");
    private static readonly int s_OutlineWidthId = Shader.PropertyToID("_OutlineWidth");
    private static readonly int s_RimPowerId = Shader.PropertyToID("_RimPower");
    private static readonly int s_RimIntensityId = Shader.PropertyToID("_RimIntensity");
    private static readonly int s_AlphaId = Shader.PropertyToID("_Alpha");

    private void OnValidate()
    {
        if (xrayOutlineShader == null)
        {
            xrayOutlineShader = Shader.Find("HouseOfGhosts/FlashlightXRayOutline");
        }
    }

    private void Awake()
    {
        m_SelfColliders = GetComponentsInChildren<Collider>(true);

        // Clean up any old beacon light if one existed from previous testing
        Transform oldBeacon = transform.Find("Recovery Beacon Light");
        if (oldBeacon != null)
        {
            Destroy(oldBeacon.gameObject);
        }
    }

    private void Start()
    {
        if (playerHead == null && Camera.main != null)
        {
            playerHead = Camera.main.transform;
        }

        m_GrabInteractable = GetComponent<XRGrabInteractable>();
        BuildXRayOverlayRenderers();
        SetOverlayRenderersActive(false);
    }

    private void OnDestroy()
    {
        if (m_OutlineMaterialInstance != null)
        {
            Destroy(m_OutlineMaterialInstance);
        }
    }

    private void Update()
    {
        if (playerHead == null && Camera.main != null)
        {
            playerHead = Camera.main.transform;
        }

        if (m_GrabInteractable == null)
        {
            m_GrabInteractable = GetComponent<XRGrabInteractable>();
        }

        bool isHeldOrStored = m_GrabInteractable != null && m_GrabInteractable.isSelected;
        float targetBlend = 0f;
        isBehindWall = false;

        if (!isHeldOrStored && playerHead != null)
        {
            Vector2 playerXZ = new Vector2(playerHead.position.x, playerHead.position.z);
            Vector2 flashlightXZ = new Vector2(transform.position.x, transform.position.z);
            currentHorizontalDistance = Vector2.Distance(playerXZ, flashlightXZ);

            // Check 1: Far away distance trigger (e.g. >= 3.5m)
            if (currentHorizontalDistance >= highlightStartDistance)
            {
                float span = Mathf.Max(0.1f, fullHighlightDistance - highlightStartDistance);
                targetBlend = Mathf.Clamp01((currentHorizontalDistance - highlightStartDistance) / span);
                targetBlend = Mathf.Max(0.4f, targetBlend);
            }

            // Check 2: Occluded behind a solid wall/furniture trigger
            if (highlightWhenBehindWall && currentHorizontalDistance >= wallOccludedMinDistance)
            {
                isBehindWall = IsOccludedByWall();
                if (isBehindWall)
                {
                    targetBlend = Mathf.Max(targetBlend, 0.9f);
                }
            }
        }
        else
        {
            currentHorizontalDistance = 0f;
        }

        isHighlighted = targetBlend > 0.01f;
        m_CurrentHighlightBlend = Mathf.MoveTowards(m_CurrentHighlightBlend, targetBlend, 3.5f * Time.deltaTime);

        if (m_CurrentHighlightBlend > 0.001f)
        {
            UpdateOverlayVisuals(m_CurrentHighlightBlend);
        }
        else
        {
            SetOverlayRenderersActive(false);
        }
    }

    private bool IsOccludedByWall()
    {
        if (playerHead == null)
            return false;

        Vector3 origin = playerHead.position;
        Vector3 targetPos = transform.position;
        Vector3 toFlashlight = targetPos - origin;
        float distance = toFlashlight.magnitude;

        if (distance < 0.2f)
            return false;

        if (Physics.Raycast(origin, toFlashlight / distance, out RaycastHit hit, distance, wallLayers, QueryTriggerInteraction.Ignore))
        {
            // Ignore player rig and the flashlight itself
            if (hit.collider.transform.IsChildOf(transform) || hit.collider.transform.IsChildOf(playerHead.root))
                return false;

            return true;
        }

        return false;
    }

    private void BuildXRayOverlayRenderers()
    {
        if (xrayOutlineShader == null)
        {
            xrayOutlineShader = Shader.Find("HouseOfGhosts/FlashlightXRayOutline");
        }

        if (xrayOutlineShader == null)
        {
            Debug.LogError("[FlashlightRecoveryHighlight] Could not find shader 'HouseOfGhosts/FlashlightXRayOutline'!", this);
            return;
        }

        m_OutlineMaterialInstance = new Material(xrayOutlineShader)
        {
            name = "Flashlight_XRayOutline_RuntimeMat"
        };

        MeshFilter[] meshFilters = GetComponentsInChildren<MeshFilter>(true);
        for (int i = 0; i < meshFilters.Length; i++)
        {
            MeshFilter mf = meshFilters[i];
            if (mf == null || mf.sharedMesh == null)
                continue;

            // Avoid duplicating an existing overlay
            if (mf.gameObject.name.EndsWith("_XRayOverlay"))
                continue;

            // Skip runtime volumetric beam cones or any helper mesh under the Spotlight!
            if (mf.gameObject.name.Contains("VolumetricBeam") || mf.GetComponentInParent<Light>() != null)
                continue;

            MeshRenderer sourceRenderer = mf.GetComponent<MeshRenderer>();
            if (sourceRenderer == null)
                continue;

            GameObject overlayObj = new GameObject(mf.gameObject.name + "_XRayOverlay");
            overlayObj.transform.SetParent(mf.transform, false);
            overlayObj.transform.localPosition = Vector3.zero;
            overlayObj.transform.localRotation = Quaternion.identity;
            overlayObj.transform.localScale = Vector3.one;

            MeshFilter overlayMf = overlayObj.AddComponent<MeshFilter>();
            overlayMf.sharedMesh = mf.sharedMesh;

            MeshRenderer overlayMr = overlayObj.AddComponent<MeshRenderer>();
            overlayMr.shadowCastingMode = ShadowCastingMode.Off;
            overlayMr.receiveShadows = false;
            overlayMr.lightProbeUsage = LightProbeUsage.Off;
            overlayMr.reflectionProbeUsage = ReflectionProbeUsage.Off;

            // Assign the X-Ray material to all submeshes (e.g. Flashlight body + lens)
            int subMeshCount = Mathf.Max(1, mf.sharedMesh.subMeshCount);
            Material[] mats = new Material[subMeshCount];
            for (int m = 0; m < subMeshCount; m++)
            {
                mats[m] = m_OutlineMaterialInstance;
            }
            overlayMr.sharedMaterials = mats;
            overlayMr.enabled = false;

            m_OverlayRenderers.Add(overlayMr);
        }
    }

    private void UpdateOverlayVisuals(float blend)
    {
        if (m_OutlineMaterialInstance == null)
            return;

        SetOverlayRenderersActive(true);

        // Smooth subtle breathing pulse (0.7 to 1.0)
        float pulse = 0.7f + 0.3f * (0.5f + 0.5f * Mathf.Sin(Time.time * pulseFrequency));
        float finalAlpha = Mathf.Clamp01(blend * pulse);

        m_OutlineMaterialInstance.SetColor(s_OutlineColorId, outlineColor);
        m_OutlineMaterialInstance.SetFloat(s_OutlineWidthId, outlineWidth);
        m_OutlineMaterialInstance.SetFloat(s_RimPowerId, fresnelRimSharpness);
        m_OutlineMaterialInstance.SetFloat(s_RimIntensityId, fresnelRimIntensity);
        m_OutlineMaterialInstance.SetFloat(s_AlphaId, finalAlpha);
    }

    private void SetOverlayRenderersActive(bool active)
    {
        for (int i = 0; i < m_OverlayRenderers.Count; i++)
        {
            if (m_OverlayRenderers[i] != null && m_OverlayRenderers[i].enabled != active)
            {
                m_OverlayRenderers[i].enabled = active;
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = isHighlighted ? outlineColor : new Color(0.15f, 0.92f, 1.0f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, highlightStartDistance);
    }
}
