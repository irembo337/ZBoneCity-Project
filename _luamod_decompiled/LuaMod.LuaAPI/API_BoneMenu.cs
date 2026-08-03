using BoneLib.BoneMenu;
using LuaMod.BoneMenu;
using UnityEngine;

namespace LuaMod.LuaAPI;

public class API_BoneMenu
{
	public static readonly API_BoneMenu Instance = new API_BoneMenu();

	public static Page BL_Page = Page.Root;

	public static LuaFunctionElement BL_CreateFunction(Page page, string name, Color color, LuaBehaviour owner, string function)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		LuaFunctionElement luaFunctionElement = new LuaFunctionElement(name, color, owner, function);
		page.Add((Element)(object)luaFunctionElement);
		return luaFunctionElement;
	}

	public static bool BL_DeletePage(Page page)
	{
		Menu.DestroyPage(page);
		return true;
	}

	public static void InvokeFloatAction()
	{
	}
}
