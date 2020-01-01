using System;

namespace LaundryMachine.LaundryCode
{
    public class StateLogicErrorException : Exception
    {
        public StateLogicErrorException()
        {
        }

        public StateLogicErrorException(string message) : base(message)
        {
        }

        public StateLogicErrorException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}