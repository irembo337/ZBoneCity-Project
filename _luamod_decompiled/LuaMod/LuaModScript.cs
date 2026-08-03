using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Il2Cpp;
using Il2CppAra;
using Il2CppDynamite3D.RealIvy;
using Il2CppECE;
using Il2CppLuxURPEssentials;
using Il2CppLux_SRP_GrassDisplacement;
using Il2CppMK.Glow;
using Il2CppRealisticEyeMovements;
using Il2CppSLZ.Bonelab;
using Il2CppSLZ.Bonelab.Obsolete;
using Il2CppSLZ.Bonelab.SaveData;
using Il2CppSLZ.Bonelab.VoidLogic;
using Il2CppSLZ.Combat;
using Il2CppSLZ.Data;
using Il2CppSLZ.Marrow;
using Il2CppSLZ.Marrow.AI;
using Il2CppSLZ.Marrow.Circuits;
using Il2CppSLZ.Marrow.Console;
using Il2CppSLZ.Marrow.Data;
using Il2CppSLZ.Marrow.Input;
using Il2CppSLZ.Marrow.PuppetMasta;
using Il2CppSLZ.Marrow.SaveData;
using Il2CppSLZ.Marrow.SceneStreaming;
using Il2CppSLZ.Marrow.VoidLogic;
using Il2CppSLZ.Marrow.Warehouse;
using Il2CppSLZ.Marrow.Zones;
using Il2CppSLZ.SFX;
using Il2CppSLZ.VFX;
using Il2CppSLZ.VRMK;
using Il2CppSLZ.Vehicle;
using Il2CppSplineMesh;
using Il2CppTMPro;
using Il2CppTMPro.SpriteAssetUtilities;
using LuaMod.LuaAPI;
using MelonLoader;
using MoonSharp.Interpreter;
using MoonSharp.Interpreter.Loaders;
using Unity.Rendering.HybridV2;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;
using UnityEngine.Experimental.AI;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Playables;
using UnityEngine.Profiling.Memory.Experimental;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RendererUtils;
using UnityEngine.SceneManagement;
using UnityEngine.Search;
using UnityEngine.TerrainTools;
using UnityEngine.TerrainUtils;
using UnityEngine.U2D;
using UnityEngine.Video;
using UnityEngineInternal;
using UnityEngineInternal.Video;

namespace LuaMod;

public class LuaModScript
{
	public delegate bool del_postreload();

	public enum StrikeReason
	{
		ExecutionTime,
		MemoryUse
	}

	public class ScriptStrikeRecord
	{
		public int Strikes;

		public StrikeReason Reason;
	}

	private Script _LuaScript;

	private string LuaFileName;

	private TextAsset LuaAsset;

	public del_postreload PostReloadScript;

	private readonly Dictionary<string, DynValue> _loadedModules = new Dictionary<string, DynValue>();

	protected const int MaxScriptExecutionTime = 500;

	protected const float ScriptMemoryBudget = 15f;

	private const int MaxStrikes = 3;

	private static readonly Dictionary<string, ScriptStrikeRecord> StrikeRecords = new Dictionary<string, ScriptStrikeRecord>();

	private string GetScriptIdentifier()
	{
		object result;
		if (string.IsNullOrEmpty(LuaFileName))
		{
			TextAsset luaAsset = LuaAsset;
			result = ((luaAsset != null) ? ((Object)luaAsset).name : null) ?? "UnknownScript";
		}
		else
		{
			result = LuaFileName;
		}
		return (string)result;
	}

	private void AddStrike(StrikeReason reason)
	{
		string scriptIdentifier = GetScriptIdentifier();
		if (!StrikeRecords.TryGetValue(scriptIdentifier, out var value))
		{
			value = new ScriptStrikeRecord();
			StrikeRecords[scriptIdentifier] = value;
		}
		value.Strikes++;
		value.Reason = reason;
		if (value.Strikes >= 3)
		{
			MelonLogger.Error($"Script '{scriptIdentifier}' blocked due to {value.Strikes} strikes ({reason}).");
			DestroyScript();
		}
		MelonLogger.Warning($"Script '{scriptIdentifier}' received a strike ({value.Strikes}): {reason}");
	}

	private static bool CheckBan(string path)
	{
		if (StrikeRecords.TryGetValue(path, out var value) && value.Strikes >= 3)
		{
			return true;
		}
		return false;
	}

	public void SetGlobal(string name, object val)
	{
		if (_LuaScript != null)
		{
			_LuaScript.Globals.Set(name, DynValue.FromObject(_LuaScript, val));
		}
	}

	public DynValue GetGlobal(string name)
	{
		if (_LuaScript != null)
		{
			return _LuaScript.Globals.Get(name);
		}
		return null;
	}

	public bool ScriptIsValid()
	{
		return _LuaScript != null;
	}

	public bool LoadScript(string filename, bool reloading, LuaBehaviour host)
	{
		//IL_0173: Expected O, but got Unknown
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b5: Expected O, but got Unknown
		//IL_00c0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ca: Expected O, but got Unknown
		//IL_0089: Unknown result type (might be due to invalid IL or missing references)
		filename = API_Utils.RemoveDoubleSlashes(filename);
		if (!Security.IsSafePath(filename))
		{
			throw new ScriptRuntimeException("attempted to access an unsafe path: " + filename);
		}
		if (!File.Exists(filename))
		{
			return false;
		}
		if (CheckBan(filename))
		{
			throw new ScriptRuntimeException("Script '" + filename + "' not allowed -- too many strikes");
		}
		MelonLogger.Msg("LuaBehaviour loading script: " + filename);
		_LuaScript = new Script((CoreModules)1387);
		_LuaScript.Options.ScriptLoader = (IScriptLoader)new FileSystemScriptLoader();
		_LuaScript.Options.DebugPrint = delegate(string s)
		{
			MelonLogger.Msg("[Lua: " + filename + "] " + s);
		};
		_LuaScript.Options.CheckThreadAccess = true;
		LuaFileName = filename;
		_LuaScript.Globals.Set("BL_Host", UserData.Create((object)((Component)host).gameObject));
		_LuaScript.Globals.Set("BL_This", UserData.Create((object)host));
		try
		{
			DynValue luaFunc = _LuaScript.LoadFile(filename, (Table)null, (string)null);
			LoadFunctionPointers();
			CallScriptFunction(luaFunc);
		}
		catch (ScriptRuntimeException val)
		{
			ScriptRuntimeException val2 = val;
			MelonLogger.Error("Lua error while loading " + filename + ": " + ((InterpreterException)val2).DecoratedMessage);
			return false;
		}
		if (!reloading)
		{
			ScriptManager.RegisterScript(this);
		}
		return true;
	}

	public bool LoadScript(TextAsset scriptAsset, bool reloading, LuaBehaviour host)
	{
		//IL_0145: Expected O, but got Unknown
		//IL_0072: Unknown result type (might be due to invalid IL or missing references)
		//IL_007c: Expected O, but got Unknown
		//IL_008c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0096: Expected O, but got Unknown
		if ((Object)(object)scriptAsset == (Object)null)
		{
			return false;
		}
		string virtualPath = ((Object)scriptAsset).name;
		if (CheckBan(virtualPath))
		{
			throw new UnauthorizedAccessException("Script '" + virtualPath + "' not allowed -- too many strikes");
		}
		MelonLogger.Msg("LuaBehaviour loading asset script: " + virtualPath + ".txt");
		_LuaScript = new Script((CoreModules)1387);
		_LuaScript.Options.ScriptLoader = (IScriptLoader)new UnityAssetsScriptLoader("LuaScripts");
		_LuaScript.Options.DebugPrint = delegate(string s)
		{
			MelonLogger.Msg("[Lua: " + virtualPath + "] " + s);
		};
		_LuaScript.Options.CheckThreadAccess = true;
		LuaFileName = string.Empty;
		LuaAsset = scriptAsset;
		_LuaScript.Globals.Set("BL_Host", UserData.Create((object)((Component)host).gameObject));
		_LuaScript.Globals.Set("BL_This", UserData.Create((object)host));
		try
		{
			DynValue luaFunc = _LuaScript.LoadString(scriptAsset.text, (Table)null, (string)null);
			LoadFunctionPointers();
			CallScriptFunction(luaFunc);
		}
		catch (ScriptRuntimeException val)
		{
			ScriptRuntimeException val2 = val;
			MelonLogger.Error("Lua error in asset script " + virtualPath + ": " + ((InterpreterException)val2).DecoratedMessage);
			return false;
		}
		if (!reloading)
		{
			ScriptManager.RegisterScript(this);
		}
		return true;
	}

