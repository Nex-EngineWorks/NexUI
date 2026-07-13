using System;
using UnityEngine;

namespace emiteat.NexUI.MotionClip
{
    /// <summary>
    /// How an incoming transition behaves if one is already playing (brief §10's Interrupt Policy).
    /// Only the three that can actually be implemented without a blending engine are exposed -
    /// Blend/Queue/Reverse from the brief need Motion Layers (Architecture-Audit.md §5) and are not
    /// offered here rather than being silently downgraded to Restart.
    /// </summary>
    public enum UIMotionStateInterruptPolicy
    {
        /// <summary>Cancel whatever is currently playing and start the new transition from scratch.</summary>
        Restart,

        /// <summary>If a transition is already in flight, drop the new request entirely.</summary>
        Ignore,

        /// <summary>Snap the interrupted transition's clip to its final pose, then start the new transition.</summary>
        CompleteImmediately
    }

    /// <summary>One authored state change: which clip plays, and how it behaves if interrupted.</summary>
    [Serializable]
    public sealed class UIMotionStateTransition
    {
        /// <summary>When true, this transition matches from any current state (brief's "Any State Transition"); <see cref="from"/> is ignored.</summary>
        public bool fromAnyState;
        public UIMotionState from;
        public UIMotionState to;
        public UIMotionClip clip;
        public UIMotionStateInterruptPolicy interruptPolicy = UIMotionStateInterruptPolicy.Restart;
    }

    /// <summary>
    /// Authoring asset mapping component-state changes to <see cref="UIMotionClip"/>s. Execution
    /// lives in <see cref="UIMotionStateRunner"/> so this asset stays pure data, shared by every
    /// instance of whatever component uses it.
    /// </summary>
    [CreateAssetMenu(menuName = "NexUI/Motion State Machine", fileName = "NewMotionStateMachine")]
    public sealed class UIMotionStateMachine : ScriptableObject
    {
        public UIMotionState defaultState = UIMotionState.Normal;
        public UIMotionStateTransition[] transitions = Array.Empty<UIMotionStateTransition>();

        /// <summary>Exact from/to match wins; falls back to an Any-State transition targeting <paramref name="to"/>; returns null if neither is authored (caller should snap state with no motion).</summary>
        public UIMotionStateTransition FindTransition(UIMotionState from, UIMotionState to)
        {
            if (transitions == null) return null;

            foreach (var transition in transitions)
                if (transition != null && !transition.fromAnyState && transition.from == from && transition.to == to)
                    return transition;

            foreach (var transition in transitions)
                if (transition != null && transition.fromAnyState && transition.to == to)
                    return transition;

            return null;
        }
    }
}
