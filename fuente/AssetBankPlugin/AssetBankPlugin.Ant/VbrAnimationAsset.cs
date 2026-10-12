using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using AssetBankPlugin.Export;
using FrostySdk.IO;

namespace AssetBankPlugin.Ant;

public class VbrAnimationAsset : AnimationAsset
{
	public byte[] Data = new byte[0];

	public ushort NumKeys;

	public ushort NumVec3;

	public ushort NumFloat;

	public ushort ConstFloatCount;

	public int DataSize;

	public bool Cycle;

	public float QuatMin;

	public float TrajMin;

	public float Vec3Min;

	public float FloatMin;

	public float QuatMax;

	public float TrajMax;

	public float Vec3Max;

	public float FloatMax;

	public float VectorOffsetScale;

	public float FloatOffsetScale;

	public float Dct;

	public ushort NumQuats;

	public ushort NumFloatVec;

	public ushort ConstChanMapSize;

	public ushort QuaternionCount;

	public ushort ConstPaletteSize;

	public ushort Vector3Count;

	public ushort ConstQuaternionCount;

	public ushort ConstVector3Count;

	public ushort VectorOffsetSize;

	public ushort FloatOffsetSize;

	public ushort CodecTypeID;

	public ushort EndFrame2;

	public ushort Flags;

	public int offset;

	public int FrameBlockSize;

	public ushort[] PaletteIndexes;

	public byte[] ConstChanMap;

	public byte[] VectorOffsets;

	public byte QuantizeMultSubblock;

	public byte CatchAllBitCount;

	public float[] ConstantPalette;

	public ushort[] FrameBlockSizes;

	public short[] DeltaBaseX;

	public short[] DeltaBaseY;

	public short[] DeltaBaseZ;

	public short[] DeltaBaseW;

	public ushort[] BitsPerSubblock;

	public ushort[] KeyTimes;

	public ushort QuantizeMultBlock;

	public ushort KeyTimeSize;

	private List<Vector4> DecompressedData;

	private const ushort eNoChannelMap = 8;

	public static float[,] DctCoeffs = new float[8, 8]
	{
		{ 0.125f, 0.245196313f, 0.230969876f, 0.2078674f, 0.176776692f, 0.138892546f, 0.09567086f, 0.04877256f },
		{ 0.125f, 0.2078674f, 0.09567086f, -0.04877258f, -0.176776692f, -0.245196328f, -0.230969876f, -0.1388925f },
		{ 0.125f, 0.138892546f, -0.09567088f, -0.245196328f, -0.176776662f, 0.0487726f, 0.230969891f, 0.207867339f },
		{ 0.125f, 0.04877256f, -0.2309699f, -0.1388925f, 0.176776752f, 0.207867339f, -0.09567098f, -0.2451963f },
		{ 0.125f, -0.04877258f, -0.230969876f, 0.1388926f, 0.176776692f, -0.207867444f, -0.09567074f, 0.245196357f },
		{ 0.125f, -0.138892591f, -0.09567078f, 0.245196313f, -0.176776811f, -0.04877255f, 0.230969831f, -0.2078674f },
		{ 0.125f, -0.207867414f, 0.0956709f, 0.04877252f, -0.176776633f, 0.245196328f, -0.230969936f, 0.138892785f },
		{ 0.125f, -0.245196328f, 0.230969891f, -0.207867444f, 0.1767767f, -0.13889271f, 0.09567098f, -0.04877289f }
	};

	private static readonly float[] LogVals = new float[8] { 0.693147f, 1.098612f, 1.386294f, 1.609438f, 1.791759f, 1.94591f, 2.079442f, 2.197225f };

	private (float rot, float trj, float tra)[] blockQuantParams;

	public void InvalidateDecompressedCache()
	{
		DecompressedData = null;
	}

