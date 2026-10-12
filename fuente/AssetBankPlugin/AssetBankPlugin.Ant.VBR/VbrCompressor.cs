using System;
using System.Collections.Generic;
using System.Numerics;
using AssetBankPlugin.Export;
using Assimp;
using Frosty.Core;

namespace AssetBankPlugin.Ant.VBR;

internal static class VbrCompressor
{
	private sealed class CurveFitHelper
	{
		private struct CurvePoint
		{
			public uint X;

			public float Y;
		}

		private sealed class Binner
		{
			private readonly int _ch;

			private uint _cur;

			private readonly uint _half;

			private readonly uint _bin;

			private readonly uint _frameCount;

			private readonly uint _stride;

			private readonly float[] _data;

			public Binner(int ch, uint binSize, int frameCount, float[] data, int stride)
			{
				_ch = ch;
				_cur = 0u;
				_half = binSize / 2;
				_bin = binSize;
				_frameCount = (uint)frameCount;
				_stride = (uint)stride;
				_data = data;
			}

			public void GetInitial(out uint pos, out float val)
			{
				pos = 0u;
				val = _data[_ch];
			}

			public void GetCurrent(out uint pos, out float val)
			{
				uint num = _frameCount - _cur;
				if (num < _bin * 2)
				{
					if (num < _bin)
					{
						_cur = _frameCount - 1;
						pos = _cur;
						val = _data[(int)(_stride * _cur) + _ch];
						return;
					}
					float num2 = 0f;
					uint num3 = _cur + _half;
					for (uint num4 = num3; num4 < _frameCount; num4++)
					{
						num2 += _data[(int)(_stride * num4) + _ch];
					}
					_cur = _frameCount - 1;
					pos = _cur;
					val = num2 / (float)(_frameCount - num3);
				}
				else
				{
					uint num5 = _cur + _half;
					float num6 = 0f;
					for (uint num7 = num5; num7 < num5 + _bin; num7++)
					{
						num6 += _data[(int)(_stride * num7) + _ch];
					}
					_cur += _bin;
					pos = _cur;
					val = num6 / (float)_bin;
				}
			}

			public bool AtEnd()
			{
				return _cur >= _frameCount - 1;
			}
		}

		private struct StabbingSet
		{
			private float _eps;

			private CurvePoint _tail;

			private CurvePoint _pivot;

			private float _uX;

			private float _uY;

			private float _lX;

			private float _lY;

			public StabbingSet(uint originX, float originY, uint initX, float initY, float eps)
			{
				_eps = eps;
				_tail = new CurvePoint
				{
					X = initX,
					Y = initY
				};
				uint num = initX - originX;
				float num2 = initY - originY;
				_uX = (num2 + 2f * eps) / (float)num;
				_uY = 0f - eps;
				_lX = (num2 - 2f * eps) / (float)num;
				_lY = eps;
				if (eps > 0f)
				{
					float num3 = 2f * eps / (_uX - _lX);
					_pivot = new CurvePoint
					{
						X = (uint)((float)originX + num3),
						Y = originY + num3 * _uX + _uY
					};
				}
				else
				{
					_pivot = _tail;
				}
			}

			public bool Add(uint x, float y)
			{
				uint num = x - _pivot.X;
				float num2 = num;
				float num3 = y - _pivot.Y;
				float num4 = num2 * _uX + _uY;
				float num5 = num2 * _lX + _lY;
				bool flag = num < 2040;
				bool num6 = num3 - _eps <= num4;
				bool flag2 = num3 + _eps >= num5;
				bool num7 = num6 && flag2 && flag;
				if (num7)
				{
					if (num3 + _eps < num4)
					{
						_uX = (num3 + _eps) / num2;
						_uY = 0f;
					}
					if (num3 - _eps > num5)
					{
						_lX = (num3 - _eps) / num2;
						_lY = 0f;
					}
					_tail = new CurvePoint
					{
						X = x,
						Y = y
					};
				}
				return num7;
			}

			public CurvePoint GetTail()
			{
				return _tail;
			}
		}

		private struct SegmentFinder
		{
			private int _cur;

			public float Delta(uint x, float y, List<CurvePoint> pts)
			{
				while (_cur != pts.Count - 1 && pts[_cur + 1].X < x)
				{
					_cur++;
				}
				if (_cur != pts.Count - 1)
				{
					CurvePoint curvePoint = pts[_cur + 1];
					CurvePoint curvePoint2 = pts[_cur];
					uint num = x - curvePoint2.X;
					float num2 = curvePoint.X - curvePoint2.X;
					float num3 = (curvePoint.Y - curvePoint2.Y) / num2 * (float)num + curvePoint2.Y;
					return y - num3;
				}
				return y - pts[_cur].Y;
			}
		}

		private readonly float[][][] _allVecs;

		private readonly float[][] _allFloats;

		private readonly bool[] _constCh;

		private readonly int _totalQ;

		private readonly int _totalV;

		private readonly int _totalF;

		private readonly int _mVectors;

		private readonly int _mFloats;

		private readonly int _frameCount;

		private float[] _minChannels;

		private float[] _maxChannels;

		private float[] _minDeltaChannels;

		private float[] _maxDeltaChannels;

		private float[] _usedEpsilon;

		private float[] _deltaChannel;

		private float[] _toDeltaChannels;

		private List<CurvePoint>[] _deltaCurves;

		public float VectorOffsetScale;

		public float FloatOffsetScale;

		public ushort VectorOffsetSize;

		public ushort FloatOffsetSize;

		public byte[] VectorOffsets;

		public byte[] FloatOffsets;

		public CurveFitHelper(float[][][] vecs, float[][] floats, bool[] constCh, int totalQ, int totalV, int totalF, int frameCount)
		{
			_allVecs = vecs;
			_allFloats = floats;
			_constCh = constCh;
			_totalQ = totalQ;
			_totalV = totalV;
			_totalF = totalF;
			_frameCount = frameCount;
			int num = 0;
			for (int i = 0; i < totalV; i++)
			{
				if (constCh[totalQ + i])
				{
					num++;
				}
			}
			int num2 = 0;
			for (int j = 0; j < totalF; j++)
			{
				if (constCh[totalQ + totalV + j])
				{
					num2++;
				}
			}
			_mVectors = totalV - num;
			_mFloats = totalF - num2;
		}

		public float GetToDeltaChannelFloat(int floatIdx, int frame)
		{
			int num = _mVectors * 3 + _mFloats;
			return _toDeltaChannels[num * frame + _mVectors * 3 + floatIdx];
		}

		public void GetToDeltaChannelsVector3(int vecIdx, int frame, out float x, out float y, out float z)
		{
			int num = (_mVectors * 3 + _mFloats) * frame + vecIdx * 3;
			x = _toDeltaChannels[num];
			y = _toDeltaChannels[num + 1];
			z = _toDeltaChannels[num + 2];
		}

		public void DoCurveFitting()
		{
			int num = _mVectors * 3 + _mFloats;
			if (num == 0)
			{
				return;
			}
			_minChannels = new float[num];
			_maxChannels = new float[num];
			_minDeltaChannels = new float[num];
			_maxDeltaChannels = new float[num];
			_usedEpsilon = new float[num];
			_deltaChannel = new float[_frameCount];
			_toDeltaChannels = new float[num * _frameCount];
			_deltaCurves = new List<CurvePoint>[num];
			for (int i = 0; i < num; i++)
			{
				_minChannels[i] = float.MaxValue;
				_maxChannels[i] = float.MinValue;
			}
			int num2 = 0;
			for (int j = 0; j < _totalV; j++)
			{
				if (_constCh[_totalQ + j])
				{
					continue;
				}
				for (int k = 0; k < _frameCount; k++)
				{
					float[] array = _allVecs[j][k];
					int num3 = num * k + num2 * 3;
					_toDeltaChannels[num3] = array[0];
					_toDeltaChannels[num3 + 1] = array[1];
					_toDeltaChannels[num3 + 2] = array[2];
					if (array[0] < _minChannels[num2 * 3])
					{
						_minChannels[num2 * 3] = array[0];
					}
					if (array[0] > _maxChannels[num2 * 3])
					{
						_maxChannels[num2 * 3] = array[0];
					}
					if (array[1] < _minChannels[num2 * 3 + 1])
					{
						_minChannels[num2 * 3 + 1] = array[1];
					}
					if (array[1] > _maxChannels[num2 * 3 + 1])
					{
						_maxChannels[num2 * 3 + 1] = array[1];
					}
					if (array[2] < _minChannels[num2 * 3 + 2])
					{
						_minChannels[num2 * 3 + 2] = array[2];
					}
					if (array[2] > _maxChannels[num2 * 3 + 2])
					{
						_maxChannels[num2 * 3 + 2] = array[2];
					}
				}
				num2++;
			}
			num2 = 0;
			for (int l = 0; l < _totalF; l++)
			{
				if (_constCh[_totalQ + _totalV + l])
				{
					continue;
				}
				for (int m = 0; m < _frameCount; m++)
				{
					float num4 = _allFloats[l][m];
					_toDeltaChannels[num * m + _mVectors * 3 + num2] = num4;
					if (num4 < _minChannels[_mVectors * 3 + num2])
					{
						_minChannels[_mVectors * 3 + num2] = num4;
					}
					if (num4 > _maxChannels[_mVectors * 3 + num2])
					{
						_maxChannels[_mVectors * 3 + num2] = num4;
					}
				}
				num2++;
			}
			Array.Copy(_minChannels, _minDeltaChannels, num);
			Array.Copy(_maxChannels, _maxDeltaChannels, num);
			for (int n = 0; n < num; n++)
			{
				FirstPassCurveFit(n);
			}
			bool flag = true;
			while (flag)
			{
				flag = false;
				for (int num5 = 0; num5 < _mVectors * 3; num5++)
				{
					flag |= SecondPassCurveFit(num5, 0, _mVectors * 3);
				}
				for (int num6 = 0; num6 < _mFloats; num6++)
				{
					flag |= SecondPassCurveFit(_mVectors * 3 + num6, _mVectors * 3, _mFloats);
				}
			}
			VectorOffsetScale = 0f;
			uint numCurves = 0u;
			uint numPoints = 0u;
			FindOffsetScale(0, _mVectors * 3, ref VectorOffsetScale, ref numCurves, ref numPoints);
			ReduceQualityAndCompressCurve(0, _mVectors * 3, aRecalculate: true, VectorOffsetScale, numCurves, numPoints, out VectorOffsetSize, out VectorOffsets);
			FloatOffsetScale = 0f;
			uint numCurves2 = 0u;
			uint numPoints2 = 0u;
			FindOffsetScale(_mVectors * 3, _mFloats, ref FloatOffsetScale, ref numCurves2, ref numPoints2);
			ReduceQualityAndCompressCurve(_mVectors * 3, _mFloats, aRecalculate: false, FloatOffsetScale, numCurves2, numPoints2, out FloatOffsetSize, out FloatOffsets);
			for (int num7 = 0; num7 < num; num7++)
			{
				if (_deltaCurves[num7] != null)
				{
					SubtractDelta(num7, _deltaCurves[num7]);
				}
			}
		}

