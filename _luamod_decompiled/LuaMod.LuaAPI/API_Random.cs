using UnityEngine;

namespace LuaMod.LuaAPI;

public class API_Random
{
	public static readonly API_Random Instance = new API_Random();

	public int Seed
	{
		get
		{
			return Random.seed;
		}
		set
		{
			Random.seed = value;
		}
	}

	public float RangeFloat(float min, float max)
	{
		return Random.Range(min, max);
	}

	public int RangeInt(int min, int max)
	{
		return Random.Range(min, max);
	}

	public float Value()
	{
		return Random.value;
	}

	public bool Bool()
	{
		return Random.value > 0.5f;
	}

	public Quaternion Rotation()
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		return Random.rotation;
	}

	public Quaternion RotationUniform()
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		return Random.rotationUniform;
	}

	public Vector3 InsideUnitSphere()
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		return Random.insideUnitSphere;
	}

	public Vector2 InsideUnitCircle()
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		return Random.insideUnitCircle;
	}

	public Vector3 OnUnitSphere()
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		return Random.onUnitSphere;
	}

	public void InitState(int seed)
	{
		Random.InitState(seed);
	}

	public State GetState()
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		return Random.state;
	}

	public void SetState(State state)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		Random.state = state;
	}
}
