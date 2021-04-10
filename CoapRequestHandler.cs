using System;
using System.Threading.Tasks;

namespace coap.core
{
    public class CoapRequestHandler : IRequestHandler
    {
        public CoapRequestHandler()
        {

        }
        
        public async Task HandleRequest(CoapMessage Request)
        {
            await Task.Run(()=>{});
            Console.WriteLine("I'll do something at some point");
        }
    }
}