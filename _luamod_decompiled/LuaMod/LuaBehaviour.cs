using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using MelonLoader;
using MoonSharp.Interpreter;
using UnityEngine;

namespace LuaMod;

public class LuaBehaviour : MonoBehaviour
{
	public string ScriptName;

	public TextAsset ScriptAsset;

	public float SlowUpdateTime = 0.5f;

	public List<string> ScriptTags;

	public bool Ready = false;

	public LuaModScript BehaviourScript;

	protected DynValue StartFunction;

	protected DynValue LateStartFunction;

	protected DynValue UpdateFunction;

	protected DynValue FixedUpdateFunction;

	protected DynValue AwakeFunction;

	protected DynValue OnEnableFunction;

	protected DynValue OnDisableFunction;

	protected DynValue OnDestroyFunction;

	protected DynValue OnCollisionEnterFunction;

	protected DynValue OnCollisionExitFunction;

	protected DynValue OnCollisionStayFunction;

	protected DynValue SlowUpdateFunction;

	protected DynValue LateUpdateFunction;

	protected DynValue OnTriggerEnterFunction;

	protected DynValue OnTriggerExitFunction;

	protected DynValue OnTriggerStayFunction;

	protected DynValue OnBecameInvisibleFunction;

	protected DynValue OnBecameVisibleFunction;

	protected DynValue OnParticleSystemStoppedFunction;

	protected DynValue OnParticleCollisionFunction;

	protected DynValue OnParticleTriggerFunction;

	protected DynValue OnParticleUpdateJobScheduledFunction;

	protected DynValue OnTransformChildrenChangedFunction;

	protected DynValue OnTransformParentChangedFunction;

	protected DynValue OnJointBreakFunction;

	[MoonSharpHidden]
	public void LoadBehaviourFunctionPointers()
	{
	}

	[MoonSharpHidden]
	public void Start()
	{
		BehaviourScript = new LuaModScript();
		bool flag = false;
		if (ScriptName != "" && LoadScript(Security.GetRelativeScriptPath(ScriptName)))
		{
			flag = true;
		}
		else if ((Object)(object)ScriptAsset != (Object)null && LoadScript(ScriptAsset))
		{
			flag = true;
		}
		if (!flag)
		{
			MelonLogger.Warning("No Valid script for LuaBehaviour " + ((Object)this).name);
			Ready = false;
			return;
		}
		CallScriptFunction(StartFunction);
		if (SlowUpdateFunction != null && SlowUpdateFunction != DynValue.Nil)
		{
			SetSlowUpdate(running: true, SlowUpdateTime);
		}
		if (LateStartFunction != null && LateStartFunction != DynValue.Nil)
		{
			((MonoBehaviour)this).Invoke("LateStart", 1.5f);
		}
		Ready = true;
	}

	public void SetSlowUpdate(bool running, float time)
	{
		float num = (SlowUpdateTime = Mathf.Max(time, 0.1f));
		((MonoBehaviour)this).CancelInvoke("SlowUpdate");
		if (running)
		{
			((MonoBehaviour)this).InvokeRepeating("SlowUpdate", Random.Range(0f, 3f * num), num);
		}
	}

	[MoonSharpHidden]
	private void OnTriggerEnter(Collider othercol)
	{
		CallScriptFunction(OnTriggerEnterFunction, othercol);
	}

	[MoonSharpHidden]
	private void OnTriggerExit(Collider othercol)
	{
		CallScriptFunction(OnTriggerExitFunction, othercol);
	}

	[MoonSharpHidden]
	private void OnTriggerStay(Collider othercol)
	{
		CallScriptFunction(OnTriggerStayFunction, othercol);
	}

	[MoonSharpHidden]
	private void LateStart()
	{
		CallScriptFunction(LateStartFunction);
	}

	[MoonSharpHidden]
	private void LateUpdate()
	{
		CallScriptFunction(LateUpdateFunction);
	}

	[MoonSharpHidden]
	private void OnBecameInvisible()
	{
		CallScriptFunction(OnBecameInvisibleFunction);
	}

