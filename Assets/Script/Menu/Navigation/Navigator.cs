using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using YARG.Core.Input;
using YARG.Core.Logging;
using YARG.Input;
using YARG.Menu.Persistent;
using YARG.Player;

namespace YARG.Menu.Navigation
{
    public readonly struct NavigationContext
    {
        /// <summary>
        /// The type of navigation.
        /// </summary>
        public readonly MenuAction Action;

        /// <summary>
        /// The <see cref="YargPlayer"/> that this event was invoked from.
        /// </summary>
        public readonly YargPlayer Player;

        /// <summary>
        /// Whether or not this action is a repeat.
        /// </summary>
        public readonly bool IsRepeat;

        public NavigationContext(MenuAction action, YargPlayer player, bool repeat = false)
        {
            Action = action;
            Player = player;
            IsRepeat = repeat;
        }

        public bool IsSameAs(NavigationContext other)
        {
            return other.Action == Action && other.Player == Player;
        }

        public NavigationContext AsRepeat()
        {
            return new NavigationContext(Action, Player, true);
        }
    }

    [DefaultExecutionOrder(-10)]
    public class Navigator : MonoSingleton<Navigator>
    {
        private const float INPUT_REPEAT_TIME = 0.035f;
        private const float INPUT_REPEAT_COOLDOWN = 0.5f;

        private static readonly HashSet<MenuAction> RepeatActions = new()
        {
            MenuAction.Up,
            MenuAction.Down,
            MenuAction.Left,
            MenuAction.Right,
        };

        /// <summary>
        /// Tracks a held directional input so we can repeat it.
        /// </summary>
        private class RepeatContext
        {
            public readonly NavigationContext Context;
            public float Timer;

            public RepeatContext(NavigationContext context)
            {
                Context = context;
                Timer = INPUT_REPEAT_COOLDOWN;
            }
        }

        /// <summary>
        /// Tracks a held action
        /// </summary>
        private class NavigationHold
        {
            public readonly NavigationContext Context;
            public readonly HoldTracker Tracker;

            public NavigationHold(NavigationContext context, HoldTracker tracker)
            {
                Context = context;
                Tracker = tracker;
            }
        }

        public class HoldContext
        {
            public readonly NavigationContext Context;
            public float Timer;

            public HoldContext(NavigationContext context)
            {
                Context = context;
                Timer = INPUT_REPEAT_COOLDOWN;
            }
        }

        public bool MusicPlayerActive => HelpBar.Instance.MusicPlayer.isActiveAndEnabled;

        private bool _disableMenuInputs;
        private NavigationInputBlockState _inputBlockState;
        private int _textInputSchemeCount;

        public bool DisableMenuInputs
        {
            get => _disableMenuInputs;
            set => SetDisableMenuInputs(value);
        }

        public event Action<NavigationContext> NavigationEvent;

        private readonly List<RepeatContext> _repeatInputs = new();
        private readonly List<NavigationHold> _holdInputs = new();
        private readonly Stack<NavigationScheme> _schemeStack = new();

        private void SetDisableMenuInputs(bool value)
        {
            if (_disableMenuInputs == value) return;

            _disableMenuInputs = value;

            if (_disableMenuInputs)
            {
                ClearTrackedInputs();
            }
        }

        public IDisposable PushInputBlocker()
        {
            _inputBlockState = _inputBlockState.AddBlocker();
            ClearTrackedInputs();

            return new InputBlocker(() =>
            {
                _inputBlockState = _inputBlockState.RemoveBlocker();
            });
        }

        private sealed class InputBlocker : IDisposable
        {
            private Action _release;

            public InputBlocker(Action release)
            {
                _release = release;
            }

            public void Dispose()
            {
                _release?.Invoke();
                _release = null;
            }
        }

        private void ClearTrackedInputs()
        {
            _repeatInputs.Clear();

            for (int i = _holdInputs.Count - 1; i >= 0; i--)
            {
                _holdInputs[i].Tracker.StopHolding();
                _holdInputs[i].Tracker.ClearEvents();
            }

            _holdInputs.Clear();
        }

        private void Start()
        {
            InputManager.MenuInput += ProcessInput;
            UpdateHelpBar().Forget();
        }

        private void Update()
        {
            if (ShouldBlockInputs())
            {
                ClearTrackedInputs();
                return;
            }

            foreach (var hold in _holdInputs)
            {
                hold.Tracker.Tick();
            }

            foreach (var repeat in _repeatInputs)
            {
                repeat.Timer -= Time.unscaledDeltaTime;

                if (repeat.Timer <= 0f)
                {
                    repeat.Timer = INPUT_REPEAT_TIME;
                    InvokeNavigationEvent(repeat.Context.AsRepeat());
                }
            }
        }

        private void ProcessInput(YargPlayer player, ref GameInput input)
        {
            if (ShouldBlockInputs()) return;

            var action = (MenuAction) input.Action;

            // Swap up and down for lefty flip
            if (player?.Profile.LeftyFlip == true)
            {
                action = action switch
                {
                    MenuAction.Up    => MenuAction.Down,
                    MenuAction.Down  => MenuAction.Up,
                    MenuAction.Left  => MenuAction.Right,
                    MenuAction.Right => MenuAction.Left,
                    _                => action
                };
            }

            var context = new NavigationContext(action, player);

            if (input.Button)
            {
                StartNavigationHold(context);
            }
            else
            {
                EndNavigationHold(context);
            }
        }

