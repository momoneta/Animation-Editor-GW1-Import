using System;
using AssetBankPlugin.Export;
using MeshSetPlugin.Render;
using MeshSetPlugin.Screens;

namespace AssetBankPlugin.Render;

public class AntAnimPreviewScreen : MultiMeshPreviewScreen
{
	private InternalAnimation _storedAnim;

	private int _storedEndFrame;

	private MeshRenderAnim _currentRenderAnim;

	private double _currentTime;

	private int _trackFrame;

	private const double FrameAdvanceThreshold = 1.0 / 30.0;

	private volatile bool _forceSkeletonUpdate;

	public MeshRenderSkeleton CurrentSkeleton { get; private set; }

	public double SpeedMultiplier { get; set; } = 1.0;

	public bool IsPaused { get; set; }

	public int CurrentFrame { get; private set; }

	public int TotalFrames { get; private set; } = 1;

	public event Action<int> FrameChanged;

	public void LoadSkeleton(MeshRenderSkeleton skeleton)
	{
		CurrentSkeleton = skeleton;
		base.VisualizeSkeleton = skeleton;
	}

	public void LoadAnimation(MeshRenderAnim anim, InternalAnimation internalAnim, int endFrame)
	{
		_storedAnim = internalAnim;
		_storedEndFrame = endFrame;
		_currentRenderAnim = anim;
		TotalFrames = Math.Max(1, endFrame);
		_currentTime = 0.0;
		_trackFrame = 0;
		CurrentFrame = 0;
		IsPaused = false;
		SetAnimation(anim);
	}

	public void Clear()
	{
		_storedAnim = null;
		_storedEndFrame = 0;
		_currentRenderAnim = null;
		TotalFrames = 1;
		_currentTime = 0.0;
		_trackFrame = 0;
		CurrentFrame = 0;
		IsPaused = false;
		SetAnimation(null);
		this.FrameChanged?.Invoke(0);
	}

	public void Restart()
	{
		if (_storedAnim != null)
		{
			MeshRenderAnim animation = (_currentRenderAnim = AntAnimationConverter.Convert(_storedAnim, _storedEndFrame));
			_currentTime = 0.0;
			_trackFrame = 0;
			CurrentFrame = 0;
			SetAnimation(animation);
			this.FrameChanged?.Invoke(0);
		}
	}

	public void RefreshPose()
	{
		if (IsPaused)
		{
			_forceSkeletonUpdate = true;
		}
	}

	public void SeekToFrame(int targetFrame)
	{
		if (_storedAnim != null)
		{
			targetFrame = Math.Max(0, targetFrame % TotalFrames);
			MeshRenderAnim meshRenderAnim = AntAnimationConverter.Convert(_storedAnim, _storedEndFrame);
			double deltaTime = 0.034333333333333334;
			for (int i = 0; i < targetFrame; i++)
			{
				meshRenderAnim.Update(deltaTime);
			}
			_currentRenderAnim = meshRenderAnim;
			_currentTime = 0.0;
			_trackFrame = targetFrame;
			CurrentFrame = targetFrame;
			SetAnimation(meshRenderAnim);
			_forceSkeletonUpdate = true;
			this.FrameChanged?.Invoke(targetFrame);
		}
	}

	public override void Update(double timestep)
	{
		if (IsPaused)
		{
			if (_forceSkeletonUpdate)
			{
				_forceSkeletonUpdate = false;
				base.Update(0.0);
			}
			else
			{
				SetAnimation(null);
				base.Update(timestep);
				SetAnimation(_currentRenderAnim);
			}
			return;
		}
		double num = timestep * SpeedMultiplier;
		base.Update(num);
		_currentTime += num;
		if (_currentTime > 1.0 / 30.0)
		{
			_currentTime = 0.0;
			_trackFrame = (_trackFrame + 1) % TotalFrames;
			if (_trackFrame != CurrentFrame)
			{
				CurrentFrame = _trackFrame;
				this.FrameChanged?.Invoke(CurrentFrame);
			}
		}
	}
}
