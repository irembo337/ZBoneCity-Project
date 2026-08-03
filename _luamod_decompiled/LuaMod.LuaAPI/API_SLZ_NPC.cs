using System;
using System.Runtime.CompilerServices;
using Il2CppSLZ.Marrow.AI;
using MoonSharp.Interpreter;
using UnityEngine;
using UnityEngine.AI;

namespace LuaMod.LuaAPI;

public class API_SLZ_NPC
{
	public static readonly API_SLZ_NPC Instance = new API_SLZ_NPC();

	public static Vector3? BL_SamplePosition(Vector3 position, float maxDistance, int areaMask = -1)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		NavMeshHit val = default(NavMeshHit);
		return LuaSafeCall.Run(() => NavMesh.SamplePosition(position, ref val, maxDistance, areaMask) ? new Vector3?(((NavMeshHit)(ref val)).position) : null, $"BL_SamplePosition(pos: {position}, dist: {maxDistance}, mask: {areaMask})");
	}

	public static NavMeshPath BL_CalculatePath(Vector3 start_pos, Vector3 end_pos, int areaMask = -1)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		return LuaSafeCall.Run(delegate
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0007: Expected O, but got Unknown
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			//IL_000e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0024: Unknown result type (might be due to invalid IL or missing references)
			//IL_002a: Invalid comparison between Unknown and I4
			NavMeshPath val = new NavMeshPath();
			return (!NavMesh.CalculatePath(start_pos, end_pos, areaMask, val) || (int)val.status > 0) ? null : val;
		}, $"BL_CalculatePath(start: {start_pos}, end: {end_pos}, areaMask: {areaMask})");
	}

	public bool BL_SetNPCAnger(GameObject NPC, GameObject Target)
	{
		Func<bool> func = delegate
		{
			//IL_0017: Unknown result type (might be due to invalid IL or missing references)
			//IL_0033: Unknown result type (might be due to invalid IL or missing references)
			//IL_006a: Unknown result type (might be due to invalid IL or missing references)
			if ((Object)(object)NPC == (Object)null)
			{
				throw new ScriptRuntimeException("NPC GameObject is null");
			}
			if ((Object)(object)Target == (Object)null)
			{
				throw new ScriptRuntimeException("Target GameObject is null");
			}
			AIBrain val = default(AIBrain);
			if (!NPC.TryGetComponent<AIBrain>(ref val))
			{
				throw new ScriptRuntimeException("BL_SetNPCAnger: NPC '" + ((Object)NPC).name + "' has no AIBrain component.");
			}
			TriggerRefProxy val2 = default(TriggerRefProxy);
			if (!Target.TryGetComponent<TriggerRefProxy>(ref val2))
			{
				val2 = Target.AddComponent<TriggerRefProxy>();
			}
			val2.triggerType = (TriggerType)0;
			val.behaviour.AddThreat(val2, 999f);
			return true;
		};
		DefaultInterpolatedStringHandler defaultInterpolatedStringHandler = new DefaultInterpolatedStringHandler(35, 2);
		defaultInterpolatedStringHandler.AppendLiteral("BL_SetNPCAnger(NPC: '");
		GameObject obj = NPC;
		defaultInterpolatedStringHandler.AppendFormatted((obj != null) ? ((Object)obj).name : null);
		defaultInterpolatedStringHandler.AppendLiteral("', Target: '");
		GameObject obj2 = Target;
		defaultInterpolatedStringHandler.AppendFormatted((obj2 != null) ? ((Object)obj2).name : null);
		defaultInterpolatedStringHandler.AppendLiteral("')");
		return LuaSafeCall.Run(func, defaultInterpolatedStringHandler.ToStringAndClear());
	}
}
