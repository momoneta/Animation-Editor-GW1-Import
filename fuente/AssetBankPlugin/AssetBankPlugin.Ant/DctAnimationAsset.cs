using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using AssetBankPlugin.Export;

namespace AssetBankPlugin.Ant;

public class DctAnimationAsset : AnimationAsset
{
	public ushort[] KeyTimes = new ushort[0];

	public byte[] Data = new byte[0];

	public ushort NumKeys;

	public ushort NumVec3;

	public ushort NumFloat;

	public int DataSize;

	public bool Cycle;

	public ushort NumQuats;

	public ushort NumFloatVec;

	public ushort QuantizeMultBlock;

	public byte QuantizeMultSubblock;

	public byte CatchAllBitCount;

	public byte[] DofTableDescBytes;

	public short[] DeltaBaseX;

	public short[] DeltaBaseY;

	public short[] DeltaBaseZ;

	public short[] DeltaBaseW;

	public ushort[] BitsPerSubblock;

	private List<Vector4> DecompressedData = new List<Vector4>();

	public static float[,] DctCoeffs = new float[8, 8]
	{
		{ 0.25f, 0.245196313f, 0.230969876f, 0.2078674f, 0.176776692f, 0.138892546f, 0.09567086f, 0.04877256f },
		{ 0.25f, 0.2078674f, 0.09567086f, -0.04877258f, -0.176776692f, -0.245196328f, -0.230969876f, -0.1388925f },
		{ 0.25f, 0.138892546f, -0.09567088f, -0.245196328f, -0.176776662f, 0.0487726f, 0.230969891f, 0.207867339f },
		{ 0.25f, 0.04877256f, -0.2309699f, -0.1388925f, 0.176776752f, 0.207867339f, -0.09567098f, -0.2451963f },
		{ 0.25f, -0.04877258f, -0.230969876f, 0.1388926f, 0.176776692f, -0.207867444f, -0.09567074f, 0.245196357f },
		{ 0.25f, -0.138892591f, -0.09567078f, 0.245196313f, -0.176776811f, -0.04877255f, 0.230969831f, -0.2078674f },
		{ 0.25f, -0.207867414f, 0.0956709f, 0.04877252f, -0.176776633f, 0.245196328f, -0.230969936f, 0.138892785f },
		{ 0.25f, -0.245196328f, 0.230969891f, -0.207867444f, 0.1767767f, -0.13889271f, 0.09567098f, -0.04877289f }
	};

	public override void SetData(Dictionary<string, object> data)
	{
		Name = (string)data["__name"];
		ID = (Guid)data["__guid"];
		KeyTimes = data["KeyTimes"] as ushort[];
		Data = data["Data"] as byte[];
		NumKeys = (ushort)data["NumKeys"];
		NumVec3 = (ushort)data["NumVec3"];
		NumFloat = (ushort)data["NumFloat"];
		DataSize = (int)data["DataSize"];
		Cycle = (bool)data["Cycle"];
		NumQuats = (ushort)data["NumQuats"];
		NumFloatVec = (ushort)data["NumFloatVec"];
		QuantizeMultBlock = (ushort)data["QuantizeMultBlock"];
		QuantizeMultSubblock = (byte)data["QuantizeMultSubblock"];
		CatchAllBitCount = (byte)data["CatchAllBitCount"];
		DofTableDescBytes = data["DofTableDescBytes"] as byte[];
		DeltaBaseX = data["DeltaBaseX"] as short[];
		DeltaBaseY = data["DeltaBaseY"] as short[];
		DeltaBaseZ = data["DeltaBaseZ"] as short[];
		DeltaBaseW = data["DeltaBaseW"] as short[];
		BitsPerSubblock = data["BitsPerSubblock"] as ushort[];
		DecompressedData = Decompress();
		base.SetData(data);
	}

	public override InternalAnimation ConvertToInternal()
	{
		InternalAnimation internalAnimation = new InternalAnimation();
		List<string> list = new List<string>();
		List<string> list2 = new List<string>();
		List<string> list3 = new List<string>();
		foreach (KeyValuePair<string, BoneChannelType> channel in Channels)
		{
			if (channel.Value == BoneChannelType.Rotation)
			{
				list2.Add(channel.Key);
			}
			else if (channel.Value == BoneChannelType.Position)
			{
				list.Add(channel.Key);
			}
			else if (channel.Value == BoneChannelType.Scale)
			{
				list3.Add(channel.Key);
			}
		}
		int num = NumQuats + NumVec3 + NumFloatVec;
		for (int i = 0; i < KeyTimes.Length; i++)
		{
			Frame frame = new Frame();
			List<Quaternion> list4 = new List<Quaternion>();
			List<Vector3> list5 = new List<Vector3>();
			for (int j = 0; j < NumQuats; j++)
			{
				int index = i * num + j;
				Vector4 vector = DecompressedData[index];
				list4.Add(Quaternion.Normalize(new Quaternion(vector.X, vector.Y, vector.Z, vector.W)));
			}
			for (int k = 0; k < NumVec3; k++)
			{
				if (Channels.ElementAt(NumQuats + k).Value == BoneChannelType.Position)
				{
					int index2 = i * num + NumQuats + k;
					Vector4 vector2 = DecompressedData[index2];
					list5.Add(new Vector3(vector2.X, vector2.Y, vector2.Z));
				}
			}
			frame.Rotations = list4;
			frame.Positions = list5;
			internalAnimation.Frames.Add(frame);
		}
		for (int l = 0; l < KeyTimes.Length; l++)
		{
			Frame frame2 = internalAnimation.Frames[l];
			frame2.FrameIndex = KeyTimes[l];
			internalAnimation.Frames[l] = frame2;
		}
		for (int m = 0; m < list2.Count; m++)
		{
			list2[m] = list2[m].Replace(".q", "");
		}
		for (int n = 0; n < list.Count; n++)
		{
			list[n] = list[n].Replace(".t", "");
		}
		for (int num2 = 0; num2 < list3.Count; num2++)
		{
			list3[num2] = list3[num2].Replace(".s", "");
		}
		internalAnimation.Name = Name;
		internalAnimation.PositionChannels = list;
		internalAnimation.RotationChannels = list2;
		internalAnimation.Additive = Additive;
		return internalAnimation;
	}

