using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SELib;

public class SEAnim
{
	public string DeltaTagName;

	public Dictionary<string, List<SEAnimFrame>> AnimationPositionKeys { get; private set; }

	public Dictionary<string, List<SEAnimFrame>> AnimationRotationKeys { get; private set; }

	public Dictionary<string, List<SEAnimFrame>> AnimationScaleKeys { get; private set; }

	public Dictionary<string, List<SEAnimFrame>> AnimationNotetracks { get; private set; }

	public Dictionary<string, AnimationType> AnimationBoneModifiers { get; private set; }

	public int FrameCount => CalculateFrameCount();

	public int BoneCount => CalculateBoneCount();

	public List<string> Bones => BuildUniqueOrderedBoneMap();

	public int NotificationCount => CalculateNotetracksCount();

	public AnimationType AnimType { get; set; }

	public bool Looping { get; set; }

	public float FrameRate { get; set; }

	public string APIVersion => "v1.0.1";

	public SEAnim()
	{
		AnimationPositionKeys = new Dictionary<string, List<SEAnimFrame>>();
		AnimationRotationKeys = new Dictionary<string, List<SEAnimFrame>>();
		AnimationScaleKeys = new Dictionary<string, List<SEAnimFrame>>();
		AnimationNotetracks = new Dictionary<string, List<SEAnimFrame>>();
		AnimationBoneModifiers = new Dictionary<string, AnimationType>();
		AnimType = AnimationType.Absolute;
		Looping = false;
		DeltaTagName = string.Empty;
		FrameRate = 30f;
	}

	public void RemoveNotetrack(string NoteName)
	{
		if (AnimationNotetracks.ContainsKey(NoteName))
		{
			AnimationNotetracks.Remove(NoteName);
		}
	}

	public bool RenameNotetrack(string ExistingNote, string NewNote)
	{
		if (AnimationNotetracks.ContainsKey(NewNote))
		{
			return false;
		}
		if (AnimationNotetracks.ContainsKey(ExistingNote))
		{
			List<SEAnimFrame> value = AnimationNotetracks[ExistingNote];
			AnimationNotetracks.Remove(ExistingNote);
			AnimationNotetracks.Add(NewNote, value);
		}
		return true;
	}

	public void RemoveBone(string BoneName)
	{
		if (AnimationPositionKeys.ContainsKey(BoneName))
		{
			AnimationPositionKeys.Remove(BoneName);
		}
		if (AnimationRotationKeys.ContainsKey(BoneName))
		{
			AnimationRotationKeys.Remove(BoneName);
		}
		if (AnimationScaleKeys.ContainsKey(BoneName))
		{
			AnimationScaleKeys.Remove(BoneName);
		}
		if (AnimationBoneModifiers.ContainsKey(BoneName))
		{
			AnimationBoneModifiers.Remove(BoneName);
		}
		if (DeltaTagName == BoneName)
		{
			DeltaTagName = string.Empty;
		}
	}

	public bool RenameBone(string ExistingName, string NewName)
	{
		if (Bones.Contains(NewName))
		{
			return false;
		}
		if (AnimationPositionKeys.ContainsKey(ExistingName))
		{
			List<SEAnimFrame> value = AnimationPositionKeys[ExistingName];
			AnimationPositionKeys.Remove(ExistingName);
			AnimationPositionKeys.Add(NewName, value);
		}
		if (AnimationRotationKeys.ContainsKey(ExistingName))
		{
			List<SEAnimFrame> value2 = AnimationRotationKeys[ExistingName];
			AnimationRotationKeys.Remove(ExistingName);
			AnimationRotationKeys.Add(NewName, value2);
		}
		if (AnimationScaleKeys.ContainsKey(ExistingName))
		{
			List<SEAnimFrame> value3 = AnimationScaleKeys[ExistingName];
			AnimationScaleKeys.Remove(ExistingName);
			AnimationScaleKeys.Add(NewName, value3);
		}
		if (AnimationBoneModifiers.ContainsKey(ExistingName))
		{
			AnimationType value4 = AnimationBoneModifiers[ExistingName];
			AnimationBoneModifiers.Remove(ExistingName);
			AnimationBoneModifiers.Add(NewName, value4);
		}
		if (DeltaTagName == ExistingName)
		{
			DeltaTagName = NewName;
		}
		return true;
	}

