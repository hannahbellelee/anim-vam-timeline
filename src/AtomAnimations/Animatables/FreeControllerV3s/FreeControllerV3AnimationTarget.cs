using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VamTimeline
{
    public class FreeControllerV3AnimationTarget : TransformAnimationTargetBase<FreeControllerV3Ref>, ICurveAnimationTarget
    {
        public bool recording { get; set; }

        // If it is targeted, whether it is enabled
        public bool controlPosition = true;
        public bool controlRotation = true;

        public string parentAtomId;
        public string parentRigidbodyId;

        private Rigidbody _parentRigidbody;
        private bool _parentAvailable;
        private int _lastParentAvailableCheck;

        public bool hasParentBound => _parentAvailable;

        public void SetParent(string atomId, string rigidbodyId)
        {
            if (string.IsNullOrEmpty(rigidbodyId))
            {
                parentAtomId = null;
                parentRigidbodyId = null;
                _parentRigidbody = null;
                _parentAvailable = true;
                return;
            }
            _parentAvailable = false;
            parentAtomId = atomId;
            parentRigidbodyId = rigidbodyId;
            _parentRigidbody = null;
            TryBindParent(false);
        }

        public bool EnsureParentAvailable(bool silent = true)
        {
            if (parentRigidbodyId == null) return true;
            if (_parentAvailable)
            {
                if (_parentRigidbody == null)
                {
                    _parentAvailable = false;
                    _parentRigidbody = null;
                    return false;
                }
                return true;
            }
            if (Time.frameCount == _lastParentAvailableCheck) return false;
            if (TryBindParent(silent)) return true;
            _lastParentAvailableCheck = Time.frameCount;
            return false;
        }

        public bool TryBindParent(bool silent)
        {
            if (SuperController.singleton.isLoading) return false;
            if (parentRigidbodyId == null) return true;
            var atom = SuperController.singleton.GetAtomByUid(parentAtomId);
            if (atom == null)
            {
                if (!silent) SuperController.LogError($"Timeline: Atom '{parentAtomId}' defined as a parent of {animatableRef.name} was not found in the scene. You can remove the parenting, but the animation will not show in the expected position.");
                return false;
            }
            var rigidbody = atom.linkableRigidbodies.FirstOrDefault(rb => rb.name == parentRigidbodyId);
            if (rigidbody == null)
            {
                if (!silent) SuperController.LogError($"Timeline: Atom '{parentAtomId}' does not have a rigidbody '{parentRigidbodyId}'.");
                return false;
            }

            _parentRigidbody = rigidbody;
            _parentAvailable = true;
            return true;
        }

        private Rigidbody _previousLinkedParentRB;

        public Rigidbody GetPositionParentRB()
        {
            if (!targetsPosition) return null;
            if (!ReferenceEquals(_parentRigidbody, null)) return _parentRigidbody;
            var currentPositionState = animatableRef.controller.currentPositionState;
            var linkToRB = animatableRef.controller.linkToRB;
            if (currentPositionState == FreeControllerV3.PositionState.ParentLink || currentPositionState == FreeControllerV3.PositionState.PhysicsLink)
            {
                if (ReferenceEquals(linkToRB, null)) return _previousLinkedParentRB = null;
                if (_previousLinkedParentRB == linkToRB) return _previousLinkedParentRB;
                if (ReferenceEquals(linkToRB.GetComponent<FreeControllerV3>(), null)) return _previousLinkedParentRB;
                return _previousLinkedParentRB = linkToRB;
            }

            return null;
        }

        public Rigidbody GetRotationParentRB()
        {
            if (!targetsRotation) return null;
            if (!ReferenceEquals(_parentRigidbody, null)) return _parentRigidbody;
            var currentPositionState = animatableRef.controller.currentPositionState;
            var linkToRB = animatableRef.controller.linkToRB;
            if (currentPositionState == FreeControllerV3.PositionState.ParentLink || currentPositionState == FreeControllerV3.PositionState.PhysicsLink)
            {
                if (ReferenceEquals(linkToRB, null)) return _previousLinkedParentRB = null;
                if (_previousLinkedParentRB == linkToRB) return _previousLinkedParentRB;
                if (ReferenceEquals(linkToRB.GetComponent<FreeControllerV3>(), null)) return _previousLinkedParentRB;
                return _previousLinkedParentRB = linkToRB;
            }

            return null;
        }

        public FreeControllerV3AnimationTarget(FreeControllerV3Ref animatableRef, bool targetsPosition, bool targetsRotation)
            : base(
                animatableRef,
                targetsPosition,
                new Vector3AnimationTarget<FreeControllerV3Ref>(animatableRef),
                targetsRotation,
                new QuaternionAnimationTarget<FreeControllerV3Ref>(animatableRef)
            )
        {
        }

        public override void SelectInVam()
        {
            base.SelectInVam();
            if (SuperController.singleton.GetSelectedController() == animatableRef.controller)
            {
                var selector = animatableRef.controller.containingAtom.gameObject.GetComponentInChildren<UITabSelector>();
                if (selector != null)
                    selector.SetActiveTab(selector.startingTabName);
            }
            else
            {
                SuperController.singleton.SelectController(animatableRef.controller);
            }
        }

        #region Keyframes control

        public int SetKeyframeToCurrent(float time, bool makeDirty = true)
        {
            if (!EnsureParentAvailable(false)) return -1;
            var posParent = targetsPosition ? GetPositionParentRB() : null;
            var hasPosParent = !ReferenceEquals(posParent, null);
            var rotParent = targetsRotation ? GetRotationParentRB() : null;
            var hasRotParent = !ReferenceEquals(rotParent, null);
            var controllerTransform = animatableRef.controller.transform;

            return SetKeyframeByTime(
                time,
                hasPosParent ? posParent.transform.InverseTransformPoint(controllerTransform.position) : controllerTransform.localPosition,
                hasRotParent ? Quaternion.Inverse(rotParent.rotation) * controllerTransform.rotation : controllerTransform.localRotation,
                -1,
                makeDirty
            );
        }

        public int AddKeyframeAtTime(float time, bool makeDirty = true)
        {
            return SetKeyframeByTime(time, EvaluatePosition(time), EvaluateRotation(time), -1, makeDirty);
        }

        public ICurveAnimationTarget Clone(bool copyKeyframes)
        {
            var clone = new FreeControllerV3AnimationTarget(animatableRef, targetsPosition, targetsRotation);
            if (copyKeyframes)
            {
                if (targetsPosition)
                {
                    clone.position.x.keys.AddRange(position.x.keys);
                    clone.position.y.keys.AddRange(position.y.keys);
                    clone.position.z.keys.AddRange(position.z.keys);
                }
                if (targetsRotation)
                {
                    clone.rotation.rotX.keys.AddRange(rotation.rotX.keys);
                    clone.rotation.rotY.keys.AddRange(rotation.rotY.keys);
                    clone.rotation.rotZ.keys.AddRange(rotation.rotZ.keys);
                    clone.rotation.rotW.keys.AddRange(rotation.rotW.keys);
                }
            }
            else
            {
                clone.SetKeyframeByTime(0f, GetKeyframePosition(0), GetKeyframeRotation(0), CurveTypeValues.SmoothLocal);
                clone.SetKeyframeByTime(GetKeyframeTime(length - 1), GetKeyframePosition(length - 1), GetKeyframeRotation(length - 1), CurveTypeValues.SmoothLocal);
                clone.ComputeCurves();
            }
            return clone;
        }

        public void RestoreFrom(ICurveAnimationTarget backup)
        {
            var target = backup as FreeControllerV3AnimationTarget;
            if (target == null) return;
            var maxTime = GetLeadCurve().GetLastFrame().time;
            if (targetsPosition)
            {
                position.x.keys.Clear();
                position.x.keys.AddRange(target.position.x.keys.Where(k => k.time < maxTime + 0.0001f));
                position.y.keys.Clear();
                position.y.keys.AddRange(target.position.y.keys.Where(k => k.time < maxTime + 0.0001f));
                position.z.keys.Clear();
                position.z.keys.AddRange(target.position.z.keys.Where(k => k.time < maxTime + 0.0001f));
            }
            if (targetsRotation)
            {
                rotation.rotX.keys.Clear();
                rotation.rotX.keys.AddRange(target.rotation.rotX.keys.Where(k => k.time < maxTime + 0.0001f));
                rotation.rotY.keys.Clear();
                rotation.rotY.keys.AddRange(target.rotation.rotY.keys.Where(k => k.time < maxTime + 0.0001f));
                rotation.rotZ.keys.Clear();
                rotation.rotZ.keys.AddRange(target.rotation.rotZ.keys.Where(k => k.time < maxTime + 0.0001f));
                rotation.rotW.keys.Clear();
                rotation.rotW.keys.AddRange(target.rotation.rotW.keys.Where(k => k.time < maxTime + 0.0001f));
            }
            AddEdgeFramesIfMissing(maxTime);
            dirty = true;
        }

        public void IncreaseCapacity(int capacity)
        {
            foreach (var curve in curves)
            {
                curve.keys.Capacity = Math.Max(curve.keys.Capacity, capacity);
            }
        }

        public void TrimCapacity()
        {
            foreach (var curve in curves)
            {
                curve.keys.TrimExcess();
            }
        }

        #endregion

        public bool TargetsSameAs(IAtomAnimationTarget target)
        {
            var t = target as FreeControllerV3AnimationTarget;
            if (t == null) return false;
            return TargetsSameAs(t.animatableRef, t.targetsPosition, t.targetsRotation);
        }

        public bool TargetsSameAs(AnimatableRefBase other, bool otherTargetsPos, bool otherTargetsRot)
        {
            return other == animatableRef && (otherTargetsPos == targetsPosition || otherTargetsRot == targetsRotation);
        }

        public override string ToString()
        {
            return $"[FreeControllerV3 Target: {name}]";
        }

        public class Comparer : IComparer<FreeControllerV3AnimationTarget>
        {
            public int Compare(FreeControllerV3AnimationTarget x, FreeControllerV3AnimationTarget y)
            {
                if (x?.animatableRef.controller == null || y?.animatableRef.controller == null)
                    return 0;

                var xAtom = x.animatableRef.controller.containingAtom;
                var yAtom = y.animatableRef.controller.containingAtom;
                if (xAtom != yAtom)
                {
                    if (x.animatableRef.owned)
                        return -1;
                    if (y.animatableRef.owned)
                        return 1;
                    return string.Compare(xAtom.name, yAtom.name, StringComparison.Ordinal);
                }

                if (x.animatableRef == y.animatableRef && x.targetsRotation != y.targetsRotation)
                {
                    if (x.targetsRotation)
                        return 1;
                    else
                        return -1;
                }

                return string.Compare(x.animatableRef.controller.name, y.animatableRef.controller.name, StringComparison.Ordinal);
            }
        }
    }
}