	public override void SetData(Dictionary<string, object> data)
	{
		InvalidateDecompressedCache();
		ParseBasicData(data);
		if (data.TryGetValue("QuatMin", out var value))
		{
			QuatMin = Convert.ToSingle(value);
		}
		if (data.TryGetValue("TrajMin", out var value2))
		{
			TrajMin = Convert.ToSingle(value2);
		}
		if (data.TryGetValue("Vec3Min", out var value3))
		{
			Vec3Min = Convert.ToSingle(value3);
		}
		if (data.TryGetValue("FloatMin", out var value4))
		{
			FloatMin = Convert.ToSingle(value4);
		}
		if (data.TryGetValue("QuatMax", out var value5))
		{
			QuatMax = Convert.ToSingle(value5);
		}
		if (data.TryGetValue("TrajMax", out var value6))
		{
			TrajMax = Convert.ToSingle(value6);
		}
		if (data.TryGetValue("Vec3Max", out var value7))
		{
			Vec3Max = Convert.ToSingle(value7);
		}
		if (data.TryGetValue("FloatMax", out var value8))
		{
			FloatMax = Convert.ToSingle(value8);
		}
		if (data.TryGetValue("VectorOffsetScale", out var value9))
		{
			VectorOffsetScale = Convert.ToSingle(value9);
		}
		if (data.TryGetValue("FloatOffsetScale", out var value10))
		{
			FloatOffsetScale = Convert.ToSingle(value10);
		}
		if (data.TryGetValue("Dct", out var value11))
		{
			Dct = Convert.ToSingle(value11);
		}
		if (data.TryGetValue("QuaternionCount", out var value12))
		{
			QuaternionCount = Convert.ToUInt16(value12);
		}
		if (data.TryGetValue("Vector3Count", out var value13))
		{
			Vector3Count = Convert.ToUInt16(value13);
		}
		if (data.TryGetValue("ConstQuaternionCount", out var value14))
		{
			ConstQuaternionCount = Convert.ToUInt16(value14);
		}
		if (data.TryGetValue("ConstVector3Count", out var value15))
		{
			ConstVector3Count = Convert.ToUInt16(value15);
		}
		if (data.TryGetValue("NumKeys", out var value16))
		{
			NumKeys = Convert.ToUInt16(value16);
		}
		if (data.TryGetValue("ConstChanMapSize", out var value17))
		{
			ConstChanMapSize = Convert.ToUInt16(value17);
		}
		if (data.TryGetValue("ConstPaletteSize", out var value18))
		{
			ConstPaletteSize = Convert.ToUInt16(value18);
		}
		if (data.TryGetValue("VectorOffsetSize", out var value19))
		{
			VectorOffsetSize = Convert.ToUInt16(value19);
		}
		if (data.TryGetValue("FloatOffsetSize", out var value20))
		{
			FloatOffsetSize = Convert.ToUInt16(value20);
		}
		if (data.TryGetValue("Flags", out var value21))
		{
			Flags = Convert.ToUInt16(value21);
		}
		if (data.TryGetValue("ConstantPalette", out var value22))
		{
			ConstantPalette = ConvertArray<float>(value22);
		}
		if (data.TryGetValue("FrameBlockSizes", out var value23))
		{
			FrameBlockSizes = ConvertArray<ushort>(value23);
		}
		if (data.TryGetValue("Data", out var value24))
		{
			Data = ConvertArray<byte>(value24);
		}
		if (data.TryGetValue("KeyTimeSize", out var value25))
		{
			KeyTimeSize = Convert.ToUInt16(value25);
		}
		else
		{
			KeyTimeSize = 0;
		}
		if (FrameBlockSizes != null)
		{
			FrameBlockSize = FrameBlockSizes.Length;
		}
		object value27;
		if (data.TryGetValue("FloatCount", out var value26))
		{
			NumFloat = Convert.ToUInt16(value26);
		}
		else if (data.TryGetValue("NumFloat", out value27))
		{
			NumFloat = Convert.ToUInt16(value27);
		}
		if (data.TryGetValue("ConstFloatCount", out var value28))
		{
			ConstFloatCount = Convert.ToUInt16(value28);
		}
		base.SetData(data);
	}

