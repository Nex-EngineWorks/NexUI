using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using emiteat.NexUI.MotionClip;
using emiteat.NexUI.Tests.Fakes;

namespace emiteat.NexUI.Tests.EditMode
{
    public sealed class UIMotionStateMachineTests
    {
        [Test]
        public void FindTransition_ExactMatch_PreferredOverAnyState()
        {
            var exact = new UIMotionStateTransition { from = UIMotionState.Normal, to = UIMotionState.Hover };
            var any = new UIMotionStateTransition { fromAnyState = true, to = UIMotionState.Hover };
            var machine = ScriptableObject.CreateInstance<UIMotionStateMachine>();
            machine.transitions = new[] { any, exact };

            Assert.AreSame(exact, machine.FindTransition(UIMotionState.Normal, UIMotionState.Hover));
        }

        [Test]
        public void FindTransition_FallsBackToAnyState()
        {
            var any = new UIMotionStateTransition { fromAnyState = true, to = UIMotionState.Disabled };
            var machine = ScriptableObject.CreateInstance<UIMotionStateMachine>();
            machine.transitions = new[] { any };

            Assert.AreSame(any, machine.FindTransition(UIMotionState.Selected, UIMotionState.Disabled));
        }

        [Test]
        public void FindTransition_NoMatch_ReturnsNull()
        {
            var machine = ScriptableObject.CreateInstance<UIMotionStateMachine>();
            Assert.IsNull(machine.FindTransition(UIMotionState.Normal, UIMotionState.Hover));
        }

        [Test]
        public async Task Runner_NoMachine_SetsStateDirectlyWithoutPlaying()
        {
            var player = new FakeMotionClipPlayer();
            var runner = new UIMotionStateRunner(UIMotionState.Normal, player);

            await runner.TransitionToAsync(new FakeSurface("s"), null, UIMotionState.Hover);

            Assert.AreEqual(UIMotionState.Hover, runner.CurrentState);
            Assert.AreEqual(0, player.PlayCount);
        }

        [Test]
        public async Task Runner_NoMatchingTransition_SetsStateWithoutPlaying()
        {
            var player = new FakeMotionClipPlayer();
            var runner = new UIMotionStateRunner(UIMotionState.Normal, player);
            var machine = ScriptableObject.CreateInstance<UIMotionStateMachine>();

            await runner.TransitionToAsync(new FakeSurface("s"), machine, UIMotionState.Hover);

            Assert.AreEqual(UIMotionState.Hover, runner.CurrentState);
            Assert.AreEqual(0, player.PlayCount);
        }

        [Test]
        public void Runner_MatchingTransition_PlaysClipAndUpdatesStateImmediately()
        {
            var player = new FakeMotionClipPlayer();
            var runner = new UIMotionStateRunner(UIMotionState.Normal, player);
            var clip = ScriptableObject.CreateInstance<UIMotionClip>();
            var machine = ScriptableObject.CreateInstance<UIMotionStateMachine>();
            machine.transitions = new[]
            {
                new UIMotionStateTransition { from = UIMotionState.Normal, to = UIMotionState.Hover, clip = clip }
            };

            _ = runner.TransitionToAsync(new FakeSurface("s"), machine, UIMotionState.Hover);

            // State and PlayAsync-call both happen synchronously before the first await suspends.
            Assert.AreEqual(UIMotionState.Hover, runner.CurrentState);
            Assert.AreEqual(1, player.PlayCount);
        }

        [Test]
        public void Runner_Ignore_DropsNewRequestWhileTransitionInFlight()
        {
            var player = new FakeMotionClipPlayer();
            var runner = new UIMotionStateRunner(UIMotionState.Normal, player);
            var clipA = ScriptableObject.CreateInstance<UIMotionClip>();
            var machine = ScriptableObject.CreateInstance<UIMotionStateMachine>();
            machine.transitions = new[]
            {
                new UIMotionStateTransition { from = UIMotionState.Normal, to = UIMotionState.Hover, clip = clipA },
                new UIMotionStateTransition { fromAnyState = true, to = UIMotionState.Pressed, clip = clipA, interruptPolicy = UIMotionStateInterruptPolicy.Ignore }
            };
            var surface = new FakeSurface("s");

            _ = runner.TransitionToAsync(surface, machine, UIMotionState.Hover); // never completes (fake player)
            _ = runner.TransitionToAsync(surface, machine, UIMotionState.Pressed);

            Assert.AreEqual(1, player.PlayCount, "Ignore must not start a second PlayAsync.");
            Assert.AreEqual(0, player.StopCount);
            Assert.AreEqual(UIMotionState.Hover, runner.CurrentState, "Ignored request must not change CurrentState either.");
        }

        [Test]
        public void Runner_CompleteImmediately_SnapsInterruptedClipToEndBeforeStartingNext()
        {
            var player = new FakeMotionClipPlayer();
            var runner = new UIMotionStateRunner(UIMotionState.Normal, player);
            var clipA = ScriptableObject.CreateInstance<UIMotionClip>();
            clipA.duration = 2f;
            var clipB = ScriptableObject.CreateInstance<UIMotionClip>();
            clipB.duration = 1f;
            var machine = ScriptableObject.CreateInstance<UIMotionStateMachine>();
            machine.transitions = new[]
            {
                new UIMotionStateTransition { from = UIMotionState.Normal, to = UIMotionState.Hover, clip = clipA },
                new UIMotionStateTransition { fromAnyState = true, to = UIMotionState.Pressed, clip = clipB, interruptPolicy = UIMotionStateInterruptPolicy.CompleteImmediately }
            };
            var surface = new FakeSurface("s");

            _ = runner.TransitionToAsync(surface, machine, UIMotionState.Hover); // never completes (fake player)
            Assert.AreEqual(1, player.PlayCount);

            _ = runner.TransitionToAsync(surface, machine, UIMotionState.Pressed);

            Assert.AreEqual(1, player.StopCount);
            Assert.AreEqual(1, player.EvaluateCount);
            Assert.AreEqual(clipA.duration, player.LastEvaluatedTime, 0.0001f);
            Assert.AreEqual(2, player.PlayCount);
            Assert.AreEqual(UIMotionState.Pressed, runner.CurrentState);
        }

        [Test]
        public void Runner_Restart_StopsInterruptedClipWithoutSnapping()
        {
            var player = new FakeMotionClipPlayer();
            var runner = new UIMotionStateRunner(UIMotionState.Normal, player);
            var clipA = ScriptableObject.CreateInstance<UIMotionClip>();
            var clipB = ScriptableObject.CreateInstance<UIMotionClip>();
            var machine = ScriptableObject.CreateInstance<UIMotionStateMachine>();
            machine.transitions = new[]
            {
                new UIMotionStateTransition { from = UIMotionState.Normal, to = UIMotionState.Hover, clip = clipA },
                new UIMotionStateTransition { fromAnyState = true, to = UIMotionState.Pressed, clip = clipB, interruptPolicy = UIMotionStateInterruptPolicy.Restart }
            };
            var surface = new FakeSurface("s");

            _ = runner.TransitionToAsync(surface, machine, UIMotionState.Hover);
            _ = runner.TransitionToAsync(surface, machine, UIMotionState.Pressed);

            Assert.AreEqual(1, player.StopCount);
            Assert.AreEqual(0, player.EvaluateCount, "Restart must not snap to the interrupted clip's end pose.");
            Assert.AreEqual(2, player.PlayCount);
        }
    }
}
