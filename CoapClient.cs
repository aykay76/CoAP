using System.Threading.Tasks;

namespace coap.core
{
    public class CoapClient : IResponseHandler, IRequestHandler
    {
        public Task HandleRequest(CoapMessage message)
        {
            throw new System.NotImplementedException();
        }

        public Task HandleResponse(CoapMessage response)
        {
            throw new System.NotImplementedException();
        }
    }
}