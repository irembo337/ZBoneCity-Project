using System.IO;
using MoonSharp.Interpreter;

namespace LuaMod.LuaAPI;

public class API_FileAccess
{
	public static readonly API_FileAccess Instance = new API_FileAccess();

	public static BLFileAccess BL_OpenFile(string name)
	{
		return LuaSafeCall.Run(delegate
		{
			//IL_0032: Unknown result type (might be due to invalid IL or missing references)
			string relativePath = Security.GetRelativePath(name);
			if (!Security.IsSafePath(relativePath))
			{
				throw new ScriptRuntimeException("Attempted to access an unsafe path " + relativePath);
			}
			return new BLFileAccess(name);
		}, "BL_OpenFile('" + name + "')");
	}

	public static bool BL_FileExists(string name)
	{
		return LuaSafeCall.Run(delegate
		{
			//IL_002d: Unknown result type (might be due to invalid IL or missing references)
			string relativePath = Security.GetRelativePath(name);
			if (!Security.IsSafePath(relativePath))
			{
				throw new ScriptRuntimeException("Attempted to access an unsafe path " + relativePath);
			}
			return File.Exists(relativePath);
		}, "BL_DoesFileExist('" + name + "')");
	}
}
