namespace coap.core
{
    public enum CoapMessageCode {
        Empty = 0, 
        
        // request method codes
        GET = 1, POST = 2, PUT = 3, DELETE = 4,

        // response codes - success
        Created = 65, Deleted = 66, Valid = 67, Changed = 68, Content = 69,

        // response codes - client error
        BadRequest = 128, Unauthorised = 129, BadOption = 130, Forbidden = 131, NotFound = 132, MethodNotAllowed = 133, NotAcceptable = 134, PreconditionFailed = 140, RequestEntityTooLarge = 141, UnsupportedContentFormat = 143,

        // response codes - server error
        InternalServerError = 160, NotImplemented = 161, BadGateway = 162, ServiceUnavailable = 163, GatewayTimeout = 164, ProxyingNotSupported = 165
    }
}