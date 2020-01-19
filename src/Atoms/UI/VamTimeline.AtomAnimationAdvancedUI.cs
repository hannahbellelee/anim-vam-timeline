using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace VamTimeline
{
    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public class AtomAnimationAdvancedUI : AtomAnimationBaseUI
    {
        private static readonly Regex _sanitizeRE = new Regex("[^a-zA-Z0-9 _-]", RegexOptions.Compiled);

        public const string ScreenName = "Advanced";
        private JSONStorableStringChooser _exportAnimationsJSON;

        public override string Name => ScreenName;

        public AtomAnimationAdvancedUI(IAtomPlugin plugin)
            : base(plugin)
        {

        }
        public override void Init()
        {
            base.Init();

            // Left side

            InitAnimationSelectorUI(false);

            InitPlaybackUI(false);

            InitFrameNavUI(false);

            var keyframeCurrentPoseUI = Plugin.CreateButton("Keyframe Pose (All On)", true);
            keyframeCurrentPoseUI.button.onClick.AddListener(() => KeyframeCurrentPose(true));
            _components.Add(keyframeCurrentPoseUI);

            var keyframeCurrentPoseTrackedUI = Plugin.CreateButton("Keyframe Pose (Animated)", true);
            keyframeCurrentPoseTrackedUI.button.onClick.AddListener(() => KeyframeCurrentPose(false));
            _components.Add(keyframeCurrentPoseTrackedUI);

            var bakeUI = Plugin.CreateButton("Bake Animation (Arm & Record)", true);
            bakeUI.button.onClick.AddListener(() => Bake());
            _components.Add(bakeUI);

            _exportAnimationsJSON = new JSONStorableStringChooser("Export Animation", new List<string> { "(All)" }.Concat(Plugin.Animation.GetAnimationNames()).ToList(), "(All)", "Export Animation")
            {
                isStorable = false
            };
            var exportAnimationsUI = Plugin.CreateScrollablePopup(_exportAnimationsJSON, true);
            _linkedStorables.Add(_exportAnimationsJSON);

            var exportUI = Plugin.CreateButton("Export", true);
            exportUI.button.onClick.AddListener(() => Export());
            _components.Add(exportUI);

            var importUI = Plugin.CreateButton("Import", true);
            importUI.button.onClick.AddListener(() => Import());
            _components.Add(importUI);

            // TODO: Keyframe all animatable morphs

            // TODO: Copy all missing controllers and morphs on every animation

            // TODO: Import / Export animation(s) to another atom and create an atom just to store and share animations
        }

        private void Export()
        {
            try
            {
                var fileBrowserUI = SuperController.singleton.fileBrowserUI;
                fileBrowserUI.defaultPath = SuperController.singleton.savesDirResolved + "animations";
                SuperController.singleton.activeUI = SuperController.ActiveUI.None;
                fileBrowserUI.SetTitle("Select Animation File");
                fileBrowserUI.SetTextEntry(true);
                fileBrowserUI.Show(ExportFileSelected);
                if (fileBrowserUI.fileEntryField != null)
                {
                    var dt = ((int)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds).ToString();
                    fileBrowserUI.fileEntryField.text = _exportAnimationsJSON.val == "(All)" ? $"anims-{dt}" : $"anim-{_sanitizeRE.Replace(_exportAnimationsJSON.val, "")}-{dt}";
                    fileBrowserUI.ActivateFileNameField();
                }
                else
                {
                    SuperController.LogError("VamTimeline: No fileBrowserUI.fileEntryField");
                }
            }
            catch (Exception exc)
            {
                SuperController.LogError($"VamTimeline: Failed to save file dialog: {exc}");
            }
        }

        private void ExportFileSelected(string path)
        {
            try
            {
                if (!path.EndsWith(".json"))
                    path += ".json";

                var jc = Plugin.GetAnimationJSON(_exportAnimationsJSON.val == "(All)" ? null : _exportAnimationsJSON.val);
                jc["AtomType"] = Plugin.ContainingAtom.type;
                SuperController.singleton.SaveJSON(jc, path);
                SuperController.singleton.DoSaveScreenshot(path);
            }
            catch (Exception exc)
            {
                SuperController.LogError($"VamTimeline: Failed to export animation: {exc}");
            }
        }

        private void Import()
        {
            try
            {
                var fileBrowserUI = SuperController.singleton.fileBrowserUI;
                fileBrowserUI.defaultPath = SuperController.singleton.savesDirResolved + "animations";
                SuperController.singleton.activeUI = SuperController.ActiveUI.None;
                fileBrowserUI.SetTextEntry(false);
                fileBrowserUI.keepOpen = false;
                fileBrowserUI.SetTitle("Select Animation File");
                fileBrowserUI.Show(ImportFileSelected);
            }
            catch (Exception exc)
            {
                SuperController.LogError($"VamTimeline: Failed to open file dialog: {exc}");
            }
        }

        private void ImportFileSelected(string path)
        {
            var jc = SuperController.singleton.LoadJSON(path);
            if (jc["AtomType"]?.Value != Plugin.ContainingAtom.type)
            {
                SuperController.LogError($"VamTimeline: Loaded animation for {jc["AtomType"]} but current atom type is {Plugin.ContainingAtom.type}");
                return;
            }
            try
            {
                Plugin.Load(jc);
                Plugin.Animation.Stop();
            }
            catch (Exception exc)
            {
                SuperController.LogError($"VamTimeline: Failed to import animation: {exc}");
            }
        }

        private void KeyframeCurrentPose(bool all)
        {
            try
            {
                var time = Plugin.Animation.Time;
                foreach (var fc in Plugin.ContainingAtom.freeControllers)
                {
                    if (!fc.name.EndsWith("Control")) continue;
                    if (fc.currentPositionState != FreeControllerV3.PositionState.On) continue;
                    if (fc.currentRotationState != FreeControllerV3.RotationState.On) continue;

                    var target = Plugin.Animation.Current.TargetControllers.FirstOrDefault(tc => tc.Controller == fc);
                    if (target == null)
                    {
                        if (!all) continue;
                        target = Plugin.Animation.Add(fc);
                    }
                    Plugin.Animation.SetKeyframeToCurrentTransform(target, time);
                }
                Plugin.Animation.RebuildAnimation();
                Plugin.AnimationModified();
            }
            catch (Exception exc)
            {
                SuperController.LogError("VamTimeline.AtomAnimationAdvancedUI: " + exc.ToString());
            }
        }

        private void Bake()
        {
            var controllers = Plugin.Animation.Clips.SelectMany(c => c.TargetControllers).Select(c => c.Controller).Distinct().ToList();
            foreach (var mac in Plugin.ContainingAtom.motionAnimationControls)
            {
                if (!controllers.Contains(mac.controller)) continue;
                mac.armedForRecord = true;
            }

            Plugin.Animation.Play();
            SuperController.singleton.motionAnimationMaster.StartRecord();

            Plugin.StartCoroutine(StopWhenPlaybackIsComplete());
        }

        private IEnumerator StopWhenPlaybackIsComplete()
        {
            var waitFor = Plugin.Animation.Clips.Sum(c => c.NextAnimationTime.IsSameFrame(0) ? c.AnimationLength : c.NextAnimationTime);
            yield return new WaitForSeconds(waitFor);

            SuperController.singleton.motionAnimationMaster.StopRecord();
            Plugin.Animation.Stop();
        }
    }
}