		private void FirstPassCurveFit(int ch)
		{
			_usedEpsilon[ch] = 0.1f;
			List<CurvePoint> list = new List<CurvePoint>();
			ComputeSimplified(ch, _usedEpsilon[ch], list);
			CalculateDelta(ch, list);
			float num = float.MaxValue;
			float num2 = float.MinValue;
			for (int i = 0; i < _frameCount; i++)
			{
				if (_deltaChannel[i] < num)
				{
					num = _deltaChannel[i];
				}
				if (_deltaChannel[i] > num2)
				{
					num2 = _deltaChannel[i];
				}
			}
			float num3 = Math.Abs(num2 - num);
			float num4 = Math.Abs(_maxChannels[ch] - _minChannels[ch]);
			float value = Math.Abs(num2 + num) / 2f;
			float value2 = Math.Abs(_maxChannels[ch] + _minChannels[ch]) / 2f;
			bool num5 = ((num3 > 1.1920929E-07f) ? (num4 / num3 > 2f) : (Math.Abs(num4 - num3) > 1f));
			bool flag = Math.Abs(value2) - Math.Abs(value) > 0.75f;
			if (num5 || flag || ch % 191 == 0)
			{
				_minDeltaChannels[ch] = num;
				_maxDeltaChannels[ch] = num2;
				_deltaCurves[ch] = list;
			}
		}

		private bool SecondPassCurveFit(int ch, int groupStart, int groupCount)
		{
			if (_deltaCurves[ch] == null)
			{
				return false;
			}
			float num = float.MaxValue;
			float num2 = float.MinValue;
			for (int i = groupStart; i < groupStart + groupCount; i++)
			{
				if (i != ch)
				{
					if (_minDeltaChannels[i] < num)
					{
						num = _minDeltaChannels[i];
					}
					if (_maxDeltaChannels[i] > num2)
					{
						num2 = _maxDeltaChannels[i];
					}
				}
			}
			float num3 = Math.Min(num, _minChannels[ch]);
			float num4 = Math.Max(num2, _maxChannels[ch]);
			float num5 = Math.Min(num, _minDeltaChannels[ch]);
			float num6 = Math.Max(num2, _maxDeltaChannels[ch]);
			float num7 = num4 - num3;
			float num8 = num6 - num5;
			bool flag = false;
			if (num7 > 1.1920929E-07f)
			{
				flag = (num7 - num8) / num7 > 0.2f;
				flag |= ch % 191 == 0;
			}
			if (flag)
			{
				bool flag2 = true;
				float num9 = _usedEpsilon[ch];
				float num10 = num8;
				float num11 = float.MaxValue;
				float num12 = float.MinValue;
				List<CurvePoint> list = new List<CurvePoint>();
				if (num10 > 1.1920929E-07f)
				{
					while (flag2 && num9 < 100f)
					{
						num9 *= 1.1f;
						List<CurvePoint> list2 = new List<CurvePoint>();
						ComputeSimplified(ch, num9, list2);
						CalculateDelta(ch, list2);
						float num13 = float.MaxValue;
						float num14 = float.MinValue;
						for (int j = 0; j < _frameCount; j++)
						{
							if (_deltaChannel[j] < num13)
							{
								num13 = _deltaChannel[j];
							}
							if (_deltaChannel[j] > num14)
							{
								num14 = _deltaChannel[j];
							}
						}
						float num15 = Math.Min(num, num13);
						flag2 = (Math.Max(num2, num14) - num15 - num10) / num10 < 0.2f;
						if (!flag2)
						{
							continue;
						}
						bool flag3 = list2.Count < _deltaCurves[ch].Count;
						if (list.Count != 0)
						{
							flag3 = list2.Count < list.Count;
						}
						if (flag3)
						{
							list = new List<CurvePoint>(list2);
							num11 = num13;
							num12 = num14;
							if (list.Count == 1)
							{
								flag2 = false;
							}
						}
					}
				}
				if (list.Count > 0 && _deltaCurves[ch].Count > list.Count)
				{
					_deltaCurves[ch] = list;
					_minDeltaChannels[ch] = num11;
					_maxDeltaChannels[ch] = num12;
				}
				return false;
			}
			_deltaCurves[ch] = null;
			_minDeltaChannels[ch] = _minChannels[ch];
			_maxDeltaChannels[ch] = _maxChannels[ch];
			_usedEpsilon[ch] = 0f;
			return true;
		}

		private void FindOffsetScale(int offset, int count, ref float scale, ref uint numCurves, ref uint numPoints)
		{
			scale = 0f;
			numCurves = 0u;
			numPoints = 0u;
			for (int i = 0; i < count; i++)
			{
				List<CurvePoint> list = _deltaCurves[offset + i];
				if (list == null)
				{
					continue;
				}
				numCurves++;
				for (int j = 0; j < list.Count; j++)
				{
					numPoints++;
					float num = Math.Abs(list[j].Y);
					if (num > scale)
					{
						scale = num;
					}
				}
			}
		}

		private void ReduceQualityAndCompressCurve(int offset, int count, bool aRecalculate, float scale, uint numCurves, uint numPoints, out ushort outSize, out byte[] outBytes)
		{
			outSize = 0;
			outBytes = null;
			if (numCurves == 0)
			{
				return;
			}
			outSize = (ushort)(1 + numCurves * 3 + (numPoints - numCurves) * 2);
			outBytes = new byte[outSize];
			int num = 0;
			uint num2 = 0u;
			outBytes[num++] = (byte)numCurves;
			for (int i = 0; i < count; i++)
			{
				List<CurvePoint> list = _deltaCurves[offset + i];
				if (list == null)
				{
					continue;
				}
				uint num3 = (uint)i;
				if (aRecalculate)
				{
					num3 = (uint)(i / 3 * 4);
					num3 += (uint)(i % 3);
				}
				outBytes[num++] = (byte)(num3 - num2);
				num2 = num3;
				outBytes[num++] = (byte)list.Count;
				uint num4 = 0u;
				for (int j = 0; j < list.Count; j++)
				{
					uint x = list[j].X;
					uint num5 = x - num4;
					num4 = x;
					if (j != 0)
					{
						outBytes[num++] = (byte)(num5 >> 3);
					}
					sbyte b = (sbyte)((scale != 0f) ? (list[j].Y / scale * 127f) : 0f);
					outBytes[num++] = (byte)b;
					CurvePoint value = list[j];
					value.Y = (float)b / 127f * scale;
					list[j] = value;
				}
			}
		}

		private void CalculateDelta(int ch, List<CurvePoint> pts)
		{
			int num = _mVectors * 3 + _mFloats;
			SegmentFinder segmentFinder = default(SegmentFinder);
			for (int i = 0; i < _frameCount; i++)
			{
				_deltaChannel[i] = segmentFinder.Delta((uint)i, _toDeltaChannels[num * i + ch], pts);
			}
		}

		private void SubtractDelta(int ch, List<CurvePoint> pts)
		{
			int num = _mVectors * 3 + _mFloats;
			SegmentFinder segmentFinder = default(SegmentFinder);
			for (int i = 0; i < _frameCount; i++)
			{
				_toDeltaChannels[num * i + ch] = segmentFinder.Delta((uint)i, _toDeltaChannels[num * i + ch], pts);
			}
		}

		private void ComputeSimplified(int ch, float eps, List<CurvePoint> pts)
		{
			int stride = _mVectors * 3 + _mFloats;
			Binner binner = new Binner(ch, 8u, _frameCount, _toDeltaChannels, stride);
			binner.GetInitial(out var pos, out var val);
			pts.Add(new CurvePoint
			{
				X = pos,
				Y = val
			});
			binner.GetCurrent(out var pos2, out var val2);
			StabbingSet stabbingSet = new StabbingSet(pos, val, pos2, val2, eps);
			while (!binner.AtEnd())
			{
				binner.GetCurrent(out pos2, out val2);
				if (!stabbingSet.Add(pos2, val2))
				{
					CurvePoint tail = stabbingSet.GetTail();
					pts.Add(tail);
					stabbingSet = new StabbingSet(tail.X, tail.Y, pos2, val2, eps);
				}
			}
			if (pts.Count >= 2)
			{
				CurvePoint curvePoint = pts[pts.Count - 2];
				CurvePoint curvePoint2 = pts[pts.Count - 1];
				if (Math.Abs(curvePoint.Y - curvePoint2.Y) < eps && curvePoint.X + curvePoint2.X < 2040)
				{
					pts.RemoveAt(pts.Count - 1);
				}
			}
			if (pts.Count >= 2)
			{
				CurvePoint curvePoint3 = pts[pts.Count - 1];
				if (curvePoint3.X % 8 != 0)
				{
					CurvePoint curvePoint4 = pts[pts.Count - 2];
					uint num = curvePoint3.X - curvePoint4.X;
					float num2 = (curvePoint3.Y - curvePoint4.Y) / (float)num;
					uint num3 = 8 - num % 8;
					pts[pts.Count - 1] = new CurvePoint
					{
						X = curvePoint3.X + num3,
						Y = curvePoint3.Y + (float)num3 * num2
					};
				}
			}
		}
	}

