using System;
using UnityEngine.Events;

namespace VamTimeline
{
    public interface IAtomAnimationTarget : IDisposable
    {
        UnityEvent onAnimationKeyframesDirty { get; }
        UnityEvent onAnimationKeyframesRebuilt { get; }
        bool dirty { get; set; }
        string name { get; }
        bool selected { get; set; }
        IAtomAnimationClip clip { get; set; }

        void Validate(float animationLength);

        void StartBulkUpdates();
        void EndBulkUpdates();

        bool TargetsSameAs(IAtomAnimationTarget target);
        string GetShortName();

        float[] GetAllKeyframesTime();
        float GetTimeClosestTo(float time);
        bool HasKeyframe(float time);
        void DeleteFrame(float time);
        void AddEdgeFramesIfMissing(float animationLength);

        ISnapshot GetSnapshot(float time);
        void SetSnapshot(float time, ISnapshot snapshot);

        void SelectInVam();
    }
}
