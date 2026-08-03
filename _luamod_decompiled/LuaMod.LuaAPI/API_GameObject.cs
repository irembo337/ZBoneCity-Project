using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BoneLib;
using Il2CppCysharp.Threading.Tasks;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppSLZ.Marrow.Data;
using Il2CppSLZ.Marrow.Pool;
using Il2CppSLZ.Marrow.Warehouse;
using Il2CppSystem;
using MoonSharp.Interpreter;
using UnityEngine;

namespace LuaMod.LuaAPI;

public class API_GameObject
{
	public static readonly API_GameObject Instance = new API_GameObject();

	public GameObject BL_CreateEmptyGameObject()
	{
		return LuaSafeCall.Run((Func<GameObject>)(() => new GameObject()), "BL_CreateEmptyGameObject()");
	}

	public static bool BL_IsValid(Object obj)
	{
		return obj != (Object)null;
	}

	public GameObject BL_InstantiateGameObject(GameObject original)
	{
		return LuaSafeCall.Run(() => Object.Instantiate<GameObject>(original), "BL_InstantiateGameObject()");
	}

	public DynValue BL_FindInChildren(GameObject gameObject, string name)
	{
		return LuaSafeCall.Run((Func<DynValue>)delegate
		{
			Transform[] array = Il2CppArrayBase<Transform>.op_Implicit(gameObject.GetComponentsInChildren<Transform>(true));
			Transform[] array2 = array;
			foreach (Transform val in array2)
			{
				if (((Object)val).name == name)
				{
					return UserData.Create((object)((Component)val).gameObject);
				}
			}
			return DynValue.Nil;
		}, "BL_FindInChildren('" + name + "')");
	}

	public DynValue BL_FindInWorld(string name)
	{
		return LuaSafeCall.Run(delegate
		{
			GameObject val = GameObject.Find(name);
			return ((Object)(object)val != (Object)null) ? UserData.Create((object)val) : DynValue.Nil;
		}, "BL_FindInWorld('" + name + "')");
	}

	public DynValue BL_FindAllInWorld(string name)
	{
		return LuaSafeCall.Run(delegate
		{
			GameObject[] array = Il2CppArrayBase<GameObject>.op_Implicit(Object.FindObjectsOfType<GameObject>(true));
			List<DynValue> list = new List<DynValue>();
			GameObject[] array2 = array;
			foreach (GameObject val in array2)
			{
				if (((Object)val).name == name)
				{
					list.Add(UserData.Create((object)val));
				}
			}
			return (list.Count > 0) ? UserData.Create((object)list) : DynValue.Nil;
		}, "BL_FindAllInWorld('" + name + "')");
	}

	public DynValue BL_FindAllInChildren(GameObject gameObject, string name)
	{
		return LuaSafeCall.Run(delegate
		{
			Transform[] array = Il2CppArrayBase<Transform>.op_Implicit(gameObject.GetComponentsInChildren<Transform>());
			List<DynValue> list = new List<DynValue>();
			Transform[] array2 = array;
			foreach (Transform val in array2)
			{
				if (((Object)val).name == name)
				{
					list.Add(UserData.Create((object)((Component)val).gameObject));
				}
			}
			return (list.Count > 0) ? UserData.Create((object)list) : DynValue.Nil;
		}, "BL_FindAllInChildren('" + name + "')");
	}

	public List<DynValue> BL_GetComponentsInChildren(GameObject obj, string CompType, bool includeInactive = false)
	{
		return LuaSafeCall.Run(delegate
		{
			//IL_0018: Unknown result type (might be due to invalid IL or missing references)
			//IL_006c: Unknown result type (might be due to invalid IL or missing references)
			if ((Object)(object)obj == (Object)null)
			{
				throw new ScriptRuntimeException("GameObject is null");
			}
			Type type = LuaMod.LoadedTypes.Find((Type t) => t.Name == CompType);
			if (type == null)
			{
				throw new ScriptRuntimeException("Component type '" + CompType + "' not found");
			}
			MethodInfo methodInfo = typeof(GameObject).GetMethods(BindingFlags.Instance | BindingFlags.Public).First((MethodInfo m) => m.Name == "GetComponentsInChildren" && m.IsGenericMethod);
			MethodInfo methodInfo2 = methodInfo.MakeGenericMethod(type);
			dynamic val = methodInfo2.Invoke(obj, new object[1] { includeInactive });
			List<DynValue> list = new List<DynValue>();
			foreach (object item in val)
			{
				list.Add(UserData.Create(item));
			}
			return (list.Count > 0) ? list : null;
		}, "BL_GetComponentsInChildren('" + CompType + "')");
	}

