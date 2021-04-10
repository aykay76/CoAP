using System;
using System.Threading.Tasks;

namespace coap.core
{
    public interface IResponseHandler
    {
        Task HandleResponse(CoapMessage message);
    }
}
