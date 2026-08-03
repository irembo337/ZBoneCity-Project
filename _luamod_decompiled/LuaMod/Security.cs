using System;
using System.IO;
using System.Reflection;
using LuaMod.LuaAPI;
using MelonLoader;

namespace LuaMod;

internal static class Security
{
	private static readonly string scriptsRoot;

	private static readonly string resourcesRoot;

	static Security()
	{
		string directoryName = Path.GetDirectoryName(GetBonelabAddress());
		scriptsRoot = Path.GetFullPath(Path.Combine(directoryName, "LuaMod", "LuaScripts"));
		resourcesRoot = Path.GetFullPath(Path.Combine(directoryName, "LuaMod", "Resources"));
	}

	public static string GetBonelabAddress()
	{
		return Assembly.GetExecutingAssembly().Location;
	}

	public static string GetRelativePath(string filename)
	{
		string path = filename.TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
		return Path.GetFullPath(Path.Combine(resourcesRoot, path));
	}

	public static string GetRelativeScriptPath(string filename)
	{
		string path = filename.TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
		string fullPath = Path.GetFullPath(Path.Combine(scriptsRoot, path));
		return API_Utils.RemoveDoubleSlashes(fullPath);
	}

	public static bool IsSafePath(string path)
	{
		try
		{
			if (string.IsNullOrWhiteSpace(path))
			{
				MelonLogger.Warning("empty path: " + path);
				return false;
			}
			string fullPath = Path.GetFullPath(path);
			if (fullPath.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
			{
				MelonLogger.Warning("Blocked shortcut: " + fullPath);
				return false;
			}
			if (!fullPath.StartsWith(scriptsRoot, StringComparison.OrdinalIgnoreCase) && !fullPath.StartsWith(resourcesRoot, StringComparison.OrdinalIgnoreCase))
			{
				return false;
			}
			if (PathContainsSymlink(fullPath))
			{
				MelonLogger.Warning("Blocked symlink/junction/mount in path: " + fullPath);
				return false;
			}
			return true;
		}
		catch (Exception ex)
		{
			MelonLogger.Warning("[Security] Exception in IsSafePath: " + ex.Message);
			return false;
		}
	}

	private static bool PathContainsSymlink(string fullPath)
	{
		DirectoryInfo directoryInfo = new DirectoryInfo(fullPath);
		while (directoryInfo != null && directoryInfo.Exists)
		{
			if (IsReparsePoint(directoryInfo))
			{
				return true;
			}
			directoryInfo = directoryInfo.Parent;
		}
		return false;
	}

	private static bool IsReparsePoint(DirectoryInfo dir)
	{
		return (dir.Attributes & FileAttributes.ReparsePoint) != 0;
	}
}
