#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Adds a row of physics test stations to PlayerTestScene, behind the player's spawn point.
/// Runs once when the scene is open and does nothing while "Physics Test Stations" exists.
/// Delete that object and use the menu item to build the stations again.
/// </summary>
[InitializeOnLoad]
static class PhysicsTestStationsSetup
{
    const string ScenePath = "Assets/HouseOfGhosts/Scenes/LeoScenes/PlayerTestScene.unity";
    const string RootName = "Physics Test Stations";
    const string MaterialFolder = "Assets/_Course Library/Materials/";

    // The row stands behind the spawn point and is turned around to face it.
    static readonly Vector3 k_RowPosition = new Vector3(1f, 0f, -3.6f);
    const float k_TableHeight = 0.8f;

    static Material s_Wood, s_Dark, s_Grey, s_Yellow, s_Red, s_Blue, s_Green, s_Orange;

    static PhysicsTestStationsSetup()
    {
        EditorSceneManager.sceneOpened += OnSceneOpened;
        EditorApplication.delayCall += () => SetUp(false);
    }

    static void OnSceneOpened(Scene scene, OpenSceneMode mode)
    {
        if (scene.path == ScenePath)
            EditorApplication.delayCall += () => SetUp(false);
    }

    [MenuItem("Tools/House of Ghosts/Physics/Create Test Stations")]
    static void SetUpFromMenu()
    {
        SetUp(true);
    }