	public static SEAnim Read(Stream Stream)
	{
		SEAnim sEAnim = new SEAnim();
		using ExtendedBinaryReader extendedBinaryReader = new ExtendedBinaryReader(Stream);
		char[] first = extendedBinaryReader.ReadChars(6);
		extendedBinaryReader.ReadInt16();
		extendedBinaryReader.ReadInt16();
		if (!first.SequenceEqual(new char[6] { 'S', 'E', 'A', 'n', 'i', 'm' }))
		{
			throw new Exception("Bad SEAnim file, magic was invalid");
		}
		sEAnim.AnimType = (AnimationType)extendedBinaryReader.ReadByte();
		byte b = extendedBinaryReader.ReadByte();
		sEAnim.Looping = Convert.ToBoolean(b & 1);
		byte b2 = extendedBinaryReader.ReadByte();
		byte b3 = extendedBinaryReader.ReadByte();
		extendedBinaryReader.BaseStream.Position += 2L;
		sEAnim.FrameRate = extendedBinaryReader.ReadSingle();
		int num = extendedBinaryReader.ReadInt32();
		int num2 = extendedBinaryReader.ReadInt32();
		byte b4 = extendedBinaryReader.ReadByte();
		extendedBinaryReader.BaseStream.Position += 3L;
		int num3 = extendedBinaryReader.ReadInt32();
		List<string> list = new List<string>();
		for (int i = 0; i < num2; i++)
		{
			list.Add(extendedBinaryReader.ReadNullTermString());
		}
		if (sEAnim.AnimType == AnimationType.Delta && list.Count > 0)
		{
			sEAnim.DeltaTagName = list[0];
		}
		for (int j = 0; j < b4; j++)
		{
			ushort index = ((num2 <= 255) ? extendedBinaryReader.ReadByte() : extendedBinaryReader.ReadUInt16());
			sEAnim.AnimationBoneModifiers.Add(list[index], (AnimationType)extendedBinaryReader.ReadByte());
		}
		foreach (string item in list)
		{
			extendedBinaryReader.ReadByte();
			if (Convert.ToBoolean(b2 & 1))
			{
				int num4 = 0;
				num4 = ((num <= 255) ? extendedBinaryReader.ReadByte() : ((num > 65535) ? extendedBinaryReader.ReadInt32() : extendedBinaryReader.ReadUInt16()));
				for (int k = 0; k < num4; k++)
				{
					int num5 = 0;
					num5 = ((num <= 255) ? extendedBinaryReader.ReadByte() : ((num > 65535) ? extendedBinaryReader.ReadInt32() : extendedBinaryReader.ReadUInt16()));
					double num6 = 0.0;
					double num7 = 0.0;
					double num8 = 0.0;
					if (Convert.ToBoolean(b3 & 1))
					{
						num6 = extendedBinaryReader.ReadDouble();
						num7 = extendedBinaryReader.ReadDouble();
						num8 = extendedBinaryReader.ReadDouble();
					}
					else
					{
						num6 = extendedBinaryReader.ReadSingle();
						num7 = extendedBinaryReader.ReadSingle();
						num8 = extendedBinaryReader.ReadSingle();
					}
					sEAnim.AddTranslationKey(item, num5, num6, num7, num8);
				}
			}
			if (Convert.ToBoolean(b2 & 2))
			{
				int num9 = 0;
				num9 = ((num <= 255) ? extendedBinaryReader.ReadByte() : ((num > 65535) ? extendedBinaryReader.ReadInt32() : extendedBinaryReader.ReadUInt16()));
				for (int l = 0; l < num9; l++)
				{
					int num10 = 0;
					num10 = ((num <= 255) ? extendedBinaryReader.ReadByte() : ((num > 65535) ? extendedBinaryReader.ReadInt32() : extendedBinaryReader.ReadUInt16()));
					double num11 = 0.0;
					double num12 = 0.0;
					double num13 = 0.0;
					double num14 = 0.0;
					if (Convert.ToBoolean(b3 & 1))
					{
						num11 = extendedBinaryReader.ReadDouble();
						num12 = extendedBinaryReader.ReadDouble();
						num13 = extendedBinaryReader.ReadDouble();
						num14 = extendedBinaryReader.ReadDouble();
					}
					else
					{
						num11 = extendedBinaryReader.ReadSingle();
						num12 = extendedBinaryReader.ReadSingle();
						num13 = extendedBinaryReader.ReadSingle();
						num14 = extendedBinaryReader.ReadSingle();
					}
					sEAnim.AddRotationKey(item, num10, num11, num12, num13, num14);
				}
			}
			if (!Convert.ToBoolean(b2 & 4))
			{
				continue;
			}
			int num15 = 0;
			num15 = ((num <= 255) ? extendedBinaryReader.ReadByte() : ((num > 65535) ? extendedBinaryReader.ReadInt32() : extendedBinaryReader.ReadUInt16()));
			for (int m = 0; m < num15; m++)
			{
				int num16 = 0;
				num16 = ((num <= 255) ? extendedBinaryReader.ReadByte() : ((num > 65535) ? extendedBinaryReader.ReadInt32() : extendedBinaryReader.ReadUInt16()));
				double num17 = 0.0;
				double num18 = 0.0;
				double num19 = 0.0;
				if (Convert.ToBoolean(b3 & 1))
				{
					num17 = extendedBinaryReader.ReadDouble();
					num18 = extendedBinaryReader.ReadDouble();
					num19 = extendedBinaryReader.ReadDouble();
				}
				else
				{
					num17 = extendedBinaryReader.ReadSingle();
					num18 = extendedBinaryReader.ReadSingle();
					num19 = extendedBinaryReader.ReadSingle();
				}
				sEAnim.AddScaleKey(item, num16, num17, num18, num19);
			}
		}
		for (int n = 0; n < num3; n++)
		{
			sEAnim.AddNoteTrack(Frame: (num > 255) ? ((num > 65535) ? extendedBinaryReader.ReadInt32() : extendedBinaryReader.ReadUInt16()) : extendedBinaryReader.ReadByte(), Notification: extendedBinaryReader.ReadNullTermString());
		}
		return sEAnim;
	}

