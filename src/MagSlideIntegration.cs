#nullable disable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppSLZ.Marrow;
using Il2CppSLZ.Marrow.Data;
using MelonLoader;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BonelabAdvancedHealth
{
internal static class MagSlideIntegration
{
	private class Rec
	{
		public AmmoSocket Socket;

		public float OrigInfluence;

		public float OrigEndDistance;

		public SphereCollider Trigger;

		public float OrigTriggerRadius;

		public float AppliedRadius = -1f;

		public bool WasLocked;

		public bool PlugCaptured;

		public bool HasSeat;

		public Rigidbody GunRb;

		public Vector3 SeatMagPosL;

		public Quaternion SeatMagRotL;

		public Vector3 SocketPosL;

		public Vector3 AxisL;

		public float RailLen;

		public Magazine LastMag;

		public Rail Rail;

		public float NoRelatchUntil;
	}

	private class Rail
	{
		public ConfigurableJoint Joint;

		public Magazine Mag;

		public Transform MagT;

		public Rigidbody MagRb;

		public AmmoPlug Plug;

		public Transform PlugT;

		public Collider[] MagCols;

		public Collider[] GunCols;

		public Collider[] HandCols;

		public List<Rigidbody> HandRbs;

		public List<Collider> GunHandCols;

		public bool Armed;

		public float LatchTime;

		public float NextTelem;

		public float LastT;

		public float NextBuzz;
	}

	private class SeatData
	{
		public Vector3 SocketPosL;

		public Vector3 AxisL;

		public Vector3 SeatMagPosL;

		public Quaternion SeatMagRotL;

		public float RailLen;
	}

	private enum BuzzKind
	{
		Catch,
		Slide,
		Insert
	}

	private static readonly bool TraceEnabled = false;
	private const float SnapRange = 0.2f;
	private const float CatchRange = 0.01f;
	private const float InsertDepth = 0.7f;
	private static HarmonyLib.Harmony _harmony;
	private static MelonLogger.Instance _logger;

	private const float InfluenceFloor = 0.05f;

	private const float ScanInterval = 1f;

	private const float CatchMin = 0.001f;

	private const float CatchMax = 0.08f;

	private const float CatchDefault = 0.01f;

	private const float DepthMin = 0.4f;

	private const float DepthMax = 0.95f;

	private const float DepthDefault = 0.7f;

	private const float RetainMargin = 0.05f;

	private static readonly Dictionary<int, Rec> _known = new Dictionary<int, Rec>();

	private static float _scanAccum;

	private static int _lastCount = -1;

	private static bool _structLogged;

	private static bool _patchesOk;

	private const float RailBottomOff = 0.045f;

	private const float RailAngleGate = 25f;

	private const float RelatchAngleGate = 35f;

	private const float RelatchCooldown = 0.6f;

	private const float RailAlignRate = 0.06f;

	private static readonly List<AmmoPlug> _plugs = new List<AmmoPlug>();

	private static readonly Dictionary<string, SeatData> _seatLib = new Dictionary<string, SeatData>();

	private static readonly List<KeyValuePair<Rail, float>> _dyingRails = new List<KeyValuePair<Rail, float>>();

	

	

	public static void Initialize(HarmonyLib.Harmony harmony, MelonLogger.Instance logger)
	{
		_harmony = harmony;
		_logger = logger;
		InstallTracer();
		CleanupForLevelChange();
		ApplyToAll();
		_logger.Msg("[ZBC] Integrated MagSlide loaded.");
	}

	public static void Update()
	{
		if (true || true)
		{
			if (true)
			{
				ManageTriggers();
				UpdateRails();
			}
			ProcessDyingRails();
			_scanAccum += Time.unscaledDeltaTime;
			if (!(_scanAccum < 1f))
			{
				_scanAccum = 0f;
				ApplyToAll();
			}
		}
	}

	public static void CleanupForLevelChange()
	{
		foreach (Rec value in _known.Values)
		{
			try
			{
				if (value.Rail != null)
				{
					ReleaseRail(value, "level change");
				}
			}
			catch
			{
			}
		}
		for (int num = _dyingRails.Count - 1; num >= 0; num--)
		{
			try
			{
				SetPairIgnores(_dyingRails[num].Key, ignore: false);
				RestoreHandBodies(_dyingRails[num].Key);
			}
			catch
			{
			}
		}
		_dyingRails.Clear();
		_known.Clear();
		_lastCount = -1;
		_structLogged = false;
	}

	private static float EngagedRadius(Rec r)
	{
		return Mathf.Min(r.OrigTriggerRadius, r.OrigEndDistance + 0.05f);
	}

	private static float CompleteAt(Rec r)
	{
		float num = Mathf.Clamp(InsertDepth, 0.4f, 0.95f);
		return Mathf.Min(r.RailLen - 0.01f, r.RailLen * num);
	}

	private static void SetTrigger(Rec r, bool on, float radius)
	{
		try
		{
			if (!((UnityEngine.Object)(UnityEngine.Object)r.Trigger == (UnityEngine.Object)null))
			{
				if (((Collider)r.Trigger).enabled != on)
				{
					((Collider)r.Trigger).enabled = on;
				}
				if (Mathf.Abs(radius - r.AppliedRadius) > 0.0001f)
				{
					r.Trigger.radius = radius;
					r.AppliedRadius = radius;
				}
			}
		}
		catch
		{
		}
	}

	private static void ManageTriggers()
	{
		if (!_patchesOk)
		{
			return;
		}
		float num = Mathf.Clamp(CatchRange, 0.001f, 0.08f);
		foreach (Rec value in _known.Values)
		{
			try
			{
				if ((UnityEngine.Object)(UnityEngine.Object)value.Socket == (UnityEngine.Object)null || (UnityEngine.Object)(UnityEngine.Object)value.Trigger == (UnityEngine.Object)null)
				{
					continue;
				}
				if (value.Rail != null)
				{
					value.WasLocked = false;
					continue;
				}
				bool flag = (UnityEngine.Object)(UnityEngine.Object)((Socket)value.Socket).LockedPlug != (UnityEngine.Object)null;
				if (value.WasLocked && !flag)
				{
					value.WasLocked = false;
					value.PlugCaptured = false;
					SetTrigger(value, !value.HasSeat, Mathf.Min(value.OrigTriggerRadius, num));
					TryRelatchEjected(value);
					continue;
				}
				value.WasLocked = flag;
				if (flag || value.PlugCaptured)
				{
					SetTrigger(value, on: true, EngagedRadius(value));
				}
				else if (value.HasSeat)
				{
					SetTrigger(value, on: false, Mathf.Min(value.OrigTriggerRadius, num));
				}
				else
				{
					SetTrigger(value, on: true, Mathf.Min(value.OrigTriggerRadius, num));
				}
			}
			catch
			{
			}
		}
	}

	private static void ApplyToAll()
	{
		//IL_009c: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a1: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			Il2CppArrayBase<AmmoSocket> val = Resources.FindObjectsOfTypeAll<AmmoSocket>();
			if (val == null)
			{
				if (_lastCount != 0)
				{
					_logger?.Warning("MagSlide: AmmoSocket search returned null.");
					_lastCount = 0;
				}
				return;
			}
			int length = val.Length;
			if (length != _lastCount)
			{
				_logger?.Msg($"MagSlide: {length} mag well(s) loaded in memory.");
				_lastCount = length;
			}
			int num = 0;
			for (int i = 0; i < length; i++)
			{
				AmmoSocket val2 = val[i];
				if ((UnityEngine.Object)(UnityEngine.Object)val2 == (UnityEngine.Object)null)
				{
					continue;
				}
				try
				{
					Scene scene = ((Component)val2).gameObject.scene;
					if (scene.IsValid())
					{
						num++;
						Register(val2);
						ApplyOne(val2);
					}
				}
				catch
				{
				}
			}
			if (!_structLogged && num > 0)
			{
				_structLogged = true;
				
			}
			RefreshPlugCache();
		}
		catch (Exception ex)
		{
			_logger?.Warning("Socket sweep failed: " + ex.Message);
		}
	}

	private static void Register(AmmoSocket s)
	{
		int instanceID = ((UnityEngine.Object)s).GetInstanceID();
		if (_known.ContainsKey(instanceID))
		{
			return;
		}
		Rec rec = new Rec
		{
			Socket = s
		};
		try
		{
			rec.OrigInfluence = ((Socket)s).influenceRadius;
		}
		catch
		{
		}
		try
		{
			rec.OrigEndDistance = ((Socket)s).endDistance;
		}
		catch
		{
		}
		try
		{
			Il2CppArrayBase<SphereCollider> components = ((Component)s).GetComponents<SphereCollider>();
			if (components != null)
			{
				for (int i = 0; i < components.Length; i++)
				{
					SphereCollider val = components[i];
					if (!((UnityEngine.Object)(UnityEngine.Object)val == (UnityEngine.Object)null) && ((Collider)val).isTrigger)
					{
						rec.Trigger = val;
						rec.OrigTriggerRadius = val.radius;
						break;
					}
				}
			}
		}
		catch
		{
		}
		_known[instanceID] = rec;
	}

	private static void ApplyOne(AmmoSocket s)
	{
		int instanceID = ((UnityEngine.Object)s).GetInstanceID();
		if (!_known.TryGetValue(instanceID, out var value))
		{
			return;
		}
		bool value2 = true;
		bool value3 = true;
		if (value2)
		{
			float num = Mathf.Clamp(SnapRange, 0.05f, 1f);
			((Socket)s).influenceRadius = Mathf.Max(0.05f, value.OrigInfluence * num);
		}
		else
		{
			((Socket)s).influenceRadius = value.OrigInfluence;
		}
		((Socket)s).endDistance = value.OrigEndDistance;
		if (value3)
		{
			return;
		}
		if (value.Rail != null)
		{
			ReleaseRail(value, "mode off");
		}
		if (!((UnityEngine.Object)(UnityEngine.Object)value.Trigger != (UnityEngine.Object)null) || !(value.AppliedRadius >= 0f))
		{
			return;
		}
		try
		{
			value.Trigger.radius = value.OrigTriggerRadius;
			((Collider)value.Trigger).enabled = true;
			value.AppliedRadius = -1f;
		}
		catch
		{
		}
	}

	private static void RestoreAll()
	{
		foreach (Rec value in _known.Values)
		{
			try
			{
				if (value.Rail != null)
				{
					ReleaseRail(value, "restore");
				}
				if (!((UnityEngine.Object)(UnityEngine.Object)value.Socket == (UnityEngine.Object)null))
				{
					((Socket)value.Socket).influenceRadius = value.OrigInfluence;
					((Socket)value.Socket).endDistance = value.OrigEndDistance;
					if ((UnityEngine.Object)(UnityEngine.Object)value.Trigger != (UnityEngine.Object)null)
					{
						value.Trigger.radius = value.OrigTriggerRadius;
						((Collider)value.Trigger).enabled = true;
						value.AppliedRadius = -1f;
					}
				}
			}
			catch
			{
			}
		}
	}

	private static void ClearSeatLib()
	{
		_seatLib.Clear();
		foreach (Rec value in _known.Values)
		{
			try
			{
				if (value.Rail != null)
				{
					ReleaseRail(value, "rails cleared");
				}
				value.HasSeat = false;
				value.LastMag = null;
			}
			catch
			{
			}
		}
	}

	private static void Retune()
	{
		ApplyToAll();
	}

	private static string F(float v)
	{
		return v.ToString("R", CultureInfo.InvariantCulture);
	}

	private static float PF(string s)
	{
		return float.Parse(s, CultureInfo.InvariantCulture);
	}

	private static void LoadSeatLib()
	{
	}

	private static void SaveSeatLib()
	{
	}

	private static string GunModelKey(Component socketSide)
	{
		try
		{
			string name = ((UnityEngine.Object)socketSide.transform.root).name;
			int num = name.LastIndexOf(" [", StringComparison.Ordinal);
			return (num > 0) ? name.Substring(0, num) : name;
		}
		catch
		{
			return null;
		}
	}

	private static void TrySeatFromLibrary(Rec r)
	{
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		//IL_0067: Unknown result type (might be due to invalid IL or missing references)
		//IL_006c: Unknown result type (might be due to invalid IL or missing references)
		//IL_007f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0084: Unknown result type (might be due to invalid IL or missing references)
		//IL_008b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0090: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			if (r.HasSeat || (UnityEngine.Object)(UnityEngine.Object)r.Socket == (UnityEngine.Object)null)
			{
				return;
			}
			string text = GunModelKey((Component)(UnityEngine.Object)r.Socket);
			if (text != null && _seatLib.TryGetValue(text, out var value))
			{
				Rigidbody componentInParent = ((Component)r.Socket).GetComponentInParent<Rigidbody>();
				if (!((UnityEngine.Object)(UnityEngine.Object)componentInParent == (UnityEngine.Object)null))
				{
					r.GunRb = componentInParent;
					r.SocketPosL = value.SocketPosL;
					r.AxisL = value.AxisL;
					r.RailLen = value.RailLen;
					r.SeatMagPosL = value.SeatMagPosL;
					r.SeatMagRotL = value.SeatMagRotL;
					r.HasSeat = true;
				}
			}
		}
		catch
		{
		}
	}

	private static void RefreshPlugCache()
	{
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		_plugs.Clear();
		try
		{
			Il2CppArrayBase<AmmoPlug> val = Resources.FindObjectsOfTypeAll<AmmoPlug>();
			if (val == null)
			{
				return;
			}
			for (int i = 0; i < val.Length; i++)
			{
				AmmoPlug val2 = val[i];
				if ((UnityEngine.Object)(UnityEngine.Object)val2 == (UnityEngine.Object)null)
				{
					continue;
				}
				try
				{
					Scene scene = ((Component)val2).gameObject.scene;
					if (scene.IsValid())
					{
						_plugs.Add(val2);
					}
				}
				catch
				{
				}
			}
		}
		catch
		{
		}
	}

	private static List<Collider> CollectGunHandColliders(Rec r)
	{
		List<Collider> list = new List<Collider>();
		try
		{
			Gun gun = r.Socket.gun;
			if ((UnityEngine.Object)(UnityEngine.Object)gun == (UnityEngine.Object)null || (UnityEngine.Object)(UnityEngine.Object)gun.triggerGrip == (UnityEngine.Object)null)
			{
				return list;
			}
			Hand hand = gun.triggerGrip.GetHand();
			if ((UnityEngine.Object)(UnityEngine.Object)hand == (UnityEngine.Object)null)
			{
				return list;
			}
			try
			{
				Il2CppArrayBase<Collider> componentsInChildren = ((Component)hand).GetComponentsInChildren<Collider>(true);
				if (componentsInChildren != null)
				{
					foreach (Collider item in componentsInChildren)
					{
						if ((UnityEngine.Object)(UnityEngine.Object)item != (UnityEngine.Object)null)
						{
							list.Add(item);
						}
					}
				}
			}
			catch
			{
			}
			try
			{
				PhysHand physHand = hand.physHand;
				if ((UnityEngine.Object)(UnityEngine.Object)physHand != (UnityEngine.Object)null)
				{
					if ((UnityEngine.Object)(UnityEngine.Object)physHand.handCol != (UnityEngine.Object)null && !list.Contains((Collider)(UnityEngine.Object)physHand.handCol))
					{
						list.Add((Collider)(UnityEngine.Object)physHand.handCol);
					}
					if ((UnityEngine.Object)(UnityEngine.Object)physHand.fingersCol != (UnityEngine.Object)null && !list.Contains((Collider)(UnityEngine.Object)physHand.fingersCol))
					{
						list.Add((Collider)(UnityEngine.Object)physHand.fingersCol);
					}
					if ((UnityEngine.Object)(UnityEngine.Object)physHand.cUpper != (UnityEngine.Object)null && !list.Contains((Collider)(UnityEngine.Object)physHand.cUpper))
					{
						list.Add((Collider)(UnityEngine.Object)physHand.cUpper);
					}
					if ((UnityEngine.Object)(UnityEngine.Object)physHand.cLower != (UnityEngine.Object)null && !list.Contains((Collider)(UnityEngine.Object)physHand.cLower))
					{
						list.Add((Collider)(UnityEngine.Object)physHand.cLower);
					}
				}
			}
			catch
			{
			}
		}
		catch
		{
		}
		return list;
	}

	private static void SetPairIgnores(Rail rail, bool ignore)
	{
		if (rail == null)
		{
			return;
		}
		try
		{
			Collider[] gunCols;
			if (rail.GunCols != null && rail.MagCols != null)
			{
				gunCols = rail.GunCols;
				foreach (Collider val in gunCols)
				{
					if ((UnityEngine.Object)(UnityEngine.Object)val == (UnityEngine.Object)null || val.isTrigger)
					{
						continue;
					}
					Collider[] magCols = rail.MagCols;
					foreach (Collider val2 in magCols)
					{
						if (!((UnityEngine.Object)(UnityEngine.Object)val2 == (UnityEngine.Object)null) && !val2.isTrigger)
						{
							try
							{
								Physics.IgnoreCollision(val2, val, ignore);
							}
							catch
							{
							}
						}
					}
					if (!ignore || rail.HandCols == null)
					{
						continue;
					}
					magCols = rail.HandCols;
					foreach (Collider val3 in magCols)
					{
						if (!((UnityEngine.Object)(UnityEngine.Object)val3 == (UnityEngine.Object)null) && !val3.isTrigger)
						{
							try
							{
								Physics.IgnoreCollision(val3, val, true);
							}
							catch
							{
							}
						}
					}
				}
			}
			if (rail.GunHandCols != null && rail.MagCols != null)
			{
				foreach (Collider gunHandCol in rail.GunHandCols)
				{
					if ((UnityEngine.Object)(UnityEngine.Object)gunHandCol == (UnityEngine.Object)null || gunHandCol.isTrigger)
					{
						continue;
					}
					gunCols = rail.MagCols;
					foreach (Collider val4 in gunCols)
					{
						if (!((UnityEngine.Object)(UnityEngine.Object)val4 == (UnityEngine.Object)null) && !val4.isTrigger)
						{
							try
							{
								Physics.IgnoreCollision(val4, gunHandCol, ignore);
							}
							catch
							{
							}
						}
					}
				}
			}
			if (ignore || rail.HandCols == null || rail.GunCols == null)
			{
				return;
			}
			gunCols = rail.GunCols;
			foreach (Collider val5 in gunCols)
			{
				if ((UnityEngine.Object)(UnityEngine.Object)val5 == (UnityEngine.Object)null || val5.isTrigger)
				{
					continue;
				}
				Collider[] magCols = rail.HandCols;
				foreach (Collider val6 in magCols)
				{
					if (!((UnityEngine.Object)(UnityEngine.Object)val6 == (UnityEngine.Object)null) && !val6.isTrigger)
					{
						try
						{
							Physics.IgnoreCollision(val6, val5, false);
						}
						catch
						{
						}
					}
				}
			}
		}
		catch
		{
		}
	}

	private static void ProcessDyingRails()
	{
		for (int num = _dyingRails.Count - 1; num >= 0; num--)
		{
			if (!(Time.unscaledTime < _dyingRails[num].Value))
			{
				Rail key = _dyingRails[num].Key;
				_dyingRails.RemoveAt(num);
				SetPairIgnores(key, ignore: false);
				RestoreHandBodies(key);
			}
		}
	}

	private static void RestoreHandBodies(Rail rail)
	{
		try
		{
			if (rail.HandRbs == null)
			{
				return;
			}
			foreach (Rigidbody handRb in rail.HandRbs)
			{
				try
				{
					if ((UnityEngine.Object)(UnityEngine.Object)handRb != (UnityEngine.Object)null)
					{
						handRb.detectCollisions = true;
					}
				}
				catch
				{
				}
			}
		}
		catch
		{
		}
	}

	private static bool IsHeld(AmmoPlug plug)
	{
		try
		{
			InteractableHost host = ((Plug)plug).host;
			return (UnityEngine.Object)(UnityEngine.Object)host != (UnityEngine.Object)null && (UnityEngine.Object)(UnityEngine.Object)host.GetHand(0) != (UnityEngine.Object)null;
		}
		catch
		{
			return false;
		}
	}

	private static bool RailedAnywhere(AmmoPlug plug)
	{
		foreach (Rec value in _known.Values)
		{
			try
			{
				if (value.Rail != null && (UnityEngine.Object)(UnityEngine.Object)value.Rail.Plug == (UnityEngine.Object)(UnityEngine.Object)plug)
				{
					return true;
				}
			}
			catch
			{
			}
		}
		return false;
	}

	private static void Buzz(AmmoPlug plug, BuzzKind kind)
	{
		try
		{
			InteractableHost val = (((UnityEngine.Object)(UnityEngine.Object)plug != (UnityEngine.Object)null) ? ((Plug)plug).host : null);
			Hand val2 = (((UnityEngine.Object)(UnityEngine.Object)val != (UnityEngine.Object)null) ? val.GetHand(0) : null);
			if ((UnityEngine.Object)(UnityEngine.Object)val2 == (UnityEngine.Object)null || (UnityEngine.Object)(UnityEngine.Object)val2.Controller == (UnityEngine.Object)null)
			{
				return;
			}
			Haptor haptor = val2.Controller.haptor;
			if (!((UnityEngine.Object)(UnityEngine.Object)haptor == (UnityEngine.Object)null))
			{
				switch (kind)
				{
				case BuzzKind.Catch:
					haptor.Haptic_Tap();
					break;
				case BuzzKind.Slide:
					haptor.Haptic_SlideFriction();
					break;
				case BuzzKind.Insert:
					haptor.Haptic_WepMagInsert();
					break;
				}
			}
		}
		catch
		{
		}
	}

	private static bool PlatformMatches(Rec r, AmmoPlug plug)
	{
		try
		{
			Gun gun = r.Socket.gun;
			if ((UnityEngine.Object)(UnityEngine.Object)gun == (UnityEngine.Object)null || (UnityEngine.Object)(UnityEngine.Object)gun.defaultMagazine == (UnityEngine.Object)null)
			{
				return true;
			}
			Magazine magazine = plug.magazine;
			if ((UnityEngine.Object)(UnityEngine.Object)magazine == (UnityEngine.Object)null)
			{
				return true;
			}
			MagazineData magazineData = magazine.magazineState.magazineData;
			if ((UnityEngine.Object)(UnityEngine.Object)magazineData == (UnityEngine.Object)null)
			{
				return true;
			}
			string platform = gun.defaultMagazine.platform;
			string platform2 = magazineData.platform;
			if (string.IsNullOrEmpty(platform) || string.IsNullOrEmpty(platform2))
			{
				return true;
			}
			return platform == platform2;
		}
		catch
		{
			return true;
		}
	}

	private static void UpdateRails()
	{
		//IL_0148: Unknown result type (might be due to invalid IL or missing references)
		//IL_014d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0153: Unknown result type (might be due to invalid IL or missing references)
		//IL_0158: Unknown result type (might be due to invalid IL or missing references)
		//IL_015d: Unknown result type (might be due to invalid IL or missing references)
		//IL_015f: Unknown result type (might be due to invalid IL or missing references)
		//IL_016c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0173: Unknown result type (might be due to invalid IL or missing references)
		//IL_0178: Unknown result type (might be due to invalid IL or missing references)
		//IL_017d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c6: Unknown result type (might be due to invalid IL or missing references)
		//IL_01cc: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d1: Unknown result type (might be due to invalid IL or missing references)
		//IL_01dd: Unknown result type (might be due to invalid IL or missing references)
		if (!_patchesOk)
		{
			return;
		}
		float unscaledTime = Time.unscaledTime;
		foreach (Rec value in _known.Values)
		{
			try
			{
				if (value.Rail != null)
				{
					TickRail(value);
					continue;
				}
				if (!value.HasSeat)
				{
					TrySeatFromLibrary(value);
				}
				if (!value.HasSeat || (UnityEngine.Object)(UnityEngine.Object)value.Socket == (UnityEngine.Object)null || (UnityEngine.Object)(UnityEngine.Object)value.Trigger == (UnityEngine.Object)null)
				{
					continue;
				}
				if ((UnityEngine.Object)(UnityEngine.Object)value.GunRb == (UnityEngine.Object)null)
				{
					value.HasSeat = false;
				}
				else
				{
					if (unscaledTime < value.NoRelatchUntil || (UnityEngine.Object)(UnityEngine.Object)((Socket)value.Socket).LockedPlug != (UnityEngine.Object)null || value.PlugCaptured)
					{
						continue;
					}
					Transform transform = ((Component)value.GunRb).transform;
					transform.TransformPoint(value.SocketPosL);
					for (int i = 0; i < _plugs.Count; i++)
					{
						AmmoPlug val = _plugs[i];
						if ((UnityEngine.Object)(UnityEngine.Object)val == (UnityEngine.Object)null)
						{
							continue;
						}
						try
						{
							Magazine magazine = val.magazine;
							if (!((UnityEngine.Object)(UnityEngine.Object)magazine == (UnityEngine.Object)null) && ((Component)magazine).gameObject.activeInHierarchy && !magazine.isMagazineInserted && !RailedAnywhere(val))
							{
								Transform transform2 = ((Component)val).transform;
								Vector3 val2 = transform.InverseTransformPoint(transform2.position) - value.SocketPosL;
								float num = Vector3.Dot(val2, value.AxisL);
								Vector3 val3 = val2 - value.AxisL * num;
								float magnitude = val3.magnitude;
								if (!(num < -0.02f) && !(num > value.RailLen * 0.5f) && !(magnitude > 0.02f) && IsHeld(val) && PlatformMatches(value, val) && !(Quaternion.Angle(transform.rotation * value.SeatMagRotL, ((Component)magazine).transform.rotation) > 25f))
								{
									Latch(value, val);
									break;
								}
							}
						}
						catch
						{
						}
					}
					continue;
				}
			}
			catch
			{
			}
		}
	}

	private static void Latch(Rec r, AmmoPlug plug)
	{
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
		//IL_0223: Unknown result type (might be due to invalid IL or missing references)
		//IL_0228: Unknown result type (might be due to invalid IL or missing references)
		//IL_022d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0232: Unknown result type (might be due to invalid IL or missing references)
		//IL_0236: Unknown result type (might be due to invalid IL or missing references)
		//IL_023d: Unknown result type (might be due to invalid IL or missing references)
		//IL_023f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0244: Unknown result type (might be due to invalid IL or missing references)
		//IL_0249: Unknown result type (might be due to invalid IL or missing references)
		//IL_026b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0259: Unknown result type (might be due to invalid IL or missing references)
		//IL_025b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0260: Unknown result type (might be due to invalid IL or missing references)
		//IL_0265: Unknown result type (might be due to invalid IL or missing references)
		Magazine magazine = plug.magazine;
		Transform transform = ((Component)magazine).transform;
		Rigidbody component = ((Component)magazine).GetComponent<Rigidbody>();
		if ((UnityEngine.Object)(UnityEngine.Object)component == (UnityEngine.Object)null)
		{
			return;
		}
		Transform transform2 = ((Component)r.GunRb).transform;
		Transform transform3 = ((Component)plug).transform;
		PurgeStrayJoints(null, component);
		float num = Vector3.Dot(transform2.InverseTransformPoint(transform3.position) - r.SocketPosL, r.AxisL);
		num = Mathf.Clamp(num, 0f, r.RailLen);
		Collider[] magCols = null;
		Collider[] gunCols = null;
		Collider[] handCols = null;
		List<Rigidbody> list = null;
		List<Collider> list2 = null;
		try
		{
			magCols = ((Component)magazine).GetComponentsInChildren<Collider>(true);
			gunCols = ((Component)r.GunRb).GetComponentsInChildren<Collider>(true);
			list2 = CollectGunHandColliders(r);
			try
			{
				Hand val = (((UnityEngine.Object)(UnityEngine.Object)((Plug)plug).host != (UnityEngine.Object)null) ? ((Plug)plug).host.GetHand(0) : null);
				if ((UnityEngine.Object)(UnityEngine.Object)val != (UnityEngine.Object)null)
				{
					handCols = ((Component)val).GetComponentsInChildren<Collider>(true);
					list = new List<Rigidbody>();
					Rigidbody componentInParent = ((Component)val).GetComponentInParent<Rigidbody>();
					if ((UnityEngine.Object)(UnityEngine.Object)componentInParent != (UnityEngine.Object)null)
					{
						list.Add(componentInParent);
					}
					Il2CppArrayBase<Rigidbody> componentsInChildren = ((Component)val).GetComponentsInChildren<Rigidbody>(false);
					if (componentsInChildren != null)
					{
						foreach (Rigidbody item in componentsInChildren)
						{
							if ((UnityEngine.Object)(UnityEngine.Object)item != (UnityEngine.Object)null && !list.Contains(item))
							{
								list.Add(item);
							}
						}
					}
					foreach (Rigidbody item2 in list)
					{
						try
						{
							item2.detectCollisions = false;
						}
						catch
						{
						}
					}
				}
			}
			catch
			{
			}
			if (TraceEnabled)
			{
				_logger?.Msg($"[trace] LATCH: magHandBodies={list?.Count ?? 0} gunHandCols={list2.Count}");
			}
		}
		catch
		{
		}
		ConfigurableJoint val2 = ((Component)component).gameObject.AddComponent<ConfigurableJoint>();
		((Joint)val2).connectedBody = r.GunRb;
		Vector3 val4 = (((Joint)val2).axis = transform.InverseTransformDirection(transform2.TransformDirection(r.AxisL)));
		Vector3 val5 = Vector3.Cross(val4, Vector3.up);
		if (val5.sqrMagnitude < 0.001f)
		{
			val5 = Vector3.Cross(val4, Vector3.right);
		}
		val2.secondaryAxis = val5.normalized;
		val2.xMotion = (ConfigurableJointMotion)2;
		val2.yMotion = (ConfigurableJointMotion)0;
		val2.zMotion = (ConfigurableJointMotion)0;
		val2.angularXMotion = (ConfigurableJointMotion)0;
		val2.angularYMotion = (ConfigurableJointMotion)0;
		val2.angularZMotion = (ConfigurableJointMotion)0;
		((Joint)val2).enableCollision = false;
		((Joint)val2).enablePreprocessing = false;
		((Joint)val2).autoConfigureConnectedAnchor = false;
		try
		{
			((Collider)r.Trigger).enabled = false;
		}
		catch
		{
		}
		float num2 = CompleteAt(r);
		r.Rail = new Rail
		{
			Joint = val2,
			Mag = magazine,
			MagT = transform,
			MagRb = component,
			Plug = plug,
			PlugT = transform3,
			MagCols = magCols,
			GunCols = gunCols,
			HandCols = handCols,
			HandRbs = list,
			GunHandCols = list2,
			Armed = (num < num2 - 0.015f),
			LatchTime = Time.unscaledTime,
			LastT = num
		};
		SetPairIgnores(r.Rail, ignore: true);
		Buzz(plug, BuzzKind.Catch);
		if (TraceEnabled)
		{
			_logger?.Msg($"[trace] LATCH    gun='{GunName((Component)(UnityEngine.Object)r.Socket)}' t0={num:F3} rail={r.RailLen:F3} complete={num2:F3} armed={r.Rail.Armed}");
		}
	}

	private static void PurgeStrayJoints(Rail rail, Rigidbody magRb)
	{
		try
		{
			Il2CppArrayBase<ConfigurableJoint> components = ((Component)magRb).GetComponents<ConfigurableJoint>();
			if (components == null)
			{
				return;
			}
			for (int i = 0; i < components.Length; i++)
			{
				ConfigurableJoint val = components[i];
				if (!((UnityEngine.Object)(UnityEngine.Object)val == (UnityEngine.Object)null) && (rail == null || !((UnityEngine.Object)(UnityEngine.Object)rail.Joint != (UnityEngine.Object)null) || ((UnityEngine.Object)val).GetInstanceID() != ((UnityEngine.Object)rail.Joint).GetInstanceID()) && !((UnityEngine.Object)(UnityEngine.Object)((Joint)val).connectedBody != (UnityEngine.Object)null))
				{
					if (TraceEnabled)
					{
						_logger?.Msg("[trace] purged a world-anchored joint on the mag");
					}
					UnityEngine.Object.Destroy((UnityEngine.Object)(UnityEngine.Object)val);
				}
			}
		}
		catch
		{
		}
	}

	private static void TickRail(Rec r)
	{
		//IL_0137: Unknown result type (might be due to invalid IL or missing references)
		//IL_0139: Unknown result type (might be due to invalid IL or missing references)
		//IL_013f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0144: Unknown result type (might be due to invalid IL or missing references)
		//IL_0149: Unknown result type (might be due to invalid IL or missing references)
		//IL_0178: Unknown result type (might be due to invalid IL or missing references)
		//IL_017d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0181: Unknown result type (might be due to invalid IL or missing references)
		//IL_0188: Unknown result type (might be due to invalid IL or missing references)
		//IL_018d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0075: Unknown result type (might be due to invalid IL or missing references)
		//IL_007a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0080: Unknown result type (might be due to invalid IL or missing references)
		//IL_0085: Unknown result type (might be due to invalid IL or missing references)
		//IL_008a: Unknown result type (might be due to invalid IL or missing references)
		//IL_008b: Unknown result type (might be due to invalid IL or missing references)
		//IL_008d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fa: Unknown result type (might be due to invalid IL or missing references)
		//IL_0113: Unknown result type (might be due to invalid IL or missing references)
		//IL_0118: Unknown result type (might be due to invalid IL or missing references)
		//IL_011c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0121: Unknown result type (might be due to invalid IL or missing references)
		Rail rail = r.Rail;
		if ((UnityEngine.Object)(UnityEngine.Object)rail.Joint == (UnityEngine.Object)null || (UnityEngine.Object)(UnityEngine.Object)rail.Mag == (UnityEngine.Object)null || (UnityEngine.Object)(UnityEngine.Object)rail.PlugT == (UnityEngine.Object)null || (UnityEngine.Object)(UnityEngine.Object)r.GunRb == (UnityEngine.Object)null || !((Component)rail.Mag).gameObject.activeInHierarchy)
		{
			ReleaseRail(r, "stale");
			return;
		}
		Transform transform = ((Component)r.GunRb).transform;
		Vector3 val = transform.InverseTransformPoint(rail.PlugT.position) - r.SocketPosL;
		float num = Vector3.Dot(val, r.AxisL);
		float num2 = CompleteAt(r);
		if (num > r.RailLen)
		{
			Vector3 val2 = transform.TransformDirection(r.AxisL);
			try
			{
				Rigidbody magRb = rail.MagRb;
				magRb.position -= val2 * (num - r.RailLen);
				float num3 = Vector3.Dot(rail.MagRb.velocity - r.GunRb.velocity, val2);
				if (num3 > 0f)
				{
					Rigidbody magRb2 = rail.MagRb;
					magRb2.velocity -= val2 * num3;
				}
			}
			catch
			{
			}
			num = r.RailLen;
		}
		Vector3 val3 = val - r.AxisL * num;
		float magnitude = val3.magnitude;
		if (magnitude > 0.0005f)
		{
			try
			{
				float num4 = Mathf.Min(magnitude, 0.06f * Time.deltaTime);
				ConfigurableJoint joint = rail.Joint;
				((Joint)joint).connectedAnchor = ((Joint)joint).connectedAnchor - val3 / magnitude * num4;
			}
			catch
			{
			}
		}
		if (Mathf.Abs(num - rail.LastT) > 0.006f && Time.unscaledTime >= rail.NextBuzz)
		{
			rail.NextBuzz = Time.unscaledTime + 0.09f;
			Buzz(rail.Plug, BuzzKind.Slide);
		}
		rail.LastT = num;
		if (!rail.Armed && (num < num2 - 0.015f || Time.unscaledTime - rail.LatchTime > 0.35f))
		{
			rail.Armed = true;
		}
		if (Time.unscaledTime >= rail.NextTelem)
		{
			rail.NextTelem = Time.unscaledTime + 0.4f;
			PurgeStrayJoints(rail, rail.MagRb);
			SetPairIgnores(rail, ignore: true);
			try
			{
				if (rail.HandRbs != null)
				{
					foreach (Rigidbody handRb in rail.HandRbs)
					{
						try
						{
							if ((UnityEngine.Object)(UnityEngine.Object)handRb != (UnityEngine.Object)null)
							{
								handRb.detectCollisions = false;
							}
						}
						catch
						{
						}
					}
				}
			}
			catch
			{
			}
			if (TraceEnabled)
			{
				try
				{
					bool value = IsHeld(rail.Plug);
					int value2 = 0;
					try
					{
						value2 = ((Component)rail.MagRb).GetComponents<ConfigurableJoint>()?.Length ?? 0;
					}
					catch
					{
					}
					_logger?.Msg($"[trace] RAIL     t={num:F3}/{r.RailLen:F3} held={value} joints={value2} armed={rail.Armed}");
				}
				catch
				{
				}
			}
		}
		if (rail.Armed && num >= num2)
		{
			AmmoPlug plug = rail.Plug;
			AmmoSocket socket = r.Socket;
			ReleaseRail(r, "top", 0.4f);
			r.PlugCaptured = true;
			SetTrigger(r, on: true, EngagedRadius(r));
			try
			{
				((AlignPlug)plug).ForceInSocket((Socket)(UnityEngine.Object)socket);
			}
			catch
			{
			}
			Buzz(plug, BuzzKind.Insert);
		}
		else if (num < -0.040000003f)
		{
			AmmoPlug plug2 = rail.Plug;
			ReleaseRail(r, "pulled off");
			r.NoRelatchUntil = Time.unscaledTime + 0.6f;
			Buzz(plug2, BuzzKind.Catch);
		}
	}

	private static void ReleaseRail(Rec r, string why, float deferRestore = 0f)
	{
		Rail rail = r.Rail;
		r.Rail = null;
		if (rail == null)
		{
			return;
		}
		try
		{
			if ((UnityEngine.Object)(UnityEngine.Object)rail.Joint != (UnityEngine.Object)null)
			{
				UnityEngine.Object.Destroy((UnityEngine.Object)(UnityEngine.Object)rail.Joint);
			}
		}
		catch
		{
		}
		if (deferRestore > 0f)
		{
			_dyingRails.Add(new KeyValuePair<Rail, float>(rail, Time.unscaledTime + deferRestore));
		}
		else
		{
			SetPairIgnores(rail, ignore: false);
			RestoreHandBodies(rail);
		}
		if (!TraceEnabled)
		{
			return;
		}
		try
		{
			_logger?.Msg($"[trace] RAIL-END gun='{GunName((Component)(UnityEngine.Object)r.Socket)}' ({why})");
		}
		catch
		{
		}
	}

	private static void TryRelatchEjected(Rec r)
	{
		//IL_0087: Unknown result type (might be due to invalid IL or missing references)
		//IL_008c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0092: Unknown result type (might be due to invalid IL or missing references)
		//IL_0097: Unknown result type (might be due to invalid IL or missing references)
		//IL_009d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d8: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			if (!r.HasSeat || r.Rail != null || (UnityEngine.Object)(UnityEngine.Object)r.LastMag == (UnityEngine.Object)null || (UnityEngine.Object)(UnityEngine.Object)r.GunRb == (UnityEngine.Object)null)
			{
				return;
			}
			Magazine lastMag = r.LastMag;
			if (!((Component)lastMag).gameObject.activeInHierarchy || lastMag.isMagazineInserted)
			{
				return;
			}
			AmmoPlug magazinePlug = lastMag.magazinePlug;
			if (!((UnityEngine.Object)(UnityEngine.Object)magazinePlug == (UnityEngine.Object)null) && !RailedAnywhere(magazinePlug))
			{
				Transform transform = ((Component)r.GunRb).transform;
				float num = Vector3.Dot(transform.InverseTransformPoint(((Component)magazinePlug).transform.position) - r.SocketPosL, r.AxisL);
				if (!(num < -0.045f) && !(num > r.RailLen + 0.03f) && !(Quaternion.Angle(transform.rotation * r.SeatMagRotL, ((Component)lastMag).transform.rotation) > 35f))
				{
					Latch(r, magazinePlug);
				}
			}
		}
		catch
		{
		}
	}

	private static void RecordSeat(AmmoSocket socket, AmmoPlug plug)
	{
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_008e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0093: Unknown result type (might be due to invalid IL or missing references)
		//IL_009f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ad: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00dc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00de: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ed: Unknown result type (might be due to invalid IL or missing references)
		//IL_0102: Unknown result type (might be due to invalid IL or missing references)
		//IL_0107: Unknown result type (might be due to invalid IL or missing references)
		//IL_010c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0113: Unknown result type (might be due to invalid IL or missing references)
		//IL_0118: Unknown result type (might be due to invalid IL or missing references)
		//IL_0123: Unknown result type (might be due to invalid IL or missing references)
		//IL_0128: Unknown result type (might be due to invalid IL or missing references)
		//IL_012d: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d3: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d8: Unknown result type (might be due to invalid IL or missing references)
		//IL_01df: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e4: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f7: Unknown result type (might be due to invalid IL or missing references)
		//IL_01fc: Unknown result type (might be due to invalid IL or missing references)
		//IL_0203: Unknown result type (might be due to invalid IL or missing references)
		//IL_0208: Unknown result type (might be due to invalid IL or missing references)
		//IL_017e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0184: Unknown result type (might be due to invalid IL or missing references)
		//IL_0189: Unknown result type (might be due to invalid IL or missing references)
		//IL_018e: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a6: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b0: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			if (!_known.TryGetValue(((UnityEngine.Object)socket).GetInstanceID(), out var value))
			{
				try
				{
					Scene scene = ((Component)socket).gameObject.scene;
					if (!scene.IsValid())
					{
						return;
					}
				}
				catch
				{
					return;
				}
				Register(socket);
				if (!_known.TryGetValue(((UnityEngine.Object)socket).GetInstanceID(), out value))
				{
					return;
				}
			}
			Magazine magazine = plug.magazine;
			if ((UnityEngine.Object)(UnityEngine.Object)magazine == (UnityEngine.Object)null)
			{
				return;
			}
			Rigidbody componentInParent = ((Component)socket).GetComponentInParent<Rigidbody>();
			if ((UnityEngine.Object)(UnityEngine.Object)componentInParent == (UnityEngine.Object)null)
			{
				return;
			}
			Transform transform = ((Component)componentInParent).transform;
			Vector3 val = transform.InverseTransformPoint(((Component)plug).transform.position);
			Vector3 val2 = transform.InverseTransformPoint(((Component)socket).transform.position);
			Vector3 val3 = val - val2;
			float magnitude = val3.magnitude;
			if (magnitude < 0.03f || magnitude > 0.4f)
			{
				return;
			}
			value.GunRb = componentInParent;
			value.SocketPosL = val2;
			value.AxisL = val3 / magnitude;
			value.RailLen = magnitude;
			value.SeatMagPosL = transform.InverseTransformPoint(((Component)magazine).transform.position);
			value.SeatMagRotL = Quaternion.Inverse(transform.rotation) * ((Component)magazine).transform.rotation;
			value.LastMag = magazine;
			value.HasSeat = true;
			string text = GunModelKey((Component)(UnityEngine.Object)socket);
			if (text == null)
			{
				return;
			}
			bool flag = true;
			int num;
			if (_seatLib.TryGetValue(text, out var value2))
			{
				if (!(Mathf.Abs(value2.RailLen - value.RailLen) > 0.005f))
				{
					Vector3 val4 = value2.SocketPosL - value.SocketPosL;
					if (!(val4.sqrMagnitude > 2.5E-05f))
					{
						val4 = value2.SeatMagPosL - value.SeatMagPosL;
						num = ((val4.sqrMagnitude > 2.5E-05f) ? 1 : 0);
						goto IL_01c3;
					}
				}
				num = 1;
				goto IL_01c3;
			}
			goto IL_01c5;
			IL_01c3:
			flag = (byte)num != 0;
			goto IL_01c5;
			IL_01c5:
			_seatLib[text] = new SeatData
			{
				SocketPosL = value.SocketPosL,
				AxisL = value.AxisL,
				RailLen = value.RailLen,
				SeatMagPosL = value.SeatMagPosL,
				SeatMagRotL = value.SeatMagRotL
			};
			if (flag)
			{
				
			}
		}
		catch
		{
		}
	}

	private static void InstallTracer()
	{
		bool num = TryPatch(typeof(AmmoSocket), "OnPlugEnter", "TrPlugEnter");
		bool flag = TryPatch(typeof(AmmoSocket), "OnPlugExit", "TrPlugExit");
		_patchesOk = num && flag;
		if (!_patchesOk)
		{
			_logger?.Warning("capture hooks failed — Mag Slide trigger crush disabled (vanilla capture).");
		}
		TryPatch(typeof(AmmoSocket), "OnPlugLocked", "TrPlugLocked");
		TryPatch(typeof(AlignPlug), "InsertPlug", "TrInsertPlug");
		TryPatch(typeof(AmmoPlug), "OnPlugInsertComplete", "TrSeated");
		TryPatch(typeof(AmmoSocket), "EjectMagazine", "TrEject");
	}

	private static bool TryPatch(Type type, string method, string postfixName)
	{
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Expected O, but got Unknown
		try
		{
			MethodInfo methodInfo = AccessTools.Method(type, method, (Type[])null, (Type[])null);
			HarmonyMethod val = new HarmonyMethod(typeof(MagSlideIntegration), postfixName, (Type[])null);
			_harmony?.Patch((MethodBase)methodInfo, (HarmonyMethod)null, val, (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
			return true;
		}
		catch (Exception ex)
		{
			_logger?.Warning($"hooks: couldn't patch {type.Name}.{method}: {ex.Message}");
			return false;
		}
	}

	private static string GunName(Component c)
	{
		try
		{
			return ((UnityEngine.Object)c.transform.root).name;
		}
		catch
		{
			return "?";
		}
	}

	private static float Dist(Component a, Component b)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			return Vector3.Distance(a.transform.position, b.transform.position);
		}
		catch
		{
			return -1f;
		}
	}

	public static void TrPlugEnter(AmmoSocket __instance, Plug plug)
	{
		float value = -1f;
		try
		{
			if (_known.TryGetValue(((UnityEngine.Object)__instance).GetInstanceID(), out var value2) && (UnityEngine.Object)(UnityEngine.Object)value2.Trigger != (UnityEngine.Object)null && value2.Rail == null)
			{
				try
				{
					value = value2.Trigger.radius;
				}
				catch
				{
				}
				value2.PlugCaptured = true;
				if (true)
				{
					try
					{
						float num = EngagedRadius(value2);
						value2.Trigger.radius = num;
						value2.AppliedRadius = num;
					}
					catch
					{
					}
				}
			}
		}
		catch
		{
		}
		if (!TraceEnabled)
		{
			return;
		}
		try
		{
			_logger?.Msg($"[trace] CAPTURE  gun='{GunName((Component)(UnityEngine.Object)__instance)}' d={Dist((Component)(UnityEngine.Object)__instance, (Component)(UnityEngine.Object)plug):F3} infl={((Socket)__instance).influenceRadius:F3} end={((Socket)__instance).endDistance:F3} trigR={value:F3}");
		}
		catch
		{
		}
	}

	public static void TrPlugExit(AmmoSocket __instance, Plug plug)
	{
		try
		{
			if (_known.TryGetValue(((UnityEngine.Object)__instance).GetInstanceID(), out var value))
			{
				value.PlugCaptured = false;
			}
		}
		catch
		{
		}
		if (!TraceEnabled)
		{
			return;
		}
		try
		{
			_logger?.Msg($"[trace] EXIT     gun='{GunName((Component)(UnityEngine.Object)__instance)}' d={Dist((Component)(UnityEngine.Object)__instance, (Component)(UnityEngine.Object)plug):F3}");
		}
		catch
		{
		}
	}

	public static void TrPlugLocked(AmmoSocket __instance, Plug plug)
	{
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		if (!TraceEnabled)
		{
			return;
		}
		try
		{
			string value = "none";
			try
			{
				if ((UnityEngine.Object)(UnityEngine.Object)((Socket)__instance).endTransform != (UnityEngine.Object)null)
				{
					value = Vector3.Distance(((Socket)__instance).endTransform.position, ((Component)plug).transform.position).ToString("F3");
				}
			}
			catch
			{
			}
			_logger?.Msg($"[trace] LOCKED   gun='{GunName((Component)(UnityEngine.Object)__instance)}' d={Dist((Component)(UnityEngine.Object)__instance, (Component)(UnityEngine.Object)plug):F3} dToEndT={value} end={((Socket)__instance).endDistance:F3}");
		}
		catch
		{
		}
	}

	public static void TrInsertPlug(AlignPlug __instance, Socket socket)
	{
		if (!TraceEnabled)
		{
			return;
		}
		try
		{
			_logger?.Msg($"[trace] ENGAGE   gun='{GunName((Component)(UnityEngine.Object)socket)}' d={Dist((Component)(UnityEngine.Object)socket, (Component)(UnityEngine.Object)__instance):F3}");
		}
		catch
		{
		}
	}

	public static void TrSeated(AmmoPlug __instance)
	{
		Socket val = null;
		try
		{
			val = ((AlignPlug)__instance)._lastSocket;
		}
		catch
		{
		}
		try
		{
			AmmoSocket val2 = (((UnityEngine.Object)(UnityEngine.Object)val == (UnityEngine.Object)null) ? null : ((Il2CppObjectBase)val).TryCast<AmmoSocket>());
			if ((UnityEngine.Object)(UnityEngine.Object)val2 != (UnityEngine.Object)null)
			{
				RecordSeat(val2, __instance);
			}
		}
		catch
		{
		}
		if (!TraceEnabled)
		{
			return;
		}
		try
		{
			if ((UnityEngine.Object)(UnityEngine.Object)val != (UnityEngine.Object)null)
			{
				_logger?.Msg($"[trace] SEATED   gun='{GunName((Component)(UnityEngine.Object)val)}' d={Dist((Component)(UnityEngine.Object)val, (Component)(UnityEngine.Object)__instance):F3} end={val.endDistance:F3}");
			}
			else
			{
				_logger?.Msg("[trace] SEATED   (no socket ref)");
			}
		}
		catch
		{
		}
	}

	public static void TrEject(AmmoSocket __instance)
	{
		if (!TraceEnabled)
		{
			return;
		}
		try
		{
			_logger?.Msg("[trace] EJECT    gun='" + GunName((Component)(UnityEngine.Object)__instance) + "'");
		}
		catch
		{
		}
	}

	

}

}

