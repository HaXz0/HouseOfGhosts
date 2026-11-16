using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[ExecuteAlways]
public sealed class InteractionTestStations : MonoBehaviour
{
    [SerializeField] GameObject lampPrefab;
    [SerializeField] AudioClip switchClick;

    readonly Color wood = new Color(0.30f, 0.21f, 0.16f);
    readonly Color trim = new Color(0.16f, 0.17f, 0.15f);
    readonly Color handleColor = new Color(0.65f, 0.58f, 0.39f);

    Transform previewRoot;
    bool buildingPreview;

    void OnEnable()
    {
        if (Application.IsPlaying(gameObject) || !gameObject.scene.IsValid())
            return;

        buildingPreview = true;
        previewRoot = CreateContainer("Station Preview (not saved)", transform, Vector3.zero);
        CreateDoor();
        CreateCupboard();
        CreateDrawers();
        CreateLamp();

        foreach (var previewTransform in previewRoot.GetComponentsInChildren<Transform>(true))
            previewTransform.gameObject.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild | HideFlags.NotEditable;

        buildingPreview = false;
    }

    void OnDisable()
    {
        if (previewRoot == null)
            return;

        if (Application.isPlaying)
            Destroy(previewRoot.gameObject);
        else
            DestroyImmediate(previewRoot.gameObject);

        previewRoot = null;
    }

    void Start()
    {
        if (!Application.IsPlaying(gameObject))
            return;

        CreateDoor();
        CreateCupboard();
        CreateDrawers();
        CreateLamp();
    }

    void CreateDoor()
    {
        var station = CreateContainer("Door Station", buildingPreview ? previewRoot : transform, new Vector3(-3.2f, 0f, 1.65f));
        Box("Left Frame", station, new Vector3(-0.49f, 1.05f, 0f), new Vector3(0.1f, 2.1f, 0.16f), trim);
        Box("Right Frame", station, new Vector3(0.49f, 1.05f, 0f), new Vector3(0.1f, 2.1f, 0.16f), trim);
        Box("Top Frame", station, new Vector3(0f, 2.05f, 0f), new Vector3(1.05f, 0.1f, 0.16f), trim);

        var hinge = CreateContainer("Door Hinge", station, new Vector3(-0.43f, 0f, 0f));
        Box("Door", hinge, new Vector3(0.43f, 1.02f, 0f), new Vector3(0.84f, 2.02f, 0.07f), wood);
        CreateHingeHandle(hinge, new Vector3(0.72f, 1.05f, -0.085f));
        Label(station, "DOOR\nGrab handle to open / close", new Vector3(0f, 2.3f, 0f));
    }

    void CreateCupboard()
    {
        var station = CreateContainer("Cupboard Station", buildingPreview ? previewRoot : transform, new Vector3(-1.65f, 0f, 1.7f));
        Box("Left Side", station, new Vector3(-0.42f, 0.85f, 0.15f), new Vector3(0.06f, 1.7f, 0.55f), wood);
        Box("Right Side", station, new Vector3(0.42f, 0.85f, 0.15f), new Vector3(0.06f, 1.7f, 0.55f), wood);
        Box("Top", station, new Vector3(0f, 1.68f, 0.15f), new Vector3(0.9f, 0.06f, 0.55f), wood);
        Box("Bottom", station, new Vector3(0f, 0.06f, 0.15f), new Vector3(0.9f, 0.06f, 0.55f), wood);
        Box("Back", station, new Vector3(0f, 0.85f, 0.4f), new Vector3(0.84f, 1.58f, 0.05f), wood);
        Box("Shelf", station, new Vector3(0f, 0.85f, 0.15f), new Vector3(0.8f, 0.04f, 0.5f), wood);

        var hinge = CreateContainer("Cupboard Hinge", station, new Vector3(-0.39f, 0f, -0.16f));
        Box("Cupboard Door", hinge, new Vector3(0.39f, 0.85f, 0f), new Vector3(0.78f, 1.55f, 0.05f), wood);
        CreateHingeHandle(hinge, new Vector3(0.66f, 0.9f, -0.07f));
        Label(station, "CUPBOARD\nGrab handle to open / close", new Vector3(0f, 1.96f, 0f));
    }

    void CreateHingeHandle(Transform hinge, Vector3 position)
    {
        var handle = Box("Grab Handle", hinge, position, new Vector3(0.1f, 0.13f, 0.1f), handleColor);
        if (buildingPreview)
            return;

        handle.SetActive(false);
        handle.AddComponent<XRSimpleInteractable>();
        handle.AddComponent<XRHingedHandle>().Configure(hinge, 0f, 105f);
        handle.SetActive(true);
    }

