using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Reflection;
using MonoMod.RuntimeDetour;
using System.Runtime.CompilerServices;
using Debug = UnityEngine.Debug;

namespace SlugBase.Compatibility
{
    internal static class HookGenFix
    {
        private static Dictionary<HookInfo, Stack<Hook>> hookMap = new Dictionary<HookInfo, Stack<Hook>>();

        public static void Apply()
        {
            try
            {
                // Find HookManager
                // This should return null if the user isn't running Partiality
                Type t = Type.GetType("MonoMod.RuntimeDetour.HookManager, MonoMod.RuntimeDetour, Version=18.6.0.33006, Culture=neutral, PublicKeyToken=null");
                if (t == null) return;

                Debug.Log("Applying SlugBase compatibility changes for Partiality's HookGen...");

                ApplyToPartiality();

            }
            catch (Exception e)
            {
                Debug.Log("Failed to apply compatibility changes. This shouldn't be fatal, but may cause compatibility issues.");
                Debug.Log(e);
            }
        }

        private static void ApplyToPartiality()
        {
            // This is sequestered here since HookManager.Add might not exist
            new Hook((Action<MethodBase, Delegate>)HookManager.Add, (Action<Action<MethodBase, Delegate>, MethodBase, Delegate>)HookManager_Add);
            new Hook((Action<MethodBase, Delegate>)HookManager.Remove, (Action<Action<MethodBase, Delegate>, MethodBase, Delegate>)HookManager_Remove);
        }

        public static void HookManager_Add(Action<MethodBase, Delegate> orig, MethodBase method, Delegate hookDelegate)
        {
            HookInfo info = new HookInfo(method, hookDelegate);
            Stack<Hook> stack;
            if (!hookMap.TryGetValue(info, out stack))
                stack = hookMap[info] = new Stack<Hook>();
            Hook t = new Hook(method, hookDelegate);
            stack.Push(t);
        }

        public static void HookManager_Remove(Action<MethodBase, Delegate> orig, MethodBase method, Delegate hookDelegate)
        {
            HookInfo key = new HookInfo(method, hookDelegate);
            Stack<Hook> stack;
            if (!hookMap.TryGetValue(key, out stack))
                return;
            Hook hook = stack.Pop();
            hook.Undo();
            hook.Free();
            if (stack.Count == 0)
                hookMap.Remove(key);
        }

        /// <summary>
        /// Everything needed to identify a hook
        /// </summary>
        private struct HookInfo
        {
            public HookInfo(MethodBase from, Delegate to)
            {
                this.from = from;
                this.to = to.Method;
                target = to.Target;
            }

            public MethodBase from;
            public MethodBase to;
            public object target;
        }
    }
}