	public override InternalAnimation ConvertToInternal()
	{
		try
		{
			if (DecompressedData == null || DecompressedData.Count == 0)
			{
				DecompressedData = Decompress();
			}
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
			int num4 = QuaternionCount + Vector3Count + NumFloat;
			Quaternion[] array4 = new Quaternion[list2.Count];
			Vector3[] array5 = new Vector3[list.Count];
			Vector3[] array6 = new Vector3[list3.Count];
			for (int k = 0; k < count; k++)
			{
				ChannelInfo channelInfo = orderedChannels[k];
				if (channelInfo.Type == BoneChannelType.Rotation)
				{
					int num5 = array[k];
					if (num5 != -1)
					{
						Vector4 value;
						if (Additive)
						{
							array4[num5] = Quaternion.Identity;
						}
						else if (TryGetRigDefaultVector4(channelInfo.DofId, out value))
						{
							array4[num5] = Quaternion.Normalize(new Quaternion(value.X, value.Y, value.Z, value.W));
						}
						else
						{
							array4[num5] = Quaternion.Identity;
						}
					}
				}
				else if (channelInfo.Type == BoneChannelType.Position)
				{
					int num6 = array2[k];
					if (num6 != -1)
					{
						Vector3 value2;
						if (Additive)
						{
							array5[num6] = Vector3.Zero;
						}
						else if (TryGetRigDefaultVector3(channelInfo.DofId, out value2))
						{
							array5[num6] = value2;
						}
						else
						{
							array5[num6] = Vector3.Zero;
						}
					}
				}
				else
				{
					if (channelInfo.Type != BoneChannelType.Scale)
					{
						continue;
					}
					int num7 = array3[k];
					if (num7 != -1)
					{
						Vector3 value3;
						if (Additive)
						{
							array6[num7] = Vector3.Zero;
						}
						else if (TryGetRigDefaultVector3(channelInfo.DofId, out value3))
						{
							array6[num7] = value3;
						}
						else
						{
							array6[num7] = Vector3.One;
						}
					}
				}
			}
			int orderedIdx;
			int offset;
			int count2;
			if ((Flags & 8) != 0)
			{
				Console.WriteLine("[VBR] eNoChannelMap active - direct mapping.");
				List<int> list4 = new List<int>();
				List<int> list5 = new List<int>();
				for (int l = 0; l < count; l++)
				{
					ChannelInfo channelInfo2 = orderedChannels[l];
					if (channelInfo2.Type == BoneChannelType.Rotation)
					{
						list4.Add(l);
					}
					else if (channelInfo2.Type == BoneChannelType.Position || channelInfo2.Type == BoneChannelType.Scale)
					{
						list5.Add(l);
					}
				}
				internalAnimation.Frames = new List<Frame>(NumKeys);
				for (int m = 0; m < NumKeys; m++)
				{
					Frame frame = new Frame();
					frame.Rotations = array4.ToList();
					frame.Positions = array5.ToList();
					frame.Scales = array6.ToList();
					int num8 = m * num4;
					for (int n = 0; n < QuaternionCount && n < list4.Count; n++)
					{
						int num9 = list4[n];
						Vector4 vector = DecompressedData[num8 + n];
						int num10 = array[num9];
						if (num10 != -1 && num10 < frame.Rotations.Count)
						{
							frame.Rotations[num10] = Quaternion.Normalize(new Quaternion(vector.X, vector.Y, vector.Z, vector.W));
						}
					}
					for (int num11 = 0; num11 < Vector3Count && num11 < list5.Count; num11++)
					{
						int num12 = list5[num11];
						Vector4 vector2 = DecompressedData[num8 + QuaternionCount + num11];
						int num13 = array2[num12];
						int num14 = array3[num12];
						if (num13 != -1 && num13 < frame.Positions.Count)
						{
							frame.Positions[num13] = new Vector3(vector2.X, vector2.Y, vector2.Z);
						}
						if (num14 != -1 && num14 < frame.Scales.Count)
						{
							frame.Scales[num14] = new Vector3(vector2.X, vector2.Y, vector2.Z);
						}
					}
					frame.FrameIndex = m * 3;
					internalAnimation.Frames.Add(frame);
				}
			}
			else
			{
				List<int> list6 = new List<int>();
				List<int> list7 = new List<int>();
				List<int> list8 = new List<int>();
				List<int> list9 = new List<int>();
				List<int> list10 = new List<int>();
				List<int> list11 = new List<int>();
				if (ConstChanMap != null && ConstChanMap.Length != 0)
				{
					orderedIdx = ConstChanMap[0];
					offset = 1;
					count2 = 0;
					for (int num15 = 0; num15 < ConstQuaternionCount; num15++)
					{
						AdvanceConst();
						if (orderedIdx < count)
						{
							list6.Add(orderedIdx);
						}
						orderedIdx++;
						count2++;
					}
					for (int num16 = 0; num16 < ConstVector3Count; num16++)
					{
						AdvanceConst();
						if (orderedIdx < count)
						{
							list7.Add(orderedIdx);
						}
						orderedIdx++;
						count2++;
					}
					for (int num17 = 0; num17 < ConstFloatCount; num17++)
					{
						AdvanceConst();
						if (orderedIdx < count)
						{
							list8.Add(orderedIdx);
						}
						orderedIdx++;
						count2++;
					}
					orderedIdx = 0;
					offset = 0;
					count2 = 0;
					for (int num18 = 0; num18 < QuaternionCount; num18++)
					{
						AdvanceAnim();
						if (orderedIdx < count)
						{
							list9.Add(orderedIdx);
						}
						orderedIdx++;
						count2++;
					}
					for (int num19 = 0; num19 < Vector3Count; num19++)
					{
						AdvanceAnim();
						if (orderedIdx < count)
						{
							list10.Add(orderedIdx);
						}
						orderedIdx++;
						count2++;
					}
					for (int num20 = 0; num20 < NumFloat; num20++)
					{
						AdvanceAnim();
						if (orderedIdx < count)
						{
							list11.Add(orderedIdx);
						}
						orderedIdx++;
						count2++;
					}
				}
				else
				{
					for (int num21 = 0; num21 < count; num21++)
					{
						ChannelInfo channelInfo3 = orderedChannels[num21];
						if (channelInfo3.Type == BoneChannelType.Rotation)
						{
							if (list9.Count < QuaternionCount)
							{
								list9.Add(num21);
							}
							else if (list6.Count < ConstQuaternionCount)
							{
								list6.Add(num21);
							}
						}
						else if (channelInfo3.Type == BoneChannelType.Position || channelInfo3.Type == BoneChannelType.Scale)
						{
							if (list10.Count < Vector3Count)
							{
								list10.Add(num21);
							}
							else if (list7.Count < ConstVector3Count)
							{
								list7.Add(num21);
							}
						}
						else if (list11.Count < NumFloat)
						{
							list11.Add(num21);
						}
						else if (list8.Count < ConstFloatCount)
						{
							list8.Add(num21);
						}
					}
				}
				Vector4 vector3 = new Vector4(QuatMax - QuatMin);
				Vector4 vector4 = new Vector4(QuatMin);
				Vector3 vector5 = new Vector3(Vec3Max - Vec3Min);
				Vector3 vector6 = new Vector3(Vec3Min);
				internalAnimation.Frames = new List<Frame>(NumKeys);
				for (int num22 = 0; num22 < NumKeys; num22++)
				{
					Frame frame2 = new Frame();
					frame2.Rotations = array4.ToList();
					frame2.Positions = array5.ToList();
					frame2.Scales = array6.ToList();
					int num23 = 0;
					for (int num24 = 0; num24 < list6.Count; num24++)
					{
						int num25 = list6[num24];
						if (num23 + 3 >= PaletteIndexes.Length)
						{
							break;
						}
						int num26 = PaletteIndexes[num23];
						int num27 = PaletteIndexes[num23 + 1];
						int num28 = PaletteIndexes[num23 + 2];
						int num29 = PaletteIndexes[num23 + 3];
						if (num26 >= ConstantPalette.Length || num27 >= ConstantPalette.Length || num28 >= ConstantPalette.Length || num29 >= ConstantPalette.Length)
						{
							break;
						}
						Vector4 vector7 = new Vector4(ConstantPalette[num26], ConstantPalette[num27], ConstantPalette[num28], ConstantPalette[num29]);
						vector7 = vector7 * vector3 + vector4;
						int num30 = array[num25];
						if (num30 != -1 && num30 < frame2.Rotations.Count)
						{
							frame2.Rotations[num30] = Quaternion.Normalize(new Quaternion(vector7.X, vector7.Y, vector7.Z, vector7.W));
						}
						num23 += 4;
					}
					for (int num31 = 0; num31 < list9.Count; num31++)
					{
						int num32 = list9[num31];
						int num33 = num22 * num4 + num31;
						if (num33 >= DecompressedData.Count)
						{
							break;
						}
						Vector4 vector8 = DecompressedData[num33];
						int num34 = array[num32];
						if (num34 != -1 && num34 < frame2.Rotations.Count)
						{
							frame2.Rotations[num34] = Quaternion.Normalize(new Quaternion(vector8.X, vector8.Y, vector8.Z, vector8.W));
						}
					}
					for (int num35 = 0; num35 < list7.Count; num35++)
					{
						int num36 = list7[num35];
						if (num23 + 2 >= PaletteIndexes.Length)
						{
							break;
						}
						int num37 = PaletteIndexes[num23];
						int num38 = PaletteIndexes[num23 + 1];
						int num39 = PaletteIndexes[num23 + 2];
						if (num37 >= ConstantPalette.Length || num38 >= ConstantPalette.Length || num39 >= ConstantPalette.Length)
						{
							break;
						}
						Vector3 vector9 = new Vector3(ConstantPalette[num37], ConstantPalette[num38], ConstantPalette[num39]);
						vector9 = vector9 * vector5 + vector6;
						int num40 = array2[num36];
						int num41 = array3[num36];
						if (num40 != -1 && num40 < frame2.Positions.Count)
						{
							frame2.Positions[num40] = vector9;
						}
						if (num41 != -1 && num41 < frame2.Scales.Count)
						{
							frame2.Scales[num41] = vector9;
						}
						num23 += 3;
					}
					for (int num42 = 0; num42 < list10.Count; num42++)
					{
						int num43 = list10[num42];
						int num44 = num22 * num4 + QuaternionCount + num42;
						if (num44 >= DecompressedData.Count)
						{
							break;
						}
						Vector4 vector10 = DecompressedData[num44];
						int num45 = array2[num43];
						int num46 = array3[num43];
						if (num45 != -1 && num45 < frame2.Positions.Count)
						{
							frame2.Positions[num45] = new Vector3(vector10.X, vector10.Y, vector10.Z);
						}
						if (num46 != -1 && num46 < frame2.Scales.Count)
						{
							frame2.Scales[num46] = new Vector3(vector10.X, vector10.Y, vector10.Z);
						}
					}
					for (int num47 = 0; num47 < list8.Count; num47++)
					{
						if (num23 >= PaletteIndexes.Length)
						{
							break;
						}
						if (PaletteIndexes[num23] >= ConstantPalette.Length)
						{
							break;
						}
						num23++;
					}
					frame2.FrameIndex = num22;
					internalAnimation.Frames.Add(frame2);
				}
			}
			for (int num48 = 0; num48 < list2.Count; num48++)
			{
				list2[num48] = list2[num48].Replace(".q", "");
			}
			for (int num49 = 0; num49 < list.Count; num49++)
			{
				list[num49] = list[num49].Replace(".t", "");
			}
			for (int num50 = 0; num50 < list3.Count; num50++)
			{
				list3[num50] = list3[num50].Replace(".s", "");
			}
			internalAnimation.Name = Name;
			internalAnimation.PositionChannels = list;
			internalAnimation.RotationChannels = list2;
			internalAnimation.ScaleChannels = list3;
			internalAnimation.Additive = Additive;
			return internalAnimation;
			void AdvanceAnim()
			{
				if (offset < ConstChanMap.Length && count2 >= ConstChanMap[offset])
				{
					if (offset + 1 < ConstChanMap.Length)
					{
						orderedIdx += ConstChanMap[offset + 1];
					}
					offset += 2;
					count2 = 0;
					if (offset < ConstChanMap.Length && count2 >= ConstChanMap[offset])
					{
						if (offset + 1 < ConstChanMap.Length)
						{
							orderedIdx += ConstChanMap[offset + 1];
						}
						offset += 2;
						count2 = 0;
					}
				}
			}
			void AdvanceConst()
			{
				if (offset < ConstChanMap.Length && count2 >= ConstChanMap[offset])
				{
					if (offset + 1 < ConstChanMap.Length)
					{
						orderedIdx += ConstChanMap[offset + 1];
					}
					offset += 2;
					count2 = 0;
					if (offset < ConstChanMap.Length && count2 >= ConstChanMap[offset])
					{
						if (offset + 1 < ConstChanMap.Length)
						{
							orderedIdx += ConstChanMap[offset + 1];
						}
						offset += 2;
						count2 = 0;
					}
				}
			}
		}
		catch (Exception ex)
		{
			Console.WriteLine("Error in " + Name + ": " + ex.Message + "\n" + ex.StackTrace);
			return null;
		}
	}

