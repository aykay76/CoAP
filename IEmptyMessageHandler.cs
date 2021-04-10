using System;
using System.Threading.Tasks;

namespace coap.core
{
    public interface IEmptyMessageHandler
    {
        Task HandleEmptyMessage(CoapMessage message);
    }
}