	[MoonSharpHidden]
	private void OnBecameVisible()
	{
		CallScriptFunction(OnBecameVisibleFunction);
	}

	[MoonSharpHidden]
	private void SlowUpdate()
	{
		if (Ready && BehaviourScript.ScriptIsValid())
		{
			CallScriptFunction(SlowUpdateFunction);
		}
	}

	[MoonSharpHidden]
	private void OnEnable()
	{
		CallScriptFunction(OnEnableFunction);
	}

	[MoonSharpHidden]
	private void OnDisable()
	{
		CallScriptFunction(OnDisableFunction);
	}

	[MoonSharpHidden]
	private void Awake()
	{
		CallScriptFunction(AwakeFunction);
	}

	[MoonSharpHidden]
	private void OnDestroy()
	{
		CallScriptFunction(OnDestroyFunction);
		BehaviourScript.DestroyScript();
		BehaviourScript = null;
	}

	[MoonSharpHidden]
	private void Update()
	{
		CallScriptFunction(UpdateFunction);
	}

	[MoonSharpHidden]
	private void FixedUpdate()
	{
		CallScriptFunction(FixedUpdateFunction);
	}

	[MoonSharpHidden]
	private void OnCollisionEnter(Collision collision)
	{
		CallScriptFunction(OnCollisionEnterFunction, collision);
	}

	[MoonSharpHidden]
	private void OnCollisionExit(Collision collision)
	{
		CallScriptFunction(OnCollisionExitFunction, collision);
	}

	[MoonSharpHidden]
	private void OnCollisionStay(Collision collision)
	{
		CallScriptFunction(OnCollisionStayFunction, collision);
	}

	private void OnOnJointBreak(float breakForce)
	{
		CallScriptFunction(OnJointBreakFunction, breakForce);
	}

	[MoonSharpHidden]
	private void OnParticleCollision(GameObject other)
	{
		CallScriptFunction(OnParticleCollisionFunction, other);
	}

	[MoonSharpHidden]
	private void OnParticleSystemStopped()
	{
		CallScriptFunction(OnParticleSystemStoppedFunction);
	}

	[MoonSharpHidden]
	private void OnParticleTrigger()
	{
		CallScriptFunction(OnParticleTriggerFunction);
	}

	[MoonSharpHidden]
	private void OnParticleUpdateJobScheduled()
	{
		CallScriptFunction(OnParticleUpdateJobScheduledFunction);
	}

	[MoonSharpHidden]
	private void OnTransformChildrenChanged()
	{
		CallScriptFunction(OnTransformChildrenChangedFunction);
	}

	[MoonSharpHidden]
	private void OnTransformParentChanged()
	{
		CallScriptFunction(OnTransformParentChangedFunction);
	}

	[MoonSharpHidden]
	public DynValue CallScriptFunction(DynValue DyFunc, params object[] Args)
	{
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Invalid comparison between Unknown and I4
		//IL_009f: Unknown result type (might be due to invalid IL or missing references)
		if (DyFunc == null || (int)DyFunc.Type == 0 || (int)DyFunc.Type == 1 || BehaviourScript == null || !BehaviourScript.ScriptIsValid())
		{
			return null;
		}
		Func<DynValue> func = delegate
		{
			//IL_0025: Expected O, but got Unknown
			try
			{
				return BehaviourScript.CallScriptFunction(DyFunc, Args);
			}
			catch (ScriptRuntimeException val)
			{
				ScriptRuntimeException val2 = val;
				MelonLogger.Warning($"[CallScriptFunction] Exception when calling {((object)DyFunc.Function).ToString()} on '{((Object)this).name}' (script: {ScriptName})");
				MelonLogger.Error(((InterpreterException)val2).DecoratedMessage);
				throw;
			}
		};
		DefaultInterpolatedStringHandler defaultInterpolatedStringHandler = new DefaultInterpolatedStringHandler(41, 3);
		defaultInterpolatedStringHandler.AppendLiteral("CallScriptFunction(type: ");
		DynValue obj = DyFunc;
		defaultInterpolatedStringHandler.AppendFormatted((obj != null) ? new DataType?(obj.Type) : null);
		defaultInterpolatedStringHandler.AppendLiteral(", value: ");
		defaultInterpolatedStringHandler.AppendFormatted(((object)DyFunc)?.ToString() ?? "nil");
		defaultInterpolatedStringHandler.AppendLiteral(") on '");
		defaultInterpolatedStringHandler.AppendFormatted(((Object)this).name);
		defaultInterpolatedStringHandler.AppendLiteral("'");
		return LuaSafeCall.Run(func, defaultInterpolatedStringHandler.ToStringAndClear());
	}

