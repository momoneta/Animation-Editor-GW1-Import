using System.IO;
using System.Numerics;

namespace AssetBankPlugin.Export;

public class AnimationExporterSMD : IAnimationExporter
{
	public override void Export(InternalAnimation animation, InternalSkeleton skeleton, string path)
	{
		Directory.CreateDirectory(path);
		using StreamWriter streamWriter = new StreamWriter(File.Open(path + "\\" + animation.Name + ".smd", FileMode.Create));
		streamWriter.WriteLine("// " + animation.Name);
		streamWriter.WriteLine($"// Additive: {animation.Additive}");
		streamWriter.WriteLine($"// Frame Count: {animation.Frames.Count}");
		streamWriter.WriteLine("version 1");
		if (skeleton == null)
		{
			return;
		}
		streamWriter.WriteLine("nodes");
		for (int i = 0; i < skeleton.BoneTransforms.Count; i++)
		{
			streamWriter.WriteLine($"{i} \"{skeleton.BoneNames[i]}\" {skeleton.BoneParents[i]}");
		}
		streamWriter.WriteLine("end");
		streamWriter.WriteLine("skeleton\ntime 0");
		for (int j = 0; j < animation.Frames.Count; j++)
		{
			if (animation.Frames.Count >= 2 && j >= 1)
			{
				for (int k = 0; k < animation.Frames[j].FrameIndex - animation.Frames[j - 1].FrameIndex; k++)
				{
					streamWriter.WriteLine($"time {animation.Frames[j - 1].FrameIndex + k + 1}");
				}
			}
			for (int l = 0; l < skeleton.BoneNames.Count; l++)
			{
				int num = l;
				int num2 = l;
				if (animation.PositionChannels.Count > 0)
				{
					num = animation.RotationChannels.IndexOf(skeleton.BoneNames[l]);
					num2 = animation.PositionChannels.IndexOf(skeleton.BoneNames[l]);
				}
				Transform transform = skeleton.LocalTransforms[l];
				Vector3 vector = transform.EulerAngles;
				Vector3 vector2 = transform.Position;
				if (num != -1)
				{
					vector = Transform.ToEulerAngles(animation.Frames[j].Rotations[num]);
				}
				if (num2 != -1)
				{
					vector2 = animation.Frames[j].Positions[num2];
				}
				streamWriter.WriteLine($"{l} {vector2.X} {vector2.Y} {vector2.Z} {vector.X} {vector.Y} {vector.Z}");
			}
		}
		streamWriter.WriteLine("end");
	}
}
