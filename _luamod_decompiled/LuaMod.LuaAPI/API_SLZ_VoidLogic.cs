using Il2CppSLZ.Marrow;
using Il2CppSLZ.Marrow.Warehouse;
using MoonSharp.Interpreter;
using UnityEngine;

namespace LuaMod.LuaAPI;

public class API_SLZ_VoidLogic
{
	public static readonly API_SLZ_VoidLogic Instance = new API_SLZ_VoidLogic();

	public static bool BL_SetMarrowEntityPoseDectoratorPose(MarrowEntityPoseDecorator posedec, string barcode)
	{
		LuaSafeCall.Run(delegate
		{
			//IL_0033: Unknown result type (might be due to invalid IL or missing references)
			if (!((Object)(object)posedec != (Object)null))
			{
				throw new ScriptRuntimeException("MarrowEntityPoseDecorator is null");
			}
			posedec.MarrowEntityPose = new DataCardReference<EntityPose>(barcode);
			return true;
		}, $"SetMarrowEntityPoseDectoratorPose('{posedec}', barcode: '{barcode}')");
		return false;
	}
}
