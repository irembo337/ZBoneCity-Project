using System;
using System.Runtime.CompilerServices;
using Il2CppSLZ.Bonelab;
using Il2CppSLZ.Marrow;
using Il2CppSLZ.Marrow.Combat;
using Il2CppSLZ.Marrow.Data;
using Il2CppSLZ.Marrow.PuppetMasta;
using MelonLoader;
using MoonSharp.Interpreter;
using UnityEngine;

namespace LuaMod.LuaAPI;

public class API_SLZ_Combat
{
	public static readonly API_SLZ_Combat Instance = new API_SLZ_Combat();

	public static bool BL_AttackEnemy(GameObject obj, float damage, Collider col, Vector3 pos, Vector3 normal)
	{
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		Func<bool> func = delegate
		{
			//IL_001e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0023: Unknown result type (might be due to invalid IL or missing references)
			//IL_002b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0038: Unknown result type (might be due to invalid IL or missing references)
			//IL_003a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0045: Unknown result type (might be due to invalid IL or missing references)
			//IL_0052: Unknown result type (might be due to invalid IL or missing references)
			//IL_0054: Unknown result type (might be due to invalid IL or missing references)
			//IL_005f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0061: Unknown result type (might be due to invalid IL or missing references)
			//IL_006d: Expected O, but got Unknown
			//IL_0018: Unknown result type (might be due to invalid IL or missing references)
			if ((Object)(object)obj == (Object)null)
			{
				throw new ScriptRuntimeException("Target GameObject is null");
			}
			Attack val = new Attack
			{
				attackType = (AttackType)1,
				collider = col,
				origin = pos,
				damage = damage,
				normal = normal,
				direction = normal
			};
			BehaviourBaseNav componentInChildren = obj.GetComponentInChildren<BehaviourBaseNav>();
			BehaviourCrablet componentInChildren2 = obj.GetComponentInChildren<BehaviourCrablet>();
			ObjectDestructible componentInChildren3 = obj.GetComponentInChildren<ObjectDestructible>();
			PhysicsRig componentInChildren4 = obj.GetComponentInChildren<PhysicsRig>();
			if ((Object)(object)componentInChildren != (Object)null)
			{
				componentInChildren.health.TakeDamage(1, val);
				return true;
			}
			if ((Object)(object)componentInChildren2 != (Object)null)
			{
				((BehaviourBaseNav)componentInChildren2).health.TakeDamage(1, val);
				componentInChildren2.MountAttack(damage);
				return true;
			}
			if ((Object)(object)componentInChildren3 != (Object)null)
			{
				componentInChildren3.ReceiveAttack(val);
				return true;
			}
			if ((Object)(object)componentInChildren4 != (Object)null)
			{
				API_Player.BL_PlayerHealth().TAKEDAMAGE(damage);
				return true;
			}
			MelonLogger.Warning("[BL_AttackEnemy] No IAttackReceiver found on " + ((Object)obj).name);
			return false;
		};
		DefaultInterpolatedStringHandler defaultInterpolatedStringHandler = new DefaultInterpolatedStringHandler(20, 2);
		defaultInterpolatedStringHandler.AppendLiteral("BL_AttackEnemy('");
		GameObject obj2 = obj;
		defaultInterpolatedStringHandler.AppendFormatted((obj2 != null) ? ((Object)obj2).name : null);
		defaultInterpolatedStringHandler.AppendLiteral("', ");
		defaultInterpolatedStringHandler.AppendFormatted(damage);
		defaultInterpolatedStringHandler.AppendLiteral(")");
		return LuaSafeCall.Run(func, defaultInterpolatedStringHandler.ToStringAndClear());
	}
}