	private sealed class BitWriter
	{
		private readonly List<byte> _buf = new List<byte>(4096);

		private byte _bucket;

		private int _stored;

		private int _byteCountAtReset;

		public int ByteCount => _buf.Count;

		public int BitsSinceReset => (_buf.Count - _byteCountAtReset) * 8;

		public void ResetBitCount()
		{
			_byteCountAtReset = _buf.Count;
		}

		public void WriteBits(uint value, int bits)
		{
			while (bits > 0)
			{
				int num = 8 - _stored;
				int num2 = ((bits < num) ? bits : num);
				uint num3 = (uint)((1 << num2) - 1);
				_bucket |= (byte)((value & num3) << _stored);
				value >>= num2;
				_stored += num2;
				bits -= num2;
				if (_stored == 8)
				{
					FlushInner();
				}
			}
		}

		public void Flush()
		{
			if (_stored != 0)
			{
				FlushInner();
			}
		}

		private void FlushInner()
		{
			_buf.Add(_bucket);
			_bucket = 0;
			_stored = 0;
		}

		public byte[] ToArray()
		{
			return _buf.ToArray();
		}
	}

	private static readonly float[,] DctFwd;

	private static readonly float[,] DctInv;

	private const int NumQTables = 256;

	static VbrCompressor()
	{
		DctInv = new float[8, 8]
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
		DctFwd = new float[8, 8];
		for (int i = 0; i < 8; i++)
		{
			for (int j = 0; j < 8; j++)
			{
				DctFwd[i, j] = (float)Math.Cos(Math.PI * ((double)i + 0.5) * (double)j / 8.0);
			}
		}
	}

	public static VbrAnimationAsset Compress(Scene scene, VbrAnimationAsset template, bool bigEndian, float maxRotErrPct = 0f, float maxTransErrPct = 0f, float maxTrajErrPct = 0f, float constThresh = 1E-05f, bool linearSearchTables = true, bool curveFit = true, bool legacy = false)
	{
		int totalQ = template.QuaternionCount + template.ConstQuaternionCount;
		int totalV = template.Vector3Count + template.ConstVector3Count;
		int totalF = template.NumFloat + template.ConstFloatCount;
		ExtractFromScene(scene, template, totalQ, totalV, totalF, out var quats, out var vecs, out var floats, out var frameCount, out var cycle);
		return CompressInternal(quats, vecs, floats, frameCount, cycle, template, bigEndian, maxRotErrPct, maxTransErrPct, maxTrajErrPct, constThresh, linearSearchTables, curveFit, legacy);
	}

	public static VbrAnimationAsset CompressFromData(float[][][] quats, float[][][] vecs, float[][] floats, int frameCount, bool cycle, VbrAnimationAsset template, float maxRotErrPct = 0f, float maxTransErrPct = 0f, float maxTrajErrPct = 0f, float constThresh = 1E-05f, bool bigEndian = false, bool linearSearchTables = true, bool curveFit = true, bool legacy = false)
	{
		return CompressInternal(quats, vecs, floats, frameCount, cycle, template, bigEndian, maxRotErrPct, maxTransErrPct, maxTrajErrPct, constThresh, linearSearchTables, curveFit, legacy);
	}

	private static VbrAnimationAsset CompressInternal(float[][][] quats, float[][][] vecs, float[][] floats, int frameCount, bool cycle, VbrAnimationAsset template, bool bigEndian, float maxRotErrPct, float maxTransErrPct, float maxTrajErrPct, float constThresh, bool linearSearchTables = true, bool curveFit = true, bool legacy = false)
	{
		int paletteLimit = (legacy ? 256 : 4096);
		if (legacy)
		{
			bigEndian = false;
		}
		int num = template.QuaternionCount + template.ConstQuaternionCount;
		int num2 = template.Vector3Count + template.ConstVector3Count;
		int num3 = template.NumFloat + template.ConstFloatCount;
		int num4 = num + num2 + num3;
		bool[] array = MarkConstantChannels(quats, vecs, floats, frameCount, num, num2, num3, constThresh);
		int num5 = 0;
		for (int i = 0; i < num4; i++)
		{
			if (array[i])
			{
				num5++;
			}
		}
		if (num4 > 0 && (float)num5 / (float)num4 < 0.1f)
		{
			for (int j = 0; j < num4; j++)
			{
				array[j] = false;
			}
		}
		int num6 = 0;
		int num7 = 0;
		int num8 = 0;
		for (int k = 0; k < num; k++)
		{
			if (array[k])
			{
				num6++;
			}
		}
		for (int l = 0; l < num2; l++)
		{
			if (array[num + l])
			{
				num7++;
			}
		}
		for (int m = 0; m < num3; m++)
		{
			if (array[num + num2 + m])
			{
				num8++;
			}
		}
		int num9 = num - num6;
		int num10 = num2 - num7;
		int num11 = num3 - num8;
		int num12 = num9 * 4 + num10 * 3 + num11;
		int alignedStride = (num12 + 3) & -4;
		bool flag = num10 > 1 && !array[num];
		float[] array2 = SaveConstantChannels(quats, vecs, floats, array, num, num2, num3, frameCount);
		CurveFitHelper curveFitHelper = null;
		if (curveFit)
		{
			curveFitHelper = new CurveFitHelper(vecs, floats, array, num, num2, num3, frameCount);
			curveFitHelper.DoCurveFitting();
		}
		ComputeMinMax(quats, vecs, floats, array, frameCount, num, num2, num3, num9, num10, num11, out var qMin, out var qMax, out var vMin, out var vMax, out var fMin, out var fMax, curveFitHelper);
		float num13 = 0f;
		float num14 = 0f;
		float num15 = 0f;
		float num16 = 0f;
		float num17 = 0f;
		float num18 = 0f;
		float num19 = 0f;
		float num20 = 0f;
		int num21 = 0;
		int num22 = 0;
		while (num22 < num6 * 4)
		{
			num13 = Math.Min(num13, array2[num21]);
			num14 = Math.Max(num14, array2[num21]);
			num22++;
			num21++;
		}
		int num23 = 0;
		while (num23 < num7 * 3)
		{
			num15 = Math.Min(num15, array2[num21]);
			num16 = Math.Max(num16, array2[num21]);
			num23++;
			num21++;
		}
		int num24 = 0;
		while (num24 < num8)
		{
			num17 = Math.Min(num17, array2[num21]);
			num18 = Math.Max(num18, array2[num21]);
			num24++;
			num21++;
		}
		for (int n = 0; n < num9; n++)
		{
			num13 = Math.Min(num13, qMin[n]);
			num14 = Math.Max(num14, qMax[n]);
		}
		if (num10 > 0)
		{
			if (flag)
			{
				num19 = vMin[0];
				num20 = vMax[0];
			}
			else
			{
				num15 = Math.Min(num15, vMin[0]);
				num16 = Math.Max(num16, vMax[0]);
			}
		}
		for (int num25 = 1; num25 < num10; num25++)
		{
			num15 = Math.Min(num15, vMin[num25]);
			num16 = Math.Max(num16, vMax[num25]);
		}
		for (int num26 = 0; num26 < num11; num26++)
		{
			num17 = Math.Min(num17, fMin[num26]);
			num18 = Math.Max(num18, fMax[num26]);
		}
		if (!flag)
		{
			num19 = num15;
			num20 = num16;
		}
		for (int num27 = 0; num27 < num9; num27++)
		{
			qMin[num27] = num13;
			qMax[num27] = num14;
		}
		for (int num28 = 0; num28 < num10; num28++)
		{
			bool flag2 = num28 == 0 && flag;
			vMin[num28] = (flag2 ? num19 : num15);
			vMax[num28] = (flag2 ? num20 : num16);
		}
		for (int num29 = 0; num29 < num11; num29++)
		{
			fMin[num29] = num17;
			fMax[num29] = num18;
		}
		float[] errBlock = BuildErrorBlock(num12, num9, num10, flag, maxRotErrPct / 100f, maxTransErrPct / 100f, maxTrajErrPct / 100f);
		NormalizeConstantData(array2, num6, num7, num8, num13, num14, num15, num16, num17, num18);
		float[] array3 = BuildConstantPalette(array2, num6, num7, num8, template.Name ?? "", paletteLimit);
		float[] norm = NormalizeAnimated(quats, vecs, floats, array, frameCount, num, num2, num3, num9, num10, num11, alignedStride, qMin, qMax, vMin, vMax, fMin, fMax, curveFitHelper);
		int num30 = frameCount + 7 >> 3;
		bool trajConstant = num2 > 0 && array[num];
		byte[] vectorOffsets = curveFitHelper?.VectorOffsets;
		byte[] floatOffsets = curveFitHelper?.FloatOffsets;
		float vectorOffsetScale = curveFitHelper?.VectorOffsetScale ?? 0f;
		float floatOffsetScale = curveFitHelper?.FloatOffsetScale ?? 0f;
		ushort vectorOffsetSize = curveFitHelper?.VectorOffsetSize ?? 0;
		ushort floatOffsetSize = curveFitHelper?.FloatOffsetSize ?? 0;
		if (num12 == 0)
		{
			ushort keyTimeSize;
			ushort constChanMapSize;
			bool noChannelMap;
			bool noKeyTimes;
			byte[] data = BitPackHeaderOnly(array2, array, array3, num, num2, num3, num6, num7, num8, frameCount, cycle, out keyTimeSize, out constChanMapSize, out noChannelMap, out noKeyTimes);
			return BuildResult(template, frameCount, num9, num10, num11, num6, num7, num8, num13, num14, num19, num20, num15, num16, num17, num18, 0f, (ushort)array3.Length, keyTimeSize, constChanMapSize, noKeyTimes, noChannelMap, fastBitDecoder: false, cycle, flag, num, trajConstant, array3, Array.Empty<ushort>(), data, vectorOffsets, floatOffsets, vectorOffsetScale, floatOffsetScale, vectorOffsetSize, floatOffsetSize);
		}
		float[] dctData = ForwardDCT(norm, frameCount, alignedStride, num30);
		GetDctMinMax(dctData, num30, num12, alignedStride, out var dctMin, out var dctMax);
		float num31 = Math.Max(Math.Abs(dctMin), Math.Abs(dctMax));
		if (num31 == 0f)
		{
			num31 = 1f;
		}
		float[,] qTables = InitQuantizationTables(dctMin, dctMax);
		int num32 = num12 * 8 + 3;
		short[] array4 = new short[num30 * num32];
		for (int num33 = 0; num33 < num30; num33++)
		{
			DetermineQuantTables(num33, frameCount, dctData, norm, qTables, errBlock, num12, alignedStride, num9, num10, flag, linearSearchTables, out var rotTable, out var trjTable, out var traTable);
			array4[num33 * num32] = (short)rotTable;
			array4[num33 * num32 + 1] = (short)trjTable;
			array4[num33 * num32 + 2] = (short)traTable;
			QuantizeBlock(num33, frameCount, dctData, qTables, array4, num12, alignedStride, num9, num10, flag, rotTable, trjTable, traTable, num32);
		}
		bool fastBitDecoder;
		int[] numBits = ComputeNumBits(array4, num12, num30, num32, out fastBitDecoder);
		if (legacy)
		{
			int num34 = 1;
			while (!fastBitDecoder && num34 < 256)
			{
				for (int num35 = 0; num35 < num30; num35++)
				{
					int num36 = Math.Max(array4[num35 * num32], num34);
					int num37 = Math.Max(array4[num35 * num32 + 1], num34);
					int num38 = Math.Max(array4[num35 * num32 + 2], num34);
					array4[num35 * num32] = (short)num36;
					array4[num35 * num32 + 1] = (short)num37;
					array4[num35 * num32 + 2] = (short)num38;
					QuantizeBlock(num35, frameCount, dctData, qTables, array4, num12, alignedStride, num9, num10, flag, num36, num37, num38, num32);
				}
				numBits = ComputeNumBits(array4, num12, num30, num32, out fastBitDecoder);
				num34++;
			}
		}
		ushort[] frameBlockSizes;
		ushort keyTimeSize2;
		ushort constChanMapSize2;
		bool noKeyTimes2;
		bool noChannelMap2;
		byte[] data2 = BitPackAll(array, array2, array3, array4, numBits, frameCount, num30, num, num2, num3, num9, num10, num11, num12, alignedStride, flag, cycle, bigEndian, num32, vectorOffsets, floatOffsets, out frameBlockSizes, out keyTimeSize2, out constChanMapSize2, out noKeyTimes2, out noChannelMap2);
		return BuildResult(template, frameCount, num9, num10, num11, num6, num7, num8, num13, num14, num19, num20, num15, num16, num17, num18, num31, (ushort)array3.Length, keyTimeSize2, constChanMapSize2, noKeyTimes2, noChannelMap2, fastBitDecoder, cycle, flag, num, trajConstant, array3, frameBlockSizes, data2, vectorOffsets, floatOffsets, vectorOffsetScale, floatOffsetScale, vectorOffsetSize, floatOffsetSize);
	}

