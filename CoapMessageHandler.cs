using System;
using System.Threading.Tasks;

namespace coap.core
{
    public class CoapMessageHandler : IMessageHandler
    {
        public CoapMessageHandler()
        {

        }
        
        public async Task HandleMessage(CoapMessage message)
        {
            await Task.Run(()=>{});
            Console.WriteLine("I'll do something at some point");
        }
    }
}