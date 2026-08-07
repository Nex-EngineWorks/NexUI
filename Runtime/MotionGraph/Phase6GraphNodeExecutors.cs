using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using emiteat.NexUI.Abstractions;
using UnityEngine;

namespace emiteat.NexUI.MotionGraph
{
    /// <summary>
    /// Phase 6 node set (brief §17/19/13/24, Architecture-Audit.md Phase 6): expressions, Blackboard
    /// variable writes, Dispatch Command, and the remaining flow-control nodes (Repeat/Loop via a
    /// negative count, Timeout, Race, Subgraph). Stagger/For Each and Tooltip/Sound nodes are not in
    /// this set - they need an Element Query/list system and, respectively, real Tooltip/Audio
    /// Runtime subsystems that don't exist yet; adding them here would mean either a fake no-op node
    /// or a rushed half-built subsystem, so they're left for a dedicated pass.
    /// </summary>
    public sealed class ExpressionNodeExecutor : IUIGraphNodeExecutor
    {
        public string NodeType => "Data.Expression";

        public Task ExecuteAsync(UIGraphNodeExecutionArgs args)
        {
            var operation = args.ResolveInput("Operation").stringValue;
            var a = args.ResolveInput("A");
            var b = args.ResolveInput("B");
            UIGraphValue result;

            switch (operation)
            {
                case "Add": result = UIGraphValue.Float(a.floatValue + b.floatValue); break;
                case "Subtract": result = UIGraphValue.Float(a.floatValue - b.floatValue); break;
                case "Multiply": result = UIGraphValue.Float(a.floatValue * b.floatValue); break;
                case "Divide": result = UIGraphValue.Float(!Mathf.Approximately(b.floatValue, 0f) ? a.floatValue / b.floatValue : 0f); break;
                case "GreaterThan": result = UIGraphValue.Bool(a.floatValue > b.floatValue); break;
                case "LessThan": result = UIGraphValue.Bool(a.floatValue < b.floatValue); break;
                case "Equals": result = UIGraphValue.Bool(Mathf.Approximately(a.floatValue, b.floatValue)); break;
                case "And": result = UIGraphValue.Bool(a.boolValue && b.boolValue); break;
                case "Or": result = UIGraphValue.Bool(a.boolValue || b.boolValue); break;
                case "Not": result = UIGraphValue.Bool(!a.boolValue); break;
                default: result = default; break;
            }

            args.Context.SetNodeOutput(args.Node.id, "Result", result);
            return args.RunNext("Next", args.CancellationToken);
        }
    }

    /// <summary>Writes a named float into <see cref="UIGraphExecutionContext.Variables"/> - the Blackboard's Variable scope (brief §17).</summary>
    public sealed class SetFloatVariableNodeExecutor : IUIGraphNodeExecutor
    {
        public string NodeType => "Data.SetFloatVariable";

        public Task ExecuteAsync(UIGraphNodeExecutionArgs args)
        {
            var name = args.ResolveInput("Name").stringValue;
            if (!string.IsNullOrEmpty(name))
                args.Context.Variables[name] = UIGraphValue.Float(args.ResolveInput("Value").floatValue);
            return args.RunNext("Next", args.CancellationToken);
        }
    }

    /// <summary>Writes a named bool into <see cref="UIGraphExecutionContext.Variables"/>.</summary>
    public sealed class SetBoolVariableNodeExecutor : IUIGraphNodeExecutor
    {
        public string NodeType => "Data.SetBoolVariable";

        public Task ExecuteAsync(UIGraphNodeExecutionArgs args)
        {
            var name = args.ResolveInput("Name").stringValue;
            if (!string.IsNullOrEmpty(name))
                args.Context.Variables[name] = UIGraphValue.Bool(args.ResolveInput("Value").boolValue);
            return args.RunNext("Next", args.CancellationToken);
        }
    }

    /// <summary>Generic <see cref="IUICommand"/> envelope a game registers one <see cref="IUICommandHandler{TCommand}"/> for, routing internally by <see cref="CommandId"/> - lets the graph dispatch string-keyed commands through the same typed pipeline everything else uses, without the graph needing a concrete command type per command.</summary>
    public sealed class UIGraphCommand : IUICommand
    {
        public string CommandId { get; }
        public IReadOnlyDictionary<string, UIGraphValue> Payload { get; }

        public UIGraphCommand(string commandId, IReadOnlyDictionary<string, UIGraphValue> payload)
        {
            CommandId = commandId;
            Payload = payload;
        }
    }

    /// <summary>
    /// Dispatches a <see cref="UIGraphCommand"/> through <see cref="UIGraphExecutionContext.CommandDispatcher"/>.
    /// Data inputs named "Payload.*" are bundled into the command's payload (key = name with the
    /// prefix stripped). Continues to "Success" or "Failed" - "Failed" covers both "no dispatcher
    /// wired up" and the dispatcher/handler throwing.
    /// </summary>
    public sealed class DispatchCommandNodeExecutor : IUIGraphNodeExecutor
    {
        private const string PayloadPrefix = "Payload.";

        public string NodeType => "Command.Dispatch";