	private float ComputeQuantScale(int coeffIdx, float scale, float currentDct)
	{
		return (scale * LogVals[coeffIdx] + 1f) * currentDct / 32768f;
	}

	private float UnpackVecWithQuant(short[] values, int coeffIdx, float[] quantScales, float delta, float minus)
	{
		float num = 0.5f;
		for (int i = 0; i < 8; i++)
		{
			num += (float)values[i] * DctCoeffs[coeffIdx, i] * quantScales[i];
		}
		return num * delta + minus;
	}

	private static byte ReverseBits(byte b)
	{
		b = (byte)((b >> 4) | (b << 4));
		b = (byte)(((b & 0xCC) >> 2) | ((b & 0x33) << 2));
		b = (byte)(((b & 0xAA) >> 1) | ((b & 0x55) << 1));
		return b;
	}

	public static uint ReverseBits(uint value, int bitLength)
	{
		uint num = 0u;
		for (int i = 0; i < bitLength; i++)
		{
			num = (num << 1) | (value & 1);
			value >>= 1;
		}
		return num;
	}

	public List<Vector4> Decompress()
	{
		int channelCount = QuaternionCount * 4 + Vector3Count * 3 + NumFloat;
		int num = QuaternionCount + Vector3Count + NumFloat;
		int num2 = ConstQuaternionCount * 4 + ConstVector3Count * 3 + ConstFloatCount;
		byte[] array = (byte[])Data.Clone();
		int num3 = 0;
		for (int i = 0; i < FrameBlockSize; i++)
		{
			num3 += FrameBlockSizes[i];
		}
		int num4 = array.Length - num3;
		int num5 = KeyTimeSize + num2 + ConstChanMapSize + channelCount * 4 + VectorOffsetSize + FloatOffsetSize;
		if (num5 != num4)
		{
			int num6 = num5;
			for (int j = 0; j < FrameBlockSize; j++)
			{
				int num7 = FrameBlockSizes[j];
				int num8 = num6 + 3;
				int num9 = num7 - 3;
				if (num8 + num9 <= array.Length && num9 > 0)
				{
					Array.Reverse(array, num8, num9);
				}
				num6 += num7;
			}
			num6 = num4;
			for (int k = 0; k < FrameBlockSize; k++)
			{
				int num10 = FrameBlockSizes[k];
				int num11 = num6 + 3;
				int num12 = num10 - 3;
				if (num11 + num12 <= array.Length && num12 > 0)
				{
					Array.Reverse(array, num11, num12);
				}
				num6 += num10;
			}
		}
		byte[] array2 = new byte[num3];
		byte[] array3 = new byte[num4];
		Array.Copy(array, num4, array2, 0, array2.Length);
		Array.Copy(array, 0, array3, 0, num4);
		int num13 = 0;
		num13 += KeyTimeSize;
		bool num14 = ConstPaletteSize <= 256;
		int num15 = (num14 ? 1 : 2);
		int num16 = num2 * num15;
		PaletteIndexes = new ushort[num2];
		if (num14)
		{
			for (int l = 0; l < num2; l++)
			{
				PaletteIndexes[l] = array3[num13 + l];
			}
		}
		else
		{
			for (int m = 0; m < num2; m++)
			{
				PaletteIndexes[m] = BitConverter.ToUInt16(array3, num13 + m * 2);
			}
		}
		num13 += num16;
		ConstChanMap = new byte[ConstChanMapSize];
		if (ConstChanMapSize > 0)
		{
			Array.Copy(array3, num13, ConstChanMap, 0, ConstChanMapSize);
			num13 += ConstChanMapSize;
		}
		int num17 = channelCount * 4;
		byte[] array4 = new byte[num17];
		Array.Copy(array3, num13, array4, 0, num17);
		num13 += num17;
		VectorOffsets = new byte[VectorOffsetSize + FloatOffsetSize];
		if (VectorOffsetSize + FloatOffsetSize > 0)
		{
			Array.Copy(array3, num13, VectorOffsets, 0, VectorOffsets.Length);
			num13 += VectorOffsets.Length;
		}
		byte[] array5 = new byte[array4.Length * 2];
		for (int n = 0; n < array4.Length; n++)
		{
			array5[n * 2] = (byte)((array4[n] >> 4) & 0xF);
			array5[n * 2 + 1] = (byte)(array4[n] & 0xF);
		}
		int num18 = 0;
		byte[][] array6 = new byte[FrameBlockSize][];
		blockQuantParams = new(float, float, float)[FrameBlockSize];
		for (int num19 = 0; num19 < FrameBlockSize; num19++)
		{
			array6[num19] = new byte[FrameBlockSizes[num19]];
			Array.Copy(array2, num18, array6[num19], 0, FrameBlockSizes[num19]);
			float item = (int)array6[num19][0];
			float item2 = (int)array6[num19][1];
			float item3 = (int)array6[num19][2];
			blockQuantParams[num19] = (rot: item, trj: item2, tra: item3);
			byte[] array7 = new byte[FrameBlockSizes[num19] - 3];
			Array.Copy(array6[num19], 3, array7, 0, array7.Length);
			Array.Reverse(array7);
			for (int num20 = 0; num20 < array7.Length; num20++)
			{
				array7[num20] = ReverseBits(array7[num20]);
			}
			array6[num19] = array7;
			num18 += FrameBlockSizes[num19];
		}
		List<short[]> list = new List<short[]>((NumKeys + 7) / 8 * channelCount);
		try
		{
			for (int num21 = 0; num21 < (NumKeys + 7) / 8; num21++)
			{
				BitReader bitReader = new BitReader(new MemoryStream(array6[num21]), 8, Endian.Little);
				for (int num22 = 0; num22 < channelCount; num22++)
				{
					short[] array8 = new short[8];
					int[] array9 = new int[8];
					int num23 = num22 * 8;
					array9[0] = array5[num23 + 1];
					array9[1] = array5[num23];
					array9[2] = array5[num23 + 3];
					array9[3] = array5[num23 + 2];
					array9[4] = array5[num23 + 5];
					array9[5] = array5[num23 + 4];
					array9[6] = array5[num23 + 7];
					array9[7] = array5[num23 + 6];
					int[] array10 = new int[8];
					int[] array11 = new int[8];
					for (int num24 = 0; num24 < 8; num24++)
					{
						if (array9[num24] > 0)
						{
							array10[num24] = (int)bitReader.ReadUIntHigh(1);
						}
					}
					for (int num25 = 0; num25 < 8; num25++)
					{
						if (array10[num25] > 0 && array9[num25] > 0)
						{
							array11[num25] = (int)bitReader.ReadUIntHigh(1);
						}
					}
					for (int num26 = 0; num26 < 8; num26++)
					{
						if (array10[num26] > 0)
						{
							short num27 = (short)ReverseBits((uint)bitReader.ReadUIntHigh(array9[num26]), array9[num26]);
							if (array11[num26] < 1)
							{
								num27 = (short)(-num27);
							}
							array8[num26] = num27;
						}
					}
					list.Add(array8);
				}
			}
		}
		catch (Exception ex)
		{
			Console.WriteLine("Error in " + Name + ": " + ex.Message);
		}
		float[][] array12 = new float[FrameBlockSize][];
		float[][] array13 = new float[FrameBlockSize][];
		float[][] array14 = new float[FrameBlockSize][];
		float num28 = ((Dct <= 0f) ? 4f : Dct);
		for (int num29 = 0; num29 < FrameBlockSize; num29++)
		{
			(float rot, float trj, float tra) tuple = blockQuantParams[num29];
			float item4 = tuple.rot;
			float item5 = tuple.trj;
			float item6 = tuple.tra;
			float scale = (item4 + 1f) * 0.2f * num28;
			float scale2 = (item5 + 1f) * 0.2f * num28;
			float scale3 = (item6 + 1f) * 0.2f * num28;
			array12[num29] = new float[8];
			array13[num29] = new float[8];
			array14[num29] = new float[8];
			for (int num30 = 0; num30 < 8; num30++)
			{
				array12[num29][num30] = ComputeQuantScale(num30, scale, num28);
				array13[num29][num30] = ComputeQuantScale(num30, scale2, num28);
				array14[num29][num30] = ComputeQuantScale(num30, scale3, num28);
			}
		}
		List<float> result = new List<float>(NumKeys * channelCount);
		int num31 = NumKeys % 8;
		int num32 = NumKeys - num31;
		for (int num33 = 0; num33 < NumKeys; num33++)
		{
			int num34 = num33 / 8;
			int coeffIdx = ((num33 >= num32 && num33 > 7) ? (num33 - (NumKeys - 8)) : (num33 % 8));
			float[] array15 = array12[num34];
			float[] array16 = array13[num34];
			float[] array17 = array14[num34];
			for (int num35 = 0; num35 < channelCount; num35++)
			{
				int num36 = num34 * channelCount + num35;
				if (num36 >= list.Count)
				{
					break;
				}
				short[] values = list[num36];
				int num37 = QuaternionCount * 4;
				int num38 = num37 + 3;
				int num39 = num37 + Vector3Count * 3;
				float[] quantScales;
				float delta;
				float minus;
				if (num35 < num37)
				{
					quantScales = array15;
					delta = QuatMax - QuatMin;
					minus = QuatMin;
				}
				else if (num35 < num38)
				{
					quantScales = array16;
					delta = TrajMax - TrajMin;
					minus = TrajMin;
				}
				else if (num35 < num39)
				{
					quantScales = array17;
					delta = Vec3Max - Vec3Min;
					minus = Vec3Min;
				}
				else
				{
					quantScales = array17;
					delta = FloatMax - FloatMin;
					minus = FloatMin;
				}
				result.Add(UnpackVecWithQuant(values, coeffIdx, quantScales, delta, minus));
			}
		}
		ApplyCurveSet(0, VectorOffsetScale, QuaternionCount * 4);
		ApplyCurveSet(VectorOffsetSize, FloatOffsetScale, QuaternionCount * 4 + Vector3Count * 4);
		List<Vector4> list2 = new List<Vector4>(NumKeys * num);
		for (int num40 = 0; num40 < NumKeys; num40++)
		{
			for (int num41 = 0; num41 < QuaternionCount; num41++)
			{
				int num42 = num41 * 4 + num40 * channelCount;
				if (num42 + 3 < result.Count)
				{
					list2.Add(new Vector4(result[num42], result[num42 + 1], result[num42 + 2], result[num42 + 3]));
				}
				else
				{
					list2.Add(new Vector4(0f));
				}
			}
			for (int num43 = 0; num43 < Vector3Count; num43++)
			{
				int num44 = num43 * 3 + num40 * channelCount + QuaternionCount * 4;
				if (num44 + 2 < result.Count)
				{
					list2.Add(new Vector4(result[num44], result[num44 + 1], result[num44 + 2], 0f));
				}
				else
				{
					list2.Add(new Vector4(0f));
				}
			}
			for (int num45 = 0; num45 < NumFloat; num45++)
			{
				int num46 = QuaternionCount * 4 + Vector3Count * 3 + num45 + num40 * channelCount;
				if (num46 < result.Count)
				{
					list2.Add(new Vector4(result[num46], 0f, 0f, 0f));
				}
				else
				{
					list2.Add(new Vector4(0f));
				}
			}
		}
		return list2;
		void ApplyCurveSet(int startOffset, float rawScale, int startChannel)
		{
			if (startOffset < VectorOffsets.Length && rawScale != 0f)
			{
				float num47 = rawScale / 127f;
				int num48 = startOffset;
				int num49 = VectorOffsets[num48++];
				int num50 = startChannel;
				for (int num51 = 0; num51 < num49; num51++)
				{
					if (num48 >= VectorOffsets.Length)
					{
						break;
					}
					int num52 = VectorOffsets[num48++];
					num50 += num52;
					int num53 = VectorOffsets[num48++];
					if (num53 == 0)
					{
						num50++;
					}
					else
					{
						int num54 = -1;
						int num55 = QuaternionCount * 4;
						int num56 = num55 + Vector3Count * 4;
						if (num50 >= num56)
						{
							num54 = QuaternionCount * 4 + Vector3Count * 3 + (num50 - num56);
						}
						else if (num50 >= num55)
						{
							int num57 = (num50 - num55) / 4;
							int num58 = (num50 - num55) % 4;
							if (num58 < 3)
							{
								num54 = QuaternionCount * 4 + num57 * 3 + num58;
							}
						}
						float num59 = (float)(sbyte)VectorOffsets[num48++] * num47;
						int num60 = 0;
						for (int num61 = 0; num61 < num53 - 1; num61++)
						{
							if (num48 >= VectorOffsets.Length)
							{
								break;
							}
							byte num62 = VectorOffsets[num48++];
							float num63 = (float)(sbyte)VectorOffsets[num48++] * num47;
							int num64 = num62 * 8;
							if (num54 != -1)
							{
								for (int num65 = 0; num65 < num64; num65++)
								{
									int num66 = num60 + num65;
									if (num66 >= NumKeys)
									{
										break;
									}
									float num67 = (float)num65 / (float)num64;
									int num68 = num66 * channelCount + num54;
									if (num68 < result.Count)
									{
										result[num68] += num59 + (num63 - num59) * num67;
									}
								}
							}
							num60 += num64;
							num59 = num63;
						}
						if (num54 != -1)
						{
							for (int num69 = num60; num69 < NumKeys; num69++)
							{
								int num70 = num69 * channelCount + num54;
								if (num70 < result.Count)
								{
									result[num70] += num59;
								}
							}
						}
					}
				}
			}
		}
	}
}
