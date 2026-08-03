using System;
using HarmonyLib;
using Il2CppSLZ.Marrow;
using Il2CppSLZ.Marrow.Data;
using Il2CppSystem;
using MoonSharp.Interpreter;
using UnityEngine;

namespace LuaMod;

public class LuaGun : LuaBehaviour
{
	[HarmonyPatch(typeof(Gun), "Fire")]
	private static class Patch_Gun_Fire
	{
		private static bool Prefix(Gun __instance)
		{
			LuaGun component = ((Component)__instance).gameObject.gameObject.GetComponent<LuaGun>();
			if ((Object)(object)component != (Object)null)
			{
				return component.LuaTriggerPulled();
			}
			return true;
		}
	}

	[HarmonyPatch(typeof(Gun), "OnFire")]
	private static class Patch_Gun_OnFire
	{
		private static bool Prefix(Gun __instance)
		{
			LuaGun component = ((Component)__instance).gameObject.gameObject.GetComponent<LuaGun>();
			if ((Object)(object)component != (Object)null)
			{
				return component.OnFire();
			}
			return true;
		}
	}

	[HarmonyPatch(typeof(Gun), "SpawnCartridge", new Type[] { typeof(Spawnable) })]
	private static class Patch_Gun_SpawnCartridge
	{
		private static bool Prefix(Gun __instance, Spawnable spawnableCartridge)
		{
			LuaGun component = ((Component)__instance).gameObject.GetComponent<LuaGun>();
			if ((Object)(object)component != (Object)null)
			{
				return component.LuaSpawnCartridge(spawnableCartridge);
			}
			return true;
		}
	}

	[HarmonyPatch(typeof(Gun), "OnGripAttached", new Type[]
	{
		typeof(InteractableHost),
		typeof(Hand)
	})]
	private static class Patch_Gun_OnGripAttached
	{
		private static bool Prefix(Gun __instance, InteractableHost host, Hand hand)
		{
			LuaGun component = ((Component)__instance).gameObject.GetComponent<LuaGun>();
			if ((Object)(object)component != (Object)null)
			{
				component.OnGripAttached(host, hand);
				return true;
			}
			return true;
		}
	}

	public Gun AttachedGun;

	public SlideVirtualController AttachedGunSlide;

	public bool SupressBullet = true;

	private DynValue TriggerPulledFunction;

	private DynValue OnFireFunction;

	private DynValue SpawnCartridgeFunction;

	private DynValue OnMagazineEjectFunction;

	private DynValue OnSlideGrabbedFunction;

	private DynValue OnSlideReleasedFunction;

	private DynValue OnSlideUpdateFunction;

	private DynValue OnGripAttachedFunction;

	public new void Start()
	{
		if (ScriptName == "" || ScriptName == null)
		{
			ScriptName = "Spiderman_WebShooter.lua";
		}
		AttachedGun = ((Component)this).gameObject.GetComponent<Gun>();
		AttachedGunSlide = ((Component)this).gameObject.GetComponent<SlideVirtualController>();
		Gun attachedGun = AttachedGun;
		attachedGun.OnMagazineEjectDelegate += Action.op_Implicit((Action)OnMagizineEjected);
		Gun attachedGun2 = AttachedGun;
		attachedGun2.OnSlideGrabbed += Action.op_Implicit((Action)OnSlideGrabbed);
		Gun attachedGun3 = AttachedGun;
		attachedGun3.OnSlideReleased += Action.op_Implicit((Action)OnSlideReleased);
		Gun attachedGun4 = AttachedGun;
		attachedGun4.OnSlideUpdate += Action<float>.op_Implicit((Action<float>)OnSlideUpdate);
		base.Start();
	}

	public void OnGripAttached(InteractableHost host, Hand hand)
	{
		CallScriptFunction(OnGripAttachedFunction, host, hand);
	}

	public void OnMagizineEjected()
	{
		CallScriptFunction(OnMagazineEjectFunction);
	}

	public void OnSlideGrabbed()
	{
		CallScriptFunction(OnSlideGrabbedFunction);
	}

	public void OnSlideReleased()
	{
		CallScriptFunction(OnSlideReleasedFunction);
	}

	public void OnSlideUpdate(float pos)
	{
		CallScriptFunction(OnSlideUpdateFunction, pos);
	}

	public override bool SetupBehaviourFunctions()
	{
		base.SetupBehaviourFunctions();
		if (BehaviourScript != null)
		{
			TriggerPulledFunction = BehaviourScript.GetGlobal("TriggerPulled");
			OnFireFunction = BehaviourScript.GetGlobal("OnFire");
			SpawnCartridgeFunction = BehaviourScript.GetGlobal("SpawnCartridge");
			OnMagazineEjectFunction = BehaviourScript.GetGlobal("OnMagazineEject");
			OnSlideGrabbedFunction = BehaviourScript.GetGlobal("OnSlideGrabbed");
			OnSlideReleasedFunction = BehaviourScript.GetGlobal("OnSlideReleased");
			OnSlideUpdateFunction = BehaviourScript.GetGlobal("OnSlideUpdate");
			OnGripAttachedFunction = BehaviourScript.GetGlobal("OnGripAttached");
			return true;
		}
		return false;
	}

	public DynValue GetFirepointPosition()
	{
		if ((Object)(object)AttachedGun != (Object)null)
		{
			return UserData.Create((object)AttachedGun.firePointTransform);
		}
		return DynValue.Nil;
	}

	public DynValue GetMagazineRounds()
	{
		if ((Object)(object)AttachedGun != (Object)null && (Object)(object)AttachedGun.internalMagazine != (Object)null)
		{
			return DynValue.NewNumber((double)AttachedGun.AmmoCount());
		}
		return DynValue.Nil;
	}

	public bool SetMagazineRounds(int rounds)
	{
		if ((Object)(object)AttachedGun != (Object)null && (Object)(object)AttachedGun.internalMagazine != (Object)null)
		{
			int ammoCount = AttachedGun._magState.AmmoCount;
			int num = rounds - ammoCount;
			if (num > 0)
			{
				AttachedGun._magState.AddCartridge(num, (CartridgeData)null);
			}
			else if (num < 0)
			{
				AttachedGun._magState.ClearMagazine();
				AttachedGun._magState.AddCartridge(rounds, (CartridgeData)null);
				return true;
			}
			return true;
		}
		return false;
	}

	public bool ForceGunFire()
	{
		if ((Object)(object)AttachedGun != (Object)null)
		{
			AttachedGun.Fire();
			return true;
		}
		return false;
	}

	public bool OnFire()
	{
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Invalid comparison between Unknown and I4
		DynValue val = CallScriptFunction(OnFireFunction);
		if (val != null && val != DynValue.Nil && (int)val.Type == 2)
		{
			return val.Boolean;
		}
		return !SupressBullet;
	}

	public bool LuaTriggerPulled()
	{
		CallScriptFunction(TriggerPulledFunction);
		return true;
	}

	public bool LuaSpawnCartridge(Spawnable spawnableCartridge)
	{
		CallScriptFunction(SpawnCartridgeFunction);
		return false;
	}

	public LuaGun(IntPtr ptr)
		: base(ptr)
	{
	}
}
