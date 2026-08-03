using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using MoonSharp.Interpreter;
using UnityEngine;

namespace LuaMod.LuaAPI;

public class API_Physics
{
	public static readonly API_Physics Instance = new API_Physics();

	public static DynValue BL_SphereCast(Vector3 origin, Vector3 direction, float radius, float maxdistance)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		RaycastHit val = default(RaycastHit);
		return LuaSafeCall.Run(() => Physics.SphereCast(origin, radius, direction, ref val, maxdistance) ? UserData.Create((object)val) : DynValue.Nil, $"BL_SphereCast(origin: {origin}, direction: {direction}, radius: {radius}, maxdistance: {maxdistance})");
	}

	public static DynValue BL_SphereCast(Vector3 start_pos, Vector3 end_pos, float radius)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		return LuaSafeCall.Run(delegate
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			//IL_000d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0012: Unknown result type (might be due to invalid IL or missing references)
			//IL_0015: Unknown result type (might be due to invalid IL or missing references)
			//IL_001a: Unknown result type (might be due to invalid IL or missing references)
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0022: Unknown result type (might be due to invalid IL or missing references)
			//IL_0027: Unknown result type (might be due to invalid IL or missing references)
			//IL_002c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0036: Unknown result type (might be due to invalid IL or missing references)
			//IL_0041: Unknown result type (might be due to invalid IL or missing references)
			//IL_0051: Unknown result type (might be due to invalid IL or missing references)
			Vector3 val = end_pos - start_pos;
			Vector3 normalized = ((Vector3)(ref val)).normalized;
			val = end_pos - start_pos;
			float magnitude = ((Vector3)(ref val)).magnitude;
			RaycastHit val2 = default(RaycastHit);
			return Physics.SphereCast(start_pos, radius, normalized, ref val2, magnitude) ? UserData.Create((object)val2) : DynValue.Nil;
		}, $"BL_SphereCast(start: {start_pos}, end: {end_pos}, radius: {radius})");
	}

	public static DynValue BL_SphereCastAll(Vector3 origin, float radius, Vector3 direction, float maxDistance, int layerMask = -5)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		//IL_006c: Unknown result type (might be due to invalid IL or missing references)
		return LuaSafeCall.Run((Func<DynValue>)delegate
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			//IL_000e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0054: Unknown result type (might be due to invalid IL or missing references)
			//IL_0059: Unknown result type (might be due to invalid IL or missing references)
			//IL_005c: Unknown result type (might be due to invalid IL or missing references)
			RaycastHit[] array = Il2CppArrayBase<RaycastHit>.op_Implicit((Il2CppArrayBase<RaycastHit>)(object)Physics.SphereCastAll(origin, radius, direction, maxDistance, layerMask));
			if (array == null || array.Length == 0)
			{
				return DynValue.Nil;
			}
			List<DynValue> list = new List<DynValue>();
			RaycastHit[] array2 = array;
			foreach (RaycastHit val in array2)
			{
				list.Add(UserData.Create((object)val));
			}
			return UserData.Create((object)list);
		}, $"BL_SphereCastAll(origin: {origin}, dir: {direction}, radius: {radius}, dist: {maxDistance}, mask: {layerMask})");
	}

	public static DynValue BL_SphereCastAll(Vector3 start_pos, Vector3 end_pos, float radius, int layerMask = -5)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		return LuaSafeCall.Run((Func<DynValue>)delegate
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			//IL_000d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0012: Unknown result type (might be due to invalid IL or missing references)
			//IL_0016: Unknown result type (might be due to invalid IL or missing references)
			//IL_001b: Unknown result type (might be due to invalid IL or missing references)
			//IL_001d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0023: Unknown result type (might be due to invalid IL or missing references)
			//IL_0028: Unknown result type (might be due to invalid IL or missing references)
			//IL_002d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0038: Unknown result type (might be due to invalid IL or missing references)
			//IL_0043: Unknown result type (might be due to invalid IL or missing references)
			//IL_0083: Unknown result type (might be due to invalid IL or missing references)
			//IL_0088: Unknown result type (might be due to invalid IL or missing references)
			//IL_008b: Unknown result type (might be due to invalid IL or missing references)
			Vector3 val = end_pos - start_pos;
			Vector3 normalized = ((Vector3)(ref val)).normalized;
			val = end_pos - start_pos;
			float magnitude = ((Vector3)(ref val)).magnitude;
			RaycastHit[] array = Il2CppArrayBase<RaycastHit>.op_Implicit((Il2CppArrayBase<RaycastHit>)(object)Physics.SphereCastAll(start_pos, radius, normalized, magnitude, layerMask));
			if (array == null || array.Length == 0)
			{
				return DynValue.Nil;
			}
			List<DynValue> list = new List<DynValue>();
			RaycastHit[] array2 = array;
			foreach (RaycastHit val2 in array2)
			{
				list.Add(UserData.Create((object)val2));
			}
			return UserData.Create((object)list);
		}, $"BL_SphereCastAll(start: {start_pos}, end: {end_pos}, radius: {radius}, mask: {layerMask})");
	}

	public static DynValue BL_RayCast(Vector3 start_pos, Vector3 end_pos)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		return LuaSafeCall.Run(delegate
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			//IL_000d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0012: Unknown result type (might be due to invalid IL or missing references)
			//IL_0015: Unknown result type (might be due to invalid IL or missing references)
			//IL_001a: Unknown result type (might be due to invalid IL or missing references)
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0022: Unknown result type (might be due to invalid IL or missing references)
			//IL_0027: Unknown result type (might be due to invalid IL or missing references)
			//IL_002c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0036: Unknown result type (might be due to invalid IL or missing references)
			//IL_003b: Unknown result type (might be due to invalid IL or missing references)
			//IL_004b: Unknown result type (might be due to invalid IL or missing references)
			Vector3 val = end_pos - start_pos;
			Vector3 normalized = ((Vector3)(ref val)).normalized;
			val = end_pos - start_pos;
			float magnitude = ((Vector3)(ref val)).magnitude;
			RaycastHit val2 = default(RaycastHit);
			return Physics.Raycast(start_pos, normalized, ref val2, magnitude) ? UserData.Create((object)val2) : DynValue.Nil;
		}, $"BL_RayCast(start: {start_pos}, end: {end_pos})");
	}

	public static DynValue BL_RayCast(Vector3 origin, Vector3 direction, float maxdistance = float.PositiveInfinity)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		RaycastHit val = default(RaycastHit);
		return LuaSafeCall.Run(() => Physics.Raycast(origin, direction, ref val, maxdistance) ? UserData.Create((object)val) : DynValue.Nil, $"BL_RayCast(origin: {origin}, direction: {direction}, maxdistance: {maxdistance})");
	}

	public static DynValue BL_BoxCast(Vector3 center, Vector3 halfExtents, Vector3 direction, Quaternion orientation, float maxDistance, int layerMask = -5)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0059: Unknown result type (might be due to invalid IL or missing references)
		//IL_0074: Unknown result type (might be due to invalid IL or missing references)
		//IL_008f: Unknown result type (might be due to invalid IL or missing references)
		RaycastHit val = default(RaycastHit);
		return LuaSafeCall.Run(() => Physics.BoxCast(center, halfExtents, direction, ref val, orientation, maxDistance, layerMask) ? UserData.Create((object)val) : DynValue.Nil, $"BL_BoxCast(center: {center}, halfExtents: {halfExtents}, dir: {direction}, dist: {maxDistance}, mask: {layerMask})");
	}

	public static DynValue BL_BoxCastAll(Vector3 center, Vector3 halfExtents, Vector3 direction, Quaternion orientation, float maxDistance, int layerMask = -5)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0059: Unknown result type (might be due to invalid IL or missing references)
		//IL_0074: Unknown result type (might be due to invalid IL or missing references)
		//IL_008f: Unknown result type (might be due to invalid IL or missing references)
		return LuaSafeCall.Run((Func<DynValue>)delegate
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			//IL_000e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0014: Unknown result type (might be due to invalid IL or missing references)
			//IL_005a: Unknown result type (might be due to invalid IL or missing references)
			//IL_005f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0062: Unknown result type (might be due to invalid IL or missing references)
			RaycastHit[] array = Il2CppArrayBase<RaycastHit>.op_Implicit((Il2CppArrayBase<RaycastHit>)(object)Physics.BoxCastAll(center, halfExtents, direction, orientation, maxDistance, layerMask));
			if (array == null || array.Length == 0)
			{
				return DynValue.Nil;
			}
			List<DynValue> list = new List<DynValue>();
			RaycastHit[] array2 = array;
			foreach (RaycastHit val in array2)
			{
				list.Add(UserData.Create((object)val));
			}
			return UserData.Create((object)list);
		}, $"BL_BoxCastAll(center: {center}, halfExtents: {halfExtents}, dir: {direction}, dist: {maxDistance}, mask: {layerMask})");
	}

	public static DynValue BL_CapsuleCast(Vector3 point1, Vector3 point2, float radius, Vector3 direction, float maxDistance, int layerMask = -5)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0059: Unknown result type (might be due to invalid IL or missing references)
		//IL_0074: Unknown result type (might be due to invalid IL or missing references)
		//IL_00aa: Unknown result type (might be due to invalid IL or missing references)
		RaycastHit val = default(RaycastHit);
		return LuaSafeCall.Run(() => Physics.CapsuleCast(point1, point2, radius, direction, ref val, maxDistance, layerMask) ? UserData.Create((object)val) : DynValue.Nil, $"BL_CapsuleCast(p1: {point1}, p2: {point2}, radius: {radius}, dir: {direction}, dist: {maxDistance}, mask: {layerMask})");
	}

	public static DynValue BL_CapsuleCastAll(Vector3 point1, Vector3 point2, float radius, Vector3 direction, float maxDistance, int layerMask = -5)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0059: Unknown result type (might be due to invalid IL or missing references)
		//IL_0074: Unknown result type (might be due to invalid IL or missing references)
		//IL_00aa: Unknown result type (might be due to invalid IL or missing references)
		return LuaSafeCall.Run((Func<DynValue>)delegate
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			//IL_0014: Unknown result type (might be due to invalid IL or missing references)
			//IL_005a: Unknown result type (might be due to invalid IL or missing references)
			//IL_005f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0062: Unknown result type (might be due to invalid IL or missing references)
			RaycastHit[] array = Il2CppArrayBase<RaycastHit>.op_Implicit((Il2CppArrayBase<RaycastHit>)(object)Physics.CapsuleCastAll(point1, point2, radius, direction, maxDistance, layerMask));
			if (array == null || array.Length == 0)
			{
				return DynValue.Nil;
			}
			List<DynValue> list = new List<DynValue>();
			RaycastHit[] array2 = array;
			foreach (RaycastHit val in array2)
			{
				list.Add(UserData.Create((object)val));
			}
			return UserData.Create((object)list);
		}, $"BL_CapsuleCastAll(p1: {point1}, p2: {point2}, radius: {radius}, dir: {direction}, dist: {maxDistance}, mask: {layerMask})");
	}

	public static DynValue BL_CheckSphere(Vector3 position, float radius, int layerMask = -5)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		return LuaSafeCall.Run(delegate
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			bool flag = Physics.CheckSphere(position, radius, layerMask);
			return DynValue.NewBoolean(flag);
		}, $"BL_CheckSphere(pos: {position}, radius: {radius}, mask: {layerMask})");
	}

	public static DynValue BL_CheckBox(Vector3 center, Vector3 halfExtents, Quaternion orientation, int layerMask = -5)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		//IL_007f: Unknown result type (might be due to invalid IL or missing references)
		return LuaSafeCall.Run(delegate
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			//IL_000e: Unknown result type (might be due to invalid IL or missing references)
			bool flag = Physics.CheckBox(center, halfExtents, orientation, layerMask);
			return DynValue.NewBoolean(flag);
		}, $"BL_CheckBox(center: {center}, halfExtents: {halfExtents}, orientation: {orientation}, mask: {layerMask})");
	}

	public static DynValue BL_CheckCapsule(Vector3 start, Vector3 end, float radius, int layerMask = -5)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		return LuaSafeCall.Run(delegate
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			bool flag = Physics.CheckCapsule(start, end, radius, layerMask);
			return DynValue.NewBoolean(flag);
		}, $"BL_CheckCapsule(start: {start}, end: {end}, radius: {radius}, mask: {layerMask})");
	}

	public static DynValue BL_OverlapSphere(Vector3 position, float radius, int layerMask = -5)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		return LuaSafeCall.Run((Func<DynValue>)delegate
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			Collider[] array = Il2CppArrayBase<Collider>.op_Implicit((Il2CppArrayBase<Collider>)(object)Physics.OverlapSphere(position, radius, layerMask));
			if (array == null || array.Length == 0)
			{
				return DynValue.Nil;
			}
			List<DynValue> list = new List<DynValue>();
			Collider[] array2 = array;
			foreach (Collider val in array2)
			{
				list.Add(UserData.Create((object)val));
			}
			return UserData.Create((object)list);
		}, $"BL_OverlapSphere(pos: {position}, radius: {radius}, mask: {layerMask})");
	}

	public static DynValue BL_OverlapBox(Vector3 center, Vector3 halfExtents, Quaternion orientation, int layerMask = -5)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		//IL_007f: Unknown result type (might be due to invalid IL or missing references)
		return LuaSafeCall.Run((Func<DynValue>)delegate
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			//IL_000e: Unknown result type (might be due to invalid IL or missing references)
			Collider[] array = Il2CppArrayBase<Collider>.op_Implicit((Il2CppArrayBase<Collider>)(object)Physics.OverlapBox(center, halfExtents, orientation, layerMask));
			if (array == null || array.Length == 0)
			{
				return DynValue.Nil;
			}
			List<DynValue> list = new List<DynValue>();
			Collider[] array2 = array;
			foreach (Collider val in array2)
			{
				list.Add(UserData.Create((object)val));
			}
			return UserData.Create((object)list);
		}, $"BL_OverlapBox(center: {center}, halfExtents: {halfExtents}, orientation: {orientation}, mask: {layerMask})");
	}

	public static DynValue BL_OverlapCapsule(Vector3 point0, Vector3 point1, float radius, int layerMask = -5)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		return LuaSafeCall.Run((Func<DynValue>)delegate
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			Collider[] array = Il2CppArrayBase<Collider>.op_Implicit((Il2CppArrayBase<Collider>)(object)Physics.OverlapCapsule(point0, point1, radius, layerMask));
			if (array == null || array.Length == 0)
			{
				return DynValue.Nil;
			}
			List<DynValue> list = new List<DynValue>();
			Collider[] array2 = array;
			foreach (Collider val in array2)
			{
				list.Add(UserData.Create((object)val));
			}
			return UserData.Create((object)list);
		}, $"BL_OverlapCapsule(p0: {point0}, p1: {point1}, radius: {radius}, mask: {layerMask})");
	}
}