	private static void ExtractFromScene(Scene scene, VbrAnimationAsset template, int totalQ, int totalV, int totalF, out float[][][] quats, out float[][][] vecs, out float[][] floats, out int frameCount, out bool cycle)
	{
		float num = ((template.FPS > 0f) ? template.FPS : 30f);
		Animation animation = ((scene.AnimationCount > 0) ? scene.Animations[0] : null);
		double num2 = 0.0;
		if (animation != null && animation.TicksPerSecond > 0.0)
		{
			num2 = animation.DurationInTicks / animation.TicksPerSecond;
		}
		frameCount = Math.Max(2, (int)Math.Ceiling(num2 * (double)num) + 1);
		cycle = (template.Flags & 1) != 0;
		Dictionary<string, NodeAnimationChannel> dictionary = new Dictionary<string, NodeAnimationChannel>(StringComparer.OrdinalIgnoreCase);
		if (animation != null)
		{
			foreach (NodeAnimationChannel nodeAnimationChannel3 in animation.NodeAnimationChannels)
			{
				dictionary[nodeAnimationChannel3.NodeName] = nodeAnimationChannel3;
			}
		}
		App.Logger.Log("[DBG] FBX anim node count=" + dictionary.Count + " fps=" + num + " frames=" + Math.Max(2, (animation != null && animation.TicksPerSecond > 0.0) ? ((int)Math.Ceiling(animation.DurationInTicks / animation.TicksPerSecond * (double)num) + 1) : 2));
		int num3 = 0;
		foreach (string key in dictionary.Keys)
		{
			App.Logger.Log("[DBG] FBX node: '" + key + "'");
			if (++num3 >= 8)
			{
				break;
			}
		}
		List<string> list = new List<string>();
		List<string> list2 = new List<string>();
		List<string> list3 = new List<string>();
		if (template.OrderedChannels != null)
		{
			foreach (ChannelInfo orderedChannel in template.OrderedChannels)
			{
				string item = (orderedChannel.Name ?? "").Replace(".q", "").Replace(".t", "").Replace(".s", "")
					.Trim();
				switch (orderedChannel.Type)
				{
				case BoneChannelType.Rotation:
					list.Add(item);
					break;
				case BoneChannelType.Scale:
				case BoneChannelType.Position:
					list2.Add(item);
					break;
				default:
					list3.Add(item);
					break;
				}
			}
		}
		App.Logger.Log("[DBG] quatNames=" + list.Count + " vecNames=" + list2.Count + " floatNames=" + list3.Count);
		for (int i = 0; i < Math.Min(4, list.Count); i++)
		{
			App.Logger.Log("[DBG] quat[" + i + "]='" + list[i] + "' hit=" + (FindNode(dictionary, list[i]) != null));
		}
		for (int j = 0; j < Math.Min(4, list2.Count); j++)
		{
			App.Logger.Log("[DBG] vec[" + j + "]='" + list2[j] + "' hit=" + (FindNode(dictionary, list2[j]) != null));
		}
		while (list.Count < totalQ)
		{
			list.Add("");
		}
		while (list2.Count < totalV)
		{
			list2.Add("");
		}
		while (list3.Count < totalF)
		{
			list3.Add("");
		}
		quats = new float[totalQ][][];
		vecs = new float[totalV][][];
		floats = new float[totalF][];
		Frame frame = null;
		try
		{
			InternalAnimation internalAnimation = template.ConvertToInternal();
			if (internalAnimation != null && internalAnimation.Frames != null && internalAnimation.Frames.Count > 0)
			{
				frame = internalAnimation.Frames[0];
			}
		}
		catch
		{
		}
		int num4 = 0;
		for (int k = 0; k < totalQ; k++)
		{
			quats[k] = new float[frameCount][];
			NodeAnimationChannel nodeAnimationChannel = FindNode(dictionary, list[k]);
			bool flag = (nodeAnimationChannel == null || nodeAnimationChannel.RotationKeyCount == 0) && frame != null && frame.Rotations != null && k < frame.Rotations.Count;
			if (flag)
			{
				num4++;
			}
			for (int l = 0; l < frameCount; l++)
			{
				if (flag)
				{
					System.Numerics.Quaternion quaternion = frame.Rotations[k];
					quats[k][l] = new float[4] { quaternion.X, quaternion.Y, quaternion.Z, quaternion.W };
				}
				else
				{
					quats[k][l] = SampleQuat(nodeAnimationChannel, (double)l / (double)num, animation);
				}
			}
			EnforceQuatContinuity(quats[k]);
		}
		if (num4 > 0)
		{
			App.Logger.Log("[AnimationImporter] " + num4 + " of " + totalQ + " rotation channels are not in the imported file and keep their original pose.");
		}
		bool[] array = new bool[totalV];
		int num5 = 0;
		if (template.OrderedChannels != null)
		{
			foreach (ChannelInfo orderedChannel2 in template.OrderedChannels)
			{
				if ((orderedChannel2.Type == BoneChannelType.Position || orderedChannel2.Type == BoneChannelType.Scale) && num5 < totalV)
				{
					array[num5++] = orderedChannel2.Type == BoneChannelType.Scale;
				}
			}
		}
		int num6 = 0;
		int num7 = 0;
		for (int m = 0; m < totalV; m++)
		{
			vecs[m] = new float[frameCount][];
			NodeAnimationChannel nodeAnimationChannel2 = FindNode(dictionary, list2[m]);
			int num8 = (array[m] ? num7++ : num6++);
			List<Vector3> list4 = ((frame == null) ? null : (array[m] ? frame.Scales : frame.Positions));
			bool flag2 = (nodeAnimationChannel2 == null || (array[m] ? nodeAnimationChannel2.ScalingKeyCount : nodeAnimationChannel2.PositionKeyCount) == 0) && list4 != null && num8 < list4.Count;
			for (int n = 0; n < frameCount; n++)
			{
				if (flag2)
				{
					Vector3 vector = list4[num8];
					vecs[m][n] = new float[3] { vector.X, vector.Y, vector.Z };
				}
				else
				{
					vecs[m][n] = SampleVec3(nodeAnimationChannel2, (double)n / (double)num, animation, array[m]);
				}
			}
		}
		for (int num9 = 0; num9 < totalF; num9++)
		{
			floats[num9] = new float[frameCount];
			for (int num10 = 0; num10 < frameCount; num10++)
			{
				floats[num9][num10] = 0f;
			}
		}
	}