	public DynValue BL_FindComponentsInWorld(string compType, bool includeInactive = false)
	{
		return LuaSafeCall.Run(delegate
		{
			//IL_004f: Unknown result type (might be due to invalid IL or missing references)
			Type type = LuaMod.LoadedTypes.Find((Type t) => t.Name == compType);
			if (type == null)
			{
				throw new ScriptRuntimeException("Component type '" + compType + "' not found");
			}
			MethodInfo methodInfo = typeof(Object).GetMethods(BindingFlags.Static | BindingFlags.Public).FirstOrDefault((MethodInfo m) => m.Name == "FindObjectsOfType" && m.IsGenericMethod && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(bool));
			MethodInfo methodInfo2 = methodInfo.MakeGenericMethod(type);
			object obj = methodInfo2.Invoke(null, new object[1] { includeInactive });
			List<DynValue> list = new List<DynValue>();
			foreach (object item in (IEnumerable<object>)obj)
			{
				list.Add(UserData.Create(item));
			}
			return (list.Count > 0) ? UserData.Create((object)list) : DynValue.Nil;
		}, "BL_FindComponentsInWorld('" + compType + "')");
	}

	public DynValue BL_GetComponent(GameObject obj, string CompType)
	{
		return LuaSafeCall.Run(delegate
		{
			//IL_0018: Unknown result type (might be due to invalid IL or missing references)
			//IL_006c: Unknown result type (might be due to invalid IL or missing references)
			if ((Object)(object)obj == (Object)null)
			{
				throw new ScriptRuntimeException("GameObject is null");
			}
			Type type = LuaMod.LoadedTypes.Find((Type t) => t.Name == CompType);
			if (type == null)
			{
				throw new ScriptRuntimeException("Component type '" + CompType + "' not found");
			}
			MethodInfo methodInfo = typeof(GameObject).GetMethods(BindingFlags.Instance | BindingFlags.Public).First((MethodInfo m) => m.Name == "GetComponent" && m.IsGenericMethod);
			MethodInfo methodInfo2 = methodInfo.MakeGenericMethod(type);
			object obj2 = methodInfo2.Invoke(obj, null);
			return (obj2 != null) ? UserData.Create(obj2) : DynValue.Nil;
		}, "BL_GetComponent('" + CompType + "')");
	}

	public List<DynValue> BL_GetComponents(GameObject obj, string CompType)
	{
		return LuaSafeCall.Run(delegate
		{
			//IL_0018: Unknown result type (might be due to invalid IL or missing references)
			//IL_006c: Unknown result type (might be due to invalid IL or missing references)
			if ((Object)(object)obj == (Object)null)
			{
				throw new ScriptRuntimeException("GameObject is null");
			}
			Type type = LuaMod.LoadedTypes.Find((Type t) => t.Name == CompType);
			if (type == null)
			{
				throw new ScriptRuntimeException("Component type '" + CompType + "' not found");
			}
			MethodInfo methodInfo = typeof(GameObject).GetMethods(BindingFlags.Instance | BindingFlags.Public).First((MethodInfo m) => m.Name == "GetComponents" && m.IsGenericMethod);
			MethodInfo methodInfo2 = methodInfo.MakeGenericMethod(type);
			dynamic val = methodInfo2.Invoke(obj, new object[0]);
			List<DynValue> list = new List<DynValue>();
			foreach (object item in val)
			{
				list.Add(UserData.Create(item));
			}
			return (list.Count > 0) ? list : null;
		}, "BL_GetComponents('" + CompType + "')");
	}

	public DynValue BL_AddComponent(GameObject obj, string CompType)
	{
		return LuaSafeCall.Run(delegate
		{
			//IL_0018: Unknown result type (might be due to invalid IL or missing references)
			//IL_006c: Unknown result type (might be due to invalid IL or missing references)
			if ((Object)(object)obj == (Object)null)
			{
				throw new ScriptRuntimeException("GameObject is null");
			}
			Type type = LuaMod.LoadedTypes.Find((Type t) => t.Name == CompType);
			if (type == null)
			{
				throw new ScriptRuntimeException("Component type '" + CompType + "' not found");
			}
			MethodInfo methodInfo = typeof(GameObject).GetMethods(BindingFlags.Instance | BindingFlags.Public).First((MethodInfo m) => m.Name == "AddComponent" && m.IsGenericMethod);
			MethodInfo methodInfo2 = methodInfo.MakeGenericMethod(type);
			object obj2 = methodInfo2.Invoke(obj, null);
			return (obj2 != null) ? UserData.Create(obj2) : DynValue.Nil;
		}, "BL_AddComponent('" + CompType + "')");
	}

