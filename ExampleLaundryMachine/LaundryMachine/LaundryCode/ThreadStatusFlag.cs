using System.Threading;

namespace LaundryMachine.LaundryCode
{
    internal struct  ThreadStatusFlag
    {
        public ThreadStatusFlagCode Code
        {
            get
            {
                int current = _currentState;
                return (ThreadStatusFlagCode) current;
            }
        }

        public bool TrySetInstantiated()
        {
            const int wantsToBe = (int) ThreadStatusFlagCode.Instantiated;
            const int needsToBeNow = (int) ThreadStatusFlagCode.Nil;
            return Interlocked.CompareExchange(ref _currentState, wantsToBe, needsToBeNow) == needsToBeNow;
        }

        public bool TrySetRequestedThreadStart()
        {
            const int wantsToBe = (int)ThreadStatusFlagCode.RequestedThreadStart;
            const int needsToBeNow = (int)ThreadStatusFlagCode.Instantiated;
            return Interlocked.CompareExchange(ref _currentState, wantsToBe, needsToBeNow) == needsToBeNow;
        }

        public bool TrySetThreadStarted()
        {
            const int wantsToBe = (int)ThreadStatusFlagCode.ThreadStarted;
            const int needsToBeNow = (int)ThreadStatusFlagCode.RequestedThreadStart;
            return Interlocked.CompareExchange(ref _currentState, wantsToBe, needsToBeNow) == needsToBeNow;
        }

        public bool TrySetThreadTerminated()
        {
            const int wantsToBe = (int)ThreadStatusFlagCode.ThreadTerminated;
            const int needsToBeNow = (int)ThreadStatusFlagCode.ThreadStarted;
            return Interlocked.CompareExchange(ref _currentState, wantsToBe, needsToBeNow) == needsToBeNow;
        }

        public void ForceTerminate()
        {
            const int wantsToBe = (int) ThreadStatusFlagCode.ThreadTerminated;
            Interlocked.Exchange(ref _currentState, wantsToBe);
        }


        private volatile int _currentState;
    }
}