using System;
using UnityEngine;

namespace Enemies.Core
{
    /// <summary>
    /// Robust Finite State Machine controller for Enemy AI.
    /// Manages state entry, tick updates, fixed updates, and transitions.
    /// </summary>
    [Serializable]
    public class StateMachine
    {
        public IState CurrentState { get; private set; }
        public IState PreviousState { get; private set; }

        public string CurrentStateName => CurrentState != null ? CurrentState.GetType().Name : "None";

        public event Action<IState, IState> OnStateChanged;

        public void Initialize(IState startingState)
        {
            if (startingState == null)
            {
                Debug.LogWarning("[StateMachine] Attempted to initialize with null state.");
                return;
            }

            CurrentState = startingState;
            CurrentState.Enter();
            OnStateChanged?.Invoke(null, CurrentState);
        }

        public void ChangeState(IState newState)
        {
            if (newState == null)
            {
                Debug.LogWarning("[StateMachine] Attempted to transition to null state.");
                return;
            }

            if (CurrentState == newState)
            {
                return;
            }

            PreviousState = CurrentState;
            CurrentState?.Exit();

            CurrentState = newState;
            CurrentState.Enter();

            OnStateChanged?.Invoke(PreviousState, CurrentState);
        }

        public void Tick()
        {
            CurrentState?.Tick();
        }

        public void FixedTick()
        {
            CurrentState?.FixedTick();
        }
    }
}
