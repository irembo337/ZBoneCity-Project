using BoneLib;
using Il2CppSLZ.Marrow;
using UnityEngine;

namespace LuaMod.LuaAPI;

public class API_Input
{
	public static readonly API_Input Instance = new API_Input();

	public static bool BL_IsAButtonDown()
	{
		if ((Object)(object)Player.RightController != (Object)null)
		{
			return Player.RightController.GetAButton();
		}
		return false;
	}

	public static bool BL_IsAButtonDownOnce()
	{
		if ((Object)(object)Player.RightController != (Object)null)
		{
			return Player.RightController.GetAButtonDown();
		}
		return false;
	}

	public static bool BL_IsAButtonUpOnce()
	{
		if ((Object)(object)Player.RightController != (Object)null)
		{
			return Player.RightController.GetAButtonUp();
		}
		return false;
	}

	public static bool BL_IsBButtonDown()
	{
		if ((Object)(object)Player.RightController != (Object)null)
		{
			return Player.RightController.GetBButton();
		}
		return false;
	}

	public static bool BL_IsBButtonDownOnce()
	{
		if ((Object)(object)Player.RightController != (Object)null)
		{
			return Player.RightController.GetBButtonDown();
		}
		return false;
	}

	public static bool BL_IsBButtonUpOnce()
	{
		if ((Object)(object)Player.RightController != (Object)null)
		{
			return Player.RightController.GetBButtonUp();
		}
		return false;
	}

	public static bool BL_IsXButtonDown()
	{
		if ((Object)(object)Player.LeftController != (Object)null)
		{
			return Player.LeftController.GetAButton();
		}
		return false;
	}

	public static bool BL_IsXButtonDownOnce()
	{
		if ((Object)(object)Player.LeftController != (Object)null)
		{
			return Player.LeftController.GetAButtonDown();
		}
		return false;
	}

	public static BaseController BL_GetLeftController()
	{
		if ((Object)(object)Player.LeftController != (Object)null)
		{
			return Player.LeftController;
		}
		return null;
	}

	public static BaseController BL_GetRightController()
	{
		if ((Object)(object)Player.RightController != (Object)null)
		{
			return Player.RightController;
		}
		return null;
	}

	public static bool BL_IsXButtonUpOnce()
	{
		if ((Object)(object)Player.LeftController != (Object)null)
		{
			return Player.LeftController.GetAButtonUp();
		}
		return false;
	}

	public static bool BL_IsYButtonDown()
	{
		if ((Object)(object)Player.LeftController != (Object)null)
		{
			return Player.LeftController.GetBButton();
		}
		return false;
	}

	public static bool BL_IsYButtonDownOnce()
	{
		if ((Object)(object)Player.LeftController != (Object)null)
		{
			return Player.LeftController.GetBButtonDown();
		}
		return false;
	}

	public static bool BL_IsYButtonUpOnce()
	{
		if ((Object)(object)Player.LeftController != (Object)null)
		{
			return Player.LeftController.GetBButtonUp();
		}
		return false;
	}

	public static GameObject BL_LeftHand()
	{
		if ((Object)(object)Player.LeftHand != (Object)null)
		{
			return ((Component)Player.LeftHand).gameObject;
		}
		return null;
	}

	public static GameObject BL_RightHand()
	{
		if ((Object)(object)Player.RightHand != (Object)null)
		{
			return ((Component)Player.RightHand).gameObject;
		}
		return null;
	}

	public static bool BL_RightHandEmpty()
	{
		if ((Object)(object)Player.RightHand != (Object)null)
		{
			return (Object)(object)Player.GetComponentInHand<Component>(Player.RightHand) == (Object)null;
		}
		return true;
	}

	public static bool BL_LeftHandEmpty()
	{
		if ((Object)(object)Player.LeftHand != (Object)null)
		{
			return (Object)(object)Player.GetComponentInHand<Component>(Player.LeftHand) == (Object)null;
		}
		return true;
	}

	public static bool BL_LeftController_IsGrabbed()
	{
		if ((Object)(object)Player.LeftController != (Object)null)
		{
			return Player.LeftController.GetGrabbedState();
		}
		return false;
	}

	public static bool BL_RightController_IsGrabbed()
	{
		if ((Object)(object)Player.RightController != (Object)null)
		{
			return Player.RightController.GetGrabbedState();
		}
		return false;
	}

	public static bool BL_IsKeyDown(int keyCodeArg)
	{
		return Input.GetKey((KeyCode)keyCodeArg);
	}
}
