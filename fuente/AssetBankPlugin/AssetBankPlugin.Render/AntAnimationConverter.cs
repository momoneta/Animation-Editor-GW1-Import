using System.Collections.Generic;
using System.Numerics;
using AssetBankPlugin.Export;
using Frosty.Hash;
using MeshSetPlugin.Render;
using SharpDX;

namespace AssetBankPlugin.Render;

public static class AntAnimationConverter
{
	public static MeshRenderAnim Convert(InternalAnimation anim, int frameCount = -1)
	{
		if (frameCount < 0)
		{
			frameCount = ((anim.Frames.Count <= 0) ? 1 : (anim.Frames[anim.Frames.Count - 1].FrameIndex + 1));
		}
		MeshRenderAnim meshRenderAnim = new MeshRenderAnim(frameCount);
		Dictionary<string, MeshRenderAnim.Bone> boneDict = new Dictionary<string, MeshRenderAnim.Bone>();
		foreach (Frame frame in anim.Frames)
		{
			for (int i = 0; i < anim.RotationChannels.Count && i < frame.Rotations.Count; i++)
			{
				GetOrAdd(anim.RotationChannels[i]).Rotations.Add(new MeshRenderAnim.Keyframe<SharpDX.Quaternion>
				{
					FrameTime = frame.FrameIndex,
					Value = ToSharpDX(frame.Rotations[i])
				});
			}
			for (int j = 0; j < anim.PositionChannels.Count && j < frame.Positions.Count; j++)
			{
				GetOrAdd(anim.PositionChannels[j]).Translations.Add(new MeshRenderAnim.Keyframe<SharpDX.Vector3>
				{
					FrameTime = frame.FrameIndex,
					Value = ToSharpDX(frame.Positions[j])
				});
			}
			for (int k = 0; k < anim.ScaleChannels.Count && k < frame.Scales.Count; k++)
			{
				GetOrAdd(anim.ScaleChannels[k]).Scales.Add(new MeshRenderAnim.Keyframe<SharpDX.Vector3>
				{
					FrameTime = frame.FrameIndex,
					Value = ToSharpDX(frame.Scales[k])
				});
			}
		}
		meshRenderAnim.AddBones(boneDict.Values);
		return meshRenderAnim;
		MeshRenderAnim.Bone GetOrAdd(string name)
		{
			if (!boneDict.TryGetValue(name, out var value))
			{
				value = new MeshRenderAnim.Bone
				{
					NameHash = Fnv1.HashString(name)
				};
				boneDict[name] = value;
			}
			return value;
		}
	}

	private static SharpDX.Quaternion ToSharpDX(System.Numerics.Quaternion q)
	{
		return new SharpDX.Quaternion(q.X, q.Y, q.Z, q.W);
	}

	private static SharpDX.Vector3 ToSharpDX(System.Numerics.Vector3 v)
	{
		return new SharpDX.Vector3(v.X, v.Y, v.Z);
	}
}