	public DynValue BL_GetComponentInChildren(GameObject obj, string CompType)
	{
		return LuaSafeCall.Run(delegate
		{
			//IL_0018: Unknown result type (might be due to invalid IL or missing references)
			//IL_006c: Unknown result type (might be due to invalid IL or missing references)
			if ((Object)(object)obj == (Object)null)
			{
				throw new ScriptRuntimeException("GameObject is null");
			}
			Type type = LuaMod.LoadedTypes.Find((Type t) => t.Name == CompType);
			if (type == null)
			{
				throw new ScriptRuntimeException("Component type '" + CompType + "' not found");
			}
			MethodInfo methodInfo = typeof(GameObject).GetMethods(BindingFlags.Instance | BindingFlags.Public).First((MethodInfo m) => m.Name == "GetComponentInChildren" && m.IsGenericMethod);
			MethodInfo methodInfo2 = methodInfo.MakeGenericMethod(type);
			object obj2 = methodInfo2.Invoke(obj, new object[0]);
			return (obj2 != null) ? UserData.Create(obj2) : DynValue.Nil;
		}, "BL_GetComponentInChildren('" + CompType + "')");
	}

	public static void BL_Destroy(Object obj)
	{
		if (obj != (Object)null)
		{
			Object.Destroy(obj);
		}
	}

	public static void BL_SpawnByBarcode(string SpawnBCode, Vector3 pos, Quaternion rotation)
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		LuaSafeCall.Run(delegate
		{
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			//IL_000e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0013: Unknown result type (might be due to invalid IL or missing references)
			HelperMethods.SpawnCrate(SpawnBCode, pos, rotation, Vector3.one, true, (Action<GameObject>)null, (Action<GameObject>)null);
		}, "BL_SpawnByBarcode('" + SpawnBCode + "')");
	}

	public static void BL_SpawnByBarcode(LuaBehaviour LB, string VariableName, string SpawnBCode, Vector3 pos, Quaternion rotation, GameObject NewParent, bool Active = true)
	{
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		LuaSafeCall.Run(delegate
		{
			//IL_0046: Unknown result type (might be due to invalid IL or missing references)
			//IL_004c: Expected O, but got Unknown
			//IL_004c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0051: Unknown result type (might be due to invalid IL or missing references)
			//IL_005a: Expected O, but got Unknown
			//IL_0063: Unknown result type (might be due to invalid IL or missing references)
			//IL_0069: Unknown result type (might be due to invalid IL or missing references)
			//IL_006e: Unknown result type (might be due to invalid IL or missing references)
			if ((Object)(object)LB == (Object)null)
			{
				throw new Exception("LuaBehaviour is null");
			}
			Action<Poolee> action = delegate(Poolee obj)
			{
				//IL_0022: Unknown result type (might be due to invalid IL or missing references)
				if ((Object)(object)obj == (Object)null || (Object)(object)((Component)obj).gameObject == (Object)null)
				{
					throw new ScriptRuntimeException("Spawned crate is null or missing GameObject");
				}
				LB.SetScriptVariable(VariableName, UserData.Create((object)((Component)obj).gameObject));
			};
			SpawnableCrateReference crateRef = new SpawnableCrateReference(SpawnBCode);
			Spawnable val = new Spawnable
			{
				crateRef = crateRef
			};
			AssetSpawner.Register(val);
			UniTask<Poolee> val2 = AssetSpawner.SpawnAsync(val, pos, rotation, new Nullable<Vector3>(Vector3.one), ((Object)(object)NewParent != (Object)null) ? NewParent.transform : null, Active, new Nullable<int>(), (Action<GameObject>)null, (Action<GameObject>)null, (Action<GameObject>)null);
			UniTaskExtensions.ContinueWith<Poolee>(val2, Action<Poolee>.op_Implicit(action));
		}, $"BL_SpawnByBarcode_LuaVar('{SpawnBCode}', var: '{VariableName}')");
	}

	public DataCardReference<EntityPose> BL_EntityPose(string barcode)
	{
		return new DataCardReference<EntityPose>(barcode);
	}
}
