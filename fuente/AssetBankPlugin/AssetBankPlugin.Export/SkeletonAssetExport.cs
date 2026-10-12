using System.Collections.Generic;
using System.Numerics;

namespace AssetBankPlugin.Export;

public class SkeletonAssetExport
{
	public static InternalSkeleton ConvertToInternal(dynamic dbx)
	{
		List<string> list = new List<string>();
		List<Transform> list2 = new List<Transform>();
		List<Transform> list3 = new List<Transform>();
		foreach (dynamic item3 in dbx.BoneNames)
		{
			list.Add(item3);
		}
		for (int i = 0; i < dbx.Hierarchy.Count; i++)
		{
			Vector3 vector = new Vector3(dbx.ModelPose[i].right.x, dbx.ModelPose[i].right.y, dbx.ModelPose[i].right.z);
			Vector3 vector2 = new Vector3(dbx.ModelPose[i].up.x, dbx.ModelPose[i].up.y, dbx.ModelPose[i].up.z);
			Vector3 vector3 = new Vector3(dbx.ModelPose[i].forward.x, dbx.ModelPose[i].forward.y, dbx.ModelPose[i].forward.z);
			Vector3 position = new Vector3(dbx.ModelPose[i].trans.x, dbx.ModelPose[i].trans.y, dbx.ModelPose[i].trans.z);
			Matrix4x4 matrix = new Matrix4x4(vector.X, vector.Y, vector.Z, 0f, vector2.X, vector2.Y, vector2.Z, 0f, vector3.X, vector3.Y, vector3.Z, 0f, 0f, 0f, 0f, 1f);
			Transform item = new Transform(position, Quaternion.CreateFromRotationMatrix(matrix), Vector3.One);
			list2.Add(item);
		}
		for (int j = 0; j < dbx.Hierarchy.Count; j++)
		{
			Vector3 vector4 = new Vector3(dbx.LocalPose[j].right.x, dbx.LocalPose[j].right.y, dbx.LocalPose[j].right.z);
			Vector3 vector5 = new Vector3(dbx.LocalPose[j].up.x, dbx.LocalPose[j].up.y, dbx.LocalPose[j].up.z);
			Vector3 vector6 = new Vector3(dbx.LocalPose[j].forward.x, dbx.LocalPose[j].forward.y, dbx.LocalPose[j].forward.z);
			Vector3 position2 = new Vector3(dbx.LocalPose[j].trans.x, dbx.LocalPose[j].trans.y, dbx.LocalPose[j].trans.z);
			Matrix4x4 matrix2 = new Matrix4x4(vector4.X, vector4.Y, vector4.Z, 0f, vector5.X, vector5.Y, vector5.Z, 0f, vector6.X, vector6.Y, vector6.Z, 0f, 0f, 0f, 0f, 1f);
			Transform item2 = new Transform(position2, Quaternion.CreateFromRotationMatrix(matrix2), Vector3.One);
			list3.Add(item2);
		}
		return new InternalSkeleton(dbx.Name, list, dbx.Hierarchy, list2, list3);
	}
}
