using UnityEngine;

namespace LuaMod.LuaAPI;

public class API_Vector
{
	public static readonly API_Vector Instance = new API_Vector();

	public static Vector3 BL_Vector3(float x, float y, float z)
	{
		//IL_0004: Unknown result type (might be due to invalid IL or missing references)
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		return new Vector3(x, y, z);
	}

	public static Vector3 BL_Vector2(float x, float y)
	{
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		return Vector2.op_Implicit(new Vector2(x, y));
	}

	public static Vector3 BL_Vector4(float x, float y, float z, float w)
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		return Vector4.op_Implicit(new Vector4(x, y, z, w));
	}
}
