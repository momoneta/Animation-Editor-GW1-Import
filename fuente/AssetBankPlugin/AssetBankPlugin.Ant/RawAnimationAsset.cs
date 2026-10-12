using System;
using System.Collections.Generic;
using System.Numerics;
using AssetBankPlugin.Export;

namespace AssetBankPlugin.Ant;

public class RawAnimationAsset : AnimationAsset
{
	public ushort[] KeyTimes { get; set; }

	public ushort[] MappingIndices { get; set; }

	public ushort[] ChannelIndices { get; set; }

	public float[] Data { get; set; }

	public float[] ConstData { get; set; }

	public uint NumKeys { get; set; }

	public ushort FloatCount { get; set; }

	public ushort Vec3Count { get; set; }

	public ushort QuatCount { get; set; }

	public ushort ConstFloatCount { get; set; }

	public ushort ConstVec3Count { get; set; }

	public ushort ConstQuatCount { get; set; }

	public bool Cycle { get; set; }

	public override void SetData(Dictionary<string, object> data)
	{
		base.SetData(data);
		KeyTimes = GetPropertyArray<ushort>("KeyTimes");
		MappingIndices = GetPropertyArray<ushort>("MappingIndices");
		ChannelIndices = GetPropertyArray<ushort>("ChannelIndices");
		Data = GetPropertyArray<float>("Data");
		ConstData = GetPropertyArray<float>("ConstData");
		NumKeys = GetProperty("NumKeys", 0u);
		Cycle = GetProperty("Cycle", defaultValue: false);
		FloatCount = GetProperty("FloatCount", (ushort)0);
		Vec3Count = GetProperty("Vec3Count", (ushort)0);
		QuatCount = GetProperty("QuatCount", (ushort)0);
		ConstFloatCount = GetProperty("ConstFloatCount", (ushort)0);
		ConstVec3Count = GetProperty("ConstVec3Count", (ushort)0);
		ConstQuatCount = GetProperty("ConstQuatCount", (ushort)0);
	}