    void CreateDrawers()
    {
        var station = CreateContainer("Drawer Station", buildingPreview ? previewRoot : transform, new Vector3(1.65f, 0f, 1.7f));
        Box("Left Side", station, new Vector3(-0.42f, 0.5f, 0.16f), new Vector3(0.06f, 1f, 0.58f), wood);
        Box("Right Side", station, new Vector3(0.42f, 0.5f, 0.16f), new Vector3(0.06f, 1f, 0.58f), wood);
        Box("Top", station, new Vector3(0f, 0.98f, 0.16f), new Vector3(0.9f, 0.06f, 0.58f), wood);
        Box("Bottom", station, new Vector3(0f, 0.04f, 0.16f), new Vector3(0.9f, 0.06f, 0.58f), wood);
        Box("Back", station, new Vector3(0f, 0.5f, 0.42f), new Vector3(0.8f, 0.9f, 0.04f), wood);

        var drawer = CreateContainer("Sliding Drawer", station, new Vector3(0f, 0.62f, 0f));
        Box("Drawer Front", drawer, Vector3.zero, new Vector3(0.77f, 0.27f, 0.06f), wood);
        Box("Drawer Bottom", drawer, new Vector3(0f, -0.105f, 0.16f), new Vector3(0.7f, 0.04f, 0.4f), wood);
        Box("Drawer Left", drawer, new Vector3(-0.33f, 0f, 0.16f), new Vector3(0.04f, 0.2f, 0.4f), wood);
        Box("Drawer Right", drawer, new Vector3(0.33f, 0f, 0.16f), new Vector3(0.04f, 0.2f, 0.4f), wood);
        Box("Drawer Back", drawer, new Vector3(0f, 0f, 0.35f), new Vector3(0.7f, 0.2f, 0.04f), wood);

        var start = CreateContainer("Closed Position", station, drawer.localPosition);
        var end = CreateContainer("Open Position", station, drawer.localPosition + Vector3.back * 0.38f);
        var handle = Box("Grab Handle", drawer, new Vector3(0f, 0f, -0.075f), new Vector3(0.18f, 0.08f, 0.1f), handleColor);
        if (!buildingPreview)
        {
            handle.SetActive(false);
            var slider = handle.AddComponent<XRSlider>();
            slider.handle = drawer;
            slider.start = start;
            slider.end = end;
            slider.defaultValue = 0f;
            handle.SetActive(true);
        }

        Label(station, "DRAWER\nGrab handle to open / close", new Vector3(0f, 1.27f, 0f));
    }

    void CreateLamp()
    {
        var station = CreateContainer("Lamp Station", buildingPreview ? previewRoot : transform, new Vector3(3.2f, 0f, 1.65f));
        if (lampPrefab != null)
        {
            var lamp = Instantiate(lampPrefab, station);
            lamp.name = "Floor Lamp";
        }
        else
        {
            Box("Lamp Base", station, new Vector3(0f, 0.05f, 0f), new Vector3(0.4f, 0.1f, 0.4f), trim);
            Box("Lamp Stand", station, new Vector3(0f, 0.8f, 0f), new Vector3(0.07f, 1.5f, 0.07f), trim);
            Box("Lamp Shade", station, new Vector3(0f, 1.6f, 0f), new Vector3(0.4f, 0.3f, 0.4f), handleColor);
        }

        var lightObject = new GameObject("Lamp Light");
        lightObject.transform.SetParent(station, false);
        lightObject.transform.localPosition = new Vector3(0f, 1.45f, 0f);
        var lampLight = lightObject.AddComponent<Light>();
        lampLight.type = LightType.Point;
        lampLight.color = new Color(1f, 0.83f, 0.58f);
        lampLight.range = 4f;
        lampLight.intensity = 2f;
        lampLight.shadows = LightShadows.None;
        lampLight.enabled = false;
        var switchBase = Box("Switch Base", station, new Vector3(0.38f, 1.05f, -0.2f), new Vector3(0.22f, 0.22f, 0.06f), trim);
        var button = Box("Press Switch", station, new Vector3(0.38f, 1.05f, -0.25f), new Vector3(0.13f, 0.13f, 0.06f), handleColor);
        Label(station, "LAMP\nPress switch to turn on / off", new Vector3(0f, 2f, 0f));
        if (buildingPreview)
            return;

        var toggle = lightObject.AddComponent<ToggleLight>();
        toggle.isOn = false;

        button.SetActive(false);
        var interactable = button.AddComponent<XRSimpleInteractable>();
        var pokeFilter = button.AddComponent<XRPokeFilter>();
        pokeFilter.pokeConfiguration.Value.pokeDirection = PokeAxis.Z;
        pokeFilter.pokeInteractable = interactable;
        pokeFilter.pokeCollider = button.GetComponent<Collider>();
        var audioSource = switchBase.AddComponent<AudioSource>();
        audioSource.spatialBlend = 1f;
        audioSource.playOnAwake = false;
        interactable.selectEntered.AddListener(args =>
        {
            toggle.Flip();
            SetColor(button, toggle.isOn ? new Color(0.78f, 0.68f, 0.38f) : handleColor);
            if (switchClick != null)
                audioSource.PlayOneShot(switchClick);
        });
        button.SetActive(true);
    }

    static Transform CreateContainer(string objectName, Transform parent, Vector3 position)
    {
        var container = new GameObject(objectName);
        container.transform.SetParent(parent, false);
        container.transform.localPosition = position;
        return container.transform;
    }

    static GameObject Box(string objectName, Transform parent, Vector3 position, Vector3 scale, Color color)
    {
        var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = objectName;
        box.transform.SetParent(parent, false);
        box.transform.localPosition = position;
        box.transform.localScale = scale;
        SetColor(box, color);
        return box;
    }

    static void SetColor(GameObject target, Color color)
    {
        var renderer = target.GetComponent<Renderer>();
        var properties = new MaterialPropertyBlock();
        renderer.GetPropertyBlock(properties);
        properties.SetColor("_BaseColor", color);
        properties.SetColor("_Color", color);
        renderer.SetPropertyBlock(properties);
    }

    static void Label(Transform parent, string content, Vector3 position)
    {
        var labelObject = new GameObject("Test Label");
        labelObject.transform.SetParent(parent, false);
        labelObject.transform.localPosition = position;
        var text = labelObject.AddComponent<TextMesh>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = content;
        text.anchor = TextAnchor.MiddleCenter;
        text.alignment = TextAlignment.Center;
        text.fontSize = 48;
        text.characterSize = 0.004f;
        text.color = Color.white;
        labelObject.GetComponent<MeshRenderer>().sharedMaterial = text.font.material;
    }
}