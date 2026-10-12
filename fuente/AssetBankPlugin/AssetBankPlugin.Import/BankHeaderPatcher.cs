using System;
using System.Collections.Generic;
using System.Linq;

namespace AssetBankPlugin.Import;

public static class BankHeaderPatcher
{
	private struct KeyAssetPair
	{
		public long Key;

		public ulong Ptr;
	}

	public static byte[] PatchHeaderForKeys(byte[] bank, ulong[] newKeys, bool fallbackBigEndian, ulong[] keysToRemove = null)
	{
		if (bank == null || bank.Length < 128)
		{
			throw new ArgumentException("Bank bytes are too small to contain a valid header.");
		}
		int num = -1;
		bool big = false;
		for (int i = 0; i <= 64; i += 4)
		{
			if (ReadU32(bank, i + 4, big: true) <= 4)
			{
				uint num2 = ReadU32(bank, i, big: true);
				uint num3 = ReadU32(bank, i + 8, big: true);
				uint num4 = ReadU32(bank, i + 32, big: true);
				if (num2 != 0 && num2 < bank.Length && num3 < 500000 && num4 != 0 && num4 <= bank.Length)
				{
					num = i;
					big = true;
					break;
				}
			}
			if (ReadU32(bank, i + 4, big: false) <= 4)
			{
				uint num5 = ReadU32(bank, i, big: false);
				uint num6 = ReadU32(bank, i + 8, big: false);
				uint num7 = ReadU32(bank, i + 32, big: false);
				if (num5 != 0 && num5 < bank.Length && num6 < 500000 && num7 != 0 && num7 <= bank.Length)
				{
					num = i;
					big = false;
					break;
				}
			}
		}
		if (num == -1)
		{
			throw new Exception("Could not locate valid PackageMeta header.");
		}
		uint num8 = ReadU32(bank, num, big);
		uint v = ReadU32(bank, num + 4, big);
		uint num9 = ReadU32(bank, num + 8, big);
		uint num10 = ReadU32(bank, num + 12, big);
		uint num11 = ReadU32(bank, num + 16, big);
		uint num12 = ReadU32(bank, num + 20, big);
		uint num13 = ReadU32(bank, num + 24, big);
		uint v2 = ReadU32(bank, num + 28, big);
		ReadU32(bank, num + 32, big);
		long num14 = num + 36;
		bool big2 = false;
		if (num9 >= 2)
		{
			long num15 = (long)ReadU64(bank, (int)num14, big: false);
			long num16 = (long)ReadU64(bank, (int)num14 + 16, big: false);
			long num17 = (long)ReadU64(bank, (int)num14, big: true);
			long num18 = (long)ReadU64(bank, (int)num14 + 16, big: true);
			if (num15 <= num16 && num17 > num18)
			{
				big2 = false;
			}
			else if (num17 <= num18 && num15 > num16)
			{
				big2 = true;
			}
		}
		List<KeyAssetPair> list = new List<KeyAssetPair>((int)num9 + newKeys.Length);
		for (int j = 0; j < num9; j++)
		{
			long num19 = (long)ReadU64(bank, (int)num14, big2);
			ulong ptr = ReadU64(bank, (int)num14 + 8, big2);
			if (keysToRemove == null || !keysToRemove.Contains((ulong)num19))
			{
				list.Add(new KeyAssetPair
				{
					Key = num19,
					Ptr = ptr
				});
			}
			num14 += 16;
		}
		List<long> list2 = new List<long>((int)num10 + newKeys.Length);
		for (int k = 0; k < num10; k++)
		{
			long num20 = (long)ReadU64(bank, (int)num14, big2);
			if (keysToRemove == null || !keysToRemove.Contains((ulong)num20))
			{
				list2.Add(num20);
			}
			num14 += 8;
		}
		long num21 = (long)num11 * 16L;
		byte[] array = new byte[num21];
		Buffer.BlockCopy(bank, (int)num14, array, 0, (int)num21);
		num14 += num21;
		long num22 = (long)num12 * 16L;
		byte[] array2 = new byte[num22];
		Buffer.BlockCopy(bank, (int)num14, array2, 0, (int)num22);
		num14 += num22;
		long num23 = (long)num13 * 16L;
		byte[] array3 = new byte[num23];
		Buffer.BlockCopy(bank, (int)num14, array3, 0, (int)num23);
		num14 += num23;
		long num24 = num14;
		long num25 = bank.Length - num24;
		byte[] array4 = new byte[num25];
		Buffer.BlockCopy(bank, (int)num24, array4, 0, (int)num25);
		if (array4.Length >= 12 && array4[0] == 71 && array4[1] == 68 && array4[2] == 46 && array4[3] == 83 && array4[4] == 84 && array4[5] == 82 && array4[6] == 77)
		{
			bool big3 = array4[7] == 98;
			uint v3 = (uint)num25;
			WriteU32(array4, 8, v3, big3);
		}
		foreach (ulong num26 in newKeys)
		{
			list.Add(new KeyAssetPair
			{
				Key = (long)num26,
				Ptr = 0uL
			});
			list2.Add((long)num26);
		}
		list.Sort((KeyAssetPair x, KeyAssetPair y) => x.Key.CompareTo(y.Key));
		list2.Sort();
		uint count = (uint)list.Count;
		long num27 = count * 16;
		uint count2 = (uint)list2.Count;
		long num28 = count2 * 8;
		long num29 = 36 + (long)num9 * 16L + (long)num10 * 8L + num21 + num22 + num23;
		long num30 = num8 - num29;
		uint num31 = (uint)(36 + num27 + num28 + num21 + num22 + num23 + num30);
		uint num32 = (uint)num25;
		byte[] array5 = new byte[num + num31 + num32];
		Buffer.BlockCopy(bank, 0, array5, 0, num);
		WriteU32(array5, num, num31, big);
		WriteU32(array5, num + 4, v, big);
		WriteU32(array5, num + 8, count, big);
		WriteU32(array5, num + 12, count2, big);
		WriteU32(array5, num + 16, num11, big);
		WriteU32(array5, num + 20, num12, big);
		WriteU32(array5, num + 24, num13, big);
		WriteU32(array5, num + 28, v2, big);
		WriteU32(array5, num + 32, num32, big);
		int num33 = num + 36;
		foreach (KeyAssetPair item in list)
		{
			WriteU64(array5, num33, (ulong)item.Key, big2);
			WriteU64(array5, num33 + 8, item.Ptr, big2);
			num33 += 16;
		}
		foreach (long item2 in list2)
		{
			WriteU64(array5, num33, (ulong)item2, big2);
			num33 += 8;
		}
		Buffer.BlockCopy(array, 0, array5, num33, array.Length);
		num33 += array.Length;
		Buffer.BlockCopy(array2, 0, array5, num33, array2.Length);
		num33 += array2.Length;
		Buffer.BlockCopy(array3, 0, array5, num33, array3.Length);
		num33 += array3.Length;
		Buffer.BlockCopy(array4, 0, array5, num33, array4.Length);
		return array5;
	}

