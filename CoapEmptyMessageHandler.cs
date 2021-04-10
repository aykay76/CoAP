using System;
using System.Threading.Tasks;

namespace coap.core
{
    public class CoapEmptyMessageHandler : IEmptyMessageHandler
    {
        public CoapEmptyMessageHandler()
        {

        }
        
        public async Task HandleEmptyMessage(CoapMessage Request)
        {
            await Task.Run(()=>{});
            Console.WriteLine("I received a response to a message");
        }
    }
}