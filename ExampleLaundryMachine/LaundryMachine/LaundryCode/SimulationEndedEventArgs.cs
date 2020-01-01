using System;

namespace LaundryMachine.LaundryCode
{
    public sealed class SimulationEndedEventArgs : EventArgs
    {
        public ref readonly LaundrySimulationResults SimulationResults => ref _results;

        public SimulationEndedEventArgs(in LaundrySimulationResults results) => _results = results;

        public override string ToString() => _stringRep ??= GetStringRep();

        private string GetStringRep() =>
            $"[{nameof(SimulationEndedEventArgs)}] -- Simulation ended with following results: " +
            $"{Environment.NewLine} {_results.ToString()}{Environment.NewLine}";

        private string _stringRep;
        private readonly LaundrySimulationResults _results;
    }
}