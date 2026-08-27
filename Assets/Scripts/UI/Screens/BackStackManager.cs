using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AlipiriAR.UI
{
    /// <summary>
    /// Android hardware back handling. This project runs the new Input System exclusively
    /// (ProjectSettings activeInputHandler=1), where the legacy Input.GetKeyDown throws — so
    /// this polls Keyboard.current, which is where Unity routes the Android back button.
    /// Handlers register on push (top overlay opens) and unregister on pop (it closes); Update
    /// walks the stack top-down so the topmost overlay always gets first refusal. PLAN.md §06:
    /// "closes the top overlay, then pops the pushed screen, then returns to the default tab,
    /// then shows a leave-confirm at the root."
    /// </summary>
    public class BackStackManager : MonoBehaviour
    {
        private static BackStackManager _instance;
        private readonly List<Func<bool>> _handlers = new();

        public static BackStackManager Instance
        {
            get
            {
                if (_instance != null) return _instance;
                var go = new GameObject("~BackStackManager");
                UnityEngine.Object.DontDestroyOnLoad(go);
                _instance = go.AddComponent<BackStackManager>();
                return _instance;
            }
        }

        /// <summary>Registers a handler. Return true from it to consume the back press
        /// (stops propagation); return false to let the next handler down the stack try.</summary>
        public void Push(Func<bool> handler) => _handlers.Add(handler);

        public void Pop(Func<bool> handler) => _handlers.Remove(handler);

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame) return;

            for (int i = _handlers.Count - 1; i >= 0; i--)
            {
                if (_handlers[i]()) return;
            }
        }
    }
}
