using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace emiteat.NexUI.State
{
    /// <summary>
    /// Maps string keys to actions, so declarative bindings (e.g. a button's command
    /// key) can invoke logic without a hard reference. Supports sync and async actions.
    /// </summary>
    public sealed class UIActionResolver
    {
        private readonly Dictionary<string, Func<Task>> _actions = new Dictionary<string, Func<Task>>();

        public void Register(string key, Action action)
        {
            if (action == null) return;
            _actions[key] = () => { action(); return Task.CompletedTask; };
        }

        public void Register(string key, Func<Task> asyncAction)
        {
            if (asyncAction == null) return;
            _actions[key] = asyncAction;
        }

        public bool Contains(string key) => _actions.ContainsKey(key);

        /// <summary>All registered action keys (for debug inspection).</summary>
        public IReadOnlyCollection<string> Keys => _actions.Keys;

        public void Unregister(string key) => _actions.Remove(key);

        public async Task ExecuteAsync(string key)
        {
            if (_actions.TryGetValue(key, out var action))
            {
                await action();
                return;
            }
            Debug.LogWarning($"[NexUI] No action registered for key '{key}'.");
        }
    }
}
