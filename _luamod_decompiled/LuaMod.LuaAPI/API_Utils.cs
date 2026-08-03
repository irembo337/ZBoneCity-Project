using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppSLZ.Marrow.Pool;
using Il2CppSLZ.Marrow.Warehouse;
using MelonLoader;
using MoonSharp.Interpreter;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LuaMod.LuaAPI;

public class API_Utils
{
	public static readonly API_Utils Instance = new API_Utils();

	public static int BL_CollectionLength(ICollection collection)
	{
		return collection?.Count ?? 0;
	}

	public static string BL_GetSceneName()
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		Scene val = ((Il2CppArrayBase<Scene>)(object)SceneManager.GetAllScenes())[0];
		return ((Scene)(ref val)).name;
	}

	public static string BL_GetBarcode(GameObject gameObject)
	{
		return LuaSafeCall.Run(delegate
		{
			//IL_0017: Unknown result type (might be due to invalid IL or missing references)
			if ((Object)(object)gameObject == (Object)null)
			{
				throw new ScriptRuntimeException("GameObject is null");
			}
			Poolee component = gameObject.GetComponent<Poolee>();
			return ((Object)(object)component != (Object)null) ? ((Scannable)component.SpawnableCrate).Barcode.ID : null;
		}, $"BL_GetBarcode(index: {gameObject})");
	}

	public static DynValue BL_ConvertObjectToType(Object obj, string CompType)
	{
		return LuaSafeCall.Run((Func<DynValue>)delegate
		{
			//IL_0019: Unknown result type (might be due to invalid IL or missing references)
			//IL_006e: Unknown result type (might be due to invalid IL or missing references)
			if (obj == (Object)null)
			{
				throw new ScriptRuntimeException("Object for conversion is null");
			}
			Type type = LuaMod.LoadedTypes.Find((Type t) => t.Name == CompType);
			if (type == null)
			{
				throw new ScriptRuntimeException("Component type '" + CompType + "' not found");
			}
			MethodInfo methodInfo = typeof(Resources).GetMethods(BindingFlags.Static | BindingFlags.Public).First((MethodInfo m) => m.Name == "ConvertObjects" && m.IsGenericMethod && m.GetParameters().Length == 1);
			MethodInfo methodInfo2 = methodInfo.MakeGenericMethod(type);
			Type type2 = typeof(Il2CppReferenceArray<>).MakeGenericType(typeof(Object));
			dynamic val = Activator.CreateInstance(type2, 1);
			val[0] = obj;
			dynamic val2 = methodInfo2.Invoke(null, new object[1] { val });
			if (val2 == null || val2.Length == 0 || val2[0] == null)
			{
				MelonLogger.Warning($"Failed to cast object '{obj.name}' to '{CompType}'");
				return (DynValue)null;
			}
			object obj2 = val2[0];
			return UserData.Create(obj2);
		}, $"BL_ConvertObjectToType('{obj.name}', '{CompType}')");
	}

	public static string RemoveDoubleSlashes(string input)
	{
		while (input.Contains("//"))
		{
			input = input.Replace("//", "/");
		}
		return input;
	}

	public static DynValue BL_GetArrayElement(object target, string fieldName, int index)
	{
		return LuaSafeCall.Run((Func<DynValue>)delegate
		{
			//IL_0016: Unknown result type (might be due to invalid IL or missing references)
			//IL_0055: Unknown result type (might be due to invalid IL or missing references)
			//IL_0108: Unknown result type (might be due to invalid IL or missing references)
			//IL_02cc: Unknown result type (might be due to invalid IL or missing references)
			//IL_02b5: Unknown result type (might be due to invalid IL or missing references)
			if (target == null)
			{
				throw new ScriptRuntimeException("collection object is null");
			}
			if (!LuaMod.RegisteredTypes.Contains(target.GetType()))
			{
				throw new ScriptRuntimeException("Attempt to manipulate array with unregistered type " + target.GetType().FullName);
			}
			object obj = target;
			UserData val = (UserData)((obj is UserData) ? obj : null);
			if (val != null)
			{
				target = val.Object;
			}
			Type type = target?.GetType();
			FieldInfo fieldInfo = type?.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public);
			PropertyInfo propertyInfo = type?.GetProperty(fieldName, BindingFlags.Instance | BindingFlags.Public);
			object obj2 = ((fieldInfo != null) ? fieldInfo.GetValue(target) : propertyInfo?.GetValue(target));
			if (obj2 == null)
			{
				throw new ScriptRuntimeException("Field or property '" + fieldName + "' not found or is null");
			}
			Type type2 = obj2.GetType();
			string fullName = type2.FullName;
			bool isGenericType = type2.IsGenericType;
			if (obj2 is Array array)
			{
				if (index >= 0 && index < array.Length)
				{
					return UserData.Create(array.GetValue(index));
				}
				return DynValue.Nil;
			}
			if (obj2 is IList list)
			{
				if (index >= 0 && index < list.Count)
				{
					return UserData.Create(list[index]);
				}
				return DynValue.Nil;
			}
			if (!isGenericType || (!fullName.StartsWith("Il2CppReferenceArray") && !fullName.StartsWith("Il2CppSystem.Collections.Generic.List")))
			{
				throw new ScriptRuntimeException("Unsupported collection type: " + type2.Name);
			}
			MethodInfo method = type2.GetMethod("get_Item", BindingFlags.Instance | BindingFlags.Public);
			PropertyInfo property = type2.GetProperty("Count", BindingFlags.Instance | BindingFlags.Public);
			if (!(method != null) || !(property != null))
			{
				throw new ScriptRuntimeException("Failed to access list structure on type " + fullName);
			}
			int num = (int)property.GetValue(obj2);
			if (index >= 0 && index < num)
			{
				object obj3 = method.Invoke(obj2, new object[1] { index });
				return UserData.Create(obj3);
			}
			return DynValue.Nil;
		}, $"BL_GetArrayElement({fieldName}[{index}])");
	}

	public static void BL_AppendToArray(object target, string fieldName, object value)
	{
		LuaSafeCall.Run(delegate
		{
			//IL_0016: Unknown result type (might be due to invalid IL or missing references)
			//IL_0060: Unknown result type (might be due to invalid IL or missing references)
			//IL_009f: Unknown result type (might be due to invalid IL or missing references)
			//IL_014f: Unknown result type (might be due to invalid IL or missing references)
			//IL_043b: Unknown result type (might be due to invalid IL or missing references)
			//IL_03c9: Unknown result type (might be due to invalid IL or missing references)
			if (target == null)
			{
				throw new ScriptRuntimeException("Collection object is null");
			}
			if (value != null && !LuaMod.RegisteredTypes.Contains(value.GetType()))
			{
				throw new ScriptRuntimeException("Attempt to manipulate array with unregistered type " + value.GetType().FullName);
			}
			if (!LuaMod.RegisteredTypes.Contains(target.GetType()))
			{
				throw new ScriptRuntimeException("Attempt to manipulate array with unregistered type " + target.GetType().FullName);
			}
			object obj = target;
			UserData val = (UserData)((obj is UserData) ? obj : null);
			if (val != null)
			{
				target = val.Object;
			}
			Type type = target?.GetType();
			FieldInfo fieldInfo = type?.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public);
			PropertyInfo propertyInfo = type?.GetProperty(fieldName, BindingFlags.Instance | BindingFlags.Public);
			object obj2 = ((fieldInfo != null) ? fieldInfo.GetValue(target) : propertyInfo?.GetValue(target));
			if (obj2 == null)
			{
				throw new ScriptRuntimeException("Field '" + fieldName + "' not found or is null");
			}
			Array array = obj2 as Array;
			if (array != null)
			{
				Type elementType = array.GetType().GetElementType();
				int num = Array.FindIndex(array.Cast<object>().ToArray(), (object item) => item == null);
				if (num == -1)
				{
					Array array2 = Array.CreateInstance(elementType, array.Length + 1);
					Array.Copy(array, array2, array.Length);
					num = array.Length;
					array2.SetValue(value, num);
					array = array2;
				}
				else
				{
					array.SetValue(value, num);
				}
				if (fieldInfo != null)
				{
					fieldInfo.SetValue(target, array);
				}
				else if ((object)propertyInfo != null && propertyInfo.CanWrite)
				{
					propertyInfo.SetValue(target, array);
				}
			}
			else if (obj2 is IList list)
			{
				int num2 = list.IndexOf(null);
				if (num2 == -1)
				{
					list.Add(value);
				}
				else
				{
					list[num2] = value;
				}
			}
			else
			{
				if (!(obj2 is IEnumerable source) || !obj2.GetType().IsGenericType || !obj2.GetType().GetGenericTypeDefinition().Name.StartsWith("Il2CppReferenceArray"))
				{
					throw new ScriptRuntimeException("Unsupported collection type: " + obj2.GetType().Name);
				}
				Type elementType2 = obj2.GetType().GetGenericArguments()[0];
				List<object> list2 = source.Cast<object>().ToList();
				int num3 = list2.FindIndex((object item) => item == null);
				if (num3 == -1)
				{
					num3 = list2.Count;
					list2.Add(value);
				}
				else
				{
					list2[num3] = value;
				}
				Array array3 = Array.CreateInstance(elementType2, list2.Count);
				for (int i = 0; i < list2.Count; i++)
				{
					array3.SetValue(list2[i], i);
				}
				MethodInfo method = obj2.GetType().GetMethod("op_Implicit", BindingFlags.Static | BindingFlags.Public);
				if (method == null)
				{
					throw new ScriptRuntimeException("Cannot convert array to Il2CppReferenceArray — missing op_Implicit");
				}
				object value2 = method.Invoke(null, new object[1] { array3 });
				if (fieldInfo != null)
				{
					fieldInfo.SetValue(target, value2);
				}
				else if ((object)propertyInfo != null && propertyInfo.CanWrite)
				{
					propertyInfo.SetValue(target, value2);
				}
			}
		}, "BL_AppendToArray(" + fieldName + ")");
	}

	public static void BL_SetArrayElement(object target, string fieldName, int index, object value)
	{
		LuaSafeCall.Run(delegate
		{
			//IL_0016: Unknown result type (might be due to invalid IL or missing references)
			//IL_0060: Unknown result type (might be due to invalid IL or missing references)
			//IL_009f: Unknown result type (might be due to invalid IL or missing references)
			//IL_00dd: Unknown result type (might be due to invalid IL or missing references)
			//IL_015e: Unknown result type (might be due to invalid IL or missing references)
			//IL_01d1: Unknown result type (might be due to invalid IL or missing references)
			//IL_04e2: Unknown result type (might be due to invalid IL or missing references)
			//IL_02f7: Unknown result type (might be due to invalid IL or missing references)
			//IL_042c: Unknown result type (might be due to invalid IL or missing references)
			if (target == null)
			{
				throw new ScriptRuntimeException("Collection object is null");
			}
			if (value != null && !LuaMod.RegisteredTypes.Contains(value.GetType()))
			{
				throw new ScriptRuntimeException("Attempt to manipulate array with unregistered type " + value.GetType().FullName);
			}
			if (!LuaMod.RegisteredTypes.Contains(target.GetType()))
			{
				throw new ScriptRuntimeException("Attempt to manipulate array with unregistered type " + target.GetType().FullName);
			}
			object obj = target;
			UserData val = (UserData)((obj is UserData) ? obj : null);
			if (val != null)
			{
				target = val.Object;
			}
			if (string.IsNullOrEmpty(fieldName))
			{
				throw new ScriptRuntimeException("Field name cannot be null or empty");
			}
			Type type = target.GetType();
			FieldInfo field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public);
			PropertyInfo property = type.GetProperty(fieldName, BindingFlags.Instance | BindingFlags.Public);
			object obj2 = ((field != null) ? field.GetValue(target) : property?.GetValue(target));
			if (obj2 == null)
			{
				throw new ScriptRuntimeException("Field or property '" + fieldName + "' not found or is null on " + type.Name);
			}
			Array array = obj2 as Array;
			if (array != null)
			{
				Type elementType = array.GetType().GetElementType();
				if (value != null && !elementType.IsInstanceOfType(value))
				{
					throw new ScriptRuntimeException("Value of type " + value.GetType().Name + " is not assignable to array element type " + elementType.Name);
				}
				if (index >= array.Length)
				{
					Array array2 = Array.CreateInstance(elementType, index + 1);
					Array.Copy(array, array2, array.Length);
					array = array2;
				}
				array.SetValue(value, index);
				if (field != null)
				{
					field.SetValue(target, array);
				}
				else if ((object)property != null && property.CanWrite)
				{
					property.SetValue(target, array);
				}
			}
			else if (obj2 is IList list)
			{
				Type type2 = obj2.GetType();
				Type type3 = (type2.IsGenericType ? type2.GetGenericArguments()[0] : typeof(object));
				if (value != null && !type3.IsInstanceOfType(value))
				{
					throw new ScriptRuntimeException("Value type " + value.GetType().Name + " is not assignable to list element type " + type3.Name);
				}
				while (list.Count <= index)
				{
					list.Add(null);
				}
				list[index] = value;
			}
			else
			{
				Type type4 = obj2.GetType();
				if (!(obj2 is IEnumerable) || !type4.IsGenericType || !type4.GetGenericTypeDefinition().Name.StartsWith("Il2CppReferenceArray"))
				{
					throw new ScriptRuntimeException($"Field '{fieldName}' is not assignable (type: {obj2.GetType().Name})");
				}
				Type elementType2 = type4.GetGenericArguments()[0];
				List<object> list2 = ((IEnumerable)obj2).Cast<object>().ToList();
				int length = Math.Max(index + 1, list2.Count);
				Array array3 = Array.CreateInstance(elementType2, length);
				for (int i = 0; i < list2.Count; i++)
				{
					array3.SetValue(list2[i], i);
				}
				array3.SetValue(value, index);
				Type type5 = type4;
				MethodInfo method = type5.GetMethod("op_Implicit", BindingFlags.Static | BindingFlags.Public);
				if (method == null)
				{
					throw new ScriptRuntimeException("Could not find implicit op_Implicit method for " + type5.Name);
				}
				object value2 = method.Invoke(null, new object[1] { array3 });
				if (field != null)
				{
					field.SetValue(target, value2);
				}
				else if ((object)property != null && property.CanWrite)
				{
					property.SetValue(target, value2);
				}
			}
		}, $"BL_SetArrayElement({fieldName}[{index}])");
	}
}
