using System;
using System.Collections.Generic;

namespace emiteat.NexUI.Compiled
{
    /// <summary>What raised an interaction. Mirrors the authoring trigger set one-for-one.</summary>
    /// <summary>
    /// What raises an interaction rule.
    /// </summary>
    /// <remarks>
    /// Appended, never renumbered: the value is what a compiled program serializes, so reordering
    /// would silently repoint every authored rule in every project.
    ///
    /// <see cref="OnShow"/> and <see cref="OnHide"/> are screen lifecycle; everything else needs a
    /// listener on the node, and only the nodes whose triggers are actually authored get one.
    /// </remarks>
    public enum NexTrigger
    {
        OnClick = 0,
        OnShow = 1,
        OnHide = 2,

        OnPointerEnter = 3,
        OnPointerExit = 4,
        OnPointerDown = 5,
        OnPointerUp = 6,

        /// <summary>The submit action of the active input device - Enter, gamepad South, and so on.</summary>
        /// <remarks>
        /// Distinct from <see cref="OnClick"/> on purpose. A pointer click and a submit are the
        /// same intent from different devices, but only submit reaches an element that was focused
        /// by keyboard or gamepad - which is what makes a screen operable without a mouse.
        /// </remarks>
        OnSubmit = 7,
        OnCancel = 8,

        OnLongPress = 9,
        OnDoubleClick = 10,

        /// <summary>Raised on the element being dragged, when the drag starts.</summary>
        OnDragBegin = 11,

        /// <summary>Raised on the element being dragged, every frame the pointer moves.</summary>
        OnDrag = 12,

        /// <summary>Raised on the element being dragged, when the pointer is released.</summary>
        OnDragEnd = 13,

        /// <summary>
        /// Raised on the element the drag was released <em>over</em> - the receiver, not the thing
        /// dragged.
        /// </summary>
        /// <remarks>
        /// The only trigger whose subject is a different element from the one that started the
        /// gesture, which is why the drag source is published to state: a drop rule almost always
        /// needs to know what it caught before it can decide whether to accept it.
        /// </remarks>
        OnDrop = 14,

        /// <summary>
        /// Raised on an overlay when something asked it to close - a backdrop click, a dismiss
        /// button, a toast running out of time.
        /// </summary>
        /// <remarks>
        /// A request, not the closing itself. The overlay does not close on its own, so a rule can
        /// intercept this to confirm first, save a draft, or refuse. When nothing listens the
        /// backend closes it anyway - an overlay that ignored every close request would lock the
        /// screen, which is worse than any policy an author might have wanted.
        /// </remarks>
        OnCloseRequested = 15
    }

    /// <summary>Where in the propagation path a rule listens.</summary>
    public enum NexPhase
    {
        Target = 0,
        Bubble = 1,
        Capture = 2
    }

    public enum NexComparison
    {
        Equals = 0,
        NotEquals = 1,
        GreaterThan = 2,
        LessThan = 3
    }

    public enum NexActionKind
    {
        ExecuteCommand = 0,
        SetState = 1,
        SetVisible = 2,
        SetText = 3,

        /// <summary>Pause before the rule's remaining actions run.</summary>
        Delay = 4
    }

    /// <summary>One compiled step of a rule.</summary>
    /// <remarks>
    /// <see cref="TargetNodeIndex"/> is an index, not an element id: the compiler resolved the
    /// authored id while it still had the document, so the runtime never does a name lookup and a
    /// target that no longer exists is a compile error rather than a silent no-op at runtime.
    ///
    /// The authored string value is pre-parsed into <see cref="NumberValue"/> at compile time
    /// (with <see cref="IsNumeric"/> recording whether that succeeded), so a comparison inside a
    /// click handler never parses a string.
    /// </remarks>
    [Serializable]
    public struct NexInteractionAction
    {
        public NexActionKind Kind;
        public string CommandId;
        public string StateKey;
        public string StringValue;
        public double NumberValue;
        public bool IsNumeric;
        public bool BoolValue;

