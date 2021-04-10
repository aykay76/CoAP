using System;
using System.Threading.Tasks;

namespace coap.core
{
    public interface IRequestHandler
    {
        Task HandleRequest(CoapMessage message);
    }
}
