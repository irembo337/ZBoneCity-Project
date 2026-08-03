using System;
using Il2CppSLZ.VRMK;
using UnityEngine;

namespace LuaMod;

internal class LuaAvatar : LuaBehaviour
{
	public Avatar AttachedAvatar;

	public new void Start()
	{
		if (ScriptName == "" || ScriptName == null)
		{
			ScriptName = "TestAvatar.lua";
		}
		AttachedAvatar = ((Component)this).gameObject.GetComponent<Avatar>();
		base.Start();
	}

	public LuaAvatar(IntPtr ptr)
		: base(ptr)
	{
	}
}