	public void SetScriptVariable(string name, DynValue DyVar)
	{
		if (BehaviourScript != null && BehaviourScript.ScriptIsValid() && DyVar != null)
		{
			LuaSafeCall.Run(delegate
			{
				BehaviourScript.SetGlobal(name, DyVar);
			}, "SetScriptVariable('" + name + "')");
			return;
		}
		MelonLogger.Warning("attempting to set script variable failed " + name + " " + ((object)DyVar).ToString() + " BehaviourScript not null " + (BehaviourScript != null) + " script valid: " + BehaviourScript.ScriptIsValid());
	}

	public DynValue GetScriptVariable(string name)
	{
		return LuaSafeCall.Run((Func<DynValue>)delegate
		{
			//IL_0044: Unknown result type (might be due to invalid IL or missing references)
			//IL_004c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0052: Invalid comparison between Unknown and I4
			if (BehaviourScript != null && BehaviourScript.ScriptIsValid())
			{
				DynValue global = BehaviourScript.GetGlobal(name);
				if (global == null || (int)global.Type == 0 || (int)global.Type == 1)
				{
					MelonLogger.Warning($"[GetScriptVariable] Variable '{name}' is nil or missing on script '{ScriptName}' (object: '{((Object)this).name}')");
					return DynValue.Nil;
				}
				return global;
			}
			MelonLogger.Warning($"[GetScriptVariable] Cannot access variable '{name}' - BehaviourScript is null or invalid on object '{((Object)this).name}'");
			return DynValue.Nil;
		}, "GetScriptVariable('" + name + "')");
	}

	[MoonSharpHidden]
	public bool CallScriptFunctionDynamic(DynValue DyFunc, params object[] Args)
	{
		//IL_0033: Expected O, but got Unknown
		if (BehaviourScript != null && DyFunc != null && DyFunc != DynValue.Nil)
		{
			try
			{
				BehaviourScript.CallScriptFunction(DyFunc, Args);
				return true;
			}
			catch (ScriptRuntimeException val)
			{
				ScriptRuntimeException val2 = val;
				MelonLogger.Error("An error occurred! " + ((InterpreterException)val2).DecoratedMessage);
				return false;
			}
		}
		MelonLogger.Error("Error when calling function " + DyFunc.ToPrintString() + " " + BehaviourScript.ToString());
		return false;
	}

