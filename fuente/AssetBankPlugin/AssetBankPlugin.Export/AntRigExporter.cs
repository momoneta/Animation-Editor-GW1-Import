using System;
using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using AssetBankPlugin.Ant;
using Frosty.Core;
using FrostySdk;
using FrostySdk.Ebx;
using FrostySdk.Managers.Entries;

namespace AssetBankPlugin.Export;

public static class AntRigExporter
{
	public static InternalSkeleton BuildInternalSkeletonFromRig(RigAsset rig, string originalSkelPath = "")
	{
		if (rig == null)
		{
			return null;
		}
		if (string.IsNullOrEmpty(originalSkelPath))
		{
			AnimationOptions animationOptions = new AnimationOptions();
			animationOptions.Load();
			originalSkelPath = animationOptions.ExportSkeletonAsset;
		}
		Dictionary<uint, Quaternion> dictionary = new Dictionary<uint, Quaternion>();
		if (rig.DefaultVector4Values != null)
		{
			foreach (DefaultDofVector4 defaultVector4Value in rig.DefaultVector4Values)
			{
				if (defaultVector4Value.Value != null && defaultVector4Value.Value.Length >= 4)
				{
					dictionary[defaultVector4Value.DofId] = new Quaternion(defaultVector4Value.Value[0], defaultVector4Value.Value[1], defaultVector4Value.Value[2], defaultVector4Value.Value[3]);
				}
			}
		}
		Dictionary<uint, Vector3> dictionary2 = new Dictionary<uint, Vector3>();
		if (rig.DefaultVector3Values != null)
		{
			foreach (DefaultDofVector3 defaultVector3Value in rig.DefaultVector3Values)
			{
				if (defaultVector3Value.Value != null && defaultVector3Value.Value.Length >= 3)
				{
					dictionary2[defaultVector3Value.DofId] = new Vector3(defaultVector3Value.Value[0], defaultVector3Value.Value[1], defaultVector3Value.Value[2]);
				}
			}
		}
		Dictionary<string, Quaternion> dictionary3 = new Dictionary<string, Quaternion>(StringComparer.OrdinalIgnoreCase);
		Dictionary<string, Vector3> dictionary4 = new Dictionary<string, Vector3>(StringComparer.OrdinalIgnoreCase);
		Dictionary<string, Vector3> dictionary5 = new Dictionary<string, Vector3>(StringComparer.OrdinalIgnoreCase);
		if (rig.DofIds != null && rig.RigDofSets != null)
		{
			int num = 0;
			for (int i = 0; i < rig.RigDofSets.Length; i++)
			{
				AntAsset antAsset = AntRefTable.Get(rig.RigDofSets[i]);
				if (antAsset == null || !(antAsset.AssetType == "DofSetAsset") || !(antAsset.GetProperty<object>("Slots") is IEnumerable enumerable))
				{
					continue;
				}
				uint num2 = ((rig.DofSetIdIndices != null && i < rig.DofSetIdIndices.Length) ? rig.DofSetIdIndices[i] : ((uint)num));
				int num3 = 0;
				foreach (object item3 in enumerable)
				{
					string text = "";
					if (item3 is Dictionary<string, object> dictionary6)
					{
						text = (dictionary6.TryGetValue("Name", out var value) ? value.ToString() : "");
					}
					else if (item3 is AntAsset antAsset2)
					{
						text = antAsset2.Name;
					}
					uint num4 = num2 + (uint)num3;
					if (num4 < rig.DofIds.Length)
					{
						uint key = rig.DofIds[num4];
						if (!string.IsNullOrEmpty(text))
						{
							int num5 = text.LastIndexOf('.');
							if (num5 > 0)
							{
								string key2 = text.Substring(0, num5);
								char c = text[num5 + 1];
								Vector3 value3;
								Vector3 value4;
								if (c == 'q' && dictionary.TryGetValue(key, out var value2))
								{
									dictionary3[key2] = value2;
								}
								else if (c == 't' && dictionary2.TryGetValue(key, out value3))
								{
									dictionary4[key2] = value3;
								}
								else if (c == 's' && dictionary2.TryGetValue(key, out value4))
								{
									dictionary5[key2] = value4;
								}
							}
						}
					}
					num3++;
				}
				num += num3;
			}
		}
		List<string> list = new List<string>();
		List<int> list2 = new List<int>();
		List<Transform> list3 = new List<Transform>();
		Dictionary<string, int> dictionary7 = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
		EbxAssetEntry ebxAssetEntry = ((!string.IsNullOrEmpty(originalSkelPath)) ? App.AssetManager.GetEbxEntry(originalSkelPath) : null);
		if (ebxAssetEntry != null)
		{
			dynamic rootObject = App.AssetManager.GetEbx(ebxAssetEntry).RootObject;
			for (int j = 0; j < rootObject.BoneNames.Count; j++)
			{
				string text2 = rootObject.BoneNames[j].ToString();
				list.Add(text2);
				list2.Add((int)rootObject.Hierarchy[j]);
				dynamic val = rootObject.LocalPose[j];
				Vector3 position = new Vector3((float)val.trans.x, (float)val.trans.y, (float)val.trans.z);
				Quaternion rotation = Quaternion.CreateFromRotationMatrix(new Matrix4x4((float)val.right.x, (float)val.right.y, (float)val.right.z, 0f, (float)val.up.x, (float)val.up.y, (float)val.up.z, 0f, (float)val.forward.x, (float)val.forward.y, (float)val.forward.z, 0f, 0f, 0f, 0f, 1f));
				list3.Add(new Transform(position, rotation, Vector3.One));
				dictionary7[text2] = j;
			}
		}
		List<string> list4 = new List<string>();
		List<int> list5 = new List<int>();
		List<Transform> list6 = new List<Transform>();
		AntAsset antAsset3 = AntRefTable.Get(rig.Skeleton);
		if (antAsset3 != null && antAsset3.GetProperty<object>("Joints") is IEnumerable enumerable2)
		{
			foreach (object item4 in enumerable2)
			{
				string text3 = "unknown";
				int item = -1;
				if (item4 is Dictionary<string, object> dictionary8)
				{
					if (dictionary8.TryGetValue("JointName", out var value5))
					{
						text3 = value5.ToString();
					}
					if (dictionary8.TryGetValue("ParentIndex", out var value6))
					{
						item = Convert.ToInt32(value6);
					}
				}
				else if (item4 is AntAsset antAsset4)
				{
					text3 = antAsset4.Name;
					item = antAsset4.GetProperty("ParentIndex", -1);
				}
				list4.Add(text3);
				list5.Add(item);
				Quaternion value7;
				Quaternion rotation2 = (dictionary3.TryGetValue(text3, out value7) ? value7 : Quaternion.Identity);
				Vector3 value8;
				Vector3 position2 = (dictionary4.TryGetValue(text3, out value8) ? value8 : Vector3.Zero);
				Vector3 value9;
				Vector3 scale = (dictionary5.TryGetValue(text3, out value9) ? value9 : Vector3.One);
				list6.Add(new Transform(position2, rotation2, scale));
			}
		}
		for (int k = 0; k < list.Count; k++)
		{
			string key3 = list[k];
			Transform transform = list3[k];
			Quaternion value10;
			Quaternion rotation3 = (dictionary3.TryGetValue(key3, out value10) ? value10 : transform.Rotation);
			Vector3 value11;
			Vector3 position3 = (dictionary4.TryGetValue(key3, out value11) ? value11 : transform.Position);
			Vector3 value12;
			Vector3 scale2 = (dictionary5.TryGetValue(key3, out value12) ? value12 : transform.Scale);
			list3[k] = new Transform(position3, rotation3, scale2);
		}
		for (int l = 0; l < list4.Count; l++)
		{
			string text4 = list4[l];
			if (dictionary7.ContainsKey(text4))
			{
				continue;
			}
			int num6 = list5[l];
			int item2 = -1;
			if (num6 >= 0 && num6 < list4.Count)
			{
				string key4 = list4[num6];
				if (dictionary7.TryGetValue(key4, out var value13))
				{
					item2 = value13;
				}
			}
			int count = list.Count;
			list.Add(text4);
			list2.Add(item2);
			list3.Add(list6[l]);
			dictionary7[text4] = count;
		}
		int count2 = list.Count;
		if (count2 == 0)
		{
			return null;
		}
		List<Transform> list7 = new List<Transform>(count2);
		Matrix4x4[] array = new Matrix4x4[count2];
		for (int m = 0; m < count2; m++)
		{
			int num7 = list2[m];
			Transform transform2 = list3[m];
			Matrix4x4 matrix4x = Matrix4x4.CreateScale(transform2.Scale) * Matrix4x4.CreateFromQuaternion(transform2.Rotation) * Matrix4x4.CreateTranslation(transform2.Position);
			list7.Add(new Transform(array[m] = ((num7 < 0 || num7 >= m) ? matrix4x : (matrix4x * array[num7]))));
		}
		return new InternalSkeleton(rig.Name, list, list2, list7, list3);
	}

