using System;
using System.Collections.Generic;

namespace LuaMod;

public static class ScriptManager
{
	public static List<LuaModScript> ScriptList = new List<LuaModScript>(500);

	public static void RegisterScript(LuaModScript script)
	{
		ScriptList.Add(script);
	}

	public static void DeregisterScript(LuaModScript script)
	{
		ScriptList.Remove(script);
	}

	public static void ReloadScripts()
	{
		throw new NotImplementedException();
	}

	public static void InitiateFileSystemMonitor()
	{
		throw new NotImplementedException();
	}
}
