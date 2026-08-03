using System;
using System.Collections.Generic;
using LuaMod.LuaAPI;
using MoonSharp.Interpreter;
using UnityEngine;

namespace LuaMod;

public class LuaResources : MonoBehaviour
{
	public List<string> stringKeys = new List<string>();

	public List<string> stringValues = new List<string>();

	public List<string> floatKeys = new List<string>();

	public List<float> floatValues = new List<float>();

	public List<string> boolKeys = new List<string>();

	public List<bool> boolValues = new List<bool>();

	public List<string> objectKeys = new List<string>();

	public List<Object> objectValues = new List<Object>();

	private Dictionary<string, string> _strings;

	private Dictionary<string, float> _floats;

	private Dictionary<string, bool> _bools;

	private Dictionary<string, Object> _objects;

	public Dictionary<string, string> Strings => _strings ?? (_strings = Build(stringKeys, stringValues));

	public Dictionary<string, float> Floats => _floats ?? (_floats = Build(floatKeys, floatValues));

	public Dictionary<string, bool> Bools => _bools ?? (_bools = Build(boolKeys, boolValues));

	public Dictionary<string, Object> Objects => _objects ?? (_objects = Build(objectKeys, objectValues));

	public static T Cast<T>(Object input) where T : class
	{
		return (input as T) ?? throw new InvalidCastException($"Cannot cast to {typeof(T)}");
	}

	private Dictionary<string, T> Build<T>(List<string> keys, List<T> values)
	{
		Dictionary<string, T> dictionary = new Dictionary<string, T>();
		for (int i = 0; i < keys.Count && i < values.Count; i++)
		{
			if (!dictionary.ContainsKey(keys[i]))
			{
				dictionary[keys[i]] = values[i];
			}
		}
		return dictionary;
	}

	public string GetString(string key)
	{
		string value;
		return Strings.TryGetValue(key, out value) ? value : null;
	}

	public float GetFloat(string key)
	{
		float value;
		return Floats.TryGetValue(key, out value) ? value : 0f;
	}

	public bool GetBool(string key)
	{
		bool value;
		return Bools.TryGetValue(key, out value) && value;
	}

	public void SetString(string key, string value)
	{
		SetValue(stringKeys, stringValues, ref _strings, key, value);
	}

	public void SetFloat(string key, float value)
	{
		SetValue(floatKeys, floatValues, ref _floats, key, value);
	}

	public void SetBool(string key, bool value)
	{
		SetValue(boolKeys, boolValues, ref _bools, key, value);
	}

	public void SetObject(string key, Object value)
	{
		SetValue(objectKeys, objectValues, ref _objects, key, value);
	}

	public DynValue GetObject(string key, string CompType)
	{
		return LuaSafeCall.Run(delegate
		{
			//IL_003f: Unknown result type (might be due to invalid IL or missing references)
			if (!Objects.TryGetValue(key, out var value) || value == (Object)null)
			{
				throw new ScriptRuntimeException("Object with key '" + key + "' not found");
			}
			return API_Utils.BL_ConvertObjectToType(value, CompType);
		}, $"GetObject('{key}', '{CompType}')");
	}

	private void SetValue<T>(List<string> keys, List<T> values, ref Dictionary<string, T> dict, string key, T value)
	{
		int num = keys.IndexOf(key);
		if (num >= 0)
		{
			values[num] = value;
		}
		else
		{
			keys.Add(key);
			values.Add(value);
		}
		if (dict == null)
		{
			dict = new Dictionary<string, T>();
		}
		dict[key] = value;
	}

	public void RebuildAll()
	{
		_strings = Build(stringKeys, stringValues);
		_floats = Build(floatKeys, floatValues);
		_bools = Build(boolKeys, boolValues);
		_objects = Build(objectKeys, objectValues);
	}

	public HashSet<string> GetAllKeys()
	{
		HashSet<string> hashSet = new HashSet<string>();
		hashSet.UnionWith(stringKeys);
		hashSet.UnionWith(floatKeys);
		hashSet.UnionWith(boolKeys);
		hashSet.UnionWith(objectKeys);
		return hashSet;
	}

	public List<string> GetDuplicateKeys()
	{
		Dictionary<string, int> count = new Dictionary<string, int>();
		AddKeys(stringKeys);
		AddKeys(floatKeys);
		AddKeys(boolKeys);
		AddKeys(objectKeys);
		List<string> list2 = new List<string>();
		foreach (KeyValuePair<string, int> item in count)
		{
			if (item.Value > 1)
			{
				list2.Add(item.Key);
			}
		}
		return list2;
		void AddKeys(List<string> list)
		{
			foreach (string item2 in list)
			{
				if (!string.IsNullOrEmpty(item2))
				{
					if (!count.ContainsKey(item2))
					{
						count[item2] = 0;
					}
					count[item2]++;
				}
			}
		}
	}
}
