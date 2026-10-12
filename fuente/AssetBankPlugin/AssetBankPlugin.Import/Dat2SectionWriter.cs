using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace AssetBankPlugin.Import;

internal sealed class Dat2SectionWriter
{
	private struct HeapBlock
	{
		public int PtrOffsetInField;

		public int ElemAlignment;

		public byte[] Data;
	}

	private const ulong kIID = 51274245uL;

	private const int FieldDataStart = 44;

	private const int RelocBase = 12;

	private readonly bool _bigEndian;

	private readonly MemoryStream _fieldBuf = new MemoryStream(256);

	private readonly List<HeapBlock> _heapBlocks = new List<HeapBlock>();

	public Dat2SectionWriter(bool bigEndian)
	{
		_bigEndian = bigEndian;
	}

	public void WriteKey(ulong key)
	{
		EmitU64(key);
	}

	public void WriteUInt64(ulong v)
	{
		EmitU64(v);
	}

	public void WriteUInt32(uint v)
	{
		EmitU32(v);
	}

	public void WriteUInt16(ushort v)
	{
		EmitU16(v);
	}

	public void WriteByte(byte v)
	{
		_fieldBuf.WriteByte(v);
	}

	public void WriteFloat(float v)
	{
		EmitU32(FloatBits(v));
	}

	public void WriteBool(bool v)
	{
		_fieldBuf.WriteByte(v ? ((byte)1) : ((byte)0));
	}

	public void WritePad(int count)
	{
		for (int i = 0; i < count; i++)
		{
			_fieldBuf.WriteByte(0);
		}
	}

	public void PadToFieldOffset(int target)
	{
		int num = (int)_fieldBuf.Position;
		if (target > num)
		{
			WritePad(target - num);
		}
	}

	public void WriteString(string s)
	{
		byte[] bytes = Encoding.UTF8.GetBytes(s);
		byte[] array = new byte[bytes.Length + 1];
		Buffer.BlockCopy(bytes, 0, array, 0, bytes.Length);
		uint v = (uint)array.Length;
		EmitU32(v);
		EmitU32(v);
		ScheduleHeap(array, 1);
	}

	public void WriteUInt16List(IList<ushort> list)
	{
		if (list == null || list.Count == 0)
		{
			EmitU32(0u);
			EmitU32(0u);
			EmitU64(0uL);
			return;
		}
		byte[] array = new byte[list.Count * 2];
		for (int i = 0; i < list.Count; i++)
		{
			WriteU16LE(array, i * 2, _bigEndian ? Swap(list[i]) : list[i]);
		}
		EmitU32((uint)list.Count);
		EmitU32((uint)list.Count);
		ScheduleHeap(array, 2);
	}

	public void WriteFloatList(IList<float> list)
	{
		if (list == null || list.Count == 0)
		{
			EmitU32(0u);
			EmitU32(0u);
			EmitU64(0uL);
			return;
		}
		byte[] array = new byte[list.Count * 4];
		for (int i = 0; i < list.Count; i++)
		{
			WriteU32LE(array, i * 4, _bigEndian ? Swap(FloatBits(list[i])) : FloatBits(list[i]));
		}
		EmitU32((uint)list.Count);
		EmitU32((uint)list.Count);
		ScheduleHeap(array, 4);
	}

	public void WriteUInt32List(IList<uint> list)
	{
		if (list == null || list.Count == 0)
		{
			EmitU32(0u);
			EmitU32(0u);
			EmitU64(0uL);
			return;
		}
		byte[] array = new byte[list.Count * 4];
		for (int i = 0; i < list.Count; i++)
		{
			WriteU32LE(array, i * 4, _bigEndian ? Swap(list[i]) : list[i]);
		}
		EmitU32((uint)list.Count);
		EmitU32((uint)list.Count);
		ScheduleHeap(array, 4);
	}