        private void StartNavigationHold(NavigationContext context)
        {
            // Skip if the input is already being tracked as a hold
            if (_holdInputs.Any(i => i.Context.IsSameAs(context)))
            {
                return;
            }

            // Skip if the input is already being tracked as a repeat
            if (_repeatInputs.Any(i => i.Context.IsSameAs(context)))
            {
                return;
            }

            if (_schemeStack.Count > 0 &&
                _schemeStack.Peek().TryGetHoldSeconds(context.Action, out var holdSeconds))
            {
                var tracker = new HoldTracker(holdSeconds);
                var navHold = new NavigationHold(context, tracker);

                var ctx = context;
                tracker.OnClick += () => InvokeNavigationEvent(ctx);
                tracker.OnHoldComplete += () => InvokeHoldEvent(ctx);

                tracker.StartHolding();
                _holdInputs.Add(navHold);
                return;
            }

            InvokeNavigationEvent(context);

            if (RepeatActions.Contains(context.Action))
            {
                _repeatInputs.Add(new RepeatContext(context));
            }
        }

        private void EndNavigationHold(NavigationContext context)
        {
            for (int i = _holdInputs.Count - 1; i >= 0; i--)
            {
                if (!_holdInputs[i].Context.IsSameAs(context))
                {
                    continue;
                }

                _holdInputs[i].Tracker.StopHolding();
                _holdInputs[i].Tracker.ClearEvents();
                _holdInputs.RemoveAt(i);
            }

            // Remove matching repeat inputs
            for (int i = _repeatInputs.Count - 1; i >= 0; i--)
            {
                if (_repeatInputs[i].Context.IsSameAs(context))
                {
                    _repeatInputs.RemoveAt(i);
                }
            }

            InvokeHoldOffEvent(context);
        }

        public float GetHoldProgress(MenuAction action)
        {
            float progress = -1f;
            foreach (var hold in _holdInputs)
            {
                if (hold.Context.Action == action)
                {
                    progress = Mathf.Max(progress, hold.Tracker.HoldProgress);
                }
            }
            return progress;
        }

        private void InvokeNavigationEvent(NavigationContext ctx)
        {
            if (ShouldBlockInputs())
            {
                return;
            }

            NavigationEvent?.Invoke(ctx);

            if (_schemeStack.Count > 0)
            {
                _schemeStack.Peek().InvokeFuncs(ctx);
            }
        }

        private void InvokeHoldOffEvent(NavigationContext ctx)
        {
            if (ShouldBlockInputs())
            {
                return;
            }

            if (_schemeStack.Count > 0)
            {
                _schemeStack.Peek().InvokeHoldOffFuncs(ctx);
            }
        }

        private void InvokeHoldEvent(NavigationContext ctx)
        {
            if (ShouldBlockInputs())
            {
                return;
            }

            if (_schemeStack.Count > 0)
            {
                _schemeStack.Peek().InvokeHoldFuncs(ctx);
            }
        }

        public async UniTaskVoid PushScheme(NavigationScheme scheme)
        {
            // If there is a dialog open, wait for it to close before pushing the new scheme
            if (DialogManager.Instance.IsDialogShowing)
            {
                await DialogManager.Instance.WaitUntilCurrentClosed();
            }

            _schemeStack.Push(scheme);
            UpdateHelpBar().Forget();
        }

        /// <summary>
        /// Pushes a scheme immediately, without waiting for any open dialog to
        /// close. Use this for schemes that must be active *while* a dialog is
        /// open (e.g. the color picker's slider-edit scheme). The async
        /// <see cref="PushScheme"/> would otherwise delay the push until the
        /// dialog closes — by which point the dialog's objects are destroyed.
        /// </summary>
        public void PushSchemeImmediate(NavigationScheme scheme)
        {
            _schemeStack.Push(scheme);
            UpdateHelpBar().Forget();
        }

        /// <summary>
        /// Pushes a scheme that is allowed to receive input while a TMP input field is
        /// focused. This is reserved for text-editing actions such as leaving Music
        /// Library search; ordinary menu navigation remains blocked.
        /// </summary>
        public void PushTextInputScheme(NavigationScheme scheme)
        {
            _textInputSchemeCount++;
            scheme.PopCallback += () => _textInputSchemeCount = Mathf.Max(0, _textInputSchemeCount - 1);
            PushSchemeImmediate(scheme);
        }

        public void PopScheme()
        {
            if (_schemeStack.Count == 0)
            {
                YargLogger.LogFormatWarning("Tried to pop a NavigationScheme when none were present. Stack Trace: {0}", Environment.StackTrace);
            }
            else
            {
                var scheme = _schemeStack.Pop();
                scheme.PopCallback?.Invoke();
            }

            UpdateHelpBar().Forget();
        }

        public void PopAllSchemes()
        {
            // Pop all one by one so we can call each callback (instead of clearing)
            while (_schemeStack.Count >= 1)
            {
                var scheme = _schemeStack.Pop();
                scheme.PopCallback?.Invoke();
            }

            UpdateHelpBar().Forget();
        }

        private async UniTask UpdateHelpBar()
        {
            // Wait one frame to update, in case another one gets pushed.
            // This prevents the music player from resetting across schemes.
            await UniTask.WaitForEndOfFrame(this);

            if (_schemeStack.Count <= 0)
            {
                HelpBar.Instance.Reset();
            }
            else
            {
                HelpBar.Instance.SetInfoFromScheme(_schemeStack.Peek());
            }
        }

        private bool ShouldBlockInputs()
        {
            bool blockedByTextInput = IsTextInputFocused() && _textInputSchemeCount <= 0;
            return DisableMenuInputs || _inputBlockState.IsBlocked || LoadingScreen.IsActive || blockedByTextInput;
        }

        private static bool IsTextInputFocused()
        {
            var selectedGameObject = EventSystem.current?.currentSelectedGameObject;
            return selectedGameObject != null &&
                selectedGameObject.GetComponentInParent<TMP_InputField>()?.isFocused == true;
        }
    }
}