	private static NodeAnimationChannel FindNode(Dictionary<string, NodeAnimationChannel> map, string name)
	{
		if (string.IsNullOrEmpty(name))
		{
			return null;
		}
		if (map.TryGetValue(name, out var value))
		{
			return value;
		}
		if (name.Length > 2 && name[name.Length - 2] == '.' && map.TryGetValue(name.Substring(0, name.Length - 2), out value))
		{
			return value;
		}
		return null;
	}

	private static float[] SampleQuat(NodeAnimationChannel nodeCh, double t, Animation anim)
	{
		if (nodeCh == null || nodeCh.RotationKeyCount == 0)
		{
			return new float[4] { 0f, 0f, 0f, 1f };
		}
		double num = t * ((anim != null && anim.TicksPerSecond > 0.0) ? anim.TicksPerSecond : 1.0);
		List<QuaternionKey> rotationKeys = nodeCh.RotationKeys;
		if (rotationKeys.Count == 1)
		{
			return QuatToArray(rotationKeys[0].Value);
		}
		int num2 = rotationKeys.Count - 2;
		for (int i = 0; i < rotationKeys.Count - 1; i++)
		{
			if (num <= rotationKeys[i + 1].Time)
			{
				num2 = i;
				break;
			}
		}
		double num3 = rotationKeys[num2 + 1].Time - rotationKeys[num2].Time;
		float val = ((num3 > 0.0) ? ((float)((num - rotationKeys[num2].Time) / num3)) : 0f);
		val = Math.Max(0f, Math.Min(1f, val));
		Assimp.Quaternion value = rotationKeys[num2].Value;
		Assimp.Quaternion value2 = rotationKeys[num2 + 1].Value;
		return QuatToArray(Assimp.Quaternion.Slerp(value, value2, val));
	}

	private static float[] SampleVec3(NodeAnimationChannel nodeCh, double t, Animation anim, bool isScale)
	{
		if (nodeCh == null)
		{
			if (isScale)
			{
				return new float[3] { 1f, 1f, 1f };
			}
			return new float[3];
		}
		double num = t * ((anim != null && anim.TicksPerSecond > 0.0) ? anim.TicksPerSecond : 1.0);
		if (isScale)
		{
			if (nodeCh.ScalingKeyCount == 0)
			{
				return new float[3] { 1f, 1f, 1f };
			}
			List<VectorKey> scalingKeys = nodeCh.ScalingKeys;
			if (scalingKeys.Count == 1)
			{
				return Vec3ToArray(scalingKeys[0].Value);
			}
			int num2 = scalingKeys.Count - 2;
			for (int i = 0; i < scalingKeys.Count - 1; i++)
			{
				if (num <= scalingKeys[i + 1].Time)
				{
					num2 = i;
					break;
				}
			}
			double num3 = scalingKeys[num2 + 1].Time - scalingKeys[num2].Time;
			float val = ((num3 > 0.0) ? ((float)((num - scalingKeys[num2].Time) / num3)) : 0f);
			val = Math.Max(0f, Math.Min(1f, val));
			Vector3D value = scalingKeys[num2].Value;
			Vector3D value2 = scalingKeys[num2 + 1].Value;
			return new float[3]
			{
				value.X + (value2.X - value.X) * val,
				value.Y + (value2.Y - value.Y) * val,
				value.Z + (value2.Z - value.Z) * val
			};
		}
		if (nodeCh.PositionKeyCount == 0)
		{
			return new float[3];
		}
		List<VectorKey> positionKeys = nodeCh.PositionKeys;
		if (positionKeys.Count == 1)
		{
			return Vec3ToArray(positionKeys[0].Value);
		}
		int num4 = positionKeys.Count - 2;
		for (int j = 0; j < positionKeys.Count - 1; j++)
		{
			if (num <= positionKeys[j + 1].Time)
			{
				num4 = j;
				break;
			}
		}
		double num5 = positionKeys[num4 + 1].Time - positionKeys[num4].Time;
		float val2 = ((num5 > 0.0) ? ((float)((num - positionKeys[num4].Time) / num5)) : 0f);
		val2 = Math.Max(0f, Math.Min(1f, val2));
		Vector3D value3 = positionKeys[num4].Value;
		Vector3D value4 = positionKeys[num4 + 1].Value;
		return new float[3]
		{
			value3.X + (value4.X - value3.X) * val2,
			value3.Y + (value4.Y - value3.Y) * val2,
			value3.Z + (value4.Z - value3.Z) * val2
		};
	}

	private static void EnforceQuatContinuity(float[][] frames)
	{
		if (frames == null || frames.Length < 2)
		{
			return;
		}
		for (int i = 1; i < frames.Length; i++)
		{
			float[] array = frames[i - 1];
			float[] array2 = frames[i];
			if (array != null && array2 != null && array.Length >= 4 && array2.Length >= 4 && array[0] * array2[0] + array[1] * array2[1] + array[2] * array2[2] + array[3] * array2[3] < 0f)
			{
				array2[0] = 0f - array2[0];
				array2[1] = 0f - array2[1];
				array2[2] = 0f - array2[2];
				array2[3] = 0f - array2[3];
			}
		}
	}

	private static float[] QuatToArray(Assimp.Quaternion q)
	{
		return new float[4] { q.X, q.Y, q.Z, q.W };
	}

	private static float[] Vec3ToArray(Vector3D v)
	{
		return new float[3] { v.X, v.Y, v.Z };
	}

	private static bool[] MarkConstantChannels(float[][][] quats, float[][][] vecs, float[][] floats, int frameCount, int totalQ, int totalV, int totalF, float threshold)
	{
		bool[] array = new bool[totalQ + totalV + totalF];
		int num = 0;
		int num2 = 0;
		while (num2 < totalQ)
		{
			if (frameCount < 2)
			{
				array[num] = true;
			}
			else
			{
				float[] array2 = quats[num2][0];
				float num3 = 0f;
				float num4 = 0f;
				float num5 = 0f;
				float num6 = 0f;
				for (int i = 1; i < frameCount; i++)
				{
					float[] array3 = quats[num2][i];
					num3 += Math.Abs(array2[0] - array3[0]);
					num4 += Math.Abs(array2[1] - array3[1]);
					num5 += Math.Abs(array2[2] - array3[2]);
					num6 += Math.Abs(array2[3] - array3[3]);
				}
				array[num] = num3 <= threshold && num4 <= threshold && num5 <= threshold && num6 <= threshold;
			}
			num2++;
			num++;
		}
		int num7 = 0;
		while (num7 < totalV)
		{
			if (frameCount < 2)
			{
				array[num] = true;
			}
			else
			{
				float[] array4 = vecs[num7][0];
				float num8 = 0f;
				float num9 = 0f;
				float num10 = 0f;
				for (int j = 1; j < frameCount; j++)
				{
					float[] array5 = vecs[num7][j];
					num8 += Math.Abs(array4[0] - array5[0]);
					num9 += Math.Abs(array4[1] - array5[1]);
					num10 += Math.Abs(array4[2] - array5[2]);
				}
				array[num] = num8 <= threshold && num9 <= threshold && num10 <= threshold;
			}
			num7++;
			num++;
		}
		int num11 = 0;
		while (num11 < totalF)
		{
			if (frameCount < 2)
			{
				array[num] = true;
			}
			else
			{
				float num12 = 0f;
				float num13 = floats[num11][0];
				for (int k = 1; k < frameCount; k++)
				{
					num12 += Math.Abs(num13 - floats[num11][k]);
				}
				array[num] = num12 <= threshold;
			}
			num11++;
			num++;
		}
		return array;
	}

	private static float[] SaveConstantChannels(float[][][] quats, float[][][] vecs, float[][] floats, bool[] constCh, int totalQ, int totalV, int totalF, int frameCount)
	{
		int num = 0;
		for (int i = 0; i < totalQ; i++)
		{
			if (constCh[i])
			{
				num += 4;
			}
		}
		for (int j = 0; j < totalV; j++)
		{
			if (constCh[totalQ + j])
			{
				num += 3;
			}
		}
		for (int k = 0; k < totalF; k++)
		{
			if (constCh[totalQ + totalV + k])
			{
				num++;
			}
		}
		float[] array = new float[num];
		int num2 = 0;
		for (int l = 0; l < totalQ; l++)
		{
			if (constCh[l])
			{
				float[] array2 = quats[l][0];
				array[num2++] = array2[0];
				array[num2++] = array2[1];
				array[num2++] = array2[2];
				array[num2++] = array2[3];
			}
		}
		for (int m = 0; m < totalV; m++)
		{
			if (constCh[totalQ + m])
			{
				float[] array3 = vecs[m][0];
				array[num2++] = array3[0];
				array[num2++] = array3[1];
				array[num2++] = array3[2];
			}
		}
		for (int n = 0; n < totalF; n++)
		{
			if (constCh[totalQ + totalV + n])
			{
				array[num2++] = floats[n][0];
			}
		}
		return array;
	}