        public async Task ExecuteAsync(UIGraphNodeExecutionArgs args)
        {
            var commandId = args.ResolveInput("CommandId").stringValue;
            if (string.IsNullOrEmpty(commandId) || args.Context.CommandDispatcher == null)
            {
                await args.RunNext("Failed", args.CancellationToken);
                return;
            }

            var payload = new Dictionary<string, UIGraphValue>();
            foreach (var input in args.Node.dataInputs)
            {
                if (input?.portName != null && input.portName.StartsWith(PayloadPrefix, StringComparison.Ordinal))
                    payload[input.portName.Substring(PayloadPrefix.Length)] = args.ResolveInput(input.portName);
            }

            try
            {
                await args.Context.CommandDispatcher.DispatchAsync(new UIGraphCommand(commandId, payload));
                await args.RunNext("Success", args.CancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                await args.RunNext("Failed", args.CancellationToken);
            }
        }
    }

    /// <summary>
    /// Runs "Body" <c>Count</c> times, then continues to "Completed". A negative <c>Count</c> repeats
    /// forever until the run is cancelled - this is brief's separate "Loop" node folded into Repeat
    /// rather than duplicated, since the only difference is the stop condition.
    /// </summary>
    public sealed class RepeatNodeExecutor : IUIGraphNodeExecutor
    {
        public string NodeType => "Flow.Repeat";

        public async Task ExecuteAsync(UIGraphNodeExecutionArgs args)
        {
            var count = args.ResolveInput("Count").intValue;

            if (count < 0)
            {
                while (!args.CancellationToken.IsCancellationRequested)
                    await args.RunNext("Body", args.CancellationToken);
                return;
            }

            for (var i = 0; i < count; i++)
            {
                if (args.CancellationToken.IsCancellationRequested) return;
                await args.RunNext("Body", args.CancellationToken);
            }
            await args.RunNext("Completed", args.CancellationToken);
        }
    }

    /// <summary>
    /// Races "Body" against a <c>Duration</c>-second timer. If Body finishes first, continues to
    /// "Completed"; if the timer wins, cancels Body (via a token linked to, not replacing, the
    /// caller's own) and continues to "TimedOut". Note: the losing side's task is not re-awaited
    /// after the race resolves, so a cancellation it raises afterward surfaces only as UniTask's
    /// unobserved-exception logging, not a thrown exception here - a known, accepted trade-off of
    /// the race pattern rather than a bug in this node specifically.
    /// </summary>
    public sealed class TimeoutNodeExecutor : IUIGraphNodeExecutor
    {
        public string NodeType => "Flow.Timeout";

        public async Task ExecuteAsync(UIGraphNodeExecutionArgs args)
        {
            var duration =
                Mathf.Max(0f, args.ResolveInput("Duration").floatValue);

            using var cts =
                CancellationTokenSource.CreateLinkedTokenSource(
                    args.CancellationToken);

            var bodyTask = args.RunNext(
                "Body",
                cts.Token);

            var timeoutTask = Task.Delay(
                TimeSpan.FromSeconds(duration),
                cts.Token);

            var completedTask = await Task.WhenAny(
                bodyTask,
                timeoutTask);

            if (completedTask == bodyTask)
            {
                cts.Cancel();

                await bodyTask;

                await args.RunNext(
                    "Completed",
                    args.CancellationToken);

                return;
            }

            cts.Cancel();

            await args.RunNext(
                "TimedOut",
                args.CancellationToken);
        }
    }

    /// <summary>
    /// Launches every flow output concurrently and continues to "Completed" as soon as the first
    /// one finishes, cancelling the rest (brief's Race node). Same unobserved-cancellation caveat as
    /// <see cref="TimeoutNodeExecutor"/> applies to the losing branches.
    /// </summary>
    public sealed class RaceNodeExecutor : IUIGraphNodeExecutor
    {
        public string NodeType => "Flow.Race";

        public async Task ExecuteAsync(UIGraphNodeExecutionArgs args)
        {
            if (args.Node.flowOutputs.Length == 0)
            {
                await args.RunNext("Completed", args.CancellationToken);
                return;
            }

            using (var cts = CancellationTokenSource.CreateLinkedTokenSource(args.CancellationToken))
            {
                var branches = new List<Task>();
                for (var i = 0; i < args.Node.flowOutputs.Length; i++)
                {
                    var output = args.Node.flowOutputs[i];
                    // Completed is the continuation after the race, not one of its competitors.
                    // Launching it here and again below executed the continuation twice.
                    if (string.Equals(output.name, "Completed", StringComparison.OrdinalIgnoreCase)) continue;
                    branches.Add(args.RunNext(output.name, cts.Token));
                }

                if (branches.Count > 0) await Task.WhenAny(branches);
                cts.Cancel();
            }

            await args.RunNext("Completed", args.CancellationToken);
        }
    }

    /// <summary>
    /// Runs another <see cref="UIMotionGraphAsset"/>'s named event, sharing this run's
    /// Surface/Parameters/Variables/CommandDispatcher, then continues to "Completed". Built via
    /// <see cref="UIGraphNodeExecutionArgs.CreateSubExecutor"/> so the subgraph resolves node types
    /// through the same registry as the parent graph (including any project-registered custom node
    /// executors), not just the built-in set.
    /// </summary>
    public sealed class RunSubgraphNodeExecutor : IUIGraphNodeExecutor
    {
        public string NodeType => "Graph.RunSubgraph";

        public async Task ExecuteAsync(UIGraphNodeExecutionArgs args)
        {
            var subgraph = args.ResolveInput("Graph").motionGraphValue;
            var eventName = args.ResolveInput("EventName").stringValue;

            if (subgraph != null && !string.IsNullOrEmpty(eventName))
            {
                var executor = args.CreateSubExecutor(subgraph);
                await executor.RunEventAsync(eventName, args.Context, args.CancellationToken);
            }

            await args.RunNext("Completed", args.CancellationToken);
        }
    }
}
