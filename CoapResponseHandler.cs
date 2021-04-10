using System;
using System.Threading.Tasks;

namespace coap.core
{
    public class CoapResponseHandler : IResponseHandler
    {
        public CoapResponseHandler()
        {

        }
        
        public async Task HandleResponse(CoapMessage Request)
        {
            await Task.Run(()=>{});
            Console.WriteLine("I received a response to a message");
        }
    }
}