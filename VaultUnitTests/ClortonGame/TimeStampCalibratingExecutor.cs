using DotNetVault.TestCaseHelpers;
using HpTimesStamps;
using JetBrains.Annotations;

namespace VaultUnitTests.ClortonGame
{
    sealed class TimeStampCalibratingExecutor : Executor
    {
        public TimeStampCalibratingExecutor([NotNull] string namePrefix) : base(namePrefix)
        {
        }

        protected override void StartupActions()
        {
            TimeStampSource.Calibrate();
        }
    }
}