	[MoonSharpHidden]
	public virtual bool SetupBehaviourFunctions()
	{
		if (BehaviourScript != null)
		{
			StartFunction = BehaviourScript.GetGlobal("Start");
			LateStartFunction = BehaviourScript.GetGlobal("LateStart");
			UpdateFunction = BehaviourScript.GetGlobal("Update");
			LateUpdateFunction = BehaviourScript.GetGlobal("LateUpdate");
			FixedUpdateFunction = BehaviourScript.GetGlobal("FixedUpdate");
			AwakeFunction = BehaviourScript.GetGlobal("Awake");
			OnEnableFunction = BehaviourScript.GetGlobal("OnEnable");
			OnDisableFunction = BehaviourScript.GetGlobal("OnDisable");
			OnDestroyFunction = BehaviourScript.GetGlobal("OnDestroy");
			OnCollisionEnterFunction = BehaviourScript.GetGlobal("OnCollisionEnter");
			OnCollisionExitFunction = BehaviourScript.GetGlobal("OnCollisionExit");
			OnCollisionStayFunction = BehaviourScript.GetGlobal("OnCollisionStay");
			SlowUpdateFunction = BehaviourScript.GetGlobal("SlowUpdate");
			OnTriggerEnterFunction = BehaviourScript.GetGlobal("OnTriggerEnter");
			OnTriggerExitFunction = BehaviourScript.GetGlobal("OnTriggerExit");
			OnTriggerStayFunction = BehaviourScript.GetGlobal("OnTriggerStay");
			OnJointBreakFunction = BehaviourScript.GetGlobal("OnJointBreak");
			OnParticleSystemStoppedFunction = BehaviourScript.GetGlobal("OnParticleSystemStopped");
			OnTransformChildrenChangedFunction = BehaviourScript.GetGlobal("OnTransformChildrenChanged");
			OnTransformParentChangedFunction = BehaviourScript.GetGlobal("OnTransformParentChanged");
			OnBecameInvisibleFunction = BehaviourScript.GetGlobal("OnBecameInvisible");
			OnBecameVisibleFunction = BehaviourScript.GetGlobal("OnBecameVisible");
			OnParticleCollisionFunction = BehaviourScript.GetGlobal("OnParticleCollision");
			OnParticleTriggerFunction = BehaviourScript.GetGlobal("OnParticleTrigger");
			OnParticleUpdateJobScheduledFunction = BehaviourScript.GetGlobal("OnParticleUpdateJobScheduled");
			return true;
		}
		return false;
	}

	[MoonSharpHidden]
	public bool ReloadScript()
	{
		MelonLogger.Msg("running reload on LuaBehaviour " + ((Object)((Component)this).gameObject).name);
		SetupBehaviourFunctions();
		return true;
	}

	[MoonSharpHidden]
	public bool LoadScript(TextAsset Script)
	{
		if (BehaviourScript.LoadScript(Script, reloading: false, this))
		{
			ScriptAsset = Script;
			SetupBehaviourFunctions();
			BehaviourScript.PostReloadScript = ReloadScript;
			return true;
		}
		MelonLogger.Msg("Lua Behaviour asset not found, blocked, or invalid! " + ((Object)Script).name);
		return false;
	}

	[MoonSharpHidden]
	public bool LoadScript(string Script)
	{
		if (BehaviourScript.LoadScript(Script, reloading: false, this))
		{
			SetupBehaviourFunctions();
			BehaviourScript.PostReloadScript = ReloadScript;
			return true;
		}
		MelonLogger.Msg(((Object)this).name + " Lua Behaviour script not found or blocked! " + Script);
		return false;
	}

	public void CallFunctionULTEvent(string functionname, int param1, float param2, string param3, Object param4)
	{
		DynValue val = DynValue.NewNumber((double)param1);
		DynValue val2 = DynValue.NewNumber((double)param2);
		DynValue val3 = DynValue.NewString(param3);
		DynValue val4 = ((!(param4 != (Object)null)) ? DynValue.Nil : UserData.Create((object)param4));
		CallFunction(functionname, val, val2, val3, val4);
	}

	public bool CallFunction(string functionname, params DynValue[] args)
	{
		return LuaSafeCall.Run(delegate
		{
			if (BehaviourScript == null || !BehaviourScript.ScriptIsValid())
			{
				MelonLogger.Warning($"[CallFunction] Script is invalid or null when calling '{functionname}' on '{((Object)this).name}'");
				return false;
			}
			DynValue global = BehaviourScript.GetGlobal(functionname);
			if (global == null || global == DynValue.Nil)
			{
				MelonLogger.Warning($"[CallFunction] Lua function '{functionname}' is nil or missing on '{((Object)this).name}'");
				return false;
			}
			LuaBehaviour luaBehaviour = this;
			object[] args2 = args;
			return luaBehaviour.CallScriptFunctionDynamic(global, args2);
		}, "CallFunction('" + functionname + "')");
	}

	[MoonSharpHidden]
	public LuaBehaviour(IntPtr ptr)
		: base(ptr)
	{
	}
}