	public override InternalAnimation ConvertToInternal()
	{
		InternalAnimation internalAnimation = new InternalAnimation();
		List<ChannelInfo> orderedChannels = OrderedChannels;
		if (orderedChannels == null || orderedChannels.Count == 0)
		{
			return internalAnimation;
		}
		List<string> list = new List<string>();
		List<string> list2 = new List<string>();
		List<string> list3 = new List<string>();
		foreach (ChannelInfo item in orderedChannels)
		{
			if (item.Type == BoneChannelType.Rotation)
			{
				list2.Add(item.Name);
			}
			else if (item.Type == BoneChannelType.Position)
			{
				list.Add(item.Name);
			}
			else if (item.Type == BoneChannelType.Scale)
			{
				list3.Add(item.Name);
			}
		}
		int count = orderedChannels.Count;
		int[] array = new int[count];
		int[] array2 = new int[count];
		int[] array3 = new int[count];
		for (int i = 0; i < count; i++)
		{
			array[i] = -1;
			array2[i] = -1;
			array3[i] = -1;
		}
		int num = 0;
		int num2 = 0;
		int num3 = 0;
		for (int j = 0; j < count; j++)
		{
			switch (orderedChannels[j].Type)
			{
			case BoneChannelType.Rotation:
				array[j] = num++;
				break;
			case BoneChannelType.Position:
				array2[j] = num2++;
				break;
			case BoneChannelType.Scale:
				array3[j] = num3++;
				break;
			}
		}
		Quaternion[] array4 = new Quaternion[list2.Count];
		Vector3[] array5 = new Vector3[list.Count];
		Vector3[] array6 = new Vector3[list3.Count];
		for (int k = 0; k < count; k++)
		{
			ChannelInfo channelInfo = orderedChannels[k];
			if (channelInfo.Type == BoneChannelType.Rotation)
			{
				int num4 = array[k];
				if (num4 != -1)
				{
					Vector4 value;
					if (Additive)
					{
						array4[num4] = Quaternion.Identity;
					}
					else if (TryGetRigDefaultVector4(channelInfo.DofId, out value))
					{
						array4[num4] = Quaternion.Normalize(new Quaternion(value.X, value.Y, value.Z, value.W));
					}
					else
					{
						array4[num4] = Quaternion.Identity;
					}
				}
			}
			else if (channelInfo.Type == BoneChannelType.Position)
			{
				int num5 = array2[k];
				if (num5 != -1)
				{
					Vector3 value2;
					if (Additive)
					{
						array5[num5] = Vector3.Zero;
					}
					else if (TryGetRigDefaultVector3(channelInfo.DofId, out value2))
					{
						array5[num5] = value2;
					}
					else
					{
						array5[num5] = Vector3.Zero;
					}
				}
			}
			else
			{
				if (channelInfo.Type != BoneChannelType.Scale)
				{
					continue;
				}
				int num6 = array3[k];
				if (num6 != -1)
				{
					Vector3 value3;
					if (Additive)
					{
						array6[num6] = Vector3.Zero;
					}
					else if (TryGetRigDefaultVector3(channelInfo.DofId, out value3))
					{
						array6[num6] = value3;
					}
					else
					{
						array6[num6] = Vector3.One;
					}
				}
			}
		}
		bool flag = MappingIndices != null && MappingIndices.Length != 0;
		ushort[] array7 = MappingIndices;
		if (!flag)
		{
			array7 = new ushort[count];
			ushort num7 = 0;
			for (int l = 0; l < count; l++)
			{
				if (orderedChannels[l].Type == BoneChannelType.Rotation)
				{
					array7[num7++] = (ushort)l;
				}
			}
			for (int m = 0; m < count; m++)
			{
				if (orderedChannels[m].Type == BoneChannelType.Position || orderedChannels[m].Type == BoneChannelType.Scale)
				{
					array7[num7++] = (ushort)m;
				}
			}
			for (int n = 0; n < count; n++)
			{
				if (orderedChannels[n].Type == BoneChannelType.None)
				{
					array7[num7++] = (ushort)n;
				}
			}
		}
		int num8 = QuatCount + Vec3Count + FloatCount;
		if (flag && ConstData != null && ConstData.Length != 0)
		{
			int num9 = 0;
			int num10 = num8;
			for (int num11 = 0; num11 < ConstQuatCount; num11++)
			{
				if (num9 + 3 >= ConstData.Length)
				{
					break;
				}
				int num12 = array7[num10++];
				int num13 = array[num12];
				if (num13 != -1)
				{
					array4[num13] = Quaternion.Normalize(new Quaternion(ConstData[num9], ConstData[num9 + 1], ConstData[num9 + 2], ConstData[num9 + 3]));
				}
				num9 += 4;
			}
			for (int num14 = 0; num14 < ConstVec3Count; num14++)
			{
				if (num9 + 2 >= ConstData.Length)
				{
					break;
				}
				int num15 = array7[num10++];
				int num16 = array2[num15];
				int num17 = array3[num15];
				Vector3 vector = new Vector3(ConstData[num9], ConstData[num9 + 1], ConstData[num9 + 2]);
				if (num16 != -1)
				{
					array5[num16] = vector;
				}
				if (num17 != -1)
				{
					array6[num17] = vector;
				}
				num9 += 4;
			}
		}
		if (Data != null && Data.Length != 0 && KeyTimes != null && KeyTimes.Length != 0)
		{
			int num18 = (int)Math.Min(NumKeys, KeyTimes.Length);
			internalAnimation.Frames = new List<Frame>(num18);
			int num19 = 4;
			int num20 = QuatCount * 4 + Vec3Count * num19 + FloatCount;
			int num21 = (flag ? ((num20 + 3) & -4) : num20);
			for (int num22 = 0; num22 < num18; num22++)
			{
				Frame frame = new Frame
				{
					FrameIndex = KeyTimes[num22],
					Rotations = new List<Quaternion>(array4),
					Positions = new List<Vector3>(array5),
					Scales = new List<Vector3>(array6)
				};
				int num23 = num22 * num21;
				int num24 = 0;
				for (int num25 = 0; num25 < QuatCount; num25++)
				{
					if (num23 + 3 >= Data.Length)
					{
						break;
					}
					int num26 = array7[num24++];
					int num27 = array[num26];
					if (num27 != -1)
					{
						frame.Rotations[num27] = Quaternion.Normalize(new Quaternion(Data[num23], Data[num23 + 1], Data[num23 + 2], Data[num23 + 3]));
					}
					num23 += 4;
				}
				for (int num28 = 0; num28 < Vec3Count; num28++)
				{
					if (num23 + 2 >= Data.Length)
					{
						break;
					}
					int num29 = array7[num24++];
					int num30 = array2[num29];
					int num31 = array3[num29];
					Vector3 value4 = new Vector3(Data[num23], Data[num23 + 1], Data[num23 + 2]);
					if (num30 != -1)
					{
						frame.Positions[num30] = value4;
					}
					if (num31 != -1)
					{
						frame.Scales[num31] = value4;
					}
					num23 += num19;
				}
				internalAnimation.Frames.Add(frame);
			}
		}
		for (int num32 = 0; num32 < list2.Count; num32++)
		{
			list2[num32] = list2[num32].Replace(".q", "");
		}
		for (int num33 = 0; num33 < list.Count; num33++)
		{
			list[num33] = list[num33].Replace(".t", "");
		}
		for (int num34 = 0; num34 < list3.Count; num34++)
		{
			list3[num34] = list3[num34].Replace(".s", "");
		}
		internalAnimation.Name = Name;
		internalAnimation.PositionChannels = list;
		internalAnimation.RotationChannels = list2;
		internalAnimation.ScaleChannels = list3;
		internalAnimation.Additive = Additive;
		return internalAnimation;
	}
}