	public byte[] Build(uint layoutHash)
	{
		byte[] array = _fieldBuf.ToArray();
		int num = array.Length;
		int num2 = 44 + num;
		byte[] array2;
		using (MemoryStream memoryStream = new MemoryStream())
		{
			int num3 = num2;
			foreach (HeapBlock heapBlock in _heapBlocks)
			{
				int num4 = AlignUp(num3 - 12, heapBlock.ElemAlignment) + 12 - num3;
				for (int i = 0; i < num4; i++)
				{
					memoryStream.WriteByte(0);
				}
				num3 += num4;
				long v = Encode60BitPtr(44 + heapBlock.PtrOffsetInField, num3);
				PatchI64(array, heapBlock.PtrOffsetInField, v);
				memoryStream.Write(heapBlock.Data, 0, heapBlock.Data.Length);
				num3 += heapBlock.Data.Length;
			}
			array2 = memoryStream.ToArray();
		}
		int num5 = 44 + num + array2.Length + 10;
		byte[] array3 = new byte[num5];
		array3[0] = 71;
		array3[1] = 68;
		array3[2] = 46;
		array3[3] = 68;
		array3[4] = 65;
		array3[5] = 84;
		array3[6] = 50;
		array3[7] = (byte)(_bigEndian ? 98u : 108u);
		PatchU32(array3, 8, (uint)num5);
		PatchU64(array3, 12, 51274245uL);
		PatchU64(array3, 28, layoutHash);
		PatchU16(array3, 40, 32);
		Buffer.BlockCopy(array, 0, array3, 44, num);
		if (array2.Length != 0)
		{
			Buffer.BlockCopy(array2, 0, array3, 44 + num, array2.Length);
		}
		int num6 = 44 + num + array2.Length;
		PatchI64(array3, num6, Encode60BitPtr(num6, 12L));
		PatchI16(array3, num6 + 8, 1);
		return array3;
	}

	private void ScheduleHeap(byte[] data, int elemAlignment)
	{
		int ptrOffsetInField = (int)_fieldBuf.Position;
		EmitU64(0uL);
		_heapBlocks.Add(new HeapBlock
		{
			PtrOffsetInField = ptrOffsetInField,
			ElemAlignment = elemAlignment,
			Data = data
		});
	}

	private void EmitU64(ulong v)
	{
		if (_bigEndian)
		{
			v = Swap(v);
		}
		for (int i = 0; i < 8; i++)
		{
			_fieldBuf.WriteByte((byte)(v >> i * 8));
		}
	}

	private void EmitU32(uint v)
	{
		if (_bigEndian)
		{
			v = Swap(v);
		}
		for (int i = 0; i < 4; i++)
		{
			_fieldBuf.WriteByte((byte)(v >> i * 8));
		}
	}

	private void EmitU16(ushort v)
	{
		if (_bigEndian)
		{
			v = Swap(v);
		}
		_fieldBuf.WriteByte((byte)v);
		_fieldBuf.WriteByte((byte)(v >> 8));
	}

	private void PatchU64(byte[] b, int o, ulong v)
	{
		if (_bigEndian)
		{
			v = Swap(v);
		}
		for (int i = 0; i < 8; i++)
		{
			b[o + i] = (byte)(v >> i * 8);
		}
	}

	private void PatchU32(byte[] b, int o, uint v)
	{
		if (_bigEndian)
		{
			v = Swap(v);
		}
		for (int i = 0; i < 4; i++)
		{
			b[o + i] = (byte)(v >> i * 8);
		}
	}

	private void PatchU16(byte[] b, int o, ushort v)
	{
		if (_bigEndian)
		{
			v = Swap(v);
		}
		b[o] = (byte)v;
		b[o + 1] = (byte)(v >> 8);
	}

	private void PatchI64(byte[] b, int o, long v)
	{
		PatchU64(b, o, (ulong)v);
	}

	private void PatchI16(byte[] b, int o, short v)
	{
		PatchU16(b, o, (ushort)v);
	}

	private static void WriteU16LE(byte[] b, int o, ushort v)
	{
		b[o] = (byte)v;
		b[o + 1] = (byte)(v >> 8);
	}

	private static void WriteU32LE(byte[] b, int o, uint v)
	{
		b[o] = (byte)v;
		b[o + 1] = (byte)(v >> 8);
		b[o + 2] = (byte)(v >> 16);
		b[o + 3] = (byte)(v >> 24);
	}

	private static uint FloatBits(float f)
	{
		return BitConverter.ToUInt32(BitConverter.GetBytes(f), 0);
	}

	private static long Encode60BitPtr(long from, long to)
	{
		return ((to - from) & 0xFFFFFFFFFFFFFFFL) | 0x1000000000000000L;
	}

	private static int AlignUp(int v, int a)
	{
		if (a > 1)
		{
			return (v + a - 1) & ~(a - 1);
		}
		return v;
	}

	private static ulong Swap(ulong v)
	{
		return ((v & 0xFF) << 56) | (((v >> 8) & 0xFF) << 48) | (((v >> 16) & 0xFF) << 40) | (((v >> 24) & 0xFF) << 32) | (((v >> 32) & 0xFF) << 24) | (((v >> 40) & 0xFF) << 16) | (((v >> 48) & 0xFF) << 8) | ((v >> 56) & 0xFF);
	}

	private static uint Swap(uint v)
	{
		return ((v & 0xFF) << 24) | (((v >> 8) & 0xFF) << 16) | (((v >> 16) & 0xFF) << 8) | ((v >> 24) & 0xFF);
	}

	private static ushort Swap(ushort v)
	{
		return (ushort)(((v & 0xFF) << 8) | ((v >> 8) & 0xFF));
	}
}
