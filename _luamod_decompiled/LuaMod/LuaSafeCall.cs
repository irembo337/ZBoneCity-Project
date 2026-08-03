using System;
using MelonLoader;
using MoonSharp.Interpreter;

namespace LuaMod;

public static class LuaSafeCall
{
	public static T Run<T>(Func<T> func, string context = "Unknown")
	{
		//IL_000f: Expected O, but got Unknown
		//IL_0092: Unknown result type (might be due to invalid IL or missing references)
		//IL_0098: Expected O, but got Unknown
		try
		{
			return func();
		}
		catch (ScriptRuntimeException val)
		{
			ScriptRuntimeException val2 = val;
			MelonLogger.Error("[MoonSharp Error - " + context + "]\n" + ((InterpreterException)val2).DecoratedMessage);
			throw;
		}
		catch (Exception ex)
		{
			ScriptRuntimeException val3 = new ScriptRuntimeException($"[C# Exception in {context}] {ex.GetType().Name}: {ex.Message}");
			MelonLogger.Error(((InterpreterException)val3).DecoratedMessage);
			throw val3;
		}
	}

	public static void Run(Action action, string context = "Unknown")
	{
		//IL_0010: Expected O, but got Unknown
		//IL_0093: Unknown result type (might be due to invalid IL or missing references)
		//IL_0099: Expected O, but got Unknown
		try
		{
			action();
		}
		catch (ScriptRuntimeException val)
		{
			ScriptRuntimeException val2 = val;
			MelonLogger.Error("[MoonSharp Error - " + context + "]\n" + ((InterpreterException)val2).DecoratedMessage);
			throw;
		}
		catch (Exception ex)
		{
			ScriptRuntimeException val3 = new ScriptRuntimeException($"[C# Exception in {context}] {ex.GetType().Name}: {ex.Message}");
			MelonLogger.Error(((InterpreterException)val3).DecoratedMessage);
			throw val3;
		}
	}
}
