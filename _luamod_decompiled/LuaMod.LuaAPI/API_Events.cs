using System;
using System.Collections.Generic;
using BoneLib;
using BoneLib.BoneMenu;
using HarmonyLib;
using Il2CppSLZ.Marrow;
using Il2CppSLZ.Marrow.AI;
using Il2CppSLZ.Marrow.Combat;
using Il2CppSLZ.Marrow.PuppetMasta;
using Il2CppSLZ.Marrow.Warehouse;
using Il2CppSLZ.VRMK;
using MelonLoader;
using MoonSharp.Interpreter;
using UnityEngine;
using UnityEngine.Events;

namespace LuaMod.LuaAPI;

public class API_Events
{
	[HarmonyPatch(typeof(PlayerDamageReceiver), "ReceiveAttack")]
	private static class Patch_Player_OnReceiveDamage
	{
		private static bool Prefix(Health __instance, Attack attack)
		{
			BL_InvokeEvent("Player_OnReceiveDamage", UserData.Create((object)attack));
			return true;
		}
	}

	[HarmonyPatch(typeof(ObjectDestructible), "ReceiveAttack")]
	private static class Patch_ObjectDestructible_OnReceiveDamage
	{
		private static bool Prefix(ObjectDestructible __instance, Attack attack)
		{
			BL_InvokeEvent("Object_OnReceiveDamage", UserData.Create((object)__instance), UserData.Create((object)attack));
			return true;
		}
	}

	public static readonly API_Events Instance;

	public static Dictionary<string, List<EventListner>> EventListeners;

	static API_Events()
	{
		Instance = new API_Events();
		EventListeners = new Dictionary<string, List<EventListner>>();
		SetUpEvents();
	}

	public static bool BL_SubscribeEvent(UnityEvent Uevent, LuaBehaviour Owner, string func)
	{
		throw new NotImplementedException();
	}

	private bool UnityEvent(UnityEvent Uevent)
	{
		throw new NotImplementedException();
	}

	public static bool BL_SubscribeEvent(string eventName, LuaBehaviour Owner, string func)
	{
		List<EventListner> list = (EventListeners.ContainsKey(eventName) ? EventListeners[eventName] : new List<EventListner>());
		EventListner item = default(EventListner);
		item.owner = Owner;
		item.function = func;
		list.Add(item);
		EventListeners[eventName] = list;
		return true;
	}

	public static bool BL_InvokeEvent(string eventName, params DynValue[] args)
	{
		if (!EventListeners.ContainsKey(eventName))
		{
			return false;
		}
		List<EventListner> list = EventListeners[eventName];
		foreach (EventListner item in list)
		{
			if ((Object)(object)item.owner != (Object)null && item.function != "")
			{
				item.owner.CallFunction(item.function, args);
			}
		}
		return true;
	}

