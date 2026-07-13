using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using emiteat.NexUI.MotionClip;

namespace emiteat.NexUI.MotionGraph
{
    /// <summary>
    /// Phase 5 node set (brief §12/13/15/26, Architecture-Audit.md Phase 5): an Event passthrough,
    /// Sequence/Parallel/Delay/Branch flow control, and Play Motion Clip. Mirrors
    /// <c>BuiltInDesignerCommands.cs</c>'s one-file-per-registry-worth-of-implementations convention.
    /// </summary>
    public static class BuiltInGraphNodeExecutors
    {
        public static IEnumerable<IUIGraphNodeExecutor> CreateDefaults()
        {
            yield return new EventNodeExecutor();
            yield return new SequenceNodeExecutor();
            yield return new ParallelNodeExecutor();
            yield return new DelayNodeExecutor();
            yield return new BranchNodeExecutor();
            yield return new PlayMotionClipNodeExecutor();

            // Phase 6 (see Phase6GraphNodeExecutors.cs)
            yield return new ExpressionNodeExecutor();
            yield return new SetFloatVariableNodeExecutor();
            yield return new SetBoolVariableNodeExecutor();
            yield return new DispatchCommandNodeExecutor();
            yield return new RepeatNodeExecutor();
            yield return new TimeoutNodeExecutor();
            yield return new RaceNodeExecutor();
            yield return new RunSubgraphNodeExecutor();
        }
    }

    /// <summary>Entry-point node: every graph's event nodes just pass through to "Next" - the actual event-name -> node-id mapping lives on <see cref="UIMotionGraphAsset.entryPoints"/>.</summary>
    public sealed class EventNodeExecutor : IUIGraphNodeExecutor
    {
        public string NodeType => "Event";
        public UniTask ExecuteAsync(UIGraphNodeExecutionArgs args) => args.RunNext("Next", args.CancellationToken);
    }

    /// <summary>Walks <c>Step0</c>, <c>Step1</c>, ... in the order they appear on the node, awaiting each before starting the next. New steps can be appended by adding more flow outputs (brief §13: "동적 출력 포트를 추가할 수 있게 하라").</summary>
    public sealed class SequenceNodeExecutor : IUIGraphNodeExecutor
    {
        public string NodeType => "Flow.Sequence";

        public async UniTask ExecuteAsync(UIGraphNodeExecutionArgs args)
        {
            foreach (var output in args.Node.flowOutputs)
            {
                if (args.CancellationToken.IsCancellationRequested) return;
                await args.RunNext(output.name, args.CancellationToken);
            }
        }
    }

    /// <summary>Launches every flow output concurrently and waits for all of them (brief's "All Finished" completion policy; "Any Finished"/"Do Not Wait" are not implemented yet - see Architecture-Audit.md).</summary>
    public sealed class ParallelNodeExecutor : IUIGraphNodeExecutor
    {
        public string NodeType => "Flow.Parallel";

        public async UniTask ExecuteAsync(UIGraphNodeExecutionArgs args)
        {
            var branches = new List<UniTask>();
            foreach (var output in args.Node.flowOutputs)
                branches.Add(args.RunNext(output.name, args.CancellationToken));
            await UniTask.WhenAll(branches);
        }
    }

    /// <summary>Waits <c>Duration</c> seconds (unscaled time, so it isn't affected by game pause) then continues to "Next".</summary>
    public sealed class DelayNodeExecutor : IUIGraphNodeExecutor
    {
        public string NodeType => "Flow.Delay";

        public async UniTask ExecuteAsync(UIGraphNodeExecutionArgs args)
        {
            var duration = args.ResolveInput("Duration").floatValue;
            if (duration > 0f)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(duration), DelayType.UnscaledDeltaTime,
                    PlayerLoopTiming.Update, args.CancellationToken).SuppressCancellationThrow();
            }
            await args.RunNext("Next", args.CancellationToken);
        }
    }

    /// <summary>Reads the <c>Condition</c> bool input and continues to "True" or "False".</summary>
    public sealed class BranchNodeExecutor : IUIGraphNodeExecutor
    {
        public string NodeType => "Flow.Branch";

        public UniTask ExecuteAsync(UIGraphNodeExecutionArgs args)
        {
            var condition = args.ResolveInput("Condition").boolValue;
            return args.RunNext(condition ? "True" : "False", args.CancellationToken);
        }
    }

    /// <summary>
    /// Plays the <c>Clip</c> input's <see cref="UIMotionClip"/> against the executing surface via
    /// the same <see cref="UIMotionClipPlayer"/> the Motion Clip Editor and Motion State Machine
    /// use, then continues to "Completed". The clip's own tracks resolve their target elements
    /// (no per-node target override) - consistent with how every other Motion Clip consumer works.
    /// </summary>
    public sealed class PlayMotionClipNodeExecutor : IUIGraphNodeExecutor
    {
        public string NodeType => "Motion.PlayClip";

        private readonly IUIMotionClipPlayer _player;

        public PlayMotionClipNodeExecutor(IUIMotionClipPlayer player = null)
            => _player = player ?? new UIMotionClipPlayer();

        public async UniTask ExecuteAsync(UIGraphNodeExecutionArgs args)
        {
            var clip = args.ResolveInput("Clip").motionClipValue;
            if (clip != null && args.Context.Surface != null)
                await _player.PlayAsync(args.Context.Surface, clip, args.CancellationToken);

            await args.RunNext("Completed", args.CancellationToken);
        }
    }
}