        /// <summary>Node this action affects, or -1 when the action targets no node.</summary>
        public int TargetNodeIndex;

        /// <summary>Seconds for <see cref="NexActionKind.Delay"/>.</summary>
        public double Seconds;
    }

    /// <summary>One compiled interaction rule.</summary>
    /// <remarks>
    /// Actions live in the program's shared array and are referenced by
    /// <see cref="ActionStart"/> + <see cref="ActionCount"/> rather than a nested array per rule.
    /// Unity serializes nested collections poorly, and the flat form keeps a screen's whole
    /// interaction table in two contiguous arrays.
    /// </remarks>
    [Serializable]
    public struct NexInteractionRule
    {
        public string RuleId;

        /// <summary>Node that owns the rule - the one whose trigger raises it.</summary>
        public int NodeIndex;

        public NexTrigger Trigger;

        public NexPhase Phase;

        /// <summary>Stops the event after this rule runs; no later phase or ancestor sees it.</summary>
        public bool StopsPropagation;

        public bool HasCondition;
        public string ConditionKey;
        public NexComparison Comparison;
        public string ConditionString;
        public double ConditionNumber;
        public bool ConditionIsNumeric;

        public int ActionStart;
        public int ActionCount;
    }

    /// <summary>
    /// Every interaction rule on a screen, compiled and resolved.
    /// </summary>
    /// <remarks>
    /// Rules are sorted by node then trigger by the compiler, so looking up "what fires when node
    /// 12 is clicked" is a scan over a contiguous, deterministic range. With the rule counts a UI
    /// screen actually has, a scan beats a dictionary and costs no allocation and no hashing on
    /// the click path.
    /// </remarks>
    [Serializable]
    public sealed class NexInteractionProgram
    {
        public List<NexInteractionRule> Rules = new List<NexInteractionRule>();
        public List<NexInteractionAction> Actions = new List<NexInteractionAction>();

        public bool IsEmpty => Rules.Count == 0;

        /// <summary>Rules owned by a node for one trigger and phase, in authored order.</summary>
        public IEnumerable<NexInteractionRule> RulesFor(int nodeIndex, NexTrigger trigger, NexPhase phase)
        {
            for (int i = 0; i < Rules.Count; i++)
            {
                var rule = Rules[i];
                if (rule.NodeIndex == nodeIndex && rule.Trigger == trigger && rule.Phase == phase)
                    yield return rule;
            }
        }

        /// <summary>True when any rule listens on a non-target phase for this trigger.</summary>
        /// <remarks>
        /// Lets the runtime skip building an ancestor path on the overwhelmingly common screen
        /// where every rule is a plain target-phase rule.
        /// </remarks>
        public bool HasPropagatingRules(NexTrigger trigger)
        {
            for (int i = 0; i < Rules.Count; i++)
                if (Rules[i].Trigger == trigger && Rules[i].Phase != NexPhase.Target) return true;
            return false;
        }

        /// <summary>True when any node has a rule for this trigger - lets the backend skip wiring it at all.</summary>
        public bool HasAnyTrigger(NexTrigger trigger)
        {
            for (int i = 0; i < Rules.Count; i++)
                if (Rules[i].Trigger == trigger) return true;
            return false;
        }

        public NexInteractionAction ActionAt(int index)
            => index >= 0 && index < Actions.Count ? Actions[index] : default;

        /// <summary>
        /// True when any rule pauses partway through.
        /// </summary>
        /// <remarks>
        /// A screen without delays needs no per-frame pump at all, so the backend asks this before
        /// creating one. Most screens answer false and cost nothing.
        /// </remarks>
        public bool HasDelays()
        {
            for (int i = 0; i < Actions.Count; i++)
                if (Actions[i].Kind == NexActionKind.Delay) return true;
            return false;
        }
    }
}
