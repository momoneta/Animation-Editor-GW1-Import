using System.IO;
using System.Numerics;
using SELib;

namespace AssetBankPlugin.Export;

public class AnimationExporterSEANIM : IAnimationExporter
{
	public override void Export(InternalAnimation animation, InternalSkeleton skeleton, string path)
	{
		Directory.CreateDirectory(path);
		FileStream stream = File.Open(path + "\\" + animation.Name + ".seanim", FileMode.Create);
		SEAnim sEAnim = new SEAnim();
		for (int i = 0; i < animation.Frames.Count; i++)
		{
			for (int j = 0; j < animation.Frames[i].Positions.Count; j++)
			{
				System.Numerics.Vector3 vector = animation.Frames[i].Positions[j];
				sEAnim.AddTranslationKey(animation.PositionChannels[j], animation.Frames[i].FrameIndex, vector.X, vector.Y, vector.Z);
			}
			for (int k = 0; k < animation.Frames[i].Scales.Count; k++)
			{
				System.Numerics.Vector3 vector2 = animation.Frames[i].Scales[k];
				sEAnim.AddScaleKey(animation.ScaleChannels[k], animation.Frames[i].FrameIndex, vector2.X, vector2.Y, vector2.Z);
			}
			for (int l = 0; l < animation.Frames[i].Rotations.Count; l++)
			{
				System.Numerics.Quaternion quaternion = animation.Frames[i].Rotations[l];
				sEAnim.AddRotationKey(animation.RotationChannels[l], animation.Frames[i].FrameIndex, quaternion.X, quaternion.Y, quaternion.Z, quaternion.W);
			}
		}
		sEAnim.AnimType = (animation.Additive ? AnimationType.Additive : AnimationType.Absolute);
		sEAnim.Write(stream, HighPrecision: false);
	}
}