	private static void ComputeMinMax(float[][][] quats, float[][][] vecs, float[][] floats, bool[] constCh, int frameCount, int totalQ, int totalV, int totalF, int aQ, int aV, int aF, out float[] qMin, out float[] qMax, out float[] vMin, out float[] vMax, out float[] fMin, out float[] fMax, CurveFitHelper cfHelper = null)
	{
		qMin = new float[aQ];
		qMax = new float[aQ];
		vMin = new float[aV];
		vMax = new float[aV];
		fMin = new float[aF];
		fMax = new float[aF];
		int num = 0;
		for (int i = 0; i < totalQ; i++)
		{
			if (constCh[i])
			{
				continue;
			}
			float num2 = 0f;
			float num3 = 0f;
			for (int j = 0; j < frameCount; j++)
			{
				float[] array = quats[i][j];
				for (int k = 0; k < 4; k++)
				{
					if (array[k] < num2)
					{
						num2 = array[k];
					}
					if (array[k] > num3)
					{
						num3 = array[k];
					}
				}
			}
			qMin[num] = num2;
			qMax[num] = num3;
			num++;
		}
		int num4 = 0;
		for (int l = 0; l < totalV; l++)
		{
			if (constCh[totalQ + l])
			{
				continue;
			}
			float num5 = 0f;
			float num6 = 0f;
			for (int m = 0; m < frameCount; m++)
			{
				float x;
				float y;
				float z;
				if (cfHelper != null)
				{
					cfHelper.GetToDeltaChannelsVector3(num4, m, out x, out y, out z);
				}
				else
				{
					float[] obj = vecs[l][m];
					x = obj[0];
					y = obj[1];
					z = obj[2];
				}
				if (x < num5)
				{
					num5 = x;
				}
				if (x > num6)
				{
					num6 = x;
				}
				if (y < num5)
				{
					num5 = y;
				}
				if (y > num6)
				{
					num6 = y;
				}
				if (z < num5)
				{
					num5 = z;
				}
				if (z > num6)
				{
					num6 = z;
				}
			}
			vMin[num4] = num5;
			vMax[num4] = num6;
			num4++;
		}
		int num7 = 0;
		for (int n = 0; n < totalF; n++)
		{
			if (constCh[totalQ + totalV + n])
			{
				continue;
			}
			float num8 = 0f;
			float num9 = 0f;
			for (int num10 = 0; num10 < frameCount; num10++)
			{
				float num11 = cfHelper?.GetToDeltaChannelFloat(num7, num10) ?? floats[n][num10];
				if (num11 < num8)
				{
					num8 = num11;
				}
				if (num11 > num9)
				{
					num9 = num11;
				}
			}
			fMin[num7] = num8;
			fMax[num7] = num9;
			num7++;
		}
	}

	private static float[] BuildErrorBlock(int stride, int aQ, int aV, bool sepTraj, float rotErr, float transErr, float trajErr)
	{
		float[] array = new float[stride];
		for (int i = 0; i < aQ * 4; i++)
		{
			array[i] = rotErr;
		}
		int num = aQ * 4;
		if (aV > 0 && sepTraj)
		{
			array[aQ * 4] = trajErr;
			array[aQ * 4 + 1] = trajErr;
			array[aQ * 4 + 2] = trajErr;
			num = aQ * 4 + 3;
		}
		for (int j = num; j < stride; j++)
		{
			array[j] = transErr;
		}
		return array;
	}

	private static bool IsSimilar(float a, float b, float epsilon = 1.1920929E-07f)
	{
		return Math.Abs(a - b) < epsilon;
	}

	private static void NormalizeConstantData(float[] cData, int cQ, int cV, int cF, float quatMin, float quatMax, float v3Min, float v3Max, float fltMin, float fltMax)
	{
		float num = ((!IsSimilar(quatMax - quatMin, 0f)) ? (1f / (quatMax - quatMin)) : 1f);
		float num2 = ((!IsSimilar(v3Max - v3Min, 0f)) ? (1f / (v3Max - v3Min)) : 1f);
		float num3 = ((!IsSimilar(fltMax - fltMin, 0f)) ? (1f / (fltMax - fltMin)) : 1f);
		int num4 = 0;
		int num5 = 0;
		while (num5 < cQ * 4)
		{
			cData[num4] = (cData[num4] - quatMin) * num;
			num5++;
			num4++;
		}
		int num6 = 0;
		while (num6 < cV * 3)
		{
			cData[num4] = (cData[num4] - v3Min) * num2;
			num6++;
			num4++;
		}
		int num7 = 0;
		while (num7 < cF)
		{
			cData[num4] = (cData[num4] - fltMin) * num3;
			num7++;
			num4++;
		}
	}

	private static float[] NormalizeAnimated(float[][][] quats, float[][][] vecs, float[][] floats, bool[] constCh, int frameCount, int totalQ, int totalV, int totalF, int aQ, int aV, int aF, int alignedStride, float[] qMinCh, float[] qMaxCh, float[] vMinCh, float[] vMaxCh, float[] fMinCh, float[] fMaxCh, CurveFitHelper cfHelper = null)
	{
		float[] array = new float[frameCount * alignedStride];
		int num = 0;
		for (int i = 0; i < totalQ; i++)
		{
			if (!constCh[i])
			{
				float num2 = qMinCh[num];
				float num3 = ((!IsSimilar(qMaxCh[num] - num2, 0f)) ? (1f / (qMaxCh[num] - num2)) : 1f);
				for (int j = 0; j < frameCount; j++)
				{
					float[] array2 = quats[i][j];
					int num4 = j * alignedStride + num * 4;
					array[num4] = (array2[0] - num2) * num3;
					array[num4 + 1] = (array2[1] - num2) * num3;
					array[num4 + 2] = (array2[2] - num2) * num3;
					array[num4 + 3] = (array2[3] - num2) * num3;
				}
				num++;
			}
		}
		int num5 = 0;
		for (int k = 0; k < totalV; k++)
		{
			if (constCh[totalQ + k])
			{
				continue;
			}
			float num6 = vMinCh[num5];
			float num7 = ((!IsSimilar(vMaxCh[num5] - num6, 0f)) ? (1f / (vMaxCh[num5] - num6)) : 1f);
			for (int l = 0; l < frameCount; l++)
			{
				float x;
				float y;
				float z;
				if (cfHelper != null)
				{
					cfHelper.GetToDeltaChannelsVector3(num5, l, out x, out y, out z);
				}
				else
				{
					float[] obj = vecs[k][l];
					x = obj[0];
					y = obj[1];
					z = obj[2];
				}
				int num8 = l * alignedStride + aQ * 4 + num5 * 3;
				array[num8] = (x - num6) * num7;
				array[num8 + 1] = (y - num6) * num7;
				array[num8 + 2] = (z - num6) * num7;
			}
			num5++;
		}
		int num9 = 0;
		for (int m = 0; m < totalF; m++)
		{
			if (!constCh[totalQ + totalV + m])
			{
				float num10 = fMinCh[num9];
				float num11 = ((!IsSimilar(fMaxCh[num9] - num10, 0f)) ? (1f / (fMaxCh[num9] - num10)) : 1f);
				for (int n = 0; n < frameCount; n++)
				{
					float num12 = cfHelper?.GetToDeltaChannelFloat(num9, n) ?? floats[m][n];
					array[n * alignedStride + aQ * 4 + aV * 3 + num9] = (num12 - num10) * num11;
				}
				num9++;
			}
		}
		return array;
	}

	private static float[] ForwardDCT(float[] norm, int frameCount, int alignedStride, int frameBlocks)
	{
		float[] array = new float[frameBlocks * 8 * alignedStride];
		for (int i = 0; i < frameBlocks; i++)
		{
			int num = ((frameCount >= 8) ? ((i * 8 + 8 > frameCount) ? (frameCount - 8) : (i * 8)) : 0);
			float[] array2 = new float[8];
			for (int j = 0; j < alignedStride; j++)
			{
				for (int k = 0; k < 8; k++)
				{
					int num2 = ((frameCount < 8) ? ((k < frameCount) ? k : (frameCount - 1)) : (num + k));
					array2[k] = norm[num2 * alignedStride + j] - 0.5f;
				}
				for (int l = 0; l < 8; l++)
				{
					float num3 = 0f;
					for (int m = 0; m < 8; m++)
					{
						num3 += array2[m] * DctFwd[m, l];
					}
					array[(i * 8 + l) * alignedStride + j] = num3;
				}
			}
		}
		return array;
	}

	private static void GetDctMinMax(float[] dctData, int frameBlocks, int stride, int alignedStride, out float dctMin, out float dctMax)
	{
		dctMin = float.MaxValue;
		dctMax = float.MinValue;
		for (int i = 0; i < frameBlocks; i++)
		{
			for (int j = 0; j < 8; j++)
			{
				for (int k = 0; k < stride; k++)
				{
					float num = dctData[(i * 8 + j) * alignedStride + k];
					if (num < dctMin)
					{
						dctMin = num;
					}
					if (num > dctMax)
					{
						dctMax = num;
					}
				}
			}
		}
		if (dctMin == float.MaxValue)
		{
			dctMin = 0f;
			dctMax = 0f;
		}
	}

	private static float[,] InitQuantizationTables(float dctMin, float dctMax)
	{
		float num = Math.Max(Math.Abs(dctMin), Math.Abs(dctMax));
		if (num == 0f)
		{
			num = 1f;
		}
		float[,] array = new float[256, 8];
		for (int i = 0; i < 256; i++)
		{
			float num2 = (float)(i + 1) * 0.2f;
			for (int j = 0; j < 8; j++)
			{
				double num3 = Math.Log(j + 2);
				array[i, j] = (float)((1.0 + (double)(num * num2) * num3) * (double)num / 32768.0);
			}
		}
		return array;
	}

