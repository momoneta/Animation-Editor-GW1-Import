using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;

namespace AssetBankPlugin.Export;

public class AnimationExporterCAST : IAnimationExporter
{
	public override void Export(InternalAnimation animation, InternalSkeleton skeleton, string path)
	{
		Directory.CreateDirectory(path);
		string path2 = Path.Combine(path, animation.Name + ".cast");
		Cast cast = new Cast();
		RootNode rootNode = cast.CreateRoot();
		MetadataNode metadataNode = rootNode.CreateMetadata();
		metadataNode.SetSoftware("Frosty Engine Animation Exporter");
		metadataNode.SetUpAxis("Y");
		AnimationNode animationNode = rootNode.CreateAnimation();
		animationNode.SetName(animation.Name);
		animationNode.SetFramerate(30f);
		animationNode.SetLooping(enabled: false);
		List<Frame> list = animation.Frames.OrderBy((Frame f) => f.FrameIndex).ToList();
		if (list.Count == 0)
		{
			return;
		}
		List<uint> keyFrameBuffer = list.Select((Frame f) => (uint)f.FrameIndex).ToList();
		for (int num = 0; num < animation.RotationChannels.Count; num++)
		{
			string nodeName = animation.RotationChannels[num];
			CurveNode curveNode = animationNode.CreateCurve();
			curveNode.SetNodeName(nodeName);
			curveNode.SetKeyPropertyName("rq");
			curveNode.SetMode(animation.Additive ? "additive" : "absolute");
			curveNode.SetKeyFrameBuffer(keyFrameBuffer);
			List<float[]> list2 = new List<float[]>(list.Count);
			foreach (Frame item in list)
			{
				if (num < item.Rotations.Count)
				{
					Quaternion quaternion = item.Rotations[num];
					list2.Add(new float[4] { quaternion.X, quaternion.Y, quaternion.Z, quaternion.W });
				}
				else
				{
					list2.Add(new float[4] { 0f, 0f, 0f, 1f });
				}
			}
			curveNode.SetVec4KeyValueBuffer(list2);
		}
		string[] array = new string[3] { "tx", "ty", "tz" };
		string[] array2 = new string[3] { "sx", "sy", "sz" };
		for (int num2 = 0; num2 < animation.PositionChannels.Count; num2++)
		{
			string nodeName2 = animation.PositionChannels[num2];
			for (int num3 = 0; num3 < 3; num3++)
			{
				CurveNode curveNode2 = animationNode.CreateCurve();
				curveNode2.SetNodeName(nodeName2);
				curveNode2.SetKeyPropertyName(array[num3]);
				curveNode2.SetMode(animation.Additive ? "additive" : "absolute");
				curveNode2.SetKeyFrameBuffer(keyFrameBuffer);
				List<float> list3 = new List<float>(list.Count);
				foreach (Frame item2 in list)
				{
					if (num2 < item2.Positions.Count)
					{
						Vector3 vector = item2.Positions[num2];
						switch (num3)
						{
						case 0:
							list3.Add(vector.X);
							break;
						case 1:
							list3.Add(vector.Y);
							break;
						case 2:
							list3.Add(vector.Z);
							break;
						}
					}
					else
					{
						list3.Add(0f);
					}
				}
				curveNode2.SetFloatKeyValueBuffer(list3);
			}
		}
		for (int num4 = 0; num4 < animation.ScaleChannels.Count; num4++)
		{
			string nodeName3 = animation.ScaleChannels[num4];
			for (int num5 = 0; num5 < 3; num5++)
			{
				CurveNode curveNode3 = animationNode.CreateCurve();
				curveNode3.SetNodeName(nodeName3);
				curveNode3.SetKeyPropertyName(array2[num5]);
				curveNode3.SetMode(animation.Additive ? "additive" : "absolute");
				curveNode3.SetKeyFrameBuffer(keyFrameBuffer);
				List<float> list4 = new List<float>(list.Count);
				foreach (Frame item3 in list)
				{
					if (num4 < item3.Scales.Count)
					{
						Vector3 vector2 = item3.Scales[num4];
						switch (num5)
						{
						case 0:
							list4.Add(vector2.X);
							break;
						case 1:
							list4.Add(vector2.Y);
							break;
						case 2:
							list4.Add(vector2.Z);
							break;
						}
					}
					else
					{
						list4.Add(1f);
					}
				}
				curveNode3.SetFloatKeyValueBuffer(list4);
			}
		}
		cast.Save(path2);
	}
}
