#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace BonelabAdvancedHealth.MarrowEditor
{
    public static class MedicalItemPrefabBuilder
    {
        private const string OutputFolder = "Assets/AdvancedHealthSystem/GeneratedMedicalItems";

        private static readonly ItemDefinition[] Items =
        {
            new ItemDefinition("Bandage", PrimitiveShape.Box, new Vector3(0.22f, 0.08f, 0.12f), 0.16f, new Color(0.92f, 0.90f, 0.82f)),
            new ItemDefinition("Tourniquet", PrimitiveShape.CapsuleZ, new Vector3(0.30f, 0.07f, 0.07f), 0.12f, new Color(0.03f, 0.03f, 0.035f)),
            new ItemDefinition("Morphine", PrimitiveShape.CapsuleZ, new Vector3(0.28f, 0.055f, 0.055f), 0.12f, new Color(0.72f, 0.86f, 1.00f)),
            new ItemDefinition("Painkillers", PrimitiveShape.Box, new Vector3(0.13f, 0.07f, 0.09f), 0.10f, new Color(0.92f, 0.90f, 0.78f)),
            new ItemDefinition("Adrenaline", PrimitiveShape.CapsuleZ, new Vector3(0.28f, 0.055f, 0.055f), 0.12f, new Color(1.00f, 0.86f, 0.18f)),
            new ItemDefinition("Medkit", PrimitiveShape.Box, new Vector3(0.28f, 0.16f, 0.20f), 0.72f, new Color(0.82f, 0.10f, 0.10f)),
            new ItemDefinition("Splint", PrimitiveShape.Box, new Vector3(0.11f, 0.055f, 0.42f), 0.28f, new Color(0.63f, 0.45f, 0.25f)),
            new ItemDefinition("BloodPack", PrimitiveShape.Box, new Vector3(0.18f, 0.05f, 0.23f), 0.34f, new Color(0.55f, 0.02f, 0.025f))
        };

        [MenuItem("Advanced Health System/Medical Items/Legacy/Rebuild Generated Placeholder Prefabs")]
        public static void BuildAll()
        {
            if (!AssetDatabase.IsValidFolder("Assets/AdvancedHealthSystem"))
                AssetDatabase.CreateFolder("Assets", "AdvancedHealthSystem");
            if (!AssetDatabase.IsValidFolder(OutputFolder))
                AssetDatabase.CreateFolder("Assets/AdvancedHealthSystem", "GeneratedMedicalItems");

            for (int i = 0; i < Items.Length; i++)
                BuildPrefab(Items[i]);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void BuildPrefab(ItemDefinition item)
        {
            GameObject root = new GameObject("AHS_Medical_" + item.Name);
            try
            {
                root.transform.position = Vector3.zero;
                root.transform.rotation = Quaternion.identity;
                root.layer = LayerMask.NameToLayer("Interactable");

                Collider mainCollider = AddCollider(root, item.Shape, item.Size, false);
                Rigidbody rb = root.AddComponent<Rigidbody>();
                rb.mass = item.Mass;
                rb.drag = 0.08f;
                rb.angularDrag = 0.18f;
                rb.useGravity = true;
                rb.isKinematic = false;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                rb.maxAngularVelocity = 18f;
                rb.sleepThreshold = 0.005f;

                BuildVisual(root.transform, item);
                GameObject gripPoint = CreateChild(root.transform, "AHS_GripPoint", Vector3.zero, item.Shape == PrimitiveShape.CapsuleZ ? Quaternion.Euler(0f, 90f, 0f) : Quaternion.identity);
                Collider gripCollider = CreateGripVolume(root.transform, item);

                Component host = AddComponentIfAvailable(root, "SLZ.Marrow.InteractableHost");
                if (host != null)
                {
                    SetMember(host, "ignoreBodyOnGrab", false);
                    InvokeIfAvailable(host, "EnableInteraction");
                    InvokeIfAvailable(host, "EnableFarHover");
                }

                Component grip = AddComponentIfAvailable(root, item.Shape == PrimitiveShape.CapsuleZ ? "SLZ.Marrow.CylinderGrip" : "SLZ.Marrow.BoxGrip");
                if (grip != null)
                {
                    SetMember(grip, "targetTransform", gripPoint.transform);
                    SetMember(grip, "Host", host);
                    SetMember(grip, "isThrowable", true);
                    SetMember(grip, "ignoreGripTargetOnAttach", false);
                    SetMember(grip, "priority", 1.0f);
                    SetMember(grip, "gripDistance", 0.26f);
                    SetMember(grip, "primaryMovementAxis", Vector3.forward);
                    SetMember(grip, "secondaryMovementAxis", Vector3.up);
                    SetColliderArray(grip, "gripColliders", gripCollider);
                    SetColliderArray(grip, "additionalGripColliders", mainCollider);
                    SetMember(grip, "dynamicFriction", 0.55f);
                    SetMember(grip, "staticFriction", 0.72f);
                    SetMember(grip, "limit", 0.14f);
                    SetMember(grip, "hasCapA", true);
                    SetMember(grip, "hasCapB", true);
                    SetMember(grip, "rotationalFrictionMult", 0.75f);
                    SetMember(grip, "aspectRatio", 4.0f);
                    SetMember(grip, "handWidth", 0.085f);
                    InvokeIfAvailable(grip, "EnableInteraction");
                }

                string path = OutputFolder + "/AHS_Medical_" + item.Name + ".prefab";
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static Collider AddCollider(GameObject root, PrimitiveShape shape, Vector3 size, bool trigger)
        {
            if (shape == PrimitiveShape.CapsuleZ)
            {
                CapsuleCollider capsule = root.AddComponent<CapsuleCollider>();
                capsule.direction = 2;
                capsule.radius = Mathf.Max(size.y, size.z) * 0.5f;
                capsule.height = Mathf.Max(size.x, capsule.radius * 2.0f);
                capsule.isTrigger = trigger;
                return capsule;
            }

            BoxCollider box = root.AddComponent<BoxCollider>();
            box.size = size;
            box.isTrigger = trigger;
            return box;
        }

        private static Collider CreateGripVolume(Transform root, ItemDefinition item)
        {
            GameObject volume = CreateChild(root, "AHS_GripVolume", Vector3.zero, Quaternion.identity);
            Collider collider = AddCollider(volume, item.Shape, item.Shape == PrimitiveShape.CapsuleZ ? new Vector3(item.Size.x, 0.096f, 0.096f) : item.Size + Vector3.one * 0.045f, true);
            return collider;
        }

        private static void BuildVisual(Transform root, ItemDefinition item)
        {
            switch (item.Name)
            {
                case "Bandage":
                    AddVisual(root, PrimitiveType.Cube, "GauzePad", Vector3.zero, item.Size, item.Color);
                    AddVisual(root, PrimitiveType.Cube, "WrapStripeA", new Vector3(-0.055f, 0.048f, 0f), new Vector3(0.018f, 0.014f, 0.13f), Color.white);
                    AddVisual(root, PrimitiveType.Cube, "WrapStripeB", new Vector3(0.055f, 0.048f, 0f), new Vector3(0.018f, 0.014f, 0.13f), Color.white);
                    break;
                case "Tourniquet":
                    AddVisual(root, PrimitiveType.Cylinder, "Windlass", Vector3.zero, new Vector3(0.035f, 0.16f, 0.035f), item.Color, Quaternion.Euler(90f, 0f, 0f));
                    AddVisual(root, PrimitiveType.Cube, "Strap", new Vector3(0f, -0.01f, 0f), new Vector3(0.22f, 0.018f, 0.055f), item.Color);
                    AddVisual(root, PrimitiveType.Cube, "Buckle", new Vector3(0.115f, 0f, 0f), new Vector3(0.035f, 0.04f, 0.07f), new Color(0.55f, 0.56f, 0.58f));
                    break;
                case "Morphine":
                case "Adrenaline":
                    AddSyringeVisual(root, item);
                    break;
                case "Painkillers":
                    AddVisual(root, PrimitiveType.Cube, "PillBottle", Vector3.zero, item.Size, item.Color);
                    AddVisual(root, PrimitiveType.Cube, "PillLabel", new Vector3(0f, 0.045f, 0f), new Vector3(0.10f, 0.006f, 0.055f), Color.white);
                    AddVisual(root, PrimitiveType.Cylinder, "Cap", new Vector3(0f, 0.052f, 0f), new Vector3(0.045f, 0.012f, 0.045f), new Color(0.95f, 0.95f, 0.90f));
                    break;
                case "Medkit":
                    AddVisual(root, PrimitiveType.Cube, "Case", Vector3.zero, item.Size, item.Color);
                    AddVisual(root, PrimitiveType.Cube, "CrossVertical", new Vector3(0f, 0.09f, 0.02f), new Vector3(0.035f, 0.02f, 0.12f), Color.white);
                    AddVisual(root, PrimitiveType.Cube, "CrossHorizontal", new Vector3(0f, 0.092f, 0.02f), new Vector3(0.11f, 0.022f, 0.035f), Color.white);
                    break;
                case "Splint":
                    AddVisual(root, PrimitiveType.Cube, "LeftBoard", new Vector3(-0.035f, 0f, 0f), new Vector3(0.028f, 0.035f, 0.42f), item.Color);
                    AddVisual(root, PrimitiveType.Cube, "RightBoard", new Vector3(0.035f, 0f, 0f), new Vector3(0.028f, 0.035f, 0.42f), item.Color);
                    AddVisual(root, PrimitiveType.Cube, "WrapA", new Vector3(0f, 0.03f, -0.115f), new Vector3(0.11f, 0.012f, 0.035f), new Color(0.88f, 0.86f, 0.75f));
                    AddVisual(root, PrimitiveType.Cube, "WrapB", new Vector3(0f, 0.03f, 0.115f), new Vector3(0.11f, 0.012f, 0.035f), new Color(0.88f, 0.86f, 0.75f));
                    break;
                case "BloodPack":
                    AddVisual(root, PrimitiveType.Cube, "BloodBag", Vector3.zero, item.Size, item.Color);
                    AddVisual(root, PrimitiveType.Cube, "BagLabel", new Vector3(0f, 0.034f, 0.03f), new Vector3(0.10f, 0.006f, 0.055f), Color.white);
                    AddVisual(root, PrimitiveType.Cylinder, "Tube", new Vector3(0f, 0.035f, -0.14f), new Vector3(0.006f, 0.10f, 0.006f), new Color(0.80f, 0.08f, 0.08f), Quaternion.Euler(90f, 0f, 0f));
                    break;
            }
        }

        private static void AddSyringeVisual(Transform root, ItemDefinition item)
        {
            AddVisual(root, PrimitiveType.Cylinder, "SyringeBarrel", Vector3.zero, new Vector3(0.026f, 0.12f, 0.026f), new Color(0.88f, 0.94f, 0.97f, 0.85f), Quaternion.Euler(90f, 0f, 0f));
            AddVisual(root, PrimitiveType.Cylinder, "Fluid", new Vector3(0f, 0f, -0.015f), new Vector3(0.020f, 0.075f, 0.020f), item.Color, Quaternion.Euler(90f, 0f, 0f));
            AddVisual(root, PrimitiveType.Cube, "Plunger", new Vector3(0f, 0f, -0.145f), new Vector3(0.08f, 0.012f, 0.012f), Color.white);
            AddVisual(root, PrimitiveType.Cylinder, "Needle", new Vector3(0f, 0f, 0.145f), new Vector3(0.004f, 0.075f, 0.004f), new Color(0.72f, 0.74f, 0.76f), Quaternion.Euler(90f, 0f, 0f));
        }

        private static GameObject AddVisual(Transform root, PrimitiveType primitive, string name, Vector3 localPosition, Vector3 localScale, Color color)
        {
            return AddVisual(root, primitive, name, localPosition, localScale, color, Quaternion.identity);
        }

        private static GameObject AddVisual(Transform root, PrimitiveType primitive, string name, Vector3 localPosition, Vector3 localScale, Color color, Quaternion localRotation)
        {
            GameObject visual = GameObject.CreatePrimitive(primitive);
            visual.name = "AHS_Model_" + name;
            visual.transform.SetParent(root, false);
            visual.transform.localPosition = localPosition;
            visual.transform.localRotation = localRotation;
            visual.transform.localScale = localScale;
            Collider collider = visual.GetComponent<Collider>();
            if (collider != null)
                UnityEngine.Object.DestroyImmediate(collider);
            Renderer renderer = visual.GetComponent<Renderer>();
            if (renderer != null)
            {
                Material material = new Material(Shader.Find("Standard"));
                material.color = color;
                renderer.sharedMaterial = material;
            }
            return visual;
        }

        private static GameObject CreateChild(Transform parent, string name, Vector3 localPosition, Quaternion localRotation)
        {
            GameObject child = new GameObject(name);
            child.transform.SetParent(parent, false);
            child.transform.localPosition = localPosition;
            child.transform.localRotation = localRotation;
            child.transform.localScale = Vector3.one;
            return child;
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
            Type arrayType = typeof(Collider[]);
            FieldInfo field = type.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null && field.FieldType.IsAssignableFrom(arrayType))
            {
                field.SetValue(target, new[] { collider });
                return;
            }
            PropertyInfo property = type.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null && property.CanWrite && property.PropertyType.IsAssignableFrom(arrayType))
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

        private enum PrimitiveShape
        {
            Box,
            CapsuleZ
        }

        private readonly struct ItemDefinition
        {
            public readonly string Name;
            public readonly PrimitiveShape Shape;
            public readonly Vector3 Size;
            public readonly float Mass;
            public readonly Color Color;

            public ItemDefinition(string name, PrimitiveShape shape, Vector3 size, float mass, Color color)
            {
                Name = name;
                Shape = shape;
                Size = size;
                Mass = mass;
                Color = color;
            }
        }
    }
}
#endif
