using System;

namespace coap.core
{
    public class CoapRequest : CoapMessage
    {
        public CoapRequest()
        {
            
        }

        // Construct a request message without payload
        public CoapRequest(CoapMessageType type, CoapMessageCode code, Uri uri)
        {
            Type = type;
            Uri = uri;
            Code = code;
        }

        // Construct a request message with payload
        public CoapRequest(CoapMessageType type, CoapMessageCode code, Uri uri, byte[] payload, ContentFormat format) : this(type, code, uri)
        {
            Payload = payload;
        }
    }
}