using System.Collections;
using SimpleJSON;
using UnityEngine;

namespace VamTimeline
{
    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public interface IMonoBehavior
    {
        Coroutine StartCoroutine(IEnumerator enumerator);
        void StopCoroutine(Coroutine coroutine);
    }

    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public interface IJSONStorable : IMonoBehavior
    {
        void RegisterBool(JSONStorableBool param);
        void RegisterString(JSONStorableString param);
        void RegisterFloat(JSONStorableFloat param);
        void RegisterAction(JSONStorableAction action);
        void RegisterStringChooser(JSONStorableStringChooser param);
    }

    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public interface IMVRScript : IJSONStorable
    {
        UIDynamic CreateSpacer(bool rightSide = false);
        void RemoveSpacer(UIDynamic spacer);
        UIDynamicSlider CreateSlider(JSONStorableFloat jsf, bool rightSide = false);
        void RemoveSlider(UIDynamicSlider slider);
        void RemoveSlider(JSONStorableFloat slider);
        UIDynamicButton CreateButton(string label, bool rightSide = false);
        void RemoveButton(UIDynamicButton button);
        UIDynamicToggle CreateToggle(JSONStorableBool jsb, bool rightSide = false);
        void RemoveToggle(UIDynamicToggle toggle);
        void RemoveToggle(JSONStorableBool toggle);
        UIDynamicTextField CreateTextField(JSONStorableString jss, bool rightSide = false);
        void RemoveTextField(UIDynamicTextField textfield);
        void RemoveTextField(JSONStorableString textfield);
        UIDynamicPopup CreatePopup(JSONStorableStringChooser jsc, bool rightSide = false);
        UIDynamicPopup CreateScrollablePopup(JSONStorableStringChooser jsc, bool rightSide = false);
        void RemovePopup(UIDynamicPopup popup);
        void RemovePopup(JSONStorableStringChooser popup);
    }

    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public interface IAtomPlugin : IMVRScript
    {
        Atom ContainingAtom { get; }
        AtomAnimation Animation { get; }
        AtomAnimationSerializer Serializer { get; }
        AtomClipboard Clipboard { get; }

        JSONStorableStringChooser AnimationJSON { get; }
        JSONStorableStringChooser AnimationDisplayJSON { get; }
        JSONStorableFloat ScrubberJSON { get; }
        JSONStorableAction PlayJSON { get; }
        JSONStorableAction PlayIfNotPlayingJSON { get; }
        JSONStorableBool IsPlayingJSON { get; }
        JSONStorableAction StopJSON { get; }
        JSONStorableAction StopIfPlayingJSON { get; }
        JSONStorableStringChooser FilterAnimationTargetJSON { get; }
        JSONStorableAction NextFrameJSON { get; }
        JSONStorableAction PreviousFrameJSON { get; }
        JSONStorableFloat SnapJSON { get; }
        JSONStorableAction CutJSON { get; }
        JSONStorableAction CopyJSON { get; }
        JSONStorableAction PasteJSON { get; }
        JSONStorableBool LockedJSON { get; }
        JSONStorableBool AutoKeyframeAllControllersJSON { get; }
        JSONStorableFloat SpeedJSON { get; }

        void Load(JSONNode animationJSON);
        JSONClass GetAnimationJSON(string animationName = null);

        void AnimationModified();
        void ChangeAnimation(string animationName);

        UIDynamicTextField CreateTextInput(JSONStorableString jss, bool rightSide = false);
    }
}