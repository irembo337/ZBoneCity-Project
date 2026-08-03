using BoneLib;
using UnityEngine;

namespace LuaMod.LuaAPI;

public class API_Audio
{
	public static readonly API_Audio Instance = new API_Audio();

	public bool BL_Play3DOneShot(AudioClip Clip, Vector3 position, float volume = 1f, float pitch = 1f, float spatialBlend = 1f)
	{
		Audio.Play2DOneShot(Clip, Audio.SoftInteraction, volume, pitch);
		return true;
	}
}
