namespace LaundryMachine.LaundryCode
{
    internal enum ThreadStatusFlagCode
    {
        Nil =0,
        Instantiated = 1,
        RequestedThreadStart = 2,
        ThreadStarted,
        ThreadTerminated,
    }
}