	private static void DetermineQuantTables(int fb, int frameCount, float[] dctData, float[] norm, float[,] qTables, float[] errBlock, int stride, int alignedStride, int aQ, int aV, bool sepTraj, bool linearSearchTables, out int rotTable, out int trjTable, out int traTable)
	{
		if (linearSearchTables)
		{
			rotTable = FindBestQuantTableLinear(fb, frameCount, dctData, norm, qTables, errBlock, stride, alignedStride, 0, aQ * 4);
			if (sepTraj)
			{
				trjTable = FindBestQuantTableLinear(fb, frameCount, dctData, norm, qTables, errBlock, stride, alignedStride, aQ * 4, 4);
				int num = (aQ + 1) * 4;
				int num2 = stride - num;
				traTable = ((num2 > 0) ? FindBestQuantTableLinear(fb, frameCount, dctData, norm, qTables, errBlock, stride, alignedStride, num, num2) : trjTable);
			}
			else
			{
				int num3 = aQ * 4;
				int num4 = stride - num3;
				trjTable = (traTable = ((num4 > 0) ? FindBestQuantTableLinear(fb, frameCount, dctData, norm, qTables, errBlock, stride, alignedStride, num3, num4) : 0));
			}
		}
		else
		{
			rotTable = FindBestQuantTable(fb, frameCount, dctData, norm, qTables, errBlock, stride, alignedStride, 0, aQ * 4);
			if (sepTraj)
			{
				trjTable = FindBestQuantTable(fb, frameCount, dctData, norm, qTables, errBlock, stride, alignedStride, aQ * 4, 4);
				int num5 = (aQ + 1) * 4;
				int num6 = stride - num5;
				traTable = ((num6 > 0) ? FindBestQuantTable(fb, frameCount, dctData, norm, qTables, errBlock, stride, alignedStride, num5, num6) : trjTable);
			}
			else
			{
				int num7 = aQ * 4;
				int num8 = stride - num7;
				trjTable = (traTable = ((num8 > 0) ? FindBestQuantTable(fb, frameCount, dctData, norm, qTables, errBlock, stride, alignedStride, num7, num8) : 0));
			}
		}
	}

	private static int FindBestQuantTable(int fb, int frameCount, float[] dctData, float[] norm, float[,] qTables, float[] errBlock, int stride, int alignedStride, int compOffset, int compCount)
	{
		if (compCount <= 0)
		{
			return 0;
		}
		int num = 0;
		int num2 = 256;
		while (num2 > 0)
		{
			int num3 = num2 >> 1;
			int num4 = num + num3;
			if (TestQuantizationTable(fb, frameCount, dctData, norm, qTables, errBlock, stride, alignedStride, compOffset, compCount, num4))
			{
				num = num4 + 1;
				num2 -= num3 + 1;
			}
			else
			{
				num2 = num3;
			}
		}
		if (num > 0)
		{
			num--;
		}
		return num;
	}

	private static int FindBestQuantTableLinear(int fb, int frameCount, float[] dctData, float[] norm, float[,] qTables, float[] errBlock, int stride, int alignedStride, int compOffset, int compCount)
	{
		if (compCount <= 0)
		{
			return 0;
		}
		int num = 0;
		for (int num2 = 256 - 1; num2 >= num; num2--)
		{
			if (TestQuantizationTable(fb, frameCount, dctData, norm, qTables, errBlock, stride, alignedStride, compOffset, compCount, num2))
			{
				return num2;
			}
		}
		return num;
	}

	private static bool TestQuantizationTable(int fb, int frameCount, float[] dctData, float[] norm, float[,] qTables, float[] errBlock, int stride, int alignedStride, int compOffset, int compCount, int tableIdx)
	{
		int num = ((frameCount >= 8) ? ((fb * 8 + 8 > frameCount) ? (frameCount - 8) : (fb * 8)) : 0);
		int num2 = Math.Min(8, frameCount);
		int num3 = Math.Min(compOffset + compCount, stride);
		for (int i = compOffset; i < num3; i++)
		{
			float[] array = new float[8];
			for (int j = 0; j < 8; j++)
			{
				float num4 = qTables[tableIdx, j];
				double num5 = Math.Round(dctData[(fb * 8 + j) * alignedStride + i] / num4, MidpointRounding.AwayFromZero);
				if (num5 > 32767.0)
				{
					num5 = 32767.0;
				}
				if (num5 < -32768.0)
				{
					num5 = -32768.0;
				}
				short num6 = (short)num5;
				array[j] = (float)num6 * num4;
			}
			for (int k = 0; k < num2; k++)
			{
				float num7 = 0.5f;
				for (int l = 0; l < 8; l++)
				{
					num7 += array[l] * DctInv[k, l];
				}
				float num8 = norm[(num + k) * alignedStride + i];
				if (Math.Abs(num7 - num8) > errBlock[i])
				{
					return false;
				}
			}
		}
		return true;
	}

	private static void QuantizeBlock(int fb, int frameCount, float[] dctData, float[,] qTables, short[] intermediate, int stride, int alignedStride, int aQ, int aV, bool sepTraj, int rotTable, int trjTable, int traTable, int numShorts)
	{
		int num = (sepTraj ? (aQ * 4 + 4) : (-1));
		for (int i = 0; i < stride; i++)
		{
			int num2 = ((i < aQ * 4) ? rotTable : ((!sepTraj || i >= num) ? traTable : trjTable));
			for (int j = 0; j < 8; j++)
			{
				float num3 = qTables[num2, j];
				double num4 = Math.Round(dctData[(fb * 8 + j) * alignedStride + i] / num3, MidpointRounding.AwayFromZero);
				if (num4 > 32767.0)
				{
					num4 = 32767.0;
				}
				if (num4 < -32768.0)
				{
					num4 = -32768.0;
				}
				intermediate[fb * numShorts + i * 8 + j + 3] = (short)num4;
			}
		}
	}

	private static int[] ComputeNumBits(short[] intermediate, int stride, int frameBlocks, int numShorts, out bool fastBitDecoder)
	{
		int[] array = new int[stride * 8];
		for (int i = 0; i < frameBlocks; i++)
		{
			for (int j = 0; j < stride; j++)
			{
				for (int k = 0; k < 8; k++)
				{
					int num = Math.Abs((int)intermediate[i * numShorts + j * 8 + k + 3]);
					int num2 = j * 8 + k;
					if (num > array[num2])
					{
						array[num2] = num;
					}
				}
			}
		}
		int[] array2 = new int[stride * 8];
		int num3 = 0;
		for (int l = 0; l < stride; l++)
		{
			int num4 = 0;
			for (int m = 0; m < 8; m++)
			{
				int num5 = l * 8 + m;
				int num6 = array[num5];
				int num7 = 0;
				if (num6 > 0)
				{
					num4 += 2;
					num7 = (int)Math.Ceiling(Math.Log((double)num6 + 0.5) / Math.Log(2.0));
					num4 += num7;
				}
				array2[num5] = num7;
			}
			if (num4 > num3)
			{
				num3 = num4;
			}
		}
		fastBitDecoder = num3 <= 128;
		return array2;
	}

	private static float[] BuildConstantPalette(float[] cData, int cQ, int cV, int cF, string clipName, int paletteLimit = 4096)
	{
		int num = cQ * 4 + cV * 3 + cF;
		if (num == 0)
		{
			return Array.Empty<float>();
		}
		List<float> list = new List<float>();
		bool flag = false;
		int i;
		for (i = 0; i < num; i++)
		{
			float num2 = cData[i];
			bool flag2 = false;
			for (int j = 0; j < list.Count; j++)
			{
				if (Math.Abs(num2 - list[j]) <= 4.76837158E-07f)
				{
					flag2 = true;
					break;
				}
			}
			if (!flag2)
			{
				list.Add(num2);
				if (list.Count >= paletteLimit && i < num - 1)
				{
					flag = true;
					break;
				}
			}
		}
		if (!flag && i == num)
		{
			return list.ToArray();
		}
		float[] array = new float[paletteLimit];
		for (int k = 0; k < paletteLimit; k++)
		{
			array[k] = (float)k / (float)(paletteLimit - 1);
		}
		bool[] array2 = new bool[paletteLimit];
		for (int l = 0; l < num; l++)
		{
			int num3 = FindClosestPaletteIndex(cData[l], array);
			array2[num3] = true;
		}
		List<float> list2 = new List<float>();
		for (int m = 0; m < paletteLimit; m++)
		{
			if (array2[m])
			{
				list2.Add(array[m]);
			}
		}
		return list2.ToArray();
	}

	private static int FindClosestPaletteIndex(float value, float[] palette)
	{
		int result = 0;
		float num = float.MaxValue;
		for (int i = 0; i < palette.Length; i++)
		{
			float num2 = Math.Abs(palette[i] - value);
			if (num2 < num)
			{
				num = num2;
				result = i;
			}
		}
		return result;
	}

	private static byte[] BitPackHeaderOnly(float[] cData, bool[] constCh, float[] constPalette, int totalQ, int totalV, int totalF, int cQ, int cV, int cF, int frameCount, bool cycle, out ushort keyTimeSize, out ushort constChanMapSize, out bool noChannelMap, out bool noKeyTimes)
	{
		BitWriter bitWriter = new BitWriter();
		keyTimeSize = 0;
		noKeyTimes = true;
		WriteConstantChannelHeader(bitWriter, cData, constCh, constPalette, totalQ, totalV, totalF, cQ, cV, cF, out constChanMapSize, out noChannelMap);
		return bitWriter.ToArray();
	}

