using System.Collections.Generic;
using System.Numerics;

namespace AssetBankPlugin.Export;

public class Frame
{
	public int FrameIndex;

	public List<Vector3> Positions = new List<Vector3>();

	public List<Vector3> Scales = new List<Vector3>();

	public List<Quaternion> Rotations = new List<Quaternion>();
}
