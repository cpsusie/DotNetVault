using System;

namespace ConsoleStressTest
{
    public interface IStressSimulation : IDisposable
    {
        event EventHandler<StressSimDoneEventArgs> Done;
        int ActionsPerThread { get; }
        int NumberOfThreads { get; }
        StressSimCode Status { get; }
        bool IsDisposed { get; }
        TimeSpan Duration { get; }
        Exception FaultingException { get; }
        void StartSimulation();
    }
}