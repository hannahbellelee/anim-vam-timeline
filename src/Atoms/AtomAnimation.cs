using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using Random = UnityEngine.Random;

namespace VamTimeline
{
    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public class AtomAnimation : IDisposable
    {
        public class TimeChangedEvent : UnityEvent<float> { }
        public class CurrentAnimationChangedEventArgs { public AtomAnimationClip before; public AtomAnimationClip after; }
        public class CurrentAnimationChangedEvent : UnityEvent<CurrentAnimationChangedEventArgs> { }
        public const float PaddingBeforeLoopFrame = 0.001f;
        public const string RandomizeAnimationName = "(Randomize)";
        public const string RandomizeGroupSuffix = "/*";

        private readonly Atom _atom;
        private bool _isPlaying;
        private float _playTime;
        private AtomAnimationClip _previousClip;
        private AtomAnimationClip _current;
        private float _blendingTimeLeft;
        private float _blendingDuration;
        private string _nextAnimation;
        private float _nextAnimationTime;
        private float _speed = 1f;
        // TODO: If we can either get a global counter or infer this from the plugin number, it would be better.
        private readonly int _layer = Random.Range(0, int.MaxValue);

        public TimeChangedEvent onTimeChanged = new TimeChangedEvent();
        public UnityEvent onAnimationRebuildRequested = new UnityEvent();
        public CurrentAnimationChangedEvent onCurrentAnimationChanged = new CurrentAnimationChangedEvent();
        public UnityEvent onAnimationSettingsChanged = new UnityEvent();
        public UnityEvent onClipsListChanged = new UnityEvent();
        public List<AtomAnimationClip> clips { get; } = new List<AtomAnimationClip>();
        public AtomAnimationClip current
        {
            get
            {
                return _current;
            }
            set
            {
                var previous = _current;
                _current = value;
                onCurrentAnimationChanged.Invoke(new CurrentAnimationChangedEventArgs { before = previous, after = _current });
            }
        }
        public string playedAnimation { get; private set; }

        public float time
        {
            get
            {
                var time = _playTime;
                if (current.loop) return time % current.animationLength;
                return time;
            }
            set
            {
                if (_playTime == value) return;
                _playTime = value;
                if (current == null) return;
                Sample();
                onTimeChanged.Invoke(value);
            }
        }

        public float speed
        {
            get
            {
                return _speed;
            }

            set
            {
                _speed = value;
                foreach (var clip in clips)
                {
                    if (clip.animationPattern != null)
                        clip.animationPattern.SetFloatParamValue("speed", value);
                }
            }
        }

        public AtomAnimation(Atom atom)
        {
            if (atom == null) throw new ArgumentNullException(nameof(atom));
            _atom = atom;
            // _unityAnimation = _atom.gameObject.GetComponent<Animation>() ?? _atom.gameObject.AddComponent<Animation>();
            // if (_unityAnimation == null) throw new NullReferenceException($"Could not create an Animation component on {_atom.uid}");
        }

        public void Initialize()
        {
            if (clips.Count == 0)
                AddClip(new AtomAnimationClip("Anim 1"));
            if (current == null)
                current = clips.First();
            RebuildAnimation();
        }

        public void AddClip(AtomAnimationClip clip)
        {
            clip.onAnimationSettingsModified.AddListener(OnAnimationSettingsModified);
            clip.onAnimationKeyframesModified.AddListener(OnAnimationModified);
            clip.onTargetsListChanged.AddListener(OnAnimationModified);
            clip.onTargetsSelectionChanged.AddListener(OnTargetsSelectionChanged);
            clips.Add(clip);
            onClipsListChanged.Invoke();
        }

        private void OnTargetsSelectionChanged()
        {
            foreach (var target in current.allTargets)
            {
                foreach (var clip in clips.Where(c => c != current))
                {
                    var t = clip.allTargets.FirstOrDefault(x => x.TargetsSameAs(target));
                    if (t == null) continue;
                    t.selected = target.selected;
                }
            }
        }

        public void RemoveClip(AtomAnimationClip clip)
        {
            clips.Remove(clip);
            clip.Dispose();
            onClipsListChanged.Invoke();
            OnAnimationModified();
        }

        private void OnAnimationSettingsModified()
        {
            onAnimationSettingsChanged.Invoke();
        }

        private void OnAnimationModified()
        {
            onAnimationRebuildRequested.Invoke();
        }

        public bool IsEmpty()
        {
            if (clips.Count == 0) return true;
            if (clips.Count == 1 && clips[0].IsEmpty()) return true;
            return false;
        }

        public List<string> GetAnimationNames()
        {
            var clipNames = new List<string>(clips.Count);
            for (var i = 0; i < clips.Count; i++)
                clipNames.Add(clips[i].animationName);
            return clipNames;
        }

        protected string GetNewAnimationName()
        {
            for (var i = clips.Count + 1; i < 999; i++)
            {
                var animationName = "Anim " + i;
                if (!clips.Any(c => c.animationName == animationName)) return animationName;
            }
            return Guid.NewGuid().ToString();
        }

        public void SetKeyframe(FloatParamAnimationTarget target, float time, float val)
        {
            time = time.Snap();
            if (time > current.animationLength)
                time = current.animationLength;
            target.SetKeyframe(time, val);
        }

        public void SetKeyframeToCurrentTransform(FreeControllerAnimationTarget target, float time)
        {
            time = time.Snap();
            if (time > current.animationLength)
                time = current.animationLength;
            target.SetKeyframeToCurrentTransform(time);
        }

        public AtomAnimationClip GetClip(string name)
        {
            return clips.FirstOrDefault(c => c.animationName == name);
        }

        public void Play()
        {
            if (current == null)
            {
                SuperController.LogError($"VamTimeline: Cannot play animation, Timeline is still loading");
                return;
            }
            playedAnimation = current.animationName;
            _isPlaying = true;
            if (current.animationPattern)
            {
                current.animationPattern.SetBoolParamValue("loopOnce", false);
                current.animationPattern.ResetAndPlay();
            }
            DetermineNextAnimation(_playTime);
        }

        private void DetermineNextAnimation(float time)
        {
            _nextAnimation = null;
            _nextAnimationTime = 0;

            if (current.nextAnimationName == null) return;
            if (clips.Count == 1) return;

            if (current.nextAnimationTime > 0 + float.Epsilon)
                _nextAnimationTime = (time + current.nextAnimationTime).Snap();
            else
                return;

            if (current.nextAnimationName == RandomizeAnimationName)
            {
                var idx = Random.Range(0, clips.Count - 1);
                if (idx >= clips.IndexOf(current)) idx += 1;
                _nextAnimation = clips[idx].animationName;
            }
            else if (current.nextAnimationName.EndsWith(RandomizeGroupSuffix))
            {
                var prefix = current.nextAnimationName.Substring(0, current.nextAnimationName.Length - RandomizeGroupSuffix.Length);
                var group = clips
                    .Where(c => c.animationName != current.animationName)
                    .Where(c => c.animationName.StartsWith(prefix))
                    .ToList();
                var idx = Random.Range(0, group.Count);
                _nextAnimation = group[idx].animationName;
            }
            else
            {
                _nextAnimation = current.nextAnimationName;
            }
        }

        public void Sample()
        {
            SampleParamsAnimation();
            SampleControllers();
        }

        private void SampleParamsAnimation()
        {
            var time = this.time;
            var weight = _blendingTimeLeft / _blendingDuration;
            foreach (var morph in current.targetFloatParams)
            {
                var val = morph.value.Evaluate(time);
                if (_previousClip != null)
                {
                    var blendingTarget = _previousClip.targetFloatParams.FirstOrDefault(t => t.floatParam == morph.floatParam);
                    if (blendingTarget != null)
                    {
                        morph.floatParam.val = (blendingTarget.value.Evaluate(_playTime) * weight) + (val * (1 - weight));
                    }
                    else
                    {
                        morph.floatParam.val = val;
                    }
                }
                else
                {
                    morph.floatParam.val = val;
                }
            }
        }

        private void SampleControllers()
        {
            var time = this.time;
            var weight = _blendingTimeLeft / _blendingDuration;
            foreach (var t in current.targetControllers)
            {
                Vector3 position;
                Quaternion rotation;
                if (_previousClip != null)
                {
                    var blendingTarget = _previousClip.targetControllers.FirstOrDefault(b => b.controller == t.controller);
                    if (blendingTarget != null)
                    {
                        // TODO: Replace this by state with weight
                        position = Vector3.Lerp(blendingTarget.EvaluatePosition(time), t.EvaluatePosition(time), 1f - weight);
                        rotation = Quaternion.Slerp(blendingTarget.EvaluateRotation(time), t.EvaluateRotation(time), 1f - weight);
                    }
                    else
                    {
                        position = t.EvaluatePosition(time);
                        rotation = t.EvaluateRotation(time);
                    }
                }
                else
                {
                    position = t.EvaluatePosition(time);
                    rotation = t.EvaluateRotation(time);
                }
                // TODO: Store in the target
                var rb = t.controller.GetComponent<Rigidbody>();
                rb.transform.localRotation = rotation;
                rb.transform.localPosition = position;
            }
        }

        public void Update()
        {
            if (_isPlaying)
            {
                if (_previousClip != null)
                {
                    _blendingTimeLeft -= Time.deltaTime * speed;
                    if (_blendingTimeLeft <= 0)
                    {
                        _blendingTimeLeft = 0;
                        _blendingDuration = 0;
                        _previousClip = null;
                    }
                }

                SampleParamsAnimation();

                if (_nextAnimationTime > 0 + float.Epsilon && _playTime >= _nextAnimationTime)
                {
                    if (_nextAnimation != null)
                    {
                        ChangeAnimation(_nextAnimation);
                    }
                }
            }
        }

        public void FixedUpdate()
        {
            if (_isPlaying)
            {
                _playTime += Time.fixedDeltaTime * speed;

                SampleControllers();
            }
        }

        public void Stop()
        {
            _isPlaying = false;
            if (current == null) return;
            foreach (var clip in clips)
            {
                if (clip.animationPattern)
                {
                    clip.animationPattern.SetBoolParamValue("loopOnce", true);
                }
            }
            _blendingTimeLeft = 0;
            _blendingDuration = 0;
            _previousClip = null;
            _nextAnimation = null;
            _nextAnimationTime = 0;
            if (playedAnimation != null && playedAnimation != current.animationName)
            {
                if (clips.Any(c => c.animationName == playedAnimation))
                    ChangeAnimation(playedAnimation);
                playedAnimation = null;
            }
            if (time > current.animationLength - 0.001f)
            {
                time = current.loop ? 0f : current.animationLength;
            }
            else
            {
                time = time.Snap();
            }
            Sample();
        }

        public bool IsPlaying()
        {
            return _isPlaying;
        }

        public void RebuildAnimation()
        {
            if (current == null) throw new NullReferenceException("No current animation set");
            var time = this.time.Snap();
            var sw = Stopwatch.StartNew();
            foreach (var clip in clips)
            {
                clip.Validate();
                PrepareClipCurves(clip);
                if (clip.transition)
                {
                    var previous = GetClip(clip.animationName);
                    if (previous != null && (previous.IsDirty() || clip.IsDirty()))
                        clip.Paste(0f, previous.Copy(previous.animationLength, true), false);
                    var next = GetClip(clip.nextAnimationName);
                    if (next != null && (next.IsDirty() || clip.IsDirty()))
                        clip.Paste(clip.animationLength, next.Copy(0f, true), false);
                }
                ReapplyClipCurve(clip);
            }
            if (sw.ElapsedMilliseconds > 1000)
            {
                SuperController.LogError($"VamTimeline.{nameof(RebuildAnimation)}: Suspiciously long animation rebuild ({sw.Elapsed})");
            }
        }

        private void PrepareClipCurves(AtomAnimationClip clip)
        {
            foreach (var target in clip.targetControllers)
            {
                if (!target.dirty) continue;

                target.dirty = false;

                if (clip.loop)
                    target.SetCurveSnapshot(clip.animationLength, target.GetCurveSnapshot(0f), false);

                target.ReapplyCurveTypes();

                if (clip.loop)
                    target.SmoothLoop();

                if (clip.ensureQuaternionContinuity)
                {
                    UnitySpecific.EnsureQuaternionContinuityAndRecalculateSlope(
                        target.rotX,
                        target.rotY,
                        target.rotZ,
                        target.rotW);
                }
            }

            foreach (var target in clip.targetFloatParams)
            {
                if (!target.dirty) continue;

                target.dirty = false;

                if (clip.loop)
                    target.value.SetKeyframe(clip.animationLength, target.value[0].value);

                target.value.FlatAllFrames();
            }
        }

        private void ReapplyClipCurve(AtomAnimationClip clip)
        {
            clip.alip.ClearCurves();

            foreach (var target in clip.targetControllers)
            {
                target.ReapplyCurvesToClip(clip.alip);
            }
        }

        private bool HasAnimatableControllers()
        {
            return current.targetControllers.Count > 0;
        }

        public AtomAnimationClip AddAnimation()
        {
            string animationName = GetNewAnimationName();
            var clip = new AtomAnimationClip(animationName);
            AddClip(clip);
            return clip;
        }

        public void ChangeAnimation(string animationName)
        {
            var clip = GetClip(animationName);
            if (clip == null) throw new NullReferenceException($"Could not find animation '{animationName}'. Found animations: '{string.Join("', '", clips.Select(c => c.animationName).ToArray())}'.");
            // var targetAnim = _unityAnimation[animationName];
            var time = this.time;
            if (_isPlaying)
            {
                if (HasAnimatableControllers())
                {
                    // targetAnim.time = 0f;
                    // targetAnim.enabled = true;
                    // targetAnim.weight = 0f;
                    // _unityAnimation.Blend(current.animationName, 0f, current.blendDuration);
                    // _unityAnimation.Blend(animationName, 1f, current.blendDuration);
                }
                if (current.animationPattern != null)
                {
                    // Let the loop finish during the transition
                    current.animationPattern.SetBoolParamValue("loopOnce", true);
                }
                _previousClip = current;
                _blendingTimeLeft = _blendingDuration = current.blendDuration;
            }

            var previous = current;
            current = clip;
            // _animState = targetAnim;

            if (_isPlaying)
            {
                DetermineNextAnimation(_playTime);

                if (current.animationPattern != null)
                {
                    current.animationPattern.SetBoolParamValue("loopOnce", false);
                    current.animationPattern.ResetAndPlay();
                }
            }
            else
            {
                this.time = 0f;
                Sample();
                onCurrentAnimationChanged.Invoke(new CurrentAnimationChangedEventArgs
                {
                    before = previous,
                    after = current
                });
            }
        }

        public void Dispose()
        {
            onTimeChanged.RemoveAllListeners();
            onAnimationRebuildRequested.RemoveAllListeners();
            onCurrentAnimationChanged.RemoveAllListeners();
            onAnimationSettingsChanged.RemoveAllListeners();
            foreach (var clip in clips)
            {
                clip.Dispose();
            }
        }
    }
}
