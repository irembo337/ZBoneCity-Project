using System;
using BoneLib;
using BoneLib.BoneMenu;
using UnityEngine;

namespace LuaMod.BoneMenu;

public class LuaFunctionElement : FunctionElement
{
	private Texture2D _logo;

	private string _Luacallback;

	private LuaBehaviour _owner;

	private Action<LuaFunctionElement> _callback;

	public Texture2D Logo
	{
		get
		{
			return _logo;
		}
		set
		{
			_logo = value;
			Extensions.InvokeActionSafe(((Element)this).OnElementChanged);
		}
	}

	public LuaFunctionElement(string name, Color color, LuaBehaviour own, string luafunc)
		: base(name, color, (Action)null)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		((Element)this)._elementName = name;
		((Element)this)._elementColor = color;
		_Luacallback = luafunc;
		_owner = own;
		_callback = null;
	}

	public override void OnElementSelected()
	{
		_owner.CallFunction(_Luacallback);
	}
}