	public static SEAnim Read(string FileName)
	{
		return Read(File.OpenRead(FileName));
	}

	public void Write(Stream Stream, bool HighPrecision)
	{
		using ExtendedBinaryWriter extendedBinaryWriter = new ExtendedBinaryWriter(Stream);
		extendedBinaryWriter.Write(new char[6] { 'S', 'E', 'A', 'n', 'i', 'm' });
		extendedBinaryWriter.Write((short)1);
		extendedBinaryWriter.Write((short)28);
		extendedBinaryWriter.Write((byte)AnimType);
		extendedBinaryWriter.Write(Looping ? ((byte)1) : ((byte)0));
		byte b = 0;
		if (AnimationPositionKeys.Count > 0)
		{
			b |= 1;
		}
		if (AnimationRotationKeys.Count > 0)
		{
			b |= 2;
		}
		if (AnimationScaleKeys.Count > 0)
		{
			b |= 4;
		}
		if (AnimationNotetracks.Count > 0)
		{
			b |= 0x40;
		}
		extendedBinaryWriter.Write(b);
		extendedBinaryWriter.Write(HighPrecision ? ((byte)1) : ((byte)0));
		extendedBinaryWriter.Write(new byte[2]);
		extendedBinaryWriter.Write(FrameRate);
		int frameCount = FrameCount;
		int boneCount = BoneCount;
		int notificationCount = NotificationCount;
		extendedBinaryWriter.Write(frameCount);
		extendedBinaryWriter.Write(boneCount);
		extendedBinaryWriter.Write((byte)AnimationBoneModifiers.Count);
		extendedBinaryWriter.Write(new byte[3]);
		extendedBinaryWriter.Write(notificationCount);
		List<string> list = BuildUniqueOrderedBoneMap();
		foreach (string item in list)
		{
			extendedBinaryWriter.WriteNullTermString(item);
		}
		foreach (KeyValuePair<string, AnimationType> animationBoneModifier in AnimationBoneModifiers)
		{
			if (boneCount <= 255)
			{
				extendedBinaryWriter.Write((byte)list.IndexOf(animationBoneModifier.Key));
			}
			else
			{
				extendedBinaryWriter.Write((short)list.IndexOf(animationBoneModifier.Key));
			}
			extendedBinaryWriter.Write((byte)animationBoneModifier.Value);
		}
		foreach (string item2 in list)
		{
			extendedBinaryWriter.Write((byte)0);
			if (AnimationPositionKeys.Count > 0)
			{
				if (AnimationPositionKeys.ContainsKey(item2))
				{
					if (frameCount <= 255)
					{
						extendedBinaryWriter.Write((byte)AnimationPositionKeys[item2].Count);
					}
					else if (frameCount <= 65535)
					{
						extendedBinaryWriter.Write((short)AnimationPositionKeys[item2].Count);
					}
					else
					{
						extendedBinaryWriter.Write(AnimationPositionKeys[item2].Count);
					}
					foreach (SEAnimFrame item3 in AnimationPositionKeys[item2])
					{
						if (frameCount <= 255)
						{
							extendedBinaryWriter.Write((byte)item3.Frame);
						}
						else if (frameCount <= 65535)
						{
							extendedBinaryWriter.Write((short)item3.Frame);
						}
						else
						{
							extendedBinaryWriter.Write(item3.Frame);
						}
						if (HighPrecision)
						{
							extendedBinaryWriter.Write(((Vector3)item3.Data).X);
							extendedBinaryWriter.Write(((Vector3)item3.Data).Y);
							extendedBinaryWriter.Write(((Vector3)item3.Data).Z);
						}
						else
						{
							extendedBinaryWriter.Write((float)((Vector3)item3.Data).X);
							extendedBinaryWriter.Write((float)((Vector3)item3.Data).Y);
							extendedBinaryWriter.Write((float)((Vector3)item3.Data).Z);
						}
					}
				}
				else if (frameCount <= 255)
				{
					extendedBinaryWriter.Write((byte)0);
				}
				else if (frameCount <= 65535)
				{
					extendedBinaryWriter.Write((short)0);
				}
				else
				{
					extendedBinaryWriter.Write(0);
				}
			}
			if (AnimationRotationKeys.Count > 0)
			{
				if (AnimationRotationKeys.ContainsKey(item2))
				{
					if (frameCount <= 255)
					{
						extendedBinaryWriter.Write((byte)AnimationRotationKeys[item2].Count);
					}
					else if (frameCount <= 65535)
					{
						extendedBinaryWriter.Write((short)AnimationRotationKeys[item2].Count);
					}
					else
					{
						extendedBinaryWriter.Write(AnimationRotationKeys[item2].Count);
					}
					foreach (SEAnimFrame item4 in AnimationRotationKeys[item2])
					{
						if (frameCount <= 255)
						{
							extendedBinaryWriter.Write((byte)item4.Frame);
						}
						else if (frameCount <= 65535)
						{
							extendedBinaryWriter.Write((short)item4.Frame);
						}
						else
						{
							extendedBinaryWriter.Write(item4.Frame);
						}
						if (HighPrecision)
						{
							extendedBinaryWriter.Write(((Quaternion)item4.Data).X);
							extendedBinaryWriter.Write(((Quaternion)item4.Data).Y);
							extendedBinaryWriter.Write(((Quaternion)item4.Data).Z);
							extendedBinaryWriter.Write(((Quaternion)item4.Data).W);
						}
						else
						{
							extendedBinaryWriter.Write((float)((Quaternion)item4.Data).X);
							extendedBinaryWriter.Write((float)((Quaternion)item4.Data).Y);
							extendedBinaryWriter.Write((float)((Quaternion)item4.Data).Z);
							extendedBinaryWriter.Write((float)((Quaternion)item4.Data).W);
						}
					}
				}
				else if (frameCount <= 255)
				{
					extendedBinaryWriter.Write((byte)0);
				}
				else if (frameCount <= 65535)
				{
					extendedBinaryWriter.Write((short)0);
				}
				else
				{
					extendedBinaryWriter.Write(0);
				}
			}
			if (AnimationScaleKeys.Count <= 0)
			{
				continue;
			}
			if (AnimationScaleKeys.ContainsKey(item2))
			{
				if (frameCount <= 255)
				{
					extendedBinaryWriter.Write((byte)AnimationScaleKeys[item2].Count);
				}
				else if (frameCount <= 65535)
				{
					extendedBinaryWriter.Write((short)AnimationScaleKeys[item2].Count);
				}
				else
				{
					extendedBinaryWriter.Write(AnimationScaleKeys[item2].Count);
				}
				foreach (SEAnimFrame item5 in AnimationScaleKeys[item2])
				{
					if (frameCount <= 255)
					{
						extendedBinaryWriter.Write((byte)item5.Frame);
					}
					else if (frameCount <= 65535)
					{
						extendedBinaryWriter.Write((short)item5.Frame);
					}
					else
					{
						extendedBinaryWriter.Write(item5.Frame);
					}
					if (HighPrecision)
					{
						extendedBinaryWriter.Write(((Vector3)item5.Data).X);
						extendedBinaryWriter.Write(((Vector3)item5.Data).Y);
						extendedBinaryWriter.Write(((Vector3)item5.Data).Z);
					}
					else
					{
						extendedBinaryWriter.Write((float)((Vector3)item5.Data).X);
						extendedBinaryWriter.Write((float)((Vector3)item5.Data).Y);
						extendedBinaryWriter.Write((float)((Vector3)item5.Data).Z);
					}
				}
			}
			else if (frameCount <= 255)
			{
				extendedBinaryWriter.Write((byte)0);
			}
			else if (frameCount <= 65535)
			{
				extendedBinaryWriter.Write((short)0);
			}
			else
			{
				extendedBinaryWriter.Write(0);
			}
		}
		if (AnimationNotetracks.Count <= 0)
		{
			return;
		}
		foreach (KeyValuePair<string, List<SEAnimFrame>> animationNotetrack in AnimationNotetracks)
		{
			foreach (SEAnimFrame item6 in animationNotetrack.Value)
			{
				if (frameCount <= 255)
				{
					extendedBinaryWriter.Write((byte)item6.Frame);
				}
				else if (frameCount <= 65535)
				{
					extendedBinaryWriter.Write((short)item6.Frame);
				}
				else
				{
					extendedBinaryWriter.Write(item6.Frame);
				}
				extendedBinaryWriter.WriteNullTermString(animationNotetrack.Key);
			}
		}
	}

	public void Write(string FileName, bool HighPrecision = false)
	{
		Write(File.Create(FileName), HighPrecision);
	}

	public void AddTranslationKey(string Bone, int Frame, double X, double Y, double Z)
	{
		if (!AnimationPositionKeys.ContainsKey(Bone))
		{
			AnimationPositionKeys.Add(Bone, new List<SEAnimFrame>());
		}
		AnimationPositionKeys[Bone].Add(new SEAnimFrame
		{
			Frame = Frame,
			Data = new Vector3
			{
				X = X,
				Y = Y,
				Z = Z
			}
		});
	}

	public void AddRotationKey(string Bone, int Frame, double X, double Y, double Z, double W)
	{
		if (!AnimationRotationKeys.ContainsKey(Bone))
		{
			AnimationRotationKeys.Add(Bone, new List<SEAnimFrame>());
		}
		AnimationRotationKeys[Bone].Add(new SEAnimFrame
		{
			Frame = Frame,
			Data = new Quaternion
			{
				X = X,
				Y = Y,
				Z = Z,
				W = W
			}
		});
	}

	public void AddScaleKey(string Bone, int Frame, double X, double Y, double Z)
	{
		if (!AnimationScaleKeys.ContainsKey(Bone))
		{
			AnimationScaleKeys.Add(Bone, new List<SEAnimFrame>());
		}
		AnimationScaleKeys[Bone].Add(new SEAnimFrame
		{
			Frame = Frame,
			Data = new Vector3
			{
				X = X,
				Y = Y,
				Z = Z
			}
		});
	}

	public void AddNoteTrack(string Notification, int Frame)
	{
		if (!AnimationNotetracks.ContainsKey(Notification))
		{
			AnimationNotetracks.Add(Notification, new List<SEAnimFrame>());
		}
		AnimationNotetracks[Notification].Add(new SEAnimFrame
		{
			Frame = Frame,
			Data = null
		});
	}

	public void AddBoneModifier(string Bone, AnimationType Modifier)
	{
		if (AnimationBoneModifiers.ContainsKey(Bone))
		{
			AnimationBoneModifiers[Bone] = Modifier;
		}
		else
		{
			AnimationBoneModifiers.Add(Bone, Modifier);
		}
	}

	public void RemoveTranslationKey(string Bone, int Frame)
	{
		if (!AnimationPositionKeys.ContainsKey(Bone))
		{
			return;
		}
		for (int i = 0; i < AnimationPositionKeys[Bone].Count; i++)
		{
			if (AnimationPositionKeys[Bone][i].Frame == Frame)
			{
				AnimationPositionKeys[Bone].RemoveAt(i);
				break;
			}
		}
	}

	public void RemoveRotationKey(string Bone, int Frame)
	{
		if (!AnimationRotationKeys.ContainsKey(Bone))
		{
			return;
		}
		for (int i = 0; i < AnimationRotationKeys[Bone].Count; i++)
		{
			if (AnimationRotationKeys[Bone][i].Frame == Frame)
			{
				AnimationRotationKeys[Bone].RemoveAt(i);
				break;
			}
		}
	}

