using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using TaskTypeBacker = System.Int32;
using TaskResultBacker = System.Int32;

namespace LaundryMachine.LaundryCode
{
    
    public enum LaundryMachineStateCode
    {
        PoweredDown=0,
        Empty,
        Full,
        Activating,
        Washing,
        Drying,
        Error
    }

    public enum StateMachineStateType
    {
        Error = 0,
        Terminal,
        WaitForMoreInputOnly,
        WaitForTaskComplete
    }

    // ReSharper disable once EnumUnderlyingTypeIsInt
    public enum TaskType : TaskTypeBacker
    {
        NullTask=0,
        ActivateTask, // on powerup and error correction
        WashTask,
        DryTask,
    }

    [SuppressMessage("ReSharper", "EnumUnderlyingTypeIsInt")]
    public enum TaskResultCode : TaskResultBacker
    {
        ErrorUnknownResult = -1,
        NotStartedResult = 0,
        StillPendingResult,
        CancelledResult,
        FailedResult,
        SuccessResult
    }

    public static class LaundryMachineStateCodeExtensions
    {
        public static TaskType GetStateTaskType(this LaundryMachineStateCode code)
        {
            TaskType ret;
            switch (code)
            {
                case LaundryMachineStateCode.PoweredDown:
                    ret = TaskType.NullTask;
                    break;
                case LaundryMachineStateCode.Empty:
                    ret = TaskType.NullTask;
                    break;
                case LaundryMachineStateCode.Full:
                    ret = TaskType.NullTask;
                    break;
                case LaundryMachineStateCode.Activating:
                    ret = TaskType.ActivateTask;
                    break;
                case LaundryMachineStateCode.Washing:
                    ret = TaskType.WashTask;
                    break;
                case LaundryMachineStateCode.Drying:
                    ret = TaskType.DryTask;
                    break;
                default:
                case LaundryMachineStateCode.Error:
                    ret = TaskType.NullTask;
                    break;
            }
            return ret;
        }

        public static StateMachineStateType GetStateType(this LaundryMachineStateCode code)
        {
            switch (code)
            {
                case LaundryMachineStateCode.PoweredDown:
                    return StateMachineStateType.Terminal;
                case LaundryMachineStateCode.Empty:
                    return StateMachineStateType.WaitForMoreInputOnly;
                case LaundryMachineStateCode.Full:
                    return StateMachineStateType.WaitForMoreInputOnly;
                case LaundryMachineStateCode.Activating:
                    return StateMachineStateType.WaitForTaskComplete;
                case LaundryMachineStateCode.Washing:
                    return StateMachineStateType.WaitForTaskComplete;
                case LaundryMachineStateCode.Drying:
                    return StateMachineStateType.WaitForTaskComplete;
                default:
                case LaundryMachineStateCode.Error:
                    return StateMachineStateType.Error;
            }
        }

        public static bool IsUndefinedOrErrorStateCode(this LaundryMachineStateCode code) =>
            code.ValueOrErrorIfNDef() == LaundryMachineStateCode.Error;
        public static bool IsTaskBasedStateCode(this LaundryMachineStateCode code) =>
            TheTaskBasedStateCodes.Contains(code);
        public static bool IsInputOnlyStateCode(this LaundryMachineStateCode code) =>
            TheWaitInputOnlyTaskCodes.Contains(code);
        public static LaundryMachineStateCode ValueOrThrowIfNDef(this LaundryMachineStateCode code) =>
            code.IsStateDefined()
                ? code
                : throw new InvalidEnumArgumentException(nameof(code), (int) code, typeof(LaundryMachineStateCode));
        public static LaundryMachineStateCode ValueOrErrorIfNDef(this LaundryMachineStateCode code) =>
            code.IsStateDefined() ? code : LaundryMachineStateCode.Error;
        public static bool IsStateDefined(this LaundryMachineStateCode state) => TheDefinedStateCodes.Contains(state);
        
        private static readonly ReadOnlyFlatEnumSet<LaundryMachineStateCode> TheWaitInputOnlyTaskCodes =
            InitInputOnlyStateCodes();
        private static ReadOnlyFlatEnumSet<LaundryMachineStateCode> InitInputOnlyStateCodes()
            => new ReadOnlyFlatEnumSet<LaundryMachineStateCode>(new []{LaundryMachineStateCode.Empty, LaundryMachineStateCode.Full});

        private static readonly ReadOnlyFlatEnumSet<LaundryMachineStateCode> TheTaskBasedStateCodes =
            InitTaskBasedStateCodes();

        private static ReadOnlyFlatEnumSet<LaundryMachineStateCode> InitTaskBasedStateCodes()
            => new ReadOnlyFlatEnumSet<LaundryMachineStateCode>(new []{LaundryMachineStateCode.Activating, LaundryMachineStateCode.Drying, LaundryMachineStateCode.Washing });

        private static readonly ReadOnlyFlatEnumSet<LaundryMachineStateCode> TheDefinedStateCodes =
            InitDefinedStateCodes();

        private static ReadOnlyFlatEnumSet<LaundryMachineStateCode> InitDefinedStateCodes()
        
            => new ReadOnlyFlatEnumSet<LaundryMachineStateCode>(Enum.GetValues(typeof(LaundryMachineStateCode))
                .Cast<LaundryMachineStateCode>());
        
    }


}
