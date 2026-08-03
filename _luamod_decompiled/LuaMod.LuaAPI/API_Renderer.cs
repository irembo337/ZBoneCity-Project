using System;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using MoonSharp.Interpreter;
using UnityEngine;

namespace LuaMod.LuaAPI;

[MoonSharpUserData]
public class API_Renderer
{
	public static readonly API_Renderer Instance = new API_Renderer();

	public static int BL_GetMaterialCount(MeshRenderer renderer)
	{
		return LuaSafeCall.Run(() => ((Object)(object)renderer != (Object)null && ((Renderer)renderer).materials != null) ? ((Il2CppArrayBase<Material>)(object)((Renderer)renderer).materials).Length : 0, "BL_GetMaterialCount()");
	}

	public static DynValue BL_GetMaterialAt(MeshRenderer renderer, int index)
	{
		return LuaSafeCall.Run(delegate
		{
			//IL_0016: Unknown result type (might be due to invalid IL or missing references)
			if ((Object)(object)renderer == (Object)null)
			{
				throw new ScriptRuntimeException("MeshRenderer is null");
			}
			Il2CppReferenceArray<Material> materials = ((Renderer)renderer).materials;
			return (index >= 0 && index < ((Il2CppArrayBase<Material>)(object)materials).Length) ? UserData.Create((object)((Il2CppArrayBase<Material>)(object)materials)[index]) : DynValue.Nil;
		}, $"BL_GetMaterialAt(index: {index})");
	}

	public static void BL_SetMaterialAt(MeshRenderer renderer, int index, Material mat)
	{
		LuaSafeCall.Run(delegate
		{
			//IL_0016: Unknown result type (might be due to invalid IL or missing references)
			//IL_0031: Unknown result type (might be due to invalid IL or missing references)
			//IL_008b: Unknown result type (might be due to invalid IL or missing references)
			if ((Object)(object)renderer == (Object)null)
			{
				throw new ScriptRuntimeException("MeshRenderer is null");
			}
			if ((Object)(object)mat == (Object)null)
			{
				throw new ScriptRuntimeException("Material is null");
			}
			Il2CppReferenceArray<Material> materials = ((Renderer)renderer).materials;
			if (index >= 0 && index < ((Il2CppArrayBase<Material>)(object)materials).Length)
			{
				((Il2CppArrayBase<Material>)(object)materials)[index] = mat;
				((Renderer)renderer).materials = materials;
				return;
			}
			throw new ScriptRuntimeException("Invalid index {index}");
		}, $"BL_SetMaterialAt(index: {index})");
	}

	public static Table BL_GetAllMaterials(Script script, MeshRenderer renderer)
	{
		return LuaSafeCall.Run((Func<Table>)delegate
		{
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			//IL_000d: Expected O, but got Unknown
			Table val = new Table(script);
			if ((Object)(object)renderer == (Object)null)
			{
				return val;
			}
			Il2CppReferenceArray<Material> materials = ((Renderer)renderer).materials;
			for (int i = 0; i < ((Il2CppArrayBase<Material>)(object)materials).Length; i++)
			{
				val[(object)(i + 1)] = UserData.Create((object)((Il2CppArrayBase<Material>)(object)materials)[i]);
			}
			return val;
		}, "BL_GetAllMaterials()");
	}

	public static void BL_SetAllMaterials(MeshRenderer renderer, Table matTable)
	{
		LuaSafeCall.Run(delegate
		{
			//IL_0016: Unknown result type (might be due to invalid IL or missing references)
			//IL_002e: Unknown result type (might be due to invalid IL or missing references)
			//IL_005e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0064: Invalid comparison between Unknown and I4
			//IL_00b2: Unknown result type (might be due to invalid IL or missing references)
			if ((Object)(object)renderer == (Object)null)
			{
				throw new ScriptRuntimeException("MeshRenderer is null");
			}
			if (matTable == null)
			{
				throw new ScriptRuntimeException("Material table is null");
			}
			int length = matTable.Length;
			Material[] array = (Material[])(object)new Material[length];
			int num = 1;
			while (num <= length)
			{
				DynValue val = matTable.Get(num);
				if ((int)val.Type == 8)
				{
					object @object = val.UserData.Object;
					Material val2 = (Material)((@object is Material) ? @object : null);
					if (val2 != null)
					{
						array[num - 1] = val2;
						num++;
						continue;
					}
				}
				throw new ScriptRuntimeException($"Invalid material at index {num}");
			}
			((Renderer)renderer).materials = Il2CppReferenceArray<Material>.op_Implicit(array);
		}, "BL_SetAllMaterials()");
	}
}