	public float[] GenerateCoeffs(ushort frame)
	{
		float[] array = new float[8];
		int num = frame % 8;
		for (int i = 0; i < 8; i++)
		{
			float num2 = DctCoeffs[num, i];
			float num3 = ((float)(int)QuantizeMultSubblock * 0.1f * (float)i + 1f) / (float)(int)QuantizeMultBlock;
			float num4 = num2 * num3;
			array[i] = num4;
		}
		return array;
	}

	public Vector4 UnpackVec(List<short> values, ushort frame)
	{
		Vector4 result = new Vector4(0f);
		float[] array = GenerateCoeffs(frame);
		for (int i = 0; i < 8; i++)
		{
			Vector4 left = new Vector4(values[i * 4], values[i * 4 + 1], values[i * 4 + 2], values[i * 4 + 3]);
			result += Vector4.Multiply(left, array[i]);
		}
		return result;
	}

	public List<Vector4> Decompress()
	{
		int num = NumVec3 + NumQuats + NumFloatVec;
		DofTable[] array = new DofTable[num];
		int num2 = 0;
		for (int i = 0; i < num; i++)
		{
			byte b = (byte)((DofTableDescBytes[i] >> 4) & 0xF);
			DofTable dofTable = new DofTable(b);
			dofTable.DeltaBase = new short[4]
			{
				DeltaBaseX[i],
				DeltaBaseY[i],
				DeltaBaseZ[i],
				DeltaBaseW[i]
			};
			dofTable.BitsPerSubBlock = new DofTable.BitsPerComponent[dofTable.SubBlockCount];
			for (int j = 0; j < dofTable.SubBlockCount; j++)
			{
				dofTable.BitsPerSubBlock[j] = new DofTable.BitsPerComponent(BitsPerSubblock[num2 + j]);
			}
			array[i] = dofTable;
			num2 += b;
		}
		BitReader bitReader = new BitReader(new MemoryStream(Data), 64);
		List<List<short>> list = new List<List<short>>();
		for (int k = 0; k < (NumKeys + 7) / 8; k++)
		{
			for (int l = 0; l < array.Length; l++)
			{
				List<short> list2 = new List<short>();
				DofTable dofTable2 = array[l];
				DofTable.BitsPerComponent[] array2 = dofTable2.BitsPerSubBlock;
				if (k == 0)
				{
					list2.Add(0);
					list2.Add(0);
					list2.Add(0);
					list2.Add(0);
					array2 = array2.Skip(1).ToArray();
				}
				DofTable.BitsPerComponent[] array3 = array2;
				foreach (DofTable.BitsPerComponent bitsPerComponent in array3)
				{
					long num3 = bitReader.ReadIntHigh(bitsPerComponent.SafeBitsX(CatchAllBitCount));
					long num4 = bitReader.ReadIntHigh(bitsPerComponent.SafeBitsY(CatchAllBitCount));
					long num5 = bitReader.ReadIntHigh(bitsPerComponent.SafeBitsZ(CatchAllBitCount));
					long num6 = bitReader.ReadIntHigh(bitsPerComponent.SafeBitsW(CatchAllBitCount));
					list2.Add((short)num3);
					list2.Add((short)num4);
					list2.Add((short)num5);
					list2.Add((short)num6);
				}
				if (array2.Length < 8)
				{
					for (int n = 0; n < 8 - array2.Length; n++)
					{
						list2.Add(0);
						list2.Add(0);
						list2.Add(0);
						list2.Add(0);
					}
				}
				list2[0] += dofTable2.DeltaBase[0];
				list2[1] += dofTable2.DeltaBase[1];
				list2[2] += dofTable2.DeltaBase[2];
				list2[3] += dofTable2.DeltaBase[3];
				list.Add(list2);
			}
		}
		List<Vector4> list3 = new List<Vector4>();
		for (int num7 = 0; num7 < NumKeys; num7++)
		{
			int num8 = num7 / 8;
			for (int num9 = 0; num9 < array.Length; num9++)
			{
				int num10 = num8 * array.Length + num9;
				if (num10 >= list.Count)
				{
					break;
				}
				List<short> values = list.ElementAt(num10);
				list3.Add(UnpackVec(values, (ushort)num7));
			}
		}
		return list3;
	}
}
