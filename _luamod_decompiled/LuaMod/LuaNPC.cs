using System;
using Il2CppSLZ.Marrow.AI;
using Il2CppSLZ.Marrow.PuppetMasta;
using Il2CppSystem;
using MoonSharp.Interpreter;
using UnityEngine;

namespace LuaMod;

internal class LuaNPC : LuaBehaviour
{
	private DynValue OnDeathFunction;

	private DynValue OnResurrectionFunction;

	public AIBrain AttachedNPCBrain;

	public BehaviourBaseNav AttachedNPCBehaviour;

	public PuppetMaster AttachedPuppetMaster;

	public new void Start()
	{
		AttachedNPCBrain = ((Component)this).gameObject.GetComponent<AIBrain>();
		AttachedNPCBehaviour = AttachedNPCBrain.behaviour;
		AttachedPuppetMaster = AttachedNPCBrain.puppetMaster;
		AIBrain attachedNPCBrain = AttachedNPCBrain;
		attachedNPCBrain.onDeathDelegate += Action<AIBrain>.op_Implicit((Action<AIBrain>)OnAIBrainDeath);
		AIBrain attachedNPCBrain2 = AttachedNPCBrain;
		attachedNPCBrain2.onResurrectDelegate += Action<AIBrain>.op_Implicit((Action<AIBrain>)OnAIBrainResurrect);
		base.Start();
	}

	private void OnAIBrainDeath(AIBrain aIBrain)
	{
		CallScriptFunction(OnDeathFunction);
	}

	private void OnAIBrainResurrect(AIBrain aIBrain)
	{
		CallScriptFunction(OnResurrectionFunction);
	}

	public override bool SetupBehaviourFunctions()
	{
		base.SetupBehaviourFunctions();
		if (BehaviourScript != null)
		{
			OnDeathFunction = BehaviourScript.GetGlobal("OnDeath");
			OnResurrectionFunction = BehaviourScript.GetGlobal("OnResurrection");
			return true;
		}
		return false;
	}

	public LuaNPC(IntPtr ptr)
		: base(ptr)
	{
	}
}
