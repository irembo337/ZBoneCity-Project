using System.Collections.Generic;
using System.Linq;
using System.Text;
using MoonSharp.Interpreter;

namespace LuaMod;

public static class LuaMemoryProfiler
{
	public static string ScriptMemoryReport(Script script, Dictionary<string, DynValue> loadedModules)
	{
		//IL_007e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0083: Unknown result type (might be due to invalid IL or missing references)
		HashSet<Table> hashSet = new HashSet<Table>();
		Dictionary<string, long> dictionary = new Dictionary<string, long>();
		long num = 0L;
		foreach (KeyValuePair<string, DynValue> loadedModule in loadedModules)
		{
			long num2 = EstimateValue(loadedModule.Value, hashSet);
			dictionary[loadedModule.Key] = num2;
			num += num2;
		}
		long num3 = 0L;
		foreach (TablePair pair in script.Globals.Pairs)
		{
			TablePair current2 = pair;
			string key = ((TablePair)(ref current2)).Key.ToPrintString();
			if (!loadedModules.ContainsKey(key))
			{
				num3 += EstimateValue(((TablePair)(ref current2)).Value, hashSet);
			}
		}
		num += num3;
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.AppendLine("---- Lua Memory Usage by Module ----");
		StringBuilder stringBuilder2;
		StringBuilder.AppendInterpolatedStringHandler handler;
		foreach (KeyValuePair<string, long> item in dictionary.OrderByDescending((KeyValuePair<string, long> kv) => kv.Value))
		{
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder3 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(15, 3, stringBuilder2);
			handler.AppendFormatted(item.Key);
			handler.AppendLiteral(": ");
			handler.AppendFormatted(item.Value);
			handler.AppendLiteral(" bytes (~");
			handler.AppendFormatted((double)item.Value / 1024.0, "F2");
			handler.AppendLiteral(" KB)");
			stringBuilder3.AppendLine(ref handler);
		}
		stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder4 = stringBuilder2;
		handler = new StringBuilder.AppendInterpolatedStringHandler(30, 2, stringBuilder2);
		handler.AppendLiteral("[Other Globals]: ");
		handler.AppendFormatted(num3);
		handler.AppendLiteral(" bytes (~");
		handler.AppendFormatted((double)num3 / 1024.0, "F2");
		handler.AppendLiteral(" KB)");
		stringBuilder4.AppendLine(ref handler);
		stringBuilder.AppendLine("-------------------------------------");
		stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder5 = stringBuilder2;
		handler = new StringBuilder.AppendInterpolatedStringHandler(37, 2, stringBuilder2);
		handler.AppendLiteral("Total estimated memory: ");
		handler.AppendFormatted(num);
		handler.AppendLiteral(" bytes (~");
		handler.AppendFormatted((double)num / 1024.0, "F2");
		handler.AppendLiteral(" KB)");
		stringBuilder5.AppendLine(ref handler);
		stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder6 = stringBuilder2;
		handler = new StringBuilder.AppendInterpolatedStringHandler(23, 1, stringBuilder2);
		handler.AppendLiteral("Visited ");
		handler.AppendFormatted(hashSet.Count);
		handler.AppendLiteral(" unique tables.");
		stringBuilder6.AppendLine(ref handler);
		return stringBuilder.ToString();
	}

	private static long EstimateValue(DynValue val, HashSet<Table> visited)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Expected I4, but got Unknown
		DataType type = val.Type;
		DataType val2 = type;
		switch (val2 - 2)
		{
		case 2:
			return Encoding.UTF8.GetByteCount(val.String);
		case 1:
			return 8L;
		case 0:
			return 1L;
		case 4:
			return EstimateTable(val.Table, visited);
		case 3:
		case 8:
			return 64L;
		case 6:
			return 64L;
		default:
			return 0L;
		}
	}

	public static float EstimateMemoryMB(Script script, Dictionary<string, DynValue> loadedModules)
	{
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		HashSet<Table> visited = new HashSet<Table>();
		long num = 0L;
		foreach (KeyValuePair<string, DynValue> loadedModule in loadedModules)
		{
			num += EstimateValue(loadedModule.Value, visited);
		}
		foreach (TablePair pair in script.Globals.Pairs)
		{
			TablePair current = pair;
			string key = ((TablePair)(ref current)).Key.ToPrintString();
			if (!loadedModules.ContainsKey(key))
			{
				num += EstimateValue(((TablePair)(ref current)).Value, visited);
			}
		}
		return (float)num / 1048576f;
	}

	private static long EstimateTable(Table table, HashSet<Table> visited)
	{
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		if (visited.Contains(table))
		{
			return 0L;
		}
		visited.Add(table);
		long num = 32L;
		foreach (TablePair pair in table.Pairs)
		{
			TablePair current = pair;
			num += EstimateValue(((TablePair)(ref current)).Key, visited);
			num += EstimateValue(((TablePair)(ref current)).Value, visited);
		}
		return num;
	}
}
