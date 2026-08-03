using MoonSharp.Interpreter;
using UnityEngine;

namespace LuaMod;

internal interface ISciptedObject
{
	void LoadBehaviourFunctionPointers();

	bool SetupBehaviourFunctions();

	bool ReloadScript();

	bool LoadScript(TextAsset Script, ISciptedObject host);

	bool LoadScript(string Script);

	bool CallFunction(string functionname, params DynValue[] args);
}
