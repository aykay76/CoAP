using System;

namespace coap.core
{
    public class CoapMessageFormatException : Exception
    {
        public string Message { get; set; }

        public CoapMessageFormatException(string message)
        {
            Message = message;
        }
    }
}