	public void RemoveScaleKey(string Bone, int Frame)
	{
		if (!AnimationScaleKeys.ContainsKey(Bone))
		{
			return;
		}
		for (int i = 0; i < AnimationScaleKeys[Bone].Count; i++)
		{
			if (AnimationScaleKeys[Bone][i].Frame == Frame)
			{
				AnimationScaleKeys[Bone].RemoveAt(i);
				break;
			}
		}
	}

	public void RemoveNotetrack(string Notification, int Frame)
	{
		if (!AnimationNotetracks.ContainsKey(Notification))
		{
			return;
		}
		for (int i = 0; i < AnimationNotetracks[Notification].Count; i++)
		{
			if (AnimationNotetracks[Notification][i].Frame == Frame)
			{
				AnimationNotetracks[Notification].RemoveAt(i);
				break;
			}
		}
	}

	private List<string> BuildUniqueOrderedBoneMap()
	{
		List<string> list = new List<string>();
		foreach (string key in AnimationPositionKeys.Keys)
		{
			if (!list.Contains(key))
			{
				list.Add(key);
			}
		}
		foreach (string key2 in AnimationRotationKeys.Keys)
		{
			if (!list.Contains(key2))
			{
				list.Add(key2);
			}
		}
		foreach (string key3 in AnimationScaleKeys.Keys)
		{
			if (!list.Contains(key3))
			{
				list.Add(key3);
			}
		}
		foreach (string key4 in AnimationBoneModifiers.Keys)
		{
			if (!list.Contains(key4))
			{
				list.Add(key4);
			}
		}
		if (AnimType == AnimationType.Delta && list.Contains(DeltaTagName))
		{
			list.Remove(DeltaTagName);
			list.Insert(0, DeltaTagName);
		}
		return list;
	}

	private int CalculateNotetracksCount()
	{
		int num = 0;
		foreach (List<SEAnimFrame> value in AnimationNotetracks.Values)
		{
			num += value.Count;
		}
		return num;
	}

	private int CalculateFrameCount()
	{
		int num = 0;
		foreach (List<SEAnimFrame> value in AnimationPositionKeys.Values)
		{
			foreach (SEAnimFrame item in value)
			{
				num = Math.Max(num, item.Frame);
			}
		}
		foreach (List<SEAnimFrame> value2 in AnimationRotationKeys.Values)
		{
			foreach (SEAnimFrame item2 in value2)
			{
				num = Math.Max(num, item2.Frame);
			}
		}
		foreach (List<SEAnimFrame> value3 in AnimationScaleKeys.Values)
		{
			foreach (SEAnimFrame item3 in value3)
			{
				num = Math.Max(num, item3.Frame);
			}
		}
		foreach (List<SEAnimFrame> value4 in AnimationNotetracks.Values)
		{
			foreach (SEAnimFrame item4 in value4)
			{
				num = Math.Max(num, item4.Frame);
			}
		}
		return num + 1;
	}

	private int CalculateBoneCount()
	{
		HashSet<string> hashSet = new HashSet<string>();
		foreach (string key in AnimationPositionKeys.Keys)
		{
			hashSet.Add(key);
		}
		foreach (string key2 in AnimationRotationKeys.Keys)
		{
			hashSet.Add(key2);
		}
		foreach (string key3 in AnimationScaleKeys.Keys)
		{
			hashSet.Add(key3);
		}
		foreach (string key4 in AnimationBoneModifiers.Keys)
		{
			hashSet.Add(key4);
		}
		return hashSet.Count;
	}
}
