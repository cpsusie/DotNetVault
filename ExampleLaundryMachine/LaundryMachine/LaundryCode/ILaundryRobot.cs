using System;
using System.Runtime.CompilerServices;
using System.Threading;
using JetBrains.Annotations;

namespace LaundryMachine.LaundryCode
{
    public enum LaundryRobotCategory
    {
        Loader,
        Unloader,
        Trickster
    }

    public enum RobotState
    {
        Initialized = 0,
        StartingUp,
        Active,
        Pausing,
        Paused,
        ThreadTerminated
    }

    public interface ILaundryRobot : IDisposable
    {
        event EventHandler<RobotActedEventArgs> RobotActed;
        bool IsDisposed { get; }
        RobotState State { get; }
        string RobotName { get; }
        LaundryRobotCategory RobotCategory { get; }
        Guid RobotId { get; }
        
        void StartRobot();
        bool PauseRobot();
        bool UnPauseRobot();

        (bool AccessOk, LaundryItems? HeldLaundry) QueryHeldLaundry();
        (bool AccessOk, LaundryItems? RemovedLaundry) RemoveAnyLaundry();
    }

    public struct RobotActivityFlag
    {
        public static implicit operator RobotState(RobotActivityFlag flag) => flag.State;

        public RobotState State
        {
            get
            {
                int code = _robotState;
                return CodeToState(code);
            }
        }

        public bool SetFromInitializedToStartingUp()
        {
            const int wantToBe = (int) RobotState.StartingUp;
            const int needToBeNow = (int) RobotState.Initialized;
            return Interlocked.CompareExchange(ref _robotState, wantToBe, needToBeNow) == needToBeNow;
        }

        public bool SetFromStartingUpToPaused()
        {
            const int wantToBe = (int)RobotState.Paused;
            const int needToBeNow = (int)RobotState.StartingUp;
            return Interlocked.CompareExchange(ref _robotState, wantToBe, needToBeNow) == needToBeNow;
        }

        public bool SetFromPausedToActive()
        {
            const int wantToBe = (int)RobotState.Active;
            const int needToBeNow = (int)RobotState.Paused;
            return Interlocked.CompareExchange(ref _robotState, wantToBe, needToBeNow) == needToBeNow;
        }

        public bool SetFromActiveToPausing()
        {
            const int wantToBe = (int)RobotState.Pausing;
            const int needToBeNow = (int)RobotState.Active;
            return Interlocked.CompareExchange(ref _robotState, wantToBe, needToBeNow) == needToBeNow;
        }

        public bool SetFromPausingToPaused()
        {
            const int wantToBe = (int)RobotState.Paused;
            const int needToBeNow = (int)RobotState.Pausing;
            return Interlocked.CompareExchange(ref _robotState, wantToBe, needToBeNow) == needToBeNow;
        }

        public void ForceTerminated()
        {
            Interlocked.Exchange(ref _robotState, (int) RobotState.ThreadTerminated);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static RobotState CodeToState(int code) => (RobotState) code;

        private volatile int _robotState;

        
        
    }

    public static class LaundryRobotExtensions
    {
        public static bool StartOrUnPause([NotNull] this ILaundryRobot robot)
        {
            if (robot == null) throw new ArgumentNullException(nameof(robot));

            bool ret;
            switch (robot.State)
            {
                case RobotState.Initialized:
                    robot.StartRobot();
                    ret = robot.UnPauseRobot();
                    break;
                case RobotState.Active:
                    ret = true;
                    break;
                case RobotState.Paused:
                    ret = robot.UnPauseRobot();
                    break;
                default:
                case RobotState.Pausing:
                case RobotState.StartingUp:
                case RobotState.ThreadTerminated:
                    ret = false;
                    break;
            }
            return ret;
        }
    }
}