	public static void SetUpEvents()
	{
		Hooking.OnPlayerDeath += Event_Hooking_OnPlayerDeath;
		Hooking.OnPlayerDeathImminent += Event_Hooking_OnPlayerDeathImminent;
		Hooking.OnGrabObject += Event_Hooking_OnGrabObject;
		Hooking.OnGripAttached += Event_Hooking_OnGripAttached;
		Hooking.OnGripDetached += Event_Hooking_OnGripDetached;
		Hooking.OnMarrowGameStarted += Event_Hooking_OnMarrowGameStarted;
		Hooking.OnNPCBrainDie += Event_Hooking_OnNPCBrainDie;
		Hooking.OnNPCBrainResurrected += Event_Hooking_OnNPCBrainResurrected;
		Hooking.OnNPCKillEnd += Event_Hooking_OnNPCKillEnd;
		Hooking.OnNPCKillStart += Evemt_Hooking_OnNPCKillStart;
		Hooking.OnPostFireGun += Event_Hooking_OnPostFireGun;
		Hooking.OnPreFireGun += Event_Hooking_OnPreFireGun;
		Hooking.OnReleaseObject += Event_Hooking_OnReleaseObject;
		Hooking.OnSwitchAvatarPostfix += Event_Hooking_OnSwitchAvatarPostfix;
		Hooking.OnSwitchAvatarPrefix += Event_Hooking_OnSwitchAvatarPrefix;
		Hooking.OnUIRigCreated += Event_Hooking_OnUIRigCreated;
		Hooking.OnWarehouseReady += Event_Hooking_OnWarehouseReady;
		FloatElement.OnValueChanged += Event_FloatElement_OnValueChanged;
		Dialog.OnDialogClosed += Event_Dialog_OnDialogClosed;
		Hooking.CreateHook(typeof(Projectile).GetMethod("OnEnable", AccessTools.all), typeof(API_Events).GetMethod("Event_OnProjectileFired", AccessTools.all), false);
		Hooking.CreateHook(typeof(Magazine).GetMethod("OnEject", AccessTools.all), typeof(API_Events).GetMethod("Event_OnMagazineEject", AccessTools.all), false);
		Hooking.CreateHook(typeof(Magazine).GetMethod("OnGrab", AccessTools.all), typeof(API_Events).GetMethod("Event_OnMagazineGrab", AccessTools.all), false);
		Hooking.CreateHook(typeof(Magazine).GetMethod("OnInsert", AccessTools.all), typeof(API_Events).GetMethod("Event_OnMagazineInsert", AccessTools.all), false);
		Hooking.CreateHook(typeof(CrateSpawner).GetMethod("OnPooleeSpawn", AccessTools.all), typeof(API_Events).GetMethod("Event_OnCrateSpawnerSpawned", AccessTools.all), false);
		Hooking.CreateHook(typeof(CrateSpawner).GetMethod("OnPooleeDespawn", AccessTools.all), typeof(API_Events).GetMethod("Event_OnCrateSpawnerDespawned", AccessTools.all), false);
		Hooking.CreateHook(typeof(CrateSpawner).GetMethod("OnPooleeRecycle", AccessTools.all), typeof(API_Events).GetMethod("Event_OnCrateSpawnerRecycle", AccessTools.all), false);
	}

	private static void Event_OnProjectileFired(Projectile __instance)
	{
		MelonLogger.Msg("new projectile " + ((Object)__instance).name);
		BL_InvokeEvent("OnProjectileFired", UserData.Create((object)__instance));
	}

	private static void Event_OnCrateSpawnerSpawned(CrateSpawner __instance, GameObject go)
	{
		MelonLogger.Msg("Crate Spawner " + ((Object)__instance).name + " Spawned " + ((Object)go).name);
		BL_InvokeEvent("OnCrateSpawnerSpawned", UserData.Create((object)__instance), UserData.Create((object)go));
	}

	private static void Event_OnCrateSpawnerDespawned(CrateSpawner __instance, GameObject go)
	{
		MelonLogger.Msg("Crate Spawner " + ((Object)__instance).name + " Despawned " + ((Object)go).name);
		BL_InvokeEvent("OnCrateSpawnerDespawned", UserData.Create((object)__instance), UserData.Create((object)go));
	}

	private static void Event_OnCrateSpawnerRecycle(CrateSpawner __instance, GameObject go)
	{
		MelonLogger.Msg("Crate Spawner " + ((Object)__instance).name + " Recycled " + ((Object)go).name);
		BL_InvokeEvent("OnCrateSpawnerRecycle", UserData.Create((object)__instance), UserData.Create((object)go));
	}

	private static void Event_OnMagazineEject(Magazine __instance)
	{
		MelonLogger.Msg("Magazine ejected " + ((Object)__instance).name);
		BL_InvokeEvent("OnMagazineEject", UserData.Create((object)__instance));
	}

	private static void Event_OnMagazineGrab(Hand hand, Magazine __instance)
	{
		MelonLogger.Msg("Magazine grabbed " + ((Object)__instance).name);
		BL_InvokeEvent("OnMagazineGrab", UserData.Create((object)hand), UserData.Create((object)__instance));
	}

