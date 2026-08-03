#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace BonelabAdvancedHealth.MarrowEditor
{
    public static class MedicalItemSetupTool
    {
        private const string PrefabFolder = "Assets/AdvancedHealthSystem/MedicalItems";

        [MenuItem("Advanced Health System/Medical Items/Setup Selected By Name")]
        public static void SetupSelectedByName()
        {
            GameObject[] selection = Selection.gameObjects;
            for (int i = 0; i < selection.Length; i++)
                Setup(selection[i], InferType(selection[i].name));
        }

        [MenuItem("Advanced Health System/Medical Items/Build Prefabs From Imported Models")]
        public static void BuildPrefabsFromImportedModels()
        {
            BuildPrefabFromModel("Bandage", FindModelAsset("bandage", "бинт"));
            BuildPrefabFromModel("Tourniquet", FindModelAsset("tourniquet", "турникет"));
            BuildPrefabFromModel("Splint", FindModelAsset("splint", "шина"));
            BuildPrefabFromModel("Morphine", FindModelAsset("morphine", "syringe", "морфин"));
            BuildPrefabFromModel("Medkit", FindModelAsset("medkit", "medcit", "аптеч"));
            BuildPrefabFromModel("Painkillers", FindModelAsset("painkillers", "painkiller", "pill"));
            BuildPrefabFromModel("BloodPack", FindModelAsset("bloodbag", "blood_pack", "bloodpack"));
            BuildPrefabFromModel("Adrenaline", FindModelAsset("adrenaline", "адреналин"));
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        [MenuItem("Advanced Health System/Medical Items/Setup Selected As/Bandage")]
        public static void SetupBandage() => SetupSelection("Bandage");

        [MenuItem("Advanced Health System/Medical Items/Setup Selected As/Splint")]
        public static void SetupSplint() => SetupSelection("Splint");

        [MenuItem("Advanced Health System/Medical Items/Setup Selected As/Painkillers")]
        public static void SetupPainkillers() => SetupSelection("Painkillers");

        [MenuItem("Advanced Health System/Medical Items/Setup Selected As/Morphine")]
        public static void SetupMorphine() => SetupSelection("Morphine");

        [MenuItem("Advanced Health System/Medical Items/Setup Selected As/Medkit")]
        public static void SetupMedkit() => SetupSelection("Medkit");

        [MenuItem("Advanced Health System/Medical Items/Setup Selected As/Tourniquet")]
        public static void SetupTourniquet() => SetupSelection("Tourniquet");

        [MenuItem("Advanced Health System/Medical Items/Setup Selected As/BloodPack")]
        public static void SetupBloodPack() => SetupSelection("BloodPack");

        [MenuItem("Advanced Health System/Medical Items/Setup Selected As/Adrenaline")]
        public static void SetupAdrenaline() => SetupSelection("Adrenaline");

        private static void SetupSelection(string itemType)
        {
            GameObject[] selection = Selection.gameObjects;
            for (int i = 0; i < selection.Length; i++)
                Setup(selection[i], itemType);
        }

        private static void Setup(GameObject root, string itemType)
        {
            if (root == null)
                return;

            Undo.RegisterFullObjectHierarchyUndo(root, "Setup AHS medical item");
            root.name = "AHS_Medical_" + itemType;
            root.layer = LayerMask.NameToLayer("Interactable");

            Rigidbody rb = root.GetComponent<Rigidbody>() ?? root.AddComponent<Rigidbody>();
            ConfigureRigidbody(rb, itemType);

            Collider mainCollider = EnsureMainCollider(root, itemType);
            EnsureGripPoint(root, itemType);
            Collider gripCollider = EnsureGripVolume(root, itemType);
            ConfigureMarrow(root, mainCollider, gripCollider, itemType);
            EnsureUsePoint(root);
            EnsureMarker(root, itemType);
            SavePrefab(root, itemType);

            EditorUtility.SetDirty(root);
        }

        private static void BuildPrefabFromModel(string itemType, string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
            {
                Debug.LogWarning("AHS medical model not found for " + itemType + ". Import/select it manually and run Setup Selected As/" + itemType + ".");
                return;
            }

            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (source == null)
            {
                Debug.LogWarning("AHS failed to load medical model for " + itemType + ": " + assetPath);
                return;
            }

            GameObject root = new GameObject("AHS_Medical_" + itemType);
            GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(source);
            if (model != null)
            {
                model.name = source.name;
                model.transform.SetParent(root.transform, false);
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;
                model.transform.localScale = Vector3.one;
            }

            try
            {
                Setup(root, itemType);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void ConfigureRigidbody(Rigidbody rb, string itemType)
        {
            rb.mass = GetMass(itemType);
            rb.drag = 0.08f;
            rb.angularDrag = 0.18f;
            rb.useGravity = true;
            rb.isKinematic = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.maxAngularVelocity = 18f;
            rb.sleepThreshold = 0.005f;
        }

        private static Collider EnsureMainCollider(GameObject root, string itemType)
        {
            Collider existing = root.GetComponent<Collider>();
            if (existing != null && !existing.isTrigger)
                return existing;

            Bounds bounds = CalculateRendererBounds(root);
            Vector3 size = bounds.size;
            if (size.sqrMagnitude < 0.0001f)
                size = GetDefaultSize(itemType);

            if (IsCylinderItem(itemType))
            {
                CapsuleCollider capsule = root.AddComponent<CapsuleCollider>();
                capsule.direction = 2;
                capsule.radius = Mathf.Max(0.018f, Mathf.Min(size.x, size.y) * 0.5f);
                capsule.height = Mathf.Max(size.z, capsule.radius * 2.2f);
                capsule.center = root.transform.InverseTransformPoint(bounds.center);
                capsule.isTrigger = false;
                return capsule;
            }

            BoxCollider box = root.AddComponent<BoxCollider>();
            box.size = new Vector3(Mathf.Max(0.04f, size.x), Mathf.Max(0.03f, size.y), Mathf.Max(0.04f, size.z));
            box.center = root.transform.InverseTransformPoint(bounds.center);
            box.isTrigger = false;
            return box;
        }

        private static Transform EnsureGripPoint(GameObject root, string itemType)
        {
            Transform grip = root.transform.Find("AHS_GripPoint") ?? root.transform.Find("GripPoint");
            if (grip == null)
            {
                GameObject go = new GameObject("AHS_GripPoint");
                go.transform.SetParent(root.transform, false);
                grip = go.transform;
            }

            grip.localPosition = Vector3.zero;
            grip.localRotation = IsCylinderItem(itemType) ? Quaternion.Euler(0f, 90f, 0f) : Quaternion.identity;
            return grip;
        }

        private static Collider EnsureGripVolume(GameObject root, string itemType)
        {
            Transform existing = root.transform.Find("AHS_GripVolume");
            GameObject volume = existing != null ? existing.gameObject : new GameObject("AHS_GripVolume");
            volume.transform.SetParent(root.transform, false);
            volume.transform.localPosition = Vector3.zero;
            volume.transform.localRotation = Quaternion.identity;

            Collider oldCollider = volume.GetComponent<Collider>();
            if (oldCollider != null)
                UnityEngine.Object.DestroyImmediate(oldCollider);

            Vector3 size = GetDefaultSize(itemType);
            if (IsCylinderItem(itemType))
            {
                CapsuleCollider capsule = volume.AddComponent<CapsuleCollider>();
                capsule.direction = 2;
                capsule.radius = 0.055f;
                capsule.height = Mathf.Max(0.24f, size.z);
                capsule.isTrigger = true;
                return capsule;
            }

            BoxCollider box = volume.AddComponent<BoxCollider>();
            box.size = size + Vector3.one * 0.045f;
            box.isTrigger = true;
            return box;
        }

        private static void ConfigureMarrow(GameObject root, Collider mainCollider, Collider gripCollider, string itemType)
        {
            Debug.LogWarning("[ZBC] Reflective Marrow grip/entity setup is disabled for load safety. Add InteractableHost/Grip through the Marrow SDK inspector when rebuilding native pickup prefabs.");
        }

        private static Transform EnsureUsePoint(GameObject root)
        {
            Transform usePoint = root.transform.Find("AHS_UsePoint") ?? root.transform.Find("UsePoint");
            if (usePoint == null)
            {
                GameObject go = new GameObject("AHS_UsePoint");
                go.transform.SetParent(root.transform, false);
                usePoint = go.transform;
            }

            usePoint.localPosition = Vector3.forward * 0.06f;
            usePoint.localRotation = Quaternion.identity;
            return usePoint;
        }

        private static void EnsureMarker(GameObject root, string itemType)
        {
            Transform marker = root.transform.Find("AHS_ItemType_" + itemType);
            if (marker != null)
                return;

            GameObject go = new GameObject("AHS_ItemType_" + itemType);
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
        }

        private static void SavePrefab(GameObject root, string itemType)
        {
            if (!AssetDatabase.IsValidFolder("Assets/AdvancedHealthSystem"))
                AssetDatabase.CreateFolder("Assets", "AdvancedHealthSystem");
            if (!AssetDatabase.IsValidFolder(PrefabFolder))
                AssetDatabase.CreateFolder("Assets/AdvancedHealthSystem", "MedicalItems");

            string path = PrefabFolder + "/AHS_Medical_" + itemType + ".prefab";
            PrefabUtility.SaveAsPrefabAssetAndConnect(root, path, InteractionMode.UserAction);
        }

        private static Bounds CalculateRendererBounds(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                return new Bounds(root.transform.position, GetDefaultSize(InferType(root.name)));

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        private static string InferType(string name)
        {
            if (Contains(name, "splint"))
                return "Splint";
            if (Contains(name, "morph"))
                return "Morphine";
            if (Contains(name, "pain") || Contains(name, "pill"))
                return "Painkillers";
            if (Contains(name, "medkit") || Contains(name, "med kit"))
                return "Medkit";
            return "Bandage";
        }

        private static string FindModelAsset(params string[] names)
        {
            string[] guids = AssetDatabase.FindAssets("t:Model", new[] { "Assets" });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                string file = Path.GetFileNameWithoutExtension(path);
                for (int n = 0; n < names.Length; n++)
                {
                    if (Contains(file, names[n]))
                        return path;
                }
            }

            return string.Empty;
        }

        private static bool IsCylinderItem(string itemType)
        {
            return itemType == "Morphine" || itemType == "Adrenaline" || itemType == "Tourniquet";
        }

        private static float GetMass(string itemType)
        {
            switch (itemType)
            {
                case "Medkit":
                    return 0.72f;
                case "Splint":
                    return 0.28f;
                case "BloodPack":
                    return 0.34f;
                case "Bandage":
                    return 0.16f;
                case "Painkillers":
                    return 0.10f;
                default:
                    return 0.12f;
            }
        }

        private static Vector3 GetDefaultSize(string itemType)
        {
            switch (itemType)
            {
                case "Medkit":
                    return new Vector3(0.28f, 0.16f, 0.20f);
                case "Splint":
                    return new Vector3(0.11f, 0.055f, 0.42f);
                case "Morphine":
                case "Adrenaline":
                    return new Vector3(0.055f, 0.055f, 0.28f);
                case "Tourniquet":
                    return new Vector3(0.07f, 0.07f, 0.30f);
                case "BloodPack":
                    return new Vector3(0.18f, 0.05f, 0.23f);
                case "Painkillers":
                    return new Vector3(0.13f, 0.07f, 0.09f);
                default:
                    return new Vector3(0.22f, 0.08f, 0.12f);
            }
        }

        private static bool Contains(string haystack, string needle)
        {
            return haystack != null && haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static Component AddComponentIfAvailable(GameObject target, string typeName)
        {
            Type type = FindType(typeName);
            if (type == null || !typeof(Component).IsAssignableFrom(type))
                return null;
            return target.GetComponent(type) ?? target.AddComponent(type);
        }

        private static Type FindType(string fullName)
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Type type = assemblies[i].GetType(fullName, false);
                if (type != null)
                    return type;
            }
            return null;
        }

        private static void SetMember(object target, string memberName, object value)
        {
            if (target == null)
                return;

            Type type = target.GetType();
            FieldInfo field = type.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null && IsAssignable(field.FieldType, value))
            {
                field.SetValue(target, value);
                return;
            }

            PropertyInfo property = type.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null && property.CanWrite && IsAssignable(property.PropertyType, value))
                property.SetValue(target, value, null);
        }

        private static void SetColliderArray(object target, string memberName, Collider collider)
        {
            if (target == null || collider == null)
                return;

            Type type = target.GetType();
            FieldInfo field = type.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null && field.FieldType.IsAssignableFrom(typeof(Collider[])))
            {
                field.SetValue(target, new[] { collider });
                return;
            }

            PropertyInfo property = type.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null && property.CanWrite && property.PropertyType.IsAssignableFrom(typeof(Collider[])))
                property.SetValue(target, new[] { collider }, null);
        }

        private static bool IsAssignable(Type targetType, object value)
        {
            return value == null || targetType.IsInstanceOfType(value) || targetType.IsAssignableFrom(value.GetType());
        }

        private static void InvokeIfAvailable(object target, string methodName)
        {
            if (target == null)
                return;
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
            if (method != null)
                method.Invoke(target, null);
        }
    }
}
#endif