	public static dynamic CreateEbxSkeletonFromInternal(InternalSkeleton internalSkel)
	{
		dynamic val = TypeLibrary.CreateObject("SkeletonAsset");
		val.Name = internalSkel.Name;
		val.BoneNames.Clear();
		val.Hierarchy.Clear();
		val.LocalPose.Clear();
		val.ModelPose.Clear();
		Type type = val.Hierarchy.GetType().GetGenericArguments()[0];
		for (int i = 0; i < internalSkel.BoneNames.Count; i++)
		{
			val.BoneNames.Add(new CString(internalSkel.BoneNames[i]));
			int num = internalSkel.BoneParents[i];
			if (type == typeof(short))
			{
				val.Hierarchy.Add((short)num);
			}
			else
			{
				val.Hierarchy.Add(num);
			}
			Transform xform = internalSkel.LocalTransforms[i];
			val.LocalPose.Add(ToLinearTransform(xform));
			Transform xform2 = internalSkel.BoneTransforms[i];
			val.ModelPose.Add(ToLinearTransform(xform2));
		}
		return val;
	}

	private static dynamic ToLinearTransform(Transform xform)
	{
		dynamic val = TypeLibrary.CreateObject("LinearTransform");
		Matrix4x4 matrix = xform.Matrix;
		val.right = TypeLibrary.CreateObject("Vec3");
		val.right.x = matrix.M11;
		val.right.y = matrix.M12;
		val.right.z = matrix.M13;
		val.up = TypeLibrary.CreateObject("Vec3");
		val.up.x = matrix.M21;
		val.up.y = matrix.M22;
		val.up.z = matrix.M23;
		val.forward = TypeLibrary.CreateObject("Vec3");
		val.forward.x = matrix.M31;
		val.forward.y = matrix.M32;
		val.forward.z = matrix.M33;
		val.trans = TypeLibrary.CreateObject("Vec3");
		val.trans.x = xform.Position.X;
		val.trans.y = xform.Position.Y;
		val.trans.z = xform.Position.Z;
		return val;
	}
}