    static void SetUp(bool fromMenu)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            if (fromMenu)
                Debug.LogWarning("Exit Play Mode before creating the physics test stations.");
            return;
        }

        var scene = SceneManager.GetSceneByPath(ScenePath);
        if (!scene.IsValid() || !scene.isLoaded)
        {
            if (fromMenu)
                Debug.LogWarning("Open PlayerTestScene before creating the physics test stations.");
            return;
        }

        foreach (var existing in scene.GetRootGameObjects())
        {
            if (existing.name != RootName)
                continue;
            if (fromMenu)
                Debug.LogWarning($"'{RootName}' already exists. Delete it first to build the stations again.", existing);
            return;
        }

        var wasDirty = scene.isDirty;
        var root = new GameObject(RootName);
        SceneManager.MoveGameObjectToScene(root, scene);
        root.transform.SetPositionAndRotation(k_RowPosition, Quaternion.Euler(0f, 180f, 0f));
        Undo.RegisterCreatedObjectUndo(root, "Create Physics Test Stations");
        try
        {
            LoadMaterials();
            var row = root.transform;
            Label(row, "PHYSICS TEST AREA", new Vector3(0f, 2.95f, 0f), 0.009f);
            CreateHandStopWall(Container("Hand Stop Wall Station", row, new Vector3(-5f, 0f, 0f)));
            CreateWeights(Container("Weights Station", row, new Vector3(-3f, 0f, 0f)));
            CreateBlockTower(Container("Block Tower Station", row, new Vector3(-1f, 0f, 0f)));
            CreatePunchingBag(Container("Punching Bag Station", row, new Vector3(1f, 0f, 0f)));
            CreateCatapult(Container("Catapult Station", row, new Vector3(3f, 0f, 0f)));
            CreateBodyPush(Container("Body Push Station", row, new Vector3(5f, 0f, 0f)));
            var resetButton = CreateResetButton(Container("Reset Station", row, new Vector3(0f, 0f, -1.3f)));
            root.AddComponent<PhysicsTestReset>().Configure(resetButton);
        }
        catch (Exception exception)
        {
            Undo.DestroyObjectImmediate(root);
            Debug.LogError("The physics test stations could not be created; the scene was left unchanged: " + exception.Message);
            return;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        if (wasDirty)
            Debug.Log("Physics test stations were added to PlayerTestScene. Save the scene to keep them.", root);
        else if (!EditorSceneManager.SaveScene(scene))
            Debug.LogWarning("Physics test stations were added, but the scene could not be saved.", root);
        else
            Debug.Log("Physics test stations were added to PlayerTestScene and the scene was saved.", root);
    }

    // A wall to push a hand against, with a window to reach through.
    static void CreateHandStopWall(Transform station)
    {
        Box("Wall Left", station, new Vector3(-0.5f, 1f, 0f), new Vector3(0.6f, 2f, 0.15f), s_Grey);
        Box("Wall Right", station, new Vector3(0.5f, 1f, 0f), new Vector3(0.6f, 2f, 0.15f), s_Grey);
        Box("Wall Below Window", station, new Vector3(0f, 0.5f, 0f), new Vector3(0.4f, 1f, 0.15f), s_Grey);
        Box("Wall Above Window", station, new Vector3(0f, 1.7f, 0f), new Vector3(0.4f, 0.6f, 0.15f), s_Grey);
        Box("Shelf Behind Window", station, new Vector3(0f, 0.97f, 0.25f), new Vector3(0.5f, 0.04f, 0.35f), s_Wood);
        var ball = Shape(PrimitiveType.Sphere, "Ball Behind Window", station, new Vector3(0f, 1.04f, 0.25f), Vector3.one * 0.1f, s_Red);
        MakeLoose(ball, 0.2f, true);
        Label(station, "HAND STOP WALL\nPush your hand into the wall: it stops\nReach through the window for the ball", new Vector3(0f, 2.3f, 0f));
    }

    // Three cubes that look alike but weigh very different amounts.
    static void CreateWeights(Transform station)
    {
        CreateTable(station, 1.2f, 0.5f);
        CreateWeight(station, "Light Cube", -0.35f, 0.12f, 0.2f, true, s_Yellow, "0.2 kg");
        CreateWeight(station, "Medium Cube", 0f, 0.16f, 3f, true, s_Orange, "3 kg");
        CreateWeight(station, "Heavy Cube", 0.35f, 0.22f, 25f, false, s_Dark, "25 kg");
        Label(station, "WEIGHTS\nPush them with your hand\nThe heavy one is too heavy to lift", new Vector3(0f, 1.6f, 0f));
    }

    static void CreateWeight(Transform station, string name, float x, float size, float mass, bool grabbable, Material material, string label)
    {
        var cube = Box(name, station, new Vector3(x, k_TableHeight + size * 0.5f, 0f), Vector3.one * size, material);
        MakeLoose(cube, mass, grabbable);
        Label(station, label, new Vector3(x, k_TableHeight + size + 0.09f, 0f), 0.003f);
    }

    // A pyramid of light blocks to punch, and balls to throw at it.
    static void CreateBlockTower(Transform station)
    {
        CreateTable(station, 1.2f, 0.5f);
        const float size = 0.1f;
        const float spacing = 0.105f;
        var colors = new[] { s_Blue, s_Green, s_Yellow, s_Red };
        for (var row = 0; row < 4; row++)
        {
            var count = 4 - row;
            for (var index = 0; index < count; index++)
            {
                var x = (index - (count - 1) * 0.5f) * spacing;
                var block = Box("Block", station, new Vector3(x, k_TableHeight + size * 0.5f + row * size, 0.1f), Vector3.one * size, colors[row]);
                MakeLoose(block, 0.15f, true);
            }
        }

        for (var index = 0; index < 3; index++)
        {
            var ball = Shape(PrimitiveType.Sphere, "Throwing Ball", station,
                new Vector3(-0.42f + index * 0.12f, k_TableHeight + 0.045f, -0.15f), Vector3.one * 0.09f, s_Orange);
            MakeLoose(ball, 0.25f, true);
        }
        Label(station, "BLOCK TOWER\nPunch it, or throw the balls at it", new Vector3(0f, 1.6f, 0f));
    }

    // A heavy bag hanging from a frame that swings when it is hit.
    static void CreatePunchingBag(Transform station)
    {
        Box("Left Post", station, new Vector3(-0.6f, 1.1f, 0f), new Vector3(0.1f, 2.2f, 0.1f), s_Dark);
        Box("Right Post", station, new Vector3(0.6f, 1.1f, 0f), new Vector3(0.1f, 2.2f, 0.1f), s_Dark);
        Box("Top Beam", station, new Vector3(0f, 2.25f, 0f), new Vector3(1.3f, 0.1f, 0.1f), s_Dark);

        // The bag is 0.9 m tall and hangs 0.45 m below the beam; all child values are in the bag's scaled space.
        var bag = Shape(PrimitiveType.Capsule, "Punching Bag", station, new Vector3(0f, 1.3f, 0f), new Vector3(0.35f, 0.45f, 0.35f), s_Red);
        var rope = Shape(PrimitiveType.Cylinder, "Rope", bag.transform, new Vector3(0f, 1.5f, 0f), new Vector3(0.086f, 0.5f, 0.086f), s_Dark);
        UnityEngine.Object.DestroyImmediate(rope.GetComponent<Collider>());

        var body = MakeLoose(bag, 6f, false);
        body.angularDamping = 0.3f;
        var joint = bag.AddComponent<ConfigurableJoint>();
        joint.anchor = new Vector3(0f, 2f, 0f);
        joint.xMotion = ConfigurableJointMotion.Locked;
        joint.yMotion = ConfigurableJointMotion.Locked;
        joint.zMotion = ConfigurableJointMotion.Locked;
        Label(station, "PUNCHING BAG\nHit it", new Vector3(0f, 2.55f, 0f));
    }

    // A seesaw with a short and a long arm: slapping the short end throws the ball from the long end.
    static void CreateCatapult(Transform station)
    {
        CreateTable(station, 1.2f, 0.5f);
        const float pivotX = -0.2f;
        const float pivotHeight = k_TableHeight + 0.25f;
        Box("Fulcrum", station, new Vector3(pivotX, k_TableHeight + 0.11f, 0f), new Vector3(0.04f, 0.22f, 0.3f), s_Dark);

        var plank = Container("Catapult Plank", station, new Vector3(0f, pivotHeight, 0f));
        Box("Board", plank, Vector3.zero, new Vector3(0.9f, 0.03f, 0.16f), s_Wood);
        Box("Cup End", plank, new Vector3(0.44f, 0.035f, 0f), new Vector3(0.02f, 0.04f, 0.16f), s_Wood);
        Box("Cup Inner", plank, new Vector3(0.3f, 0.035f, 0f), new Vector3(0.02f, 0.04f, 0.16f), s_Wood);
        Box("Cup Front", plank, new Vector3(0.37f, 0.035f, -0.07f), new Vector3(0.12f, 0.04f, 0.02f), s_Wood);
        Box("Cup Back", plank, new Vector3(0.37f, 0.035f, 0.07f), new Vector3(0.12f, 0.04f, 0.02f), s_Wood);
        Box("Slap Here", plank, new Vector3(-0.39f, 0.02f, 0f), new Vector3(0.1f, 0.01f, 0.14f), s_Red);

        var body = MakeLoose(plank.gameObject, 1.5f, false);
        body.angularDamping = 0.2f;
        var hinge = plank.gameObject.AddComponent<HingeJoint>();
        hinge.anchor = new Vector3(pivotX, 0f, 0f);
        hinge.axis = Vector3.forward;
        // The limits stop the plank just before either end touches the table.
        hinge.limits = new JointLimits { min = -18f, max = 18f };
        hinge.useLimits = true;

        var ball = Shape(PrimitiveType.Sphere, "Catapult Ball", station, new Vector3(0.37f, pivotHeight + 0.06f, 0f), Vector3.one * 0.09f, s_Yellow);
        MakeLoose(ball, 0.15f, true);
        Label(station, "CATAPULT\nPut the ball in the cup\nSlap the red end down", new Vector3(0f, 1.75f, 0f));
    }

    // Flap doors and boxes for the player's body, not the hands.
    static void CreateBodyPush(Transform station)
    {
        Box("Left Post", station, new Vector3(-0.56f, 0.75f, 0f), new Vector3(0.08f, 1.5f, 0.08f), s_Dark);
        Box("Right Post", station, new Vector3(0.56f, 0.75f, 0f), new Vector3(0.08f, 1.5f, 0.08f), s_Dark);
        CreateFlap(station, "Left Flap", -1f);
        CreateFlap(station, "Right Flap", 1f);

        MakeLoose(Box("Large Box", station, new Vector3(-0.3f, 0.2f, 1.2f), Vector3.one * 0.4f, s_Wood), 3f, false);
        MakeLoose(Box("Medium Box", station, new Vector3(0.25f, 0.175f, 1.3f), Vector3.one * 0.35f, s_Wood), 2f, false);
        MakeLoose(Box("Small Box", station, new Vector3(-0.3f, 0.55f, 1.2f), Vector3.one * 0.3f, s_Wood), 1.5f, false);
        Label(station, "BODY PUSH\nWalk through the flap doors\nand into the boxes", new Vector3(0f, 1.85f, 0f));
    }

    static void CreateFlap(Transform station, string name, float side)
    {
        var flap = Box(name, station, new Vector3(side * 0.255f, 0.9f, 0f), new Vector3(0.49f, 0.8f, 0.03f), s_Wood);
        MakeLoose(flap, 1f, false);
        var hinge = flap.AddComponent<HingeJoint>();
        // The hinge sits on the flap's outer edge, next to the post. The anchor is in the flap's scaled space.
        hinge.anchor = new Vector3(side * 0.5f, 0f, 0f);
        hinge.axis = Vector3.up;
        hinge.limits = new JointLimits { min = -110f, max = 110f };
        hinge.useLimits = true;
        // A weak spring swings the flap shut again.
        hinge.spring = new JointSpring { spring = 0.08f, damper = 0.01f, targetPosition = 0f };
        hinge.useSpring = true;
    }

    static XRSimpleInteractable CreateResetButton(Transform station)
    {
        Box("Post", station, new Vector3(0f, 0.55f, 0f), new Vector3(0.12f, 1.1f, 0.12f), s_Dark);
        Box("Switch Base", station, new Vector3(0f, 1.05f, -0.08f), new Vector3(0.22f, 0.22f, 0.06f), s_Dark);
        var button = Box("Reset Button", station, new Vector3(0f, 1.05f, -0.13f), new Vector3(0.13f, 0.13f, 0.06f), s_Red);
        Label(station, "RESET\nPress to put everything back", new Vector3(0f, 1.4f, 0f));

        button.SetActive(false);
        var interactable = button.AddComponent<XRSimpleInteractable>();
        var pokeFilter = button.AddComponent<XRPokeFilter>();
        pokeFilter.pokeConfiguration.Value.pokeDirection = PokeAxis.Z;
        pokeFilter.pokeInteractable = interactable;
        pokeFilter.pokeCollider = button.GetComponent<Collider>();
        button.SetActive(true);
        return interactable;
    }

    static void CreateTable(Transform station, float width, float depth)
    {
        Box("Table Top", station, new Vector3(0f, k_TableHeight - 0.02f, 0f), new Vector3(width, 0.04f, depth), s_Wood);
        var legHeight = k_TableHeight - 0.04f;
        for (var x = -1; x <= 1; x += 2)
        {
            for (var z = -1; z <= 1; z += 2)
            {
                Box("Table Leg", station, new Vector3(x * (width * 0.5f - 0.05f), legHeight * 0.5f, z * (depth * 0.5f - 0.05f)),
                    new Vector3(0.06f, legHeight, 0.06f), s_Dark);
            }
        }
    }

    // Turns a shape into a loose physics object, optionally one that can be picked up and thrown.
    static Rigidbody MakeLoose(GameObject target, float mass, bool grabbable)
    {
        var body = target.AddComponent<Rigidbody>();
        body.mass = mass;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        // Thrown objects are fast and small; continuous detection keeps them from flying through walls.
        body.collisionDetectionMode = grabbable ? CollisionDetectionMode.ContinuousDynamic : CollisionDetectionMode.Discrete;
        if (!grabbable)
            return body;

        target.SetActive(false);
        var grab = target.AddComponent<XRGrabInteractable>();
        // Velocity tracking makes a held object collide with the world; dynamic attach grabs it where the hand is.
        grab.movementType = XRBaseInteractable.MovementType.VelocityTracking;
        grab.useDynamicAttach = true;
        grab.throwOnDetach = true;
        target.SetActive(true);
        return body;
    }

    static Transform Container(string name, Transform parent, Vector3 position)
    {
        var container = new GameObject(name);
        container.transform.SetParent(parent, false);
        container.transform.localPosition = position;
        return container.transform;
    }

    static GameObject Box(string name, Transform parent, Vector3 position, Vector3 scale, Material material)
    {
        return Shape(PrimitiveType.Cube, name, parent, position, scale, material);
    }

    static GameObject Shape(PrimitiveType type, string name, Transform parent, Vector3 position, Vector3 scale, Material material)
    {
        var shape = GameObject.CreatePrimitive(type);
        shape.name = name;
        shape.transform.SetParent(parent, false);
        shape.transform.localPosition = position;
        shape.transform.localScale = scale;
        if (material != null)
            shape.GetComponent<Renderer>().sharedMaterial = material;
        return shape;
    }

    static void Label(Transform parent, string content, Vector3 position, float characterSize = 0.004f)
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
        text.characterSize = characterSize;
        text.color = Color.white;
        labelObject.GetComponent<MeshRenderer>().sharedMaterial = text.font.material;
    }

    static void LoadMaterials()
    {
        s_Wood = LoadMaterial("Object Materials/Material_Wood_Rustic.mat");
        s_Dark = LoadMaterial("Solid Colors/GreyDark.mat");
        s_Grey = LoadMaterial("Solid Colors/Grey.mat");
        s_Yellow = LoadMaterial("Solid Colors/Yellow.mat");
        s_Red = LoadMaterial("Solid Colors/Red.mat");
        s_Blue = LoadMaterial("Solid Colors/Blue.mat");
        s_Green = LoadMaterial("Solid Colors/Green.mat");
        s_Orange = LoadMaterial("Solid Colors/Orange.mat");
    }

    static Material LoadMaterial(string relativePath)
    {
        return AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + relativePath);
    }
}
#endif