	private static uint ReadU32(byte[] b, int o, bool big)
	{
		if (!big)
		{
			return (uint)(b[o] | (b[o + 1] << 8) | (b[o + 2] << 16) | (b[o + 3] << 24));
		}
		return (uint)((b[o] << 24) | (b[o + 1] << 16) | (b[o + 2] << 8) | b[o + 3]);
	}

	private static ulong ReadU64(byte[] b, int o, bool big)
	{
		if (!big)
		{
			return ReadU64LE(b, o);
		}
		return ReadU64BE(b, o);
	}

	private static ulong ReadU64LE(byte[] b, int o)
	{
		return b[o] | ((ulong)b[o + 1] << 8) | ((ulong)b[o + 2] << 16) | ((ulong)b[o + 3] << 24) | ((ulong)b[o + 4] << 32) | ((ulong)b[o + 5] << 40) | ((ulong)b[o + 6] << 48) | ((ulong)b[o + 7] << 56);
	}

	private static ulong ReadU64BE(byte[] b, int o)
	{
		return ((ulong)b[o] << 56) | ((ulong)b[o + 1] << 48) | ((ulong)b[o + 2] << 40) | ((ulong)b[o + 3] << 32) | ((ulong)b[o + 4] << 24) | ((ulong)b[o + 5] << 16) | ((ulong)b[o + 6] << 8) | b[o + 7];
	}

	private static void WriteU32(byte[] b, int o, uint v, bool big)
	{
		if (big)
		{
			v = Swap(v);
		}
		for (int i = 0; i < 4; i++)
		{
			b[o + i] = (byte)(v >> i * 8);
		}
	}

	private static void WriteU64(byte[] b, int o, ulong v, bool big)
	{
		if (big)
		{
			v = Swap(v);
		}
		for (int i = 0; i < 8; i++)
		{
			b[o + i] = (byte)(v >> i * 8);
		}
	}

	private static uint Swap(uint v)
	{
		return ((v & 0xFF) << 24) | (((v >> 8) & 0xFF) << 16) | (((v >> 16) & 0xFF) << 8) | ((v >> 24) & 0xFF);
	}

	private static ulong Swap(ulong v)
	{
		return ((v & 0xFF) << 56) | (((v >> 8) & 0xFF) << 48) | (((v >> 16) & 0xFF) << 40) | (((v >> 24) & 0xFF) << 32) | (((v >> 32) & 0xFF) << 24) | (((v >> 40) & 0xFF) << 16) | (((v >> 48) & 0xFF) << 8) | ((v >> 56) & 0xFF);
	}
}
