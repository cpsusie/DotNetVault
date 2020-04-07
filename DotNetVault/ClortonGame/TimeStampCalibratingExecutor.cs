using DotNetVault.TestCaseHelpers;
using JetBrains.Annotations;

namespace DotNetVault.ClortonGame
{
    sealed class TimeStampCalibratingExecutor : Executor
    {
        public TimeStampCalibratingExecutor([NotNull] string namePrefix) : base(namePrefix)
        {
        }

        protected override void StartupActions()
        {
            CgTimeStampSource.Calibrate();
        }
    }
}
