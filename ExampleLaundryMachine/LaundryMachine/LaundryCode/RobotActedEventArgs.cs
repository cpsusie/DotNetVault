using System;
using JetBrains.Annotations;

namespace LaundryMachine.LaundryCode
{
    public sealed class RobotActedEventArgs : EventArgs
    {
        public DateTime TimeStamp { get; }
        [NotNull] public string RobotName { get; }
        public Guid RobotId { get; }
        [NotNull] public string ActionDescription { get; }

        public RobotActedEventArgs(DateTime ts, [NotNull] string actionDescription, [NotNull] ILaundryRobot robot)
        {
            TimeStamp = ts;
            RobotName = (robot ?? throw new ArgumentNullException(nameof(robot))).RobotName;
            RobotId = robot.RobotId;
            ActionDescription = actionDescription ?? throw new ArgumentNullException(nameof(actionDescription));
            _stringRep = new LocklessLazyWriteOnce<string>(GetStringRep);
        }
        public override string ToString() => _stringRep;
       
        private string GetStringRep() =>
            $"At [{TimeStampSource.Now:O}], Laundry robot \"{RobotName}\" (id: {RobotId}) performed the following action: [{ActionDescription}].";

        
        [NotNull] private readonly LocklessLazyWriteOnce<string> _stringRep;
    }
}