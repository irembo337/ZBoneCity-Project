using MelonLoader;
using MoonSharp.Interpreter;
using UnityEngine;

namespace LuaMod.LuaAPI;

public class API_Particles
{
	public static readonly API_Particles Instance = new API_Particles();

	private static Vector3[] trailsegments = (Vector3[])(object)new Vector3[500];

	public static Particle[] BL_GetParticles(ParticleSystem system, int size, int offset)
	{
		MelonLogger.Warning("Calling GetParticles causes Crash To Desktop - bug report open with Melonloader team");
		return null;
	}

	public Vector3[] CreateTrailSegmentArray(int size)
	{
		return (Vector3[])(object)new Vector3[size];
	}

	public static DynValue BL_lineRenderer_GetPositions(LineRenderer LR)
	{
		MelonLogger.Warning("Calling GetPositions causes Crash To Desktop - bug report open with Melonloader team");
		return null;
	}
}
