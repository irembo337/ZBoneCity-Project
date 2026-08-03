using System;
using System.IO;

namespace LuaMod.LuaAPI;

public class BLFileAccess
{
	protected string relativeFileName;

	protected int currentLineNumber = 0;

	private string ResolvedFilePath => ResolvePath(relativeFileName);

	public int LineNumber
	{
		get
		{
			return currentLineNumber;
		}
		set
		{
			if (value < 0)
			{
				currentLineNumber = 0;
			}
			else
			{
				currentLineNumber = SeekToLine(value);
			}
		}
	}

	public BLFileAccess(string name)
	{
		relativeFileName = name;
	}

	private static string ResolvePath(string relativePath)
	{
		return Security.GetRelativePath(relativePath);
	}

	public bool Write(string contents, bool append)
	{
		return LuaSafeCall.Run(delegate
		{
			string resolvedFilePath = ResolvedFilePath;
			if (!Security.IsSafePath(resolvedFilePath))
			{
				throw new UnauthorizedAccessException("Attempted to write to an unsafe file path.");
			}
			using (StreamWriter streamWriter = new StreamWriter(resolvedFilePath, append))
			{
				streamWriter.Write(contents);
			}
			return true;
		}, "BLFileAccess.Write('" + contents + "')");
	}

	public bool WriteLine(string line, bool append)
	{
		return LuaSafeCall.Run(delegate
		{
			string resolvedFilePath = ResolvedFilePath;
			if (!Security.IsSafePath(resolvedFilePath))
			{
				throw new UnauthorizedAccessException("Attempted to write to an unsafe file path.");
			}
			using (StreamWriter streamWriter = new StreamWriter(resolvedFilePath, append))
			{
				streamWriter.WriteLine(line);
			}
			return true;
		}, "BLFileAccess.WriteLine('" + line + "')");
	}

	public string ReadToEnd()
	{
		return LuaSafeCall.Run(delegate
		{
			string resolvedFilePath = ResolvedFilePath;
			if (!Security.IsSafePath(resolvedFilePath))
			{
				throw new UnauthorizedAccessException("Attempted to read from an unsafe file path.");
			}
			using StreamReader streamReader = new StreamReader(resolvedFilePath);
			return streamReader.ReadToEnd();
		}, "BLFileAccess.ReadToEnd()')");
	}

	public string ReadLine()
	{
		return LuaSafeCall.Run(delegate
		{
			string resolvedFilePath = ResolvedFilePath;
			if (!Security.IsSafePath(resolvedFilePath))
			{
				throw new UnauthorizedAccessException("Attempted to read from an unsafe file path.");
			}
			using StreamReader streamReader = new StreamReader(resolvedFilePath);
			for (int i = 0; i < currentLineNumber; i++)
			{
				if (streamReader.ReadLine() == null)
				{
					return (string)null;
				}
			}
			string text = streamReader.ReadLine();
			if (text != null)
			{
				currentLineNumber++;
			}
			return text;
		}, "BLFileAccess.ReadLine()')");
	}

	private int SeekToLine(int targetLine)
	{
		string resolvedFilePath = ResolvedFilePath;
		if (!Security.IsSafePath(resolvedFilePath))
		{
			throw new UnauthorizedAccessException("Attempted to read from an unsafe file path.");
		}
		try
		{
			using StreamReader streamReader = new StreamReader(resolvedFilePath);
			int i;
			for (i = 0; i < targetLine; i++)
			{
				if (streamReader.ReadLine() == null)
				{
					break;
				}
			}
			return i;
		}
		catch
		{
			return 0;
		}
	}

	public void Close()
	{
		currentLineNumber = 0;
	}
}
