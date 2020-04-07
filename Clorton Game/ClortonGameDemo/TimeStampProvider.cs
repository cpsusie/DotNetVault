using System;
using DotNetVault.ClortonGame;

namespace ClortonGameDemo
{
    sealed class HpTimeStampProvider : TimeStampProvider
    {
        public static HpTimeStampProvider CreateInstance() 
            => new HpTimeStampProvider();

        public override DateTime Now => HpTimesStamps.TimeStampSource.Now;
        public override void Calibrate() =>
            HpTimesStamps.TimeStampSource.Calibrate();

        private HpTimeStampProvider() { }
    }
}