	private static byte[] BitPackAll(bool[] constCh, float[] cData, float[] constPalette, short[] intermediate, int[] numBits, int frameCount, int frameBlocks, int totalQ, int totalV, int totalF, int aQ, int aV, int aF, int stride, int alignedStride, bool sepTraj, bool cycle, bool bigEndian, int numShorts, byte[] vectorOffsets, byte[] floatOffsets, out ushort[] frameBlockSizes, out ushort keyTimeSize, out ushort constChanMapSize, out bool noKeyTimes, out bool noChannelMap)
	{
		BitWriter bitWriter = new BitWriter();
		keyTimeSize = 0;
		noKeyTimes = true;
		WriteConstantChannelHeader(bitWriter, cData, constCh, constPalette, totalQ, totalV, totalF, totalQ - aQ, totalV - aV, totalF - aF, out constChanMapSize, out noChannelMap);
		for (int i = 0; i < stride; i++)
		{
			for (int j = 0; j < 8; j++)
			{
				bitWriter.WriteBits((uint)numBits[i * 8 + j], 4);
			}
		}
		int num = 0;
		if (vectorOffsets != null)
		{
			num = vectorOffsets.Length;
			for (int k = 0; k < num; k++)
			{
				bitWriter.WriteBits(vectorOffsets[k], 8);
			}
		}
		int num2 = 0;
		if (floatOffsets != null)
		{
			num2 = floatOffsets.Length;
			for (int l = 0; l < num2; l++)
			{
				bitWriter.WriteBits(floatOffsets[l], 8);
			}
		}
		bitWriter.Flush();
		int byteCount = bitWriter.ByteCount;
		int[] array = new int[frameBlocks];
		frameBlockSizes = new ushort[frameBlocks];
		for (int m = 0; m < frameBlocks; m++)
		{
			array[m] = bitWriter.ByteCount;
			bitWriter.ResetBitCount();
			bitWriter.WriteBits((byte)intermediate[m * numShorts], 8);
			bitWriter.WriteBits((byte)intermediate[m * numShorts + 1], 8);
			bitWriter.WriteBits((byte)intermediate[m * numShorts + 2], 8);
			for (int n = 0; n < stride; n++)
			{
				for (int num3 = 0; num3 < 8; num3++)
				{
					if (numBits[n * 8 + num3] > 0)
					{
						bitWriter.WriteBits((intermediate[m * numShorts + n * 8 + num3 + 3] != 0) ? 1u : 0u, 1);
					}
				}
				for (int num4 = 0; num4 < 8; num4++)
				{
					if (numBits[n * 8 + num4] > 0)
					{
						short num5 = intermediate[m * numShorts + n * 8 + num4 + 3];
						if (num5 != 0)
						{
							bitWriter.WriteBits((num5 > 0) ? 1u : 0u, 1);
						}
					}
				}
				for (int num6 = 0; num6 < 8; num6++)
				{
					int num7 = numBits[n * 8 + num6];
					if (num7 > 0)
					{
						short num8 = intermediate[m * numShorts + n * 8 + num6 + 3];
						if (num8 != 0)
						{
							bitWriter.WriteBits((uint)Math.Abs((int)num8), num7);
						}
					}
				}
			}
			bitWriter.Flush();
			frameBlockSizes[m] = (ushort)(bitWriter.ByteCount - array[m]);
		}
		byte[] array2 = bitWriter.ToArray();
		if (!bigEndian)
		{
			int num9 = totalQ - aQ;
			int num10 = totalV - aV;
			int num11 = totalF - aF;
			int num12 = num9 * 4 + num10 * 3 + num11;
			bool num13 = constPalette.Length <= 256;
			int num14 = keyTimeSize + num12 + constChanMapSize + stride * 4 + num + num2;
			int num15 = (num13 ? byteCount : num14);
			for (int num16 = 0; num16 < frameBlocks; num16++)
			{
				int num17 = frameBlockSizes[num16];
				if (num17 > 3)
				{
					int num18 = num15 + 3;
					int num19 = num15 + num17 - 1;
					while (num18 < num19 && num18 >= 0 && num19 < array2.Length)
					{
						byte b = array2[num18];
						array2[num18] = array2[num19];
						array2[num19] = b;
						num18++;
						num19--;
					}
				}
				num15 += num17;
			}
		}
		return array2;
	}

	private static void WriteConstantChannelHeader(BitWriter writer, float[] cData, bool[] constCh, float[] constPalette, int totalQ, int totalV, int totalF, int cQ, int cV, int cF, out ushort constChanMapSize, out bool noChannelMap)
	{
		int num = cQ * 4 + cV * 3 + cF;
		int bits = ((constPalette.Length <= 256) ? 8 : 16);
		for (int i = 0; i < num; i++)
		{
			writer.WriteBits((uint)FindClosestPaletteIndex(cData[i], constPalette), bits);
		}
		writer.Flush();
		int num2 = totalQ + totalV + totalF;
		noChannelMap = num == 0;
		constChanMapSize = 0;
		if (noChannelMap)
		{
			return;
		}
		bool flag = false;
		int num3 = 0;
		int num4 = 0;
		while (num4 < num2)
		{
			if (constCh[num4] == flag && num3 < 255)
			{
				num3++;
				num4++;
				continue;
			}
			writer.WriteBits((uint)num3, 8);
			constChanMapSize++;
			num3 = 0;
			flag = !flag;
		}
		if (num3 > 0)
		{
			writer.WriteBits((uint)num3, 8);
			constChanMapSize++;
		}
		writer.Flush();
	}

	private static VbrAnimationAsset BuildResult(VbrAnimationAsset template, int frameCount, int aQ, int aV, int aF, int cQ, int cV, int cF, float quatMin, float quatMax, float tjMin, float tjMax, float v3Min, float v3Max, float fltMin, float fltMax, float dct, ushort constPaletteSize, ushort keyTimeSize, ushort constChanMapSize, bool noKeyTimes, bool noChannelMap, bool fastBitDecoder, bool cycle, bool sepTraj, int totalQ, bool trajConstant, float[] constPalette, ushort[] frameBlockSizes, byte[] data, byte[] vectorOffsets = null, byte[] floatOffsets = null, float vectorOffsetScale = 0f, float floatOffsetScale = 0f, ushort vectorOffsetSize = 0, ushort floatOffsetSize = 0)
	{
		ushort num = 0;
		if (cycle)
		{
			num |= 1;
		}
		if (noKeyTimes)
		{
			num |= 2;
		}
		if (fastBitDecoder)
		{
			num |= 4;
		}
		if (noChannelMap)
		{
			num |= 8;
		}
		if (trajConstant)
		{
			num |= 0x80;
		}
		VbrAnimationAsset obj = new VbrAnimationAsset
		{
			AssetType = template.AssetType,
			Bank = template.Bank,
			OrderedChannels = template.OrderedChannels,
			Channels = template.Channels,
			ChannelToDofAsset = template.ChannelToDofAsset,
			DofSetList = template.DofSetList,
			AnimId = template.AnimId,
			CodecType = template.CodecType,
			TrimOffset = template.TrimOffset,
			Additive = template.Additive,
			FPS = template.FPS,
			EndFrame = (ushort)(frameCount - 1),
			NumKeys = (ushort)frameCount,
			QuaternionCount = (ushort)aQ,
			Vector3Count = (ushort)aV,
			NumFloat = (ushort)aF,
			ConstQuaternionCount = (ushort)cQ,
			ConstVector3Count = (ushort)cV,
			ConstFloatCount = (ushort)cF,
			QuatMin = quatMin,
			QuatMax = quatMax,
			TrajMin = tjMin,
			TrajMax = tjMax,
			Vec3Min = v3Min,
			Vec3Max = v3Max,
			FloatMin = fltMin,
			FloatMax = fltMax,
			Dct = dct,
			KeyTimeSize = keyTimeSize,
			ConstChanMapSize = constChanMapSize,
			ConstPaletteSize = constPaletteSize,
			VectorOffsetScale = vectorOffsetScale,
			FloatOffsetScale = floatOffsetScale,
			VectorOffsetSize = vectorOffsetSize,
			FloatOffsetSize = floatOffsetSize,
			VectorOffsets = vectorOffsets,
			Flags = num,
			ConstantPalette = constPalette,
			FrameBlockSizes = frameBlockSizes,
			Data = data
		};
		Dictionary<string, object> dictionary = ((template.RawData != null) ? new Dictionary<string, object>(template.RawData) : new Dictionary<string, object>());
		dictionary["QuaternionCount"] = (ushort)aQ;
		dictionary["Vector3Count"] = (ushort)aV;
		dictionary["FloatCount"] = (ushort)aF;
		dictionary["ConstQuaternionCount"] = (ushort)cQ;
		dictionary["ConstVector3Count"] = (ushort)cV;
		dictionary["ConstFloatCount"] = (ushort)cF;
		dictionary["NumKeys"] = (ushort)frameCount;
		dictionary["EndFrame"] = (ushort)(frameCount - 1);
		dictionary["KeyTimeSize"] = keyTimeSize;
		dictionary["ConstChanMapSize"] = constChanMapSize;
		dictionary["ConstPaletteSize"] = constPaletteSize;
		dictionary["VectorOffsetSize"] = vectorOffsetSize;
		dictionary["FloatOffsetSize"] = floatOffsetSize;
		dictionary["Flags"] = num;
		dictionary["QuatMin"] = quatMin;
		dictionary["QuatMax"] = quatMax;
		dictionary["TrajMin"] = tjMin;
		dictionary["TrajMax"] = tjMax;
		dictionary["Vec3Min"] = v3Min;
		dictionary["Vec3Max"] = v3Max;
		dictionary["FloatMin"] = fltMin;
		dictionary["FloatMax"] = fltMax;
		dictionary["Dct"] = dct;
		dictionary["VectorOffsetScale"] = vectorOffsetScale;
		dictionary["FloatOffsetScale"] = floatOffsetScale;
		dictionary["VectorOffsets"] = vectorOffsets;
		dictionary["FloatOffsets"] = floatOffsets;
		dictionary["ConstantPalette"] = constPalette;
		dictionary["FrameBlockSizes"] = frameBlockSizes;
		dictionary["Data"] = data;
		obj.RawData = dictionary;
		return obj;
	}
}