	public DynValue CallScriptFunction(DynValue luaFunc, params object[] Args)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Invalid comparison between Unknown and I4
		//IL_0187: Expected O, but got Unknown
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Invalid comparison between Unknown and I4
		//IL_0170: Unknown result type (might be due to invalid IL or missing references)
		//IL_0177: Invalid comparison between Unknown and I4
		//IL_00fc: Unknown result type (might be due to invalid IL or missing references)
		//IL_0161: Unknown result type (might be due to invalid IL or missing references)
		if ((int)luaFunc.Type != 5 && (int)luaFunc.Type != 10)
		{
			throw new ArgumentException("DynValue must be a Lua function or callback");
		}
		if (CheckBan(GetScriptIdentifier()))
		{
			DestroyScript();
			throw new UnauthorizedAccessException("Script '" + GetScriptIdentifier() + "' not allowed -- too many strikes");
		}
		Coroutine coroutine = _LuaScript.CreateCoroutine(luaFunc).Coroutine;
		coroutine.AutoYieldCounter = 1000L;
		DynValue[] array = Array.ConvertAll(Args, (object arg) => DynValue.FromObject(_LuaScript, arg));
		Stopwatch stopwatch = Stopwatch.StartNew();
		DynValue val;
		try
		{
			val = coroutine.Resume(array);
			while ((int)val.Type == 12)
			{
				if (stopwatch.ElapsedMilliseconds > 500)
				{
					AddStrike(StrikeReason.ExecutionTime);
					throw new ScriptRuntimeException($"Lua function execution exceeded {500}ms and was aborted");
				}
				if (LuaMemoryProfiler.EstimateMemoryMB(_LuaScript, _loadedModules) > 15f)
				{
					AddStrike(StrikeReason.MemoryUse);
					throw new ScriptRuntimeException($"Lua script exceeded memory budget of {15f}MB and was aborted");
				}
				val = coroutine.Resume();
			}
		}
		catch (ScriptRuntimeException val2)
		{
			ScriptRuntimeException val3 = val2;
			MelonLogger.Error("Lua Error: " + ((InterpreterException)val3).DecoratedMessage);
			throw;
		}
		finally
		{
			stopwatch.Stop();
		}
		return val;
	}

	public static bool IsScriptPathSafe(string path)
	{
		return true;
	}

	private void LoadBehaviourFunctionReferences()
	{
	}

	public void DestroyScript()
	{
		_LuaScript = null;
		ScriptManager.DeregisterScript(this);
	}

	private DynValue LoadModule(string module)
	{
		return LoadModuleInternal(module);
	}

	private DynValue LoadModule(TextAsset moduleAsset)
	{
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)moduleAsset == (Object)null)
		{
			throw new ScriptRuntimeException("Provided TextAsset is null");
		}
		string name = ((Object)moduleAsset).name;
		return LoadModuleInternal(name, moduleAsset.text);
	}

	private DynValue LoadModuleInternal(string moduleName, string codeOverride = null)
	{
		return LuaSafeCall.Run((Func<DynValue>)delegate
		{
			//IL_0048: Unknown result type (might be due to invalid IL or missing references)
			//IL_00bb: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c1: Invalid comparison between Unknown and I4
			//IL_0076: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c4: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ca: Invalid comparison between Unknown and I4
			//IL_00cd: Unknown result type (might be due to invalid IL or missing references)
			//IL_00d3: Invalid comparison between Unknown and I4
			string text;
			if (codeOverride != null)
			{
				text = codeOverride;
			}
			else
			{
				string relativeScriptPath = Security.GetRelativeScriptPath(moduleName);
				if (!Security.IsSafePath(relativeScriptPath))
				{
					throw new ScriptRuntimeException("attempted to access an unsafe path: " + moduleName);
				}
				if (!File.Exists(relativeScriptPath))
				{
					throw new ScriptRuntimeException("Module '" + moduleName + "' not found at path: " + relativeScriptPath);
				}
				text = File.ReadAllText(relativeScriptPath);
			}
			DynValue luaFunc = _LuaScript.LoadString(text, (Table)null, "module:" + moduleName);
			DynValue val = CallScriptFunction(luaFunc);
			if ((int)val.Type == 6 || (int)val.Type == 8 || (int)val.Type == 5)
			{
				_LuaScript.Globals.Set(moduleName, val);
				_loadedModules[moduleName] = val;
				return val;
			}
			_LuaScript.Globals.Set(moduleName, DynValue.True);
			_loadedModules[moduleName] = DynValue.True;
			return DynValue.True;
		}, "loadmodule('" + moduleName + "')");
	}

	private DynValue Require(string module)
	{
		return RequireInternal(module);
	}

	private DynValue Require(TextAsset moduleAsset)
	{
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)moduleAsset == (Object)null)
		{
			throw new ScriptRuntimeException("Provided TextAsset is null");
		}
		string name = ((Object)moduleAsset).name;
		return RequireInternal(name, moduleAsset.text);
	}

	private DynValue RequireInternal(string moduleName, string codeOverride = null)
	{
		return LuaSafeCall.Run((Func<DynValue>)delegate
		{
			//IL_0071: Unknown result type (might be due to invalid IL or missing references)
			//IL_00e4: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ea: Invalid comparison between Unknown and I4
			//IL_009f: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ed: Unknown result type (might be due to invalid IL or missing references)
			//IL_00f3: Invalid comparison between Unknown and I4
			//IL_00f6: Unknown result type (might be due to invalid IL or missing references)
			//IL_00fc: Invalid comparison between Unknown and I4
			if (_loadedModules.TryGetValue(moduleName, out var value))
			{
				return value;
			}
			string text;
			if (codeOverride != null)
			{
				text = codeOverride;
			}
			else
			{
				string relativeScriptPath = Security.GetRelativeScriptPath(moduleName);
				if (!Security.IsSafePath(relativeScriptPath))
				{
					throw new ScriptRuntimeException("attempted to access an unsafe path: " + moduleName);
				}
				if (!File.Exists(relativeScriptPath))
				{
					throw new ScriptRuntimeException("Module '" + moduleName + "' not found at path: " + relativeScriptPath);
				}
				text = File.ReadAllText(relativeScriptPath);
			}
			DynValue luaFunc = _LuaScript.LoadString(text, (Table)null, "require:" + moduleName);
			DynValue val = CallScriptFunction(luaFunc);
			if ((int)val.Type == 6 || (int)val.Type == 8 || (int)val.Type == 5)
			{
				_LuaScript.Globals.Set(moduleName, val);
				_loadedModules[moduleName] = val;
				return val;
			}
			_LuaScript.Globals.Set(moduleName, DynValue.True);
			_loadedModules[moduleName] = DynValue.True;
			return DynValue.True;
		}, "require('" + moduleName + "')");
	}

	private DynValue Lua_Require(object module)
	{
		return LuaSafeCall.Run((Func<DynValue>)delegate
		{
			//IL_004e: Unknown result type (might be due to invalid IL or missing references)
			if (module is string module2)
			{
				return Require(module2);
			}
			object obj = module;
			TextAsset val = (TextAsset)((obj is TextAsset) ? obj : null);
			if (val == null)
			{
				throw new ScriptRuntimeException("require() must be called with a string or TextAsset");
			}
			return Require(val);
		}, "Lua_Require");
	}

	private DynValue Lua_LoadModule(object module)
	{
		return LuaSafeCall.Run((Func<DynValue>)delegate
		{
			//IL_004e: Unknown result type (might be due to invalid IL or missing references)
			if (module is string module2)
			{
				return LoadModule(module2);
			}
			object obj = module;
			TextAsset val = (TextAsset)((obj is TextAsset) ? obj : null);
			if (val == null)
			{
				throw new ScriptRuntimeException("loadmodule() must be called with a string or TextAsset");
			}
			return LoadModule(val);
		}, "Lua_LoadModule");
	}

	private void LoadFunctionPointers()
	{
		_LuaScript.Globals[(object)"API_GameObject"] = API_GameObject.Instance;
		_LuaScript.Globals[(object)"API_Input"] = API_Input.Instance;
		_LuaScript.Globals[(object)"API_Player"] = API_Player.Instance;
		_LuaScript.Globals[(object)"API_Vector"] = API_Vector.Instance;
		_LuaScript.Globals[(object)"API_Events"] = API_Events.Instance;
		_LuaScript.Globals[(object)"API_SLZ_Combat"] = API_SLZ_Combat.Instance;
		_LuaScript.Globals[(object)"API_SLZ_NPC"] = API_SLZ_NPC.Instance;
		_LuaScript.Globals[(object)"API_SLZ_VoidLogic"] = API_SLZ_VoidLogic.Instance;
		_LuaScript.Globals[(object)"API_Physics"] = API_Physics.Instance;
		_LuaScript.Globals[(object)"API_Utils"] = API_Utils.Instance;
		_LuaScript.Globals[(object)"API_BoneMenu"] = API_BoneMenu.Instance;
		_LuaScript.Globals[(object)"API_Audio"] = API_Audio.Instance;
		_LuaScript.Globals[(object)"API_Particles"] = API_Particles.Instance;
		_LuaScript.Globals[(object)"API_FileAccess"] = API_FileAccess.Instance;
		_LuaScript.Globals[(object)"API_Random"] = API_Random.Instance;
		_LuaScript.Globals[(object)"API_Renderer"] = API_Renderer.Instance;
		_LuaScript.Globals[(object)"GameObject"] = UserData.CreateStatic<GameObject>();
		_LuaScript.Globals[(object)"Quaternion"] = UserData.CreateStatic<Quaternion>();
		_LuaScript.Globals[(object)"Vector3"] = UserData.CreateStatic<Vector3>();
		_LuaScript.Globals[(object)"Time"] = UserData.CreateStatic<Time>();
		_LuaScript.Globals[(object)"Color"] = UserData.CreateStatic<Color>();
		_LuaScript.Globals[(object)"Physics"] = UserData.CreateStatic<Physics>();
		_LuaScript.Globals[(object)"Transform"] = UserData.CreateStatic<Transform>();
		_LuaScript.Globals[(object)"Time"] = UserData.CreateStatic<Time>();
		_LuaScript.Globals[(object)"Camera"] = UserData.CreateStatic<Camera>();
		_LuaScript.Globals[(object)"ConfigurableJointMotion"] = UserData.CreateStatic<ConfigurableJointMotion>();
		_LuaScript.Globals[(object)"ForceMode"] = UserData.CreateStatic<ForceMode>();
		_LuaScript.Globals[(object)"Mathf"] = UserData.CreateStatic<Mathf>();
		_LuaScript.Globals[(object)"VisibleLightFlags"] = UserData.CreateStatic<VisibleLightFlags>();
		_LuaScript.Globals[(object)"IsValid"] = new Func<GameObject, bool>(API_GameObject.BL_IsValid);
		_LuaScript.Globals[(object)"require"] = new Func<object, DynValue>(Lua_Require);
		_LuaScript.Globals[(object)"loadmodule"] = new Func<object, DynValue>(Lua_LoadModule);
		_LuaScript.Globals[(object)"HammerStates"] = UserData.CreateStatic<HammerStates>();
		_LuaScript.Globals[(object)"LightmapType"] = UserData.CreateStatic<LightmapType>();
		_LuaScript.Globals[(object)"TypeInferenceRules"] = UserData.CreateStatic<TypeInferenceRules>();
		_LuaScript.Globals[(object)"PrimitiveType"] = UserData.CreateStatic<PrimitiveType>();
		_LuaScript.Globals[(object)"Space"] = UserData.CreateStatic<Space>();
		_LuaScript.Globals[(object)"RuntimePlatform"] = UserData.CreateStatic<RuntimePlatform>();
		_LuaScript.Globals[(object)"SystemLanguage"] = UserData.CreateStatic<SystemLanguage>();
		_LuaScript.Globals[(object)"LogType"] = UserData.CreateStatic<LogType>();
		_LuaScript.Globals[(object)"WrapMode"] = UserData.CreateStatic<WrapMode>();
		_LuaScript.Globals[(object)"StackTraceLogType"] = UserData.CreateStatic<StackTraceLogType>();
		_LuaScript.Globals[(object)"FullScreenMode"] = UserData.CreateStatic<FullScreenMode>();
		_LuaScript.Globals[(object)"ComputeBufferMode"] = UserData.CreateStatic<ComputeBufferMode>();
		_LuaScript.Globals[(object)"LightmapsModeLegacy"] = UserData.CreateStatic<LightmapsModeLegacy>();
		_LuaScript.Globals[(object)"LightShadowCasterMode"] = UserData.CreateStatic<LightShadowCasterMode>();
		_LuaScript.Globals[(object)"RenderingPath"] = UserData.CreateStatic<RenderingPath>();
		_LuaScript.Globals[(object)"TransparencySortMode"] = UserData.CreateStatic<TransparencySortMode>();
		_LuaScript.Globals[(object)"StereoTargetEyeMask"] = UserData.CreateStatic<StereoTargetEyeMask>();
		_LuaScript.Globals[(object)"CameraType"] = UserData.CreateStatic<CameraType>();
		_LuaScript.Globals[(object)"ComputeBufferType"] = UserData.CreateStatic<ComputeBufferType>();
		_LuaScript.Globals[(object)"LightType"] = UserData.CreateStatic<LightType>();
		_LuaScript.Globals[(object)"LightShape"] = UserData.CreateStatic<LightShape>();
		_LuaScript.Globals[(object)"LightRenderMode"] = UserData.CreateStatic<LightRenderMode>();
		_LuaScript.Globals[(object)"LightShadows"] = UserData.CreateStatic<LightShadows>();
		_LuaScript.Globals[(object)"FogMode"] = UserData.CreateStatic<FogMode>();
		_LuaScript.Globals[(object)"LightmapBakeType"] = UserData.CreateStatic<LightmapBakeType>();
		_LuaScript.Globals[(object)"MixedLightingMode"] = UserData.CreateStatic<MixedLightingMode>();
		_LuaScript.Globals[(object)"ShadowmaskMode"] = UserData.CreateStatic<ShadowmaskMode>();
		_LuaScript.Globals[(object)"ShadowObjectsFilter"] = UserData.CreateStatic<ShadowObjectsFilter>();
		_LuaScript.Globals[(object)"CameraClearFlags"] = UserData.CreateStatic<CameraClearFlags>();
		_LuaScript.Globals[(object)"DepthTextureMode"] = UserData.CreateStatic<DepthTextureMode>();
		_LuaScript.Globals[(object)"AnisotropicFiltering"] = UserData.CreateStatic<AnisotropicFiltering>();
		_LuaScript.Globals[(object)"MeshTopology"] = UserData.CreateStatic<MeshTopology>();
		_LuaScript.Globals[(object)"SkinQuality"] = UserData.CreateStatic<SkinQuality>();
		_LuaScript.Globals[(object)"ColorSpace"] = UserData.CreateStatic<ColorSpace>();
		_LuaScript.Globals[(object)"FilterMode"] = UserData.CreateStatic<FilterMode>();
		_LuaScript.Globals[(object)"TextureWrapMode"] = UserData.CreateStatic<TextureWrapMode>();
		_LuaScript.Globals[(object)"TextureFormat"] = UserData.CreateStatic<TextureFormat>();
		_LuaScript.Globals[(object)"CubemapFace"] = UserData.CreateStatic<CubemapFace>();
		_LuaScript.Globals[(object)"RenderTextureFormat"] = UserData.CreateStatic<RenderTextureFormat>();
		_LuaScript.Globals[(object)"VRTextureUsage"] = UserData.CreateStatic<VRTextureUsage>();
		_LuaScript.Globals[(object)"RenderTextureReadWrite"] = UserData.CreateStatic<RenderTextureReadWrite>();
		_LuaScript.Globals[(object)"RenderTextureMemoryless"] = UserData.CreateStatic<RenderTextureMemoryless>();
		_LuaScript.Globals[(object)"LightmapsMode"] = UserData.CreateStatic<LightmapsMode>();
		_LuaScript.Globals[(object)"LineAlignment"] = UserData.CreateStatic<LineAlignment>();
		_LuaScript.Globals[(object)"LODFadeMode"] = UserData.CreateStatic<LODFadeMode>();
		_LuaScript.Globals[(object)"CursorMode"] = UserData.CreateStatic<CursorMode>();
		_LuaScript.Globals[(object)"CursorLockMode"] = UserData.CreateStatic<CursorLockMode>();
		_LuaScript.Globals[(object)"KeyCode"] = UserData.CreateStatic<KeyCode>();
		_LuaScript.Globals[(object)"HideFlags"] = UserData.CreateStatic<HideFlags>();
		_LuaScript.Globals[(object)"DisableBatchingType"] = UserData.CreateStatic<DisableBatchingType>();
		_LuaScript.Globals[(object)"OperatingSystemFamily"] = UserData.CreateStatic<OperatingSystemFamily>();
		_LuaScript.Globals[(object)"DrivenTransformProperties"] = UserData.CreateStatic<DrivenTransformProperties>();
		_LuaScript.Globals[(object)"SpriteDrawMode"] = UserData.CreateStatic<SpriteDrawMode>();
		_LuaScript.Globals[(object)"SpriteTileMode"] = UserData.CreateStatic<SpriteTileMode>();
		_LuaScript.Globals[(object)"SpriteMeshType"] = UserData.CreateStatic<SpriteMeshType>();
		_LuaScript.Globals[(object)"SpritePackingMode"] = UserData.CreateStatic<SpritePackingMode>();
		_LuaScript.Globals[(object)"SpriteSortPoint"] = UserData.CreateStatic<SpriteSortPoint>();
		_LuaScript.Globals[(object)"PersistentListenerMode"] = UserData.CreateStatic<PersistentListenerMode>();
		_LuaScript.Globals[(object)"UnityEventCallState"] = UserData.CreateStatic<UnityEventCallState>();
		_LuaScript.Globals[(object)"LoadSceneMode"] = UserData.CreateStatic<LoadSceneMode>();
		_LuaScript.Globals[(object)"IndexFormat"] = UserData.CreateStatic<IndexFormat>();
		_LuaScript.Globals[(object)"MeshUpdateFlags"] = UserData.CreateStatic<MeshUpdateFlags>();
		_LuaScript.Globals[(object)"VertexAttributeFormat"] = UserData.CreateStatic<VertexAttributeFormat>();
		_LuaScript.Globals[(object)"VertexAttribute"] = UserData.CreateStatic<VertexAttribute>();
		_LuaScript.Globals[(object)"OpaqueSortMode"] = UserData.CreateStatic<OpaqueSortMode>();
		_LuaScript.Globals[(object)"FastMemoryFlags"] = UserData.CreateStatic<FastMemoryFlags>();
		_LuaScript.Globals[(object)"BlendMode"] = UserData.CreateStatic<BlendMode>();
		_LuaScript.Globals[(object)"BlendOp"] = UserData.CreateStatic<BlendOp>();
		_LuaScript.Globals[(object)"CullMode"] = UserData.CreateStatic<CullMode>();
		_LuaScript.Globals[(object)"ColorWriteMask"] = UserData.CreateStatic<ColorWriteMask>();
		_LuaScript.Globals[(object)"StencilOp"] = UserData.CreateStatic<StencilOp>();
		_LuaScript.Globals[(object)"AmbientMode"] = UserData.CreateStatic<AmbientMode>();
		_LuaScript.Globals[(object)"CameraEvent"] = UserData.CreateStatic<CameraEvent>();
		_LuaScript.Globals[(object)"LightEvent"] = UserData.CreateStatic<LightEvent>();
		_LuaScript.Globals[(object)"ShadowMapPass"] = UserData.CreateStatic<ShadowMapPass>();
		_LuaScript.Globals[(object)"BuiltinRenderTextureType"] = UserData.CreateStatic<BuiltinRenderTextureType>();
		_LuaScript.Globals[(object)"ShadowCastingMode"] = UserData.CreateStatic<ShadowCastingMode>();
		_LuaScript.Globals[(object)"FormatSwizzle"] = UserData.CreateStatic<FormatSwizzle>();
		_LuaScript.Globals[(object)"RenderTargetFlags"] = UserData.CreateStatic<RenderTargetFlags>();
		_LuaScript.Globals[(object)"ShadowSamplingMode"] = UserData.CreateStatic<ShadowSamplingMode>();
		_LuaScript.Globals[(object)"LightProbeUsage"] = UserData.CreateStatic<LightProbeUsage>();
		_LuaScript.Globals[(object)"BuiltinShaderDefine"] = UserData.CreateStatic<BuiltinShaderDefine>();
		_LuaScript.Globals[(object)"ComputeQueueType"] = UserData.CreateStatic<ComputeQueueType>();
		_LuaScript.Globals[(object)"SinglePassStereoMode"] = UserData.CreateStatic<SinglePassStereoMode>();
		_LuaScript.Globals[(object)"RTClearFlags"] = UserData.CreateStatic<RTClearFlags>();
		_LuaScript.Globals[(object)"RenderTextureSubElement"] = UserData.CreateStatic<RenderTextureSubElement>();
		_LuaScript.Globals[(object)"CameraLateLatchMatrixType"] = UserData.CreateStatic<CameraLateLatchMatrixType>();
		_LuaScript.Globals[(object)"GraphicsFenceType"] = UserData.CreateStatic<GraphicsFenceType>();
		_LuaScript.Globals[(object)"DrawRendererFlags"] = UserData.CreateStatic<DrawRendererFlags>();
		_LuaScript.Globals[(object)"GizmoSubset"] = UserData.CreateStatic<GizmoSubset>();
		_LuaScript.Globals[(object)"PerObjectData"] = UserData.CreateStatic<PerObjectData>();
		_LuaScript.Globals[(object)"RenderStateMask"] = UserData.CreateStatic<RenderStateMask>();
		_LuaScript.Globals[(object)"SortingCriteria"] = UserData.CreateStatic<SortingCriteria>();
		_LuaScript.Globals[(object)"DistanceMetric"] = UserData.CreateStatic<DistanceMetric>();
		_LuaScript.Globals[(object)"VisibleLightFlags"] = UserData.CreateStatic<VisibleLightFlags>();
		_LuaScript.Globals[(object)"ShaderPropertyType"] = UserData.CreateStatic<ShaderPropertyType>();
		_LuaScript.Globals[(object)"ShaderPropertyFlags"] = UserData.CreateStatic<ShaderPropertyFlags>();
		_LuaScript.Globals[(object)"RendererListStatus"] = UserData.CreateStatic<RendererListStatus>();
		_LuaScript.Globals[(object)"DirectorWrapMode"] = UserData.CreateStatic<DirectorWrapMode>();
		_LuaScript.Globals[(object)"PlayableTraversalMode"] = UserData.CreateStatic<PlayableTraversalMode>();
		_LuaScript.Globals[(object)"DirectorUpdateMode"] = UserData.CreateStatic<DirectorUpdateMode>();
		_LuaScript.Globals[(object)"PlayState"] = UserData.CreateStatic<PlayState>();
		_LuaScript.Globals[(object)"FormatUsage"] = UserData.CreateStatic<FormatUsage>();
		_LuaScript.Globals[(object)"DefaultFormat"] = UserData.CreateStatic<DefaultFormat>();
		_LuaScript.Globals[(object)"GraphicsFormat"] = UserData.CreateStatic<GraphicsFormat>();
		_LuaScript.Globals[(object)"MemorylessMode"] = UserData.CreateStatic<MemorylessMode>();
		_LuaScript.Globals[(object)"GITextureType"] = UserData.CreateStatic<GITextureType>();
		_LuaScript.Globals[(object)"DOTSInstancingPropertyType"] = UserData.CreateStatic<DOTSInstancingPropertyType>();
		_LuaScript.Globals[(object)"WeightedMode"] = UserData.CreateStatic<WeightedMode>();
		_LuaScript.Globals[(object)"ReceiveGI"] = UserData.CreateStatic<ReceiveGI>();
		_LuaScript.Globals[(object)"ShadowQuality"] = UserData.CreateStatic<ShadowQuality>();
		_LuaScript.Globals[(object)"TexGenMode"] = UserData.CreateStatic<TexGenMode>();
		_LuaScript.Globals[(object)"SkinWeights"] = UserData.CreateStatic<SkinWeights>();
		_LuaScript.Globals[(object)"ColorGamut"] = UserData.CreateStatic<ColorGamut>();
		_LuaScript.Globals[(object)"NPOTSupport"] = UserData.CreateStatic<NPOTSupport>();
		_LuaScript.Globals[(object)"HDRDisplaySupportFlags"] = UserData.CreateStatic<HDRDisplaySupportFlags>();
		_LuaScript.Globals[(object)"CustomRenderTextureUpdateMode"] = UserData.CreateStatic<CustomRenderTextureUpdateMode>();
		_LuaScript.Globals[(object)"CustomRenderTextureUpdateZoneSpace"] = UserData.CreateStatic<CustomRenderTextureUpdateZoneSpace>();
		_LuaScript.Globals[(object)"D3DHDRDisplayBitDepth"] = UserData.CreateStatic<D3DHDRDisplayBitDepth>();
		_LuaScript.Globals[(object)"SnapAxis"] = UserData.CreateStatic<SnapAxis>();
		_LuaScript.Globals[(object)"FullScreenMovieControlMode"] = UserData.CreateStatic<FullScreenMovieControlMode>();
		_LuaScript.Globals[(object)"FullScreenMovieScalingMode"] = UserData.CreateStatic<FullScreenMovieScalingMode>();
		_LuaScript.Globals[(object)"AndroidActivityIndicatorStyle"] = UserData.CreateStatic<AndroidActivityIndicatorStyle>();
		_LuaScript.Globals[(object)"GradientMode"] = UserData.CreateStatic<GradientMode>();
		_LuaScript.Globals[(object)"SpriteAlignment"] = UserData.CreateStatic<SpriteAlignment>();
		_LuaScript.Globals[(object)"Light2DType"] = UserData.CreateStatic<Light2DType>();
		_LuaScript.Globals[(object)"CaptureFlags"] = UserData.CreateStatic<CaptureFlags>();
		_LuaScript.Globals[(object)"SearchViewFlags"] = UserData.CreateStatic<SearchViewFlags>();
		_LuaScript.Globals[(object)"ShaderParamType"] = UserData.CreateStatic<ShaderParamType>();
		_LuaScript.Globals[(object)"ShaderConstantType"] = UserData.CreateStatic<ShaderConstantType>();
		_LuaScript.Globals[(object)"RenderQueue"] = UserData.CreateStatic<RenderQueue>();
		_LuaScript.Globals[(object)"PassType"] = UserData.CreateStatic<PassType>();
		_LuaScript.Globals[(object)"BuiltinShaderType"] = UserData.CreateStatic<BuiltinShaderType>();
		_LuaScript.Globals[(object)"BuiltinShaderMode"] = UserData.CreateStatic<BuiltinShaderMode>();
		_LuaScript.Globals[(object)"VideoShadersIncludeMode"] = UserData.CreateStatic<VideoShadersIncludeMode>();
		_LuaScript.Globals[(object)"CameraHDRMode"] = UserData.CreateStatic<CameraHDRMode>();
		_LuaScript.Globals[(object)"RealtimeGICPUUsage"] = UserData.CreateStatic<RealtimeGICPUUsage>();
		_LuaScript.Globals[(object)"ShaderKeywordType"] = UserData.CreateStatic<ShaderKeywordType>();
		_LuaScript.Globals[(object)"RayTracingSubMeshFlags"] = UserData.CreateStatic<RayTracingSubMeshFlags>();
		_LuaScript.Globals[(object)"RayTracingMode"] = UserData.CreateStatic<RayTracingMode>();
		_LuaScript.Globals[(object)"WaitForPresentSyncPoint"] = UserData.CreateStatic<WaitForPresentSyncPoint>();
		_LuaScript.Globals[(object)"GraphicsJobsSyncPoint"] = UserData.CreateStatic<GraphicsJobsSyncPoint>();
		_LuaScript.Globals[(object)"DataStreamType"] = UserData.CreateStatic<DataStreamType>();
		_LuaScript.Globals[(object)"Camera.GateFitMode"] = UserData.CreateStatic<GateFitMode>();
		_LuaScript.Globals[(object)"Camera.StereoscopicEye"] = UserData.CreateStatic<StereoscopicEye>();
		_LuaScript.Globals[(object)"Camera.MonoOrStereoscopicEye"] = UserData.CreateStatic<MonoOrStereoscopicEye>();
		_LuaScript.Globals[(object)"Camera.SceneViewFilterMode"] = UserData.CreateStatic<SceneViewFilterMode>();
		_LuaScript.Globals[(object)"Camera.RenderRequestMode"] = UserData.CreateStatic<RenderRequestMode>();
		_LuaScript.Globals[(object)"Camera.RenderRequestOutputSpace"] = UserData.CreateStatic<RenderRequestOutputSpace>();
		_LuaScript.Globals[(object)"Camera.FieldOfViewAxis"] = UserData.CreateStatic<FieldOfViewAxis>();
		_LuaScript.Globals[(object)"GraphicsBuffer.Target"] = UserData.CreateStatic<Target>();
		_LuaScript.Globals[(object)"LightProbeProxyVolume.BoundingBoxMode"] = UserData.CreateStatic<BoundingBoxMode>();
		_LuaScript.Globals[(object)"LightProbeProxyVolume.RefreshMode"] = UserData.CreateStatic<RefreshMode>();
		_LuaScript.Globals[(object)"LightProbeProxyVolume.QualityMode"] = UserData.CreateStatic<QualityMode>();
		_LuaScript.Globals[(object)"LightProbeProxyVolume.DataFormat"] = UserData.CreateStatic<DataFormat>();
		_LuaScript.Globals[(object)"Texture2D.EXRFlags"] = UserData.CreateStatic<EXRFlags>();
		_LuaScript.Globals[(object)"TouchScreenKeyboard.Status"] = UserData.CreateStatic<Status>();
		_LuaScript.Globals[(object)"RectTransform.Edge"] = UserData.CreateStatic<Edge>();
		_LuaScript.Globals[(object)"RectTransform.Axis"] = UserData.CreateStatic<Axis>();
		_LuaScript.Globals[(object)"Scene.LoadingState"] = UserData.CreateStatic<LoadingState>();
		_LuaScript.Globals[(object)"SupportedRenderingFeatures.LightmapMixedBakeModes"] = UserData.CreateStatic<LightmapMixedBakeModes>();
		_LuaScript.Globals[(object)"FrameData.Flags"] = UserData.CreateStatic<Flags>();
		_LuaScript.Globals[(object)"RigidbodyConstraints"] = UserData.CreateStatic<RigidbodyConstraints>();
		_LuaScript.Globals[(object)"ForceMode"] = UserData.CreateStatic<ForceMode>();
		_LuaScript.Globals[(object)"PhysicMaterialCombine"] = UserData.CreateStatic<PhysicMaterialCombine>();
		_LuaScript.Globals[(object)"JointDriveMode"] = UserData.CreateStatic<JointDriveMode>();
		_LuaScript.Globals[(object)"ModifiableContactPatch.Flags"] = UserData.CreateStatic<Flags>();
		_LuaScript.Globals[(object)"RenderMode"] = UserData.CreateStatic<RenderMode>();
		_LuaScript.Globals[(object)"NavMesh"] = UserData.CreateStatic(typeof(NavMesh));
		_LuaScript.Globals[(object)"NavMeshPathStatus"] = UserData.CreateStatic<NavMeshPathStatus>();
		_LuaScript.Globals[(object)"ObstacleAvoidanceType"] = UserData.CreateStatic<ObstacleAvoidanceType>();
		_LuaScript.Globals[(object)"NavMeshObstacleShape"] = UserData.CreateStatic<NavMeshObstacleShape>();
		_LuaScript.Globals[(object)"OffMeshLinkType"] = UserData.CreateStatic<OffMeshLinkType>();
		_LuaScript.Globals[(object)"NavMeshBuildSourceShape"] = UserData.CreateStatic<NavMeshBuildSourceShape>();
		_LuaScript.Globals[(object)"NavMeshCollectGeometry"] = UserData.CreateStatic<NavMeshCollectGeometry>();
		_LuaScript.Globals[(object)"PathQueryStatus"] = UserData.CreateStatic<PathQueryStatus>();
		_LuaScript.Globals[(object)"NavMeshPolyTypes"] = UserData.CreateStatic<NavMeshPolyTypes>();
		_LuaScript.Globals[(object)"NavMeshBuildDebugFlags"] = UserData.CreateStatic<NavMeshBuildDebugFlags>();
		_LuaScript.Globals[(object)"PlayMode"] = UserData.CreateStatic<PlayMode>();
		_LuaScript.Globals[(object)"QueueMode"] = UserData.CreateStatic<QueueMode>();
		_LuaScript.Globals[(object)"AvatarTarget"] = UserData.CreateStatic<AvatarTarget>();
		_LuaScript.Globals[(object)"AvatarIKGoal"] = UserData.CreateStatic<AvatarIKGoal>();
		_LuaScript.Globals[(object)"AvatarIKHint"] = UserData.CreateStatic<AvatarIKHint>();
		_LuaScript.Globals[(object)"AnimatorControllerParameterType"] = UserData.CreateStatic<AnimatorControllerParameterType>();
		_LuaScript.Globals[(object)"StateInfoIndex"] = UserData.CreateStatic<StateInfoIndex>();
		_LuaScript.Globals[(object)"AnimatorRecorderMode"] = UserData.CreateStatic<AnimatorRecorderMode>();
		_LuaScript.Globals[(object)"AnimatorCullingMode"] = UserData.CreateStatic<AnimatorCullingMode>();
		_LuaScript.Globals[(object)"AnimatorUpdateMode"] = UserData.CreateStatic<AnimatorUpdateMode>();
		_LuaScript.Globals[(object)"HumanBodyBones"] = UserData.CreateStatic<HumanBodyBones>();
		_LuaScript.Globals[(object)"AvatarMaskBodyPart"] = UserData.CreateStatic<AvatarMaskBodyPart>();
		_LuaScript.Globals[(object)"BodyDof"] = UserData.CreateStatic<BodyDof>();
		_LuaScript.Globals[(object)"HeadDof"] = UserData.CreateStatic<HeadDof>();
		_LuaScript.Globals[(object)"LegDof"] = UserData.CreateStatic<LegDof>();
		_LuaScript.Globals[(object)"ArmDof"] = UserData.CreateStatic<ArmDof>();
		_LuaScript.Globals[(object)"FingerDof"] = UserData.CreateStatic<FingerDof>();
		_LuaScript.Globals[(object)"HumanPartDof"] = UserData.CreateStatic<HumanPartDof>();
		_LuaScript.Globals[(object)"Dof"] = UserData.CreateStatic<Dof>();
		_LuaScript.Globals[(object)"HumanParameter"] = UserData.CreateStatic<HumanParameter>();
		_LuaScript.Globals[(object)"FontStyle"] = UserData.CreateStatic<FontStyle>();
		_LuaScript.Globals[(object)"TextAlignment"] = UserData.CreateStatic<TextAlignment>();
		_LuaScript.Globals[(object)"TextAnchor"] = UserData.CreateStatic<TextAnchor>();
		_LuaScript.Globals[(object)"HorizontalWrapMode"] = UserData.CreateStatic<HorizontalWrapMode>();
		_LuaScript.Globals[(object)"VerticalWrapMode"] = UserData.CreateStatic<VerticalWrapMode>();
		_LuaScript.Globals[(object)"ParticleSystemRenderMode"] = UserData.CreateStatic<ParticleSystemRenderMode>();
		_LuaScript.Globals[(object)"ParticleSystemSortMode"] = UserData.CreateStatic<ParticleSystemSortMode>();
		_LuaScript.Globals[(object)"ParticleSystemRenderSpace"] = UserData.CreateStatic<ParticleSystemRenderSpace>();
		_LuaScript.Globals[(object)"ParticleSystemCurveMode"] = UserData.CreateStatic<ParticleSystemCurveMode>();
		_LuaScript.Globals[(object)"ParticleSystemGradientMode"] = UserData.CreateStatic<ParticleSystemGradientMode>();
		_LuaScript.Globals[(object)"ParticleSystemShapeType"] = UserData.CreateStatic<ParticleSystemShapeType>();
		_LuaScript.Globals[(object)"ParticleSystemMeshShapeType"] = UserData.CreateStatic<ParticleSystemMeshShapeType>();
		_LuaScript.Globals[(object)"ParticleSystemScalingMode"] = UserData.CreateStatic<ParticleSystemScalingMode>();
		_LuaScript.Globals[(object)"ParticleSystemEmitterVelocityMode"] = UserData.CreateStatic<ParticleSystemEmitterVelocityMode>();
		_LuaScript.Globals[(object)"ParticleSystemInheritVelocityMode"] = UserData.CreateStatic<ParticleSystemInheritVelocityMode>();
		_LuaScript.Globals[(object)"ParticleSystemVertexStream"] = UserData.CreateStatic<ParticleSystemVertexStream>();
		_LuaScript.Globals[(object)"ParticleSystemCustomData"] = UserData.CreateStatic<ParticleSystemCustomData>();
		_LuaScript.Globals[(object)"ParticleSystemNoiseQuality"] = UserData.CreateStatic<ParticleSystemNoiseQuality>();
		_LuaScript.Globals[(object)"ParticleSystemGameObjectFilter"] = UserData.CreateStatic<ParticleSystemGameObjectFilter>();
		_LuaScript.Globals[(object)"ParticleSystemForceFieldShape"] = UserData.CreateStatic<ParticleSystemForceFieldShape>();
		_LuaScript.Globals[(object)"ParticleSystemVertexStreams"] = UserData.CreateStatic<ParticleSystemVertexStreams>();
		_LuaScript.Globals[(object)"ParticleSystemShapeTextureChannel"] = UserData.CreateStatic<ParticleSystemShapeTextureChannel>();
		_LuaScript.Globals[(object)"ParticleSystemColliderQueryMode"] = UserData.CreateStatic<ParticleSystemColliderQueryMode>();
		_LuaScript.Globals[(object)"ParticleSystemCullingMode"] = UserData.CreateStatic<ParticleSystemCullingMode>();
		_LuaScript.Globals[(object)"ParticleSystemTriggerEventType"] = UserData.CreateStatic<ParticleSystemTriggerEventType>();
		_LuaScript.Globals[(object)"ParticleSystemCustomDataMode"] = UserData.CreateStatic<ParticleSystemCustomDataMode>();
		_LuaScript.Globals[(object)"ParticleSystemSubEmitterType"] = UserData.CreateStatic<ParticleSystemSubEmitterType>();
		_LuaScript.Globals[(object)"ParticleSystemSubEmitterProperties"] = UserData.CreateStatic<ParticleSystemSubEmitterProperties>();
		_LuaScript.Globals[(object)"ParticleSystemTrailMode"] = UserData.CreateStatic<ParticleSystemTrailMode>();
		_LuaScript.Globals[(object)"ParticleSystemTrailTextureMode"] = UserData.CreateStatic<ParticleSystemTrailTextureMode>();
		_LuaScript.Globals[(object)"ParticleSystemShapeMultiModeValue"] = UserData.CreateStatic<ParticleSystemShapeMultiModeValue>();
		_LuaScript.Globals[(object)"ParticleSystemRingBufferMode"] = UserData.CreateStatic<ParticleSystemRingBufferMode>();
		_LuaScript.Globals[(object)"UVChannelFlags"] = UserData.CreateStatic<UVChannelFlags>();
		_LuaScript.Globals[(object)"Particle.Flags"] = UserData.CreateStatic<Flags>();
		_LuaScript.Globals[(object)"TerrainRenderFlags"] = UserData.CreateStatic<TerrainRenderFlags>();
		_LuaScript.Globals[(object)"TerrainHeightmapSyncControl"] = UserData.CreateStatic<TerrainHeightmapSyncControl>();
		_LuaScript.Globals[(object)"TerrainMapStatusCode"] = UserData.CreateStatic<TerrainMapStatusCode>();
		_LuaScript.Globals[(object)"DetailRenderMode"] = UserData.CreateStatic<DetailRenderMode>();
		_LuaScript.Globals[(object)"TerrainChangedFlags"] = UserData.CreateStatic<TerrainChangedFlags>();
		_LuaScript.Globals[(object)"TerrainBuiltinPaintMaterialPasses"] = UserData.CreateStatic<TerrainBuiltinPaintMaterialPasses>();
		_LuaScript.Globals[(object)"Terrain.MaterialType"] = UserData.CreateStatic<MaterialType>();
		_LuaScript.Globals[(object)"TerrainData.BoundaryValueType"] = UserData.CreateStatic<BoundaryValueType>();
		_LuaScript.Globals[(object)"VideoRenderMode"] = UserData.CreateStatic<VideoRenderMode>();
		_LuaScript.Globals[(object)"Video3DLayout"] = UserData.CreateStatic<Video3DLayout>();
		_LuaScript.Globals[(object)"VideoTimeSource"] = UserData.CreateStatic<VideoTimeSource>();
		_LuaScript.Globals[(object)"VideoTimeReference"] = UserData.CreateStatic<VideoTimeReference>();
		_LuaScript.Globals[(object)"VideoSource"] = UserData.CreateStatic<VideoSource>();
		_LuaScript.Globals[(object)"VideoError"] = UserData.CreateStatic<VideoError>();
		_LuaScript.Globals[(object)"VideoPixelFormat"] = UserData.CreateStatic<VideoPixelFormat>();
		_LuaScript.Globals[(object)"VideoAlphaLayout"] = UserData.CreateStatic<VideoAlphaLayout>();
		_LuaScript.Globals[(object)"TextContainerAnchors"] = UserData.CreateStatic<TextContainerAnchors>();
		_LuaScript.Globals[(object)"Compute_DistanceTransform_EventTypes"] = UserData.CreateStatic<Compute_DistanceTransform_EventTypes>();
		_LuaScript.Globals[(object)"TMP_VertexDataUpdateFlags"] = UserData.CreateStatic<TMP_VertexDataUpdateFlags>();
		_LuaScript.Globals[(object)"ColorMode"] = UserData.CreateStatic<ColorMode>();
		_LuaScript.Globals[(object)"FontFeatureLookupFlags"] = UserData.CreateStatic<FontFeatureLookupFlags>();
		_LuaScript.Globals[(object)"VertexSortingOrder"] = UserData.CreateStatic<VertexSortingOrder>();
		_LuaScript.Globals[(object)"MarkupTag"] = UserData.CreateStatic<MarkupTag>();
		_LuaScript.Globals[(object)"TagValueType"] = UserData.CreateStatic<TagValueType>();
		_LuaScript.Globals[(object)"TagUnitType"] = UserData.CreateStatic<TagUnitType>();
		_LuaScript.Globals[(object)"TextRenderFlags"] = UserData.CreateStatic<TextRenderFlags>();
		_LuaScript.Globals[(object)"TMP_TextElementType"] = UserData.CreateStatic<TMP_TextElementType>();
		_LuaScript.Globals[(object)"MaskingTypes"] = UserData.CreateStatic<MaskingTypes>();
		_LuaScript.Globals[(object)"TextOverflowModes"] = UserData.CreateStatic<TextOverflowModes>();
		_LuaScript.Globals[(object)"MaskingOffsetMode"] = UserData.CreateStatic<MaskingOffsetMode>();
		_LuaScript.Globals[(object)"FontStyles"] = UserData.CreateStatic<FontStyles>();
		_LuaScript.Globals[(object)"FontWeight"] = UserData.CreateStatic<FontWeight>();
		_LuaScript.Globals[(object)"TextElementType"] = UserData.CreateStatic<TextElementType>();
		_LuaScript.Globals[(object)"SpriteAssetImportFormats"] = UserData.CreateStatic<SpriteAssetImportFormats>();
		_LuaScript.Globals[(object)"ColorTween.ColorTweenMode"] = UserData.CreateStatic<ColorTweenMode>();
		_LuaScript.Globals[(object)"TMP_InputField.ContentType"] = UserData.CreateStatic<ContentType>();
		_LuaScript.Globals[(object)"TMP_InputField.InputType"] = UserData.CreateStatic<InputType>();
		_LuaScript.Globals[(object)"TMP_InputField.EditState"] = UserData.CreateStatic<EditState>();
		_LuaScript.Globals[(object)"TMP_Text.TextInputSources"] = UserData.CreateStatic<TextInputSources>();
		_LuaScript.Globals[(object)"SaveFeatures"] = UserData.CreateStatic<SaveFeatures>();
		_LuaScript.Globals[(object)"GripFlags"] = UserData.CreateStatic<GripFlags>();
		_LuaScript.Globals[(object)"SlotType"] = UserData.CreateStatic<SlotType>();
		_LuaScript.Globals[(object)"InactiveStates"] = UserData.CreateStatic<InactiveStates>();
		_LuaScript.Globals[(object)"ZoneGunMode"] = UserData.CreateStatic<ZoneGunMode>();
		_LuaScript.Globals[(object)"EdgeType"] = UserData.CreateStatic<EdgeType>();
		_LuaScript.Globals[(object)"ValueType"] = UserData.CreateStatic<ValueType>();
		_LuaScript.Globals[(object)"StreamStatus"] = UserData.CreateStatic<StreamStatus>();
		_LuaScript.Globals[(object)"MuscleRemoveMode"] = UserData.CreateStatic<MuscleRemoveMode>();
		_LuaScript.Globals[(object)"FullBodyBone"] = UserData.CreateStatic<FullBodyBone>();
		_LuaScript.Globals[(object)"XRControllerType"] = UserData.CreateStatic<XRControllerType>();
		_LuaScript.Globals[(object)"HandBone"] = UserData.CreateStatic<HandBone>();
		_LuaScript.Globals[(object)"GraphicsQuality"] = UserData.CreateStatic<GraphicsQuality>();
		_LuaScript.Globals[(object)"SpectatorCameraMode"] = UserData.CreateStatic<SpectatorCameraMode>();
		_LuaScript.Globals[(object)"EyeTarget"] = UserData.CreateStatic<EyeTarget>();
		_LuaScript.Globals[(object)"SettingLevel"] = UserData.CreateStatic<SettingLevel>();
		_LuaScript.Globals[(object)"FoveatedRenderingMode"] = UserData.CreateStatic<FoveatedRenderingMode>();
		_LuaScript.Globals[(object)"FoveatedPresets"] = UserData.CreateStatic<FoveatedPresets>();
		_LuaScript.Globals[(object)"SaveFlags"] = UserData.CreateStatic<SaveFlags>();
		_LuaScript.Globals[(object)"AttackType"] = UserData.CreateStatic<AttackType>();
		_LuaScript.Globals[(object)"Weight"] = UserData.CreateStatic<Weight>();
		_LuaScript.Globals[(object)"VRPlatform"] = UserData.CreateStatic<VRPlatform>();
		_LuaScript.Globals[(object)"AvatarGrip.BodyRb"] = UserData.CreateStatic<BodyRb>();
		_LuaScript.Globals[(object)"BarrelGrip.Caps"] = UserData.CreateStatic<Caps>();
		_LuaScript.Globals[(object)"BoxGrip.Faces"] = UserData.CreateStatic<Faces>();
		_LuaScript.Globals[(object)"BoxGrip.Edges"] = UserData.CreateStatic<Edges>();
		_LuaScript.Globals[(object)"BoxGrip.Corners"] = UserData.CreateStatic<Corners>();
		_LuaScript.Globals[(object)"ForcePullGrip.GripState"] = UserData.CreateStatic<GripState>();
		_LuaScript.Globals[(object)"LadderInfo.Source"] = UserData.CreateStatic<Source>();
		_LuaScript.Globals[(object)"Constrainer.ConstraintMode"] = UserData.CreateStatic<ConstraintMode>();
		_LuaScript.Globals[(object)"Gun.FireMode"] = UserData.CreateStatic<FireMode>();
		_LuaScript.Globals[(object)"Gun.SlideStates"] = UserData.CreateStatic<SlideStates>();
		_LuaScript.Globals[(object)"Gun.HammerStates"] = UserData.CreateStatic<HammerStates>();
		_LuaScript.Globals[(object)"Gun.CartridgeStates"] = UserData.CreateStatic<CartridgeStates>();
		_LuaScript.Globals[(object)"ImpactProperties.DecalType"] = UserData.CreateStatic<DecalType>();
		_LuaScript.Globals[(object)"Haptor.HapStack"] = UserData.CreateStatic<HapStack>();
		_LuaScript.Globals[(object)"Health.HealthMode"] = UserData.CreateStatic<HealthMode>();
		_LuaScript.Globals[(object)"PlayerDamageReceiver.BodyPart"] = UserData.CreateStatic<BodyPart>();
		_LuaScript.Globals[(object)"BaseController.GesturePose"] = UserData.CreateStatic<GesturePose>();
		_LuaScript.Globals[(object)"OpenControllerRig.TrackedState"] = UserData.CreateStatic<TrackedState>();
		_LuaScript.Globals[(object)"OpenControllerRig.CurveMode"] = UserData.CreateStatic<CurveMode>();
		_LuaScript.Globals[(object)"OpenControllerRig.VrVertState"] = UserData.CreateStatic<VrVertState>();
		_LuaScript.Globals[(object)"PhysicsRig.BodyMassState"] = UserData.CreateStatic<BodyMassState>();
		_LuaScript.Globals[(object)"PhysicsRig.StepState"] = UserData.CreateStatic<StepState>();
		_LuaScript.Globals[(object)"RemapRig.TraversalState"] = UserData.CreateStatic<TraversalState>();
		_LuaScript.Globals[(object)"RemapRig.VertState"] = UserData.CreateStatic<VertState>();
		_LuaScript.Globals[(object)"RigManager.BodyState"] = UserData.CreateStatic<BodyState>();
		_LuaScript.Globals[(object)"RigManager.LeashType"] = UserData.CreateStatic<LeashType>();
		_LuaScript.Globals[(object)"Seat.SeatState"] = UserData.CreateStatic<SeatState>();
		_LuaScript.Globals[(object)"Seat.AxisAssignment"] = UserData.CreateStatic<AxisAssignment>();
		_LuaScript.Globals[(object)"ParticleSpread.Alignment"] = UserData.CreateStatic<Alignment>();
		_LuaScript.Globals[(object)"PhysHand.HandPhysState"] = UserData.CreateStatic<HandPhysState>();
		_LuaScript.Globals[(object)"Zone3dSound.SoundMode"] = UserData.CreateStatic<SoundMode>();
		_LuaScript.Globals[(object)"ZoneLinkItem.EventTypes"] = UserData.CreateStatic<EventTypes>();
		_LuaScript.Globals[(object)"MarrowQuery.LogicOperator"] = UserData.CreateStatic<LogicOperator>();
		_LuaScript.Globals[(object)"BaseEnemyConfig.LocoState"] = UserData.CreateStatic<LocoState>();
		_LuaScript.Globals[(object)"Muscle.Group"] = UserData.CreateStatic<Group>();
		_LuaScript.Globals[(object)"PuppetMaster.Mode"] = UserData.CreateStatic<Mode>();
		_LuaScript.Globals[(object)"PuppetMaster.State"] = UserData.CreateStatic<State>();
		_LuaScript.Globals[(object)"Weight.Mode"] = UserData.CreateStatic<Mode>();
		_LuaScript.Globals[(object)"DisplaySubsystemManager.ColorSpace"] = UserData.CreateStatic<ColorSpace>();
		_LuaScript.Globals[(object)"SpawnPolicyData.PolicyRule"] = UserData.CreateStatic<PolicyRule>();
		_LuaScript.Globals[(object)"BaseConsoleCommand.CommandStatus"] = UserData.CreateStatic<CommandStatus>();
		_LuaScript.Globals[(object)"AngularXSensor.Output"] = UserData.CreateStatic<Output>();
		_LuaScript.Globals[(object)"AngularYSensor.Output"] = UserData.CreateStatic<Output>();
		_LuaScript.Globals[(object)"AngularZSensor.Output"] = UserData.CreateStatic<Output>();
		_LuaScript.Globals[(object)"ButtonController.ButtonMode"] = UserData.CreateStatic<ButtonMode>();
		_LuaScript.Globals[(object)"LinearXSensor.Output"] = UserData.CreateStatic<Output>();
		_LuaScript.Globals[(object)"Encounter.SpawnOrder"] = UserData.CreateStatic<SpawnOrder>();
		_LuaScript.Globals[(object)"NPC_Display_Data.NPC_State"] = UserData.CreateStatic<NPC_State>();
		_LuaScript.Globals[(object)"SpawnAgro.MentalMode"] = UserData.CreateStatic<MentalMode>();
		_LuaScript.Globals[(object)"SpawnGroup.MentalMode"] = UserData.CreateStatic<MentalMode>();
		_LuaScript.Globals[(object)"TriggerRefProxy.TriggerType"] = UserData.CreateStatic<TriggerType>();
		_LuaScript.Globals[(object)"TriggerRefProxy.NpcType"] = UserData.CreateStatic<NpcType>();
		_LuaScript.Globals[(object)"Footstep.StepState"] = UserData.CreateStatic<StepState>();
		_LuaScript.Globals[(object)"SupportFlags"] = UserData.CreateStatic<SupportFlags>();
		_LuaScript.Globals[(object)"GradType"] = UserData.CreateStatic<GradType>();
		_LuaScript.Globals[(object)"LeanTweenType"] = UserData.CreateStatic<LeanTweenType>();
		_LuaScript.Globals[(object)"LeanProp"] = UserData.CreateStatic<LeanProp>();
		_LuaScript.Globals[(object)"ListChangeType"] = UserData.CreateStatic<ListChangeType>();
		_LuaScript.Globals[(object)"Workflow"] = UserData.CreateStatic<Workflow>();
		_LuaScript.Globals[(object)"AntiFlickerMode"] = UserData.CreateStatic<AntiFlickerMode>();
		_LuaScript.Globals[(object)"Quality"] = UserData.CreateStatic<Quality>();
		_LuaScript.Globals[(object)"DebugView"] = UserData.CreateStatic<DebugView>();
		_LuaScript.Globals[(object)"LensFlareStyle"] = UserData.CreateStatic<LensFlareStyle>();
		_LuaScript.Globals[(object)"GlareStyle"] = UserData.CreateStatic<GlareStyle>();
		_LuaScript.Globals[(object)"RenderPipeline"] = UserData.CreateStatic<RenderPipeline>();
		_LuaScript.Globals[(object)"NORMAL_OFFSET"] = UserData.CreateStatic<NORMAL_OFFSET>();
		_LuaScript.Globals[(object)"CAPSULE_COLLIDER_METHOD"] = UserData.CreateStatic<CAPSULE_COLLIDER_METHOD>();
		_LuaScript.Globals[(object)"CREATE_COLLIDER_TYPE"] = UserData.CreateStatic<CREATE_COLLIDER_TYPE>();
		_LuaScript.Globals[(object)"GIZMO_TYPE"] = UserData.CreateStatic<GIZMO_TYPE>();
		_LuaScript.Globals[(object)"RENDER_POINT_TYPE"] = UserData.CreateStatic<RENDER_POINT_TYPE>();
		_LuaScript.Globals[(object)"SKINNED_MESH_COLLIDER_TYPE"] = UserData.CreateStatic<SKINNED_MESH_COLLIDER_TYPE>();
		_LuaScript.Globals[(object)"SPHERE_COLLIDER_METHOD"] = UserData.CreateStatic<SPHERE_COLLIDER_METHOD>();
		_LuaScript.Globals[(object)"MESH_COLLIDER_METHOD"] = UserData.CreateStatic<MESH_COLLIDER_METHOD>();
		_LuaScript.Globals[(object)"VERTEX_SNAP_METHOD"] = UserData.CreateStatic<VERTEX_SNAP_METHOD>();
		_LuaScript.Globals[(object)"VHACD_RESULT_METHOD"] = UserData.CreateStatic<VHACD_RESULT_METHOD>();
		_LuaScript.Globals[(object)"ECE_WINDOW_TAB"] = UserData.CreateStatic<ECE_WINDOW_TAB>();
		_LuaScript.Globals[(object)"COLLIDER_HOLDER"] = UserData.CreateStatic<COLLIDER_HOLDER>();
		_LuaScript.Globals[(object)"CONVEX_HULL_SAVE_METHOD"] = UserData.CreateStatic<CONVEX_HULL_SAVE_METHOD>();
		_LuaScript.Globals[(object)"CategoryFilters"] = UserData.CreateStatic<CategoryFilters>();
		_LuaScript.Globals[(object)"MuzzleBreakType"] = UserData.CreateStatic<MuzzleBreakType>();
		_LuaScript.Globals[(object)"ObjetiveModes"] = UserData.CreateStatic<ObjetiveModes>();
		_LuaScript.Globals[(object)"UtilityModes"] = UserData.CreateStatic<UtilityModes>();
		_LuaScript.Globals[(object)"LogicGunMode"] = UserData.CreateStatic<LogicGunMode>();
		_LuaScript.Globals[(object)"Difficulty"] = UserData.CreateStatic<Difficulty>();
		_LuaScript.Globals[(object)"PlayMode"] = UserData.CreateStatic<PlayMode>();
		_LuaScript.Globals[(object)"RTSize"] = UserData.CreateStatic<RTSize>();
		_LuaScript.Globals[(object)"RTFormat"] = UserData.CreateStatic<RTFormat>();
		_LuaScript.Globals[(object)"GustMixLayer"] = UserData.CreateStatic<GustMixLayer>();
		_LuaScript.Globals[(object)"ProceduralTexture2D.TextureType"] = UserData.CreateStatic<TextureType>();
		_LuaScript.Globals[(object)"LTGUI.Element_Type"] = UserData.CreateStatic<Element_Type>();
		_LuaScript.Globals[(object)"NPC_Tracker_Data.NPC_State"] = UserData.CreateStatic<NPC_State>();
		_LuaScript.Globals[(object)"NPC_Tracker_Data.ALIVE_State"] = UserData.CreateStatic<ALIVE_State>();
		_LuaScript.Globals[(object)"EnemyTool.EToolMode"] = UserData.CreateStatic<EToolMode>();
		_LuaScript.Globals[(object)"MeshBender.FillingMode"] = UserData.CreateStatic<FillingMode>();
		_LuaScript.Globals[(object)"ControlData.EyeControl"] = UserData.CreateStatic<EyeControl>();
		_LuaScript.Globals[(object)"ControlData.EyelidControl"] = UserData.CreateStatic<EyelidControl>();
		_LuaScript.Globals[(object)"ControlData.EyelidBoneMode"] = UserData.CreateStatic<EyelidBoneMode>();
		_LuaScript.Globals[(object)"EyeAndHeadAnimator.HeadControl"] = UserData.CreateStatic<HeadControl>();
		_LuaScript.Globals[(object)"EyeAndHeadAnimator.HeadTweenMethod"] = UserData.CreateStatic<HeadTweenMethod>();
		_LuaScript.Globals[(object)"EyeAndHeadAnimator.BlinkState"] = UserData.CreateStatic<BlinkState>();
		_LuaScript.Globals[(object)"EyeAndHeadAnimator.HeadSpeed"] = UserData.CreateStatic<HeadSpeed>();
		_LuaScript.Globals[(object)"EyeAndHeadAnimator.EyeDelay"] = UserData.CreateStatic<EyeDelay>();
		_LuaScript.Globals[(object)"EyeAndHeadAnimator.LookTarget"] = UserData.CreateStatic<LookTarget>();
		_LuaScript.Globals[(object)"EyeAndHeadAnimator.FaceLookTarget"] = UserData.CreateStatic<FaceLookTarget>();
		_LuaScript.Globals[(object)"LookTargetController.State"] = UserData.CreateStatic<State>();
		_LuaScript.Globals[(object)"Effect.ShaderRenderPass"] = UserData.CreateStatic<ShaderRenderPass>();
		_LuaScript.Globals[(object)"Effect.MaterialKeywords"] = UserData.CreateStatic<MaterialKeywords>();
		_LuaScript.Globals[(object)"EasyColliderRotateDuplicate.ROTATE_AXIS"] = UserData.CreateStatic<ROTATE_AXIS>();
		_LuaScript.Globals[(object)"AraTrail.TrailAlignment"] = UserData.CreateStatic<TrailAlignment>();
		_LuaScript.Globals[(object)"AraTrail.TrailSpace"] = UserData.CreateStatic<TrailSpace>();
		_LuaScript.Globals[(object)"AraTrail.TrailSorting"] = UserData.CreateStatic<TrailSorting>();
		_LuaScript.Globals[(object)"AraTrail.Timescale"] = UserData.CreateStatic<Timescale>();
		_LuaScript.Globals[(object)"AraTrail.TextureMode"] = UserData.CreateStatic<TextureMode>();
		_LuaScript.Globals[(object)"IKSolverLimbSlz.BendModifier"] = UserData.CreateStatic<BendModifier>();
		_LuaScript.Globals[(object)"FadeMaterials.Fade"] = UserData.CreateStatic<Fade>();
		_LuaScript.Globals[(object)"GenericFrameTimer.FrameType"] = UserData.CreateStatic<FrameType>();
		_LuaScript.Globals[(object)"GenericTimer.TimeType"] = UserData.CreateStatic<TimeType>();
		_LuaScript.Globals[(object)"LaserVector.Alignment"] = UserData.CreateStatic<Alignment>();
		_LuaScript.Globals[(object)"Atv_WheelColliders.WheelType"] = UserData.CreateStatic<WheelType>();
		_LuaScript.Globals[(object)"Atv_WheelColliders.SpeedUnit"] = UserData.CreateStatic<SpeedUnit>();
		_LuaScript.Globals[(object)"SimpleSFX.mixerGroups"] = UserData.CreateStatic<mixerGroups>();
		_LuaScript.Globals[(object)"BoneLeaderData.ScoreType"] = UserData.CreateStatic<ScoreType>();
		_LuaScript.Globals[(object)"HandPoseViewer.ViewerMode"] = UserData.CreateStatic<ViewerMode>();
		_LuaScript.Globals[(object)"MobileEncounter.SpawnOrder"] = UserData.CreateStatic<SpawnOrder>();
		_LuaScript.Globals[(object)"Launch_Gun.LaunchMode"] = UserData.CreateStatic<LaunchMode>();
		_LuaScript.Globals[(object)"NavMeshDoor.DoorType"] = UserData.CreateStatic<DoorType>();
		_LuaScript.Globals[(object)"NPC_Objective.TowerMode"] = UserData.CreateStatic<TowerMode>();
		_LuaScript.Globals[(object)"ZipJointMover.ZipState"] = UserData.CreateStatic<ZipState>();
		_LuaScript.Globals[(object)"AgentLinkControl.LinkState"] = UserData.CreateStatic<LinkState>();
		_LuaScript.Globals[(object)"EnemyTurret.TurretStates"] = UserData.CreateStatic<TurretStates>();
		_LuaScript.Globals[(object)"ArenaCraneController.MoveState"] = UserData.CreateStatic<MoveState>();
		_LuaScript.Globals[(object)"ArenaCraneController.GrabState"] = UserData.CreateStatic<GrabState>();
		_LuaScript.Globals[(object)"Arena_BellInteractable.BellState"] = UserData.CreateStatic<BellState>();
		_LuaScript.Globals[(object)"Bell_Interactable.MoveState"] = UserData.CreateStatic<MoveState>();
		_LuaScript.Globals[(object)"GripJointMover.MoveState"] = UserData.CreateStatic<MoveState>();
		_LuaScript.Globals[(object)"SimpleGripJointMover.MoveState"] = UserData.CreateStatic<MoveState>();
		_LuaScript.Globals[(object)"ArmFinale.ArmStage"] = UserData.CreateStatic<ArmStage>();
		_LuaScript.Globals[(object)"BoneLeaderManager.LeaderMode"] = UserData.CreateStatic<LeaderMode>();
		_LuaScript.Globals[(object)"Conveyor.Mode"] = UserData.CreateStatic<Mode>();
		_LuaScript.Globals[(object)"EnemyCollisonRelay.BodyPart"] = UserData.CreateStatic<BodyPart>();
		_LuaScript.Globals[(object)"EnemyDamageReceiver.BodyPart"] = UserData.CreateStatic<BodyPart>();
		_LuaScript.Globals[(object)"TurretHeadController.FireType"] = UserData.CreateStatic<FireType>();
		_LuaScript.Globals[(object)"SpawnableSaver.SpawnerItemType"] = UserData.CreateStatic<SpawnerItemType>();
		_LuaScript.Globals[(object)"Arena_GameController.ArenaStartMode"] = UserData.CreateStatic<ArenaStartMode>();
		_LuaScript.Globals[(object)"Arena_GameController.ArenaDifficulty"] = UserData.CreateStatic<ArenaDifficulty>();
		_LuaScript.Globals[(object)"Arena_GameController.ArenaState"] = UserData.CreateStatic<ArenaState>();
		_LuaScript.Globals[(object)"BaseGameController.GameMode"] = UserData.CreateStatic<GameMode>();
		_LuaScript.Globals[(object)"BaseGameController.TimerMode"] = UserData.CreateStatic<TimerMode>();
		_LuaScript.Globals[(object)"BaseGameController.EndMode"] = UserData.CreateStatic<EndMode>();
		_LuaScript.Globals[(object)"BaseGameController.DebugMode"] = UserData.CreateStatic<DebugMode>();
		_LuaScript.Globals[(object)"TimeTrial_GameController.TimeTrialMode"] = UserData.CreateStatic<TimeTrialMode>();
		_LuaScript.Globals[(object)"TimeTrial_GameController.TimeTrialStartMode"] = UserData.CreateStatic<TimeTrialStartMode>();
		_LuaScript.Globals[(object)"TimeTrial_GameController.TTDifficulty"] = UserData.CreateStatic<TTDifficulty>();
		_LuaScript.Globals[(object)"GenericKeypressEvent.KeyPressType"] = UserData.CreateStatic<KeyPressType>();
		_LuaScript.Globals[(object)"GeoManager.GeoState"] = UserData.CreateStatic<GeoState>();
		_LuaScript.Globals[(object)"LinkData.LinkType"] = UserData.CreateStatic<LinkType>();
		_LuaScript.Globals[(object)"NarrativeState.HoldState"] = UserData.CreateStatic<HoldState>();
		_LuaScript.Globals[(object)"MineCartControl.RideSpeed"] = UserData.CreateStatic<RideSpeed>();
		_LuaScript.Globals[(object)"NooseBonelabIntro.NooseStage"] = UserData.CreateStatic<NooseStage>();
		_LuaScript.Globals[(object)"PlatformDiscriminator.Platform"] = UserData.CreateStatic<Platform>();
		_LuaScript.Globals[(object)"PlatformEvent.Platform"] = UserData.CreateStatic<Platform>();
		_LuaScript.Globals[(object)"BodyVitals.MeasurementState"] = UserData.CreateStatic<MeasurementState>();
		_LuaScript.Globals[(object)"Balloon.BalloonColor"] = UserData.CreateStatic<BalloonColor>();
		_LuaScript.Globals[(object)"Dice.DieState"] = UserData.CreateStatic<DieState>();
		_LuaScript.Globals[(object)"GlassHandler.GlassType"] = UserData.CreateStatic<GlassType>();
		_LuaScript.Globals[(object)"ArenaLootItem.LootType"] = UserData.CreateStatic<LootType>();
		_LuaScript.Globals[(object)"SplineEntity.ContactCount"] = UserData.CreateStatic<ContactCount>();
		_LuaScript.Globals[(object)"SplineJointSpawnableEmitter.Mode"] = UserData.CreateStatic<Mode>();
		_LuaScript.Globals[(object)"TextureStreamingDebugTool.Modes"] = UserData.CreateStatic<Modes>();
		_LuaScript.Globals[(object)"TutorialRig.InputHighlight"] = UserData.CreateStatic<InputHighlight>();
		_LuaScript.Globals[(object)"TutorialRig.SpecificHand"] = UserData.CreateStatic<SpecificHand>();
		_LuaScript.Globals[(object)"TutorialShaft.ShaftState"] = UserData.CreateStatic<ShaftState>();
		_LuaScript.Globals[(object)"PageItemView.SegmentType"] = UserData.CreateStatic<SegmentType>();
		_LuaScript.Globals[(object)"UI_ModGroup.PageType"] = UserData.CreateStatic<PageType>();
		_LuaScript.Globals[(object)"VRGraphicRaycaster.BlockingObjects"] = UserData.CreateStatic<BlockingObjects>();
		_LuaScript.Globals[(object)"VRStandaloneInputModule.InputMode"] = UserData.CreateStatic<InputMode>();
		_LuaScript.Globals[(object)"WeaponPack.WeaponType"] = UserData.CreateStatic<WeaponType>();
		_LuaScript.Globals[(object)"WeaponPack.MeleeType"] = UserData.CreateStatic<MeleeType>();
		_LuaScript.Globals[(object)"GraphicsManager.FoveatedRadii"] = UserData.CreateStatic<FoveatedRadii>();
		_LuaScript.Globals[(object)"GarageDoorPowerable.MOVINGSTATE"] = UserData.CreateStatic<MOVINGSTATE>();
		_LuaScript.Globals[(object)"DebugGrassDisplacementTex.DebugSize"] = UserData.CreateStatic<DebugSize>();
		_LuaScript.Globals[(object)"GrassDisplacementRenderFeature.RTDisplacementSize"] = UserData.CreateStatic<RTDisplacementSize>();
		_LuaScript.Globals[(object)"IvyController.State"] = UserData.CreateStatic<State>();
	}
}
