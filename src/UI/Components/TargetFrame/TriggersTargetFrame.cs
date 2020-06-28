using System;
using UnityEngine;
using UnityEngine.UI;

namespace VamTimeline
{
    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public class TriggersTargetFrame : TargetFrameBase<TriggersAnimationTarget>, TriggerHandler
    {
        private UIDynamicButton _editTriggersButton;

        public TriggersTargetFrame()
            : base()
        {
        }

        protected override void CreateCustom()
        {
        }

        public override void SetTime(float time, bool stopped)
        {
            base.SetTime(time, stopped);

            if (stopped)
            {
                AnimationTimelineTrigger trigger;
                var ms = plugin.animation.clipTime.ToMilliseconds();
                if (target.triggersMap.TryGetValue(ms, out trigger))
                {
                    valueText.text = $"Has Triggers";
                    if (_editTriggersButton != null) _editTriggersButton.label = "Edit Triggers";
                }
                else
                {
                    valueText.text = "-";
                    if (_editTriggersButton != null) _editTriggersButton.label = "Create Trigger";
                }
            }
        }

        public override void ToggleKeyframe(bool enable)
        {
            if (plugin.animation.isPlaying) return;
            var time = plugin.animation.clipTime.Snap();
            if (time.IsSameFrame(0f) || time.IsSameFrame(clip.animationLength))
            {
                if (!enable)
                    SetToggle(true);
                return;
            }
            if (enable)
            {
                GetOrCreateTriggerAtCurrentTime();
            }
            else
            {
                target.DeleteFrame(time);
            }
            SetTime(plugin.animation.clipTime, true);
        }

        protected override void CreateExpandPanel(RectTransform container)
        {
            var group = container.gameObject.AddComponent<HorizontalLayoutGroup>();
            group.spacing = 4f;
            group.padding = new RectOffset(8, 8, 8, 8);
            group.childAlignment = TextAnchor.MiddleCenter;

            _editTriggersButton = CreateExpandButton(
                group.transform,
                target.triggersMap.ContainsKey(plugin.animation.clipTime.ToMilliseconds()) ? "Edit Triggers" : "Create Trigger",
                EditTriggers);
        }

        private void EditTriggers()
        {
            Trigger trigger = GetOrCreateTriggerAtCurrentTime();

            trigger.triggerActionsParent = plugin.UITransform;
            trigger.handler = this;
            trigger.InitTriggerUI();
            trigger.OpenTriggerActionsPanel();
        }

        private Trigger GetOrCreateTriggerAtCurrentTime()
        {
            AnimationTimelineTrigger trigger;
            var ms = plugin.animation.clipTime.ToMilliseconds();
            if (!target.triggersMap.TryGetValue(ms, out trigger))
            {
                // TODO: Assign a display name?
                trigger = new AnimationTimelineTrigger();
                target.SetKeyframe(ms, trigger);
            }
            return trigger;
        }

        #region Trigger handler

        void TriggerHandler.RemoveTrigger(Trigger t)
        {
            throw new NotImplementedException();
        }

        void TriggerHandler.DuplicateTrigger(Trigger t)
        {
            throw new NotImplementedException();
        }

        RectTransform TriggerHandler.CreateTriggerActionsUI()
        {
            return Instantiate(VamPrefabFactory.triggerActionsPrefab);
        }

        RectTransform TriggerHandler.CreateTriggerActionMiniUI()
        {
            return Instantiate(VamPrefabFactory.triggerActionMiniPrefab);
        }

        RectTransform TriggerHandler.CreateTriggerActionDiscreteUI()
        {
            return Instantiate(VamPrefabFactory.triggerActionDiscretePrefab);
        }

        RectTransform TriggerHandler.CreateTriggerActionTransitionUI()
        {
            return Instantiate(VamPrefabFactory.triggerActionTransitionPrefab);
        }

        void TriggerHandler.RemoveTriggerActionUI(RectTransform rt)
        {
            Destroy(rt?.gameObject);
        }

        #endregion
    }
}
