using BoneLib;
using Il2CppSLZ.Marrow;
using Il2CppSLZ.VRMK;
using MoonSharp.Interpreter;
using UnityEngine;

namespace LuaMod.LuaAPI;

public class API_Player
{
	public static readonly API_Player Instance = new API_Player();

	public static Avatar BL_GetAvatar()
	{
		if ((Object)(object)Player.Avatar != (Object)null)
		{
			return Player.Avatar;
		}
		return null;
	}

	public static GameObject BL_GetAvatarGameObject()
	{
		if ((Object)(object)Player.Avatar != (Object)null)
		{
			return ((Component)Player.Avatar).gameObject;
		}
		return null;
	}

	public static DynValue BL_GetAvatarCenter()
	{
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)Player.Avatar != (Object)null)
		{
			return UserData.Create((object)((Rig)Player.PhysicsRig).m_chest.position);
		}
		return null;
	}

	public static PhysicsRig BL_GetPhysicsRig()
	{
		if ((Object)(object)Player.PhysicsRig != (Object)null)
		{
			return Player.PhysicsRig;
		}
		return null;
	}

	public static ControllerRig BL_GetControllerRig()
	{
		if ((Object)(object)Player.ControllerRig != (Object)null)
		{
			return (ControllerRig)(object)Player.ControllerRig;
		}
		return null;
	}

	public static RemapRig BL_GetRemapRig()
	{
		if ((Object)(object)Player.RemapRig != (Object)null)
		{
			return Player.RemapRig;
		}
		return null;
	}

	public static Health BL_PlayerHealth()
	{
		if ((Object)(object)Player.RigManager != (Object)null && (Object)(object)Player.RigManager.health != (Object)null)
		{
			return Player.RigManager.health;
		}
		return null;
	}

	public static bool BL_SetAvatarPosition(Vector3 pos, Vector3 fwd, bool zeroVelocity = true)
	{
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)Player.PhysicsRig != (Object)null)
		{
			Transform transform = ((Component)Player.PhysicsRig).transform;
			Player.RigManager.Teleport(pos, fwd, zeroVelocity);
			return true;
		}
		return false;
	}

	public static bool BL_SetAvatarPosition(Vector3 pos, bool zeroVelocity = true)
	{
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)Player.PhysicsRig != (Object)null)
		{
			Transform transform = ((Component)Player.PhysicsRig).transform;
			Player.RigManager.Teleport(pos, zeroVelocity);
			return true;
		}
		return false;
	}
}