	private static void Event_OnMagazineInsert(Magazine __instance)
	{
		MelonLogger.Msg("Magazine Inserted " + ((Object)__instance).name);
		BL_InvokeEvent("OnMagazineInsert", UserData.Create((object)__instance));
	}

	private static void Event_Dialog_OnDialogClosed(Dialog obj)
	{
		BL_InvokeEvent("BoneMenu_Dialog_OnDialogClosed", UserData.Create((object)obj));
	}

	private static void Event_FloatElement_OnValueChanged(Element arg1, float arg2)
	{
		BL_InvokeEvent("BoneMenu_Float_OnValueChanged", UserData.Create((object)arg1), DynValue.NewNumber((double)arg2));
	}

	private static void Event_Hooking_OnWarehouseReady()
	{
		BL_InvokeEvent("OnWarehouseReady");
	}

	private static void Event_Hooking_OnUIRigCreated()
	{
		BL_InvokeEvent("OnUIRigCreated");
	}

	private static void Event_Hooking_OnSwitchAvatarPrefix(Avatar obj)
	{
		BL_InvokeEvent("OnSwitchAvatarPostfix", UserData.Create((object)obj));
	}

	private static void Event_Hooking_OnSwitchAvatarPostfix(Avatar obj)
	{
		BL_InvokeEvent("OnSwitchAvatarPostfix", UserData.Create((object)obj));
	}

	private static void Event_Hooking_OnReleaseObject(Hand obj)
	{
		BL_InvokeEvent("OnPreFireGun", UserData.Create((object)obj));
	}

	private static void Event_Hooking_OnPreFireGun(Gun obj)
	{
		BL_InvokeEvent("OnPreFireGun", UserData.Create((object)obj));
	}

	private static void Event_Hooking_OnPostFireGun(Gun obj)
	{
		BL_InvokeEvent("OnPostFireGun", UserData.Create((object)obj));
	}

	private static void Evemt_Hooking_OnNPCKillStart(BehaviourBaseNav obj)
	{
		BL_InvokeEvent("OnNPCKillStart", UserData.Create((object)obj));
	}

	private static void Event_Hooking_OnNPCKillEnd(BehaviourBaseNav obj)
	{
		BL_InvokeEvent("OnNPCKillEnd", UserData.Create((object)obj));
	}

	private static void Event_Hooking_OnNPCBrainResurrected(AIBrain obj)
	{
		BL_InvokeEvent("OnNPCBrainResurrected", UserData.Create((object)obj));
	}

	private static void Event_Hooking_OnNPCBrainDie(AIBrain obj)
	{
		BL_InvokeEvent("OnNPCBrainDie", UserData.Create((object)obj));
	}

	private static void Event_Hooking_OnMarrowGameStarted()
	{
		BL_InvokeEvent("OnMarrowGameStarted");
	}

	private static void Event_Hooking_OnGripDetached(Grip arg1, Hand arg2)
	{
		BL_InvokeEvent("OnGripDetached", UserData.Create((object)arg1), UserData.Create((object)arg2));
	}

	private static void Event_Hooking_OnGripAttached(Grip arg1, Hand arg2)
	{
		BL_InvokeEvent("OnGripAttached", UserData.Create((object)arg1), UserData.Create((object)arg2));
	}

	private static void Event_Hooking_OnGrabObject(GameObject arg1, Hand arg2)
	{
		BL_InvokeEvent("OnGrabObject", UserData.Create((object)arg1), UserData.Create((object)arg2));
	}

	private static void Event_Hooking_OnPlayerDeath()
	{
		BL_InvokeEvent("OnPlayerDeath");
	}

	private static void Event_Hooking_OnPlayerDeathImminent(bool IsDying)
	{
		BL_InvokeEvent("OnPlayerDeathImminent", DynValue.NewBoolean(IsDying));
	}
}
