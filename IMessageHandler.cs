using System;
using System.Threading.Tasks;

namespace coap.core
{
    public interface IMessageHandler
    {
        Task HandleMessage(CoapMessage message);
    }
}
