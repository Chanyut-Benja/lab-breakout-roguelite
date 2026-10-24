namespace Enemies.Core
{
    /// <summary>
    /// Base state interface for Enemy Finite State Machine (FSM).
    /// </summary>
    public interface IState
    {
        void Enter();
        void Tick();
        void FixedTick();
        void Exit();
    }
}
