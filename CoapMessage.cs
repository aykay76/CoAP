using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;

namespace coap.core
{
    public class CoapMessage
    {
        // basic message format fields
        public byte Version { get; set; }
        public CoapMessageType Type { get; set; }
        public byte TokenLength { get; set; }
        public byte[] TokenValue { get; set; }
        public CoapMessageCode Code { get; set; }
        public UInt16 MessageID { get; set; }
        public SortedDictionary<CoapOptionNumber, List<CoapOption>> Options { get; set; }
        public byte[] Payload { get; set; }

        // IsRequest, IsEmpty, IsResponse
        public bool IsRequest()
        {
            if (Code == CoapMessageCode.GET || Code == CoapMessageCode.POST || Code == CoapMessageCode.PUT || Code == CoapMessageCode.DELETE)
            {
                return true;
            }

            return false;
        }

        public bool IsResponse()
        {
            if (IsEmpty()) return false;
            if (IsRequest()) return false;

            return false;
        }

        public bool IsEmpty()
        {
            if (Code == CoapMessageCode.Empty)
            {
                return true;
            }

            return false;
        }

        // the other endpoint where this message is going or came from
        public IPEndPoint Source { get; set; }
        public IPEndPoint Destination { get; set; }
        public Uri Uri { get; set; }

        // additional fields for request/response handling
        public DateTime LastSent { get; set; }
        public int SendAttempts { get; set; }

        // empty constructor for deserialising messages
        public CoapMessage()
        {
            Version = 1;
        }

        // useful constructor for serialising messages
        public CoapMessage(CoapMessageType type, CoapMessageCode code) : this()
        {
            Type = type;
            Code = code;
        }

        // construct message with token
        public CoapMessage(CoapMessageType type, byte[] token, CoapMessageCode code) : this(type, code)
        {
            if (token.Length > 8)
            {
                throw new CoapMessageFormatException("Token length must be 0-8 characters");
            }

            TokenValue = token;
        }

        // construct request with url
        public CoapMessage(CoapMessageType type, CoapMessageCode code, Uri url) : this(type, code)
        {
            Uri = url;
        }

        // construct request with url and token
        public CoapMessage(CoapMessageType type, byte[] token, CoapMessageCode code, Uri url) : this(type, token, code)
        {
            Uri = url;
        }

        // construct request with url, token and payload
        public CoapMessage(CoapMessageType type, byte[] token, CoapMessageCode code, Uri url, byte[] payload) : this(type, token, code, url)
        {
            Payload = payload;
        }

        public static CoapMessage FromByteArray(byte[] bytes, IPEndPoint remoteEndpoint)
        {
            int pos = 0;

            if (bytes.Length < 4)
            {
                throw new CoapMessageFormatException("Not enough data to form a valid header");
            }

            // header processing
            CoapMessage message = new CoapMessage();

            // need to do this now because we might need the endpoint and port for the url
            message.Source = remoteEndpoint;

            byte b = bytes[pos++];
            message.Version = (byte)((b & 0xc0) >> 6);
            message.Type = (CoapMessageType)((b & 0x30000000) >> 4);
            message.TokenLength = (byte)((b & 0x0f000000));

            if (message.TokenLength > 8)
            {
                throw new CoapMessageFormatException("Token length must be 0-8 bytes in length.");
            }

            b = bytes[pos++];
            message.Code = (CoapMessageCode)b;

            b = bytes[pos++];
            message.MessageID = (UInt16)(b << 8);
            b = bytes[pos++];
            message.MessageID |= b;

            if (message.TokenLength > 0)
            {
                message.TokenValue = new byte[message.TokenLength];
                Array.Copy(bytes, pos, message.TokenValue, 0, message.TokenLength);
                pos += message.TokenLength;
            }

            bool processingOptions = true;
            int optionNumber = 0;
            int optionLength = 0;

            while (pos < bytes.Length && processingOptions)
            {
                if (bytes[pos] == 0xff)
                {
                    pos++;
                    processingOptions = false;

                    // process payload
                    int payloadLength = bytes.Length - pos;
                    if (payloadLength == 0)
                    {
                        throw new CoapMessageFormatException("Payload marker cannot be followed by no payload");
                    }

                    if (payloadLength > 0)
                    {
                        message.Payload = new byte[payloadLength];
                        Array.Copy(bytes, pos, message.Payload, 0, payloadLength);
                    }
                }

                if (processingOptions)
                {
                    int delta = (char)((bytes[pos] & 0xf0) >> 4);
                    optionLength = (char)(bytes[pos] & 0x0f);
                    pos++;

                    switch (delta)
                    {
                        case 13:
                            {
                                byte extendedDelta = bytes[pos++];
                                optionNumber += (extendedDelta + 13);
                            }
                            break;
                        case 14:
                            {
                                UInt16 extendedDelta = (UInt16)bytes[pos++];
                                extendedDelta <<= 8;
                                extendedDelta |= bytes[pos++];
                                optionNumber += (extendedDelta + 269);
                            }
                            break;
                        case 15:
                            if (bytes[pos] == 0xff)
                            {
                                processingOptions = false;
                            }
                            else
                            {
                                throw new CoapMessageFormatException("Malformed payload marker or invalid option");
                            }
                            break;
                        default:
                            optionNumber += delta;
                            break;
                    }

                    switch (optionLength)
                    {
                        case 13:
                            {
                                byte extendedLength = bytes[pos++];
                                optionLength = (extendedLength + 13);
                            }
                            break;
                        case 14:
                            {
                                UInt16 extendedLength = (UInt16)bytes[pos++];
                                extendedLength <<= 8;
                                extendedLength |= bytes[pos++];
                                optionLength = (extendedLength + 269);
                            }
                            break;
                        case 15:
                            {

                            }
                            break;
                    }

                    // TODO: based on table in 5.10 process options according to format and store in subclasses according to type
                    // switch (optionNumber)
                    // {
                    //     case 1:
                    //         { // If-Match - opaque - 0-8
                    //             byte[] value = new byte[optionLength];
                    //             Array.Copy(bytes, pos, value, 0, optionLength);
                    //         }
                    //         break;
                    //     case 3:
                    //         { // Uri-Host - string - 1-255
                    //             byte[] value = new byte[optionLength];
                    //             Array.Copy(bytes, pos, value, 0, optionLength);
                    //         }
                    //         break;
                    //     case 4:
                    //         { // ETag - opaque - 1-8
                    //             byte[] value = new byte[optionLength];
                    //             Array.Copy(bytes, pos, value, 0, optionLength);
                    //         }
                    //         break;
                    //     case 5:
                    //         { // If-None-Match - empty - 0
                    //             byte[] value = new byte[optionLength];
                    //             Array.Copy(bytes, pos, value, 0, optionLength);
                    //         }
                    //         break;
                    //     case 7:
                    //         { // Uri-Port - uint - 0-2
                    //             byte[] value = new byte[optionLength];
                    //             Array.Copy(bytes, pos, value, 0, optionLength);
                    //         }
                    //         break;
                    //     case 8:
                    //         { // Location-Path - string - 0-255
                    //             byte[] value = new byte[optionLength];
                    //             Array.Copy(bytes, pos, value, 0, optionLength);
                    //         }
                    //         break;
                    //     case 11:
                    //         { // Uri-Path - string - 0-255
                    //             byte[] value = new byte[optionLength];
                    //             Array.Copy(bytes, pos, value, 0, optionLength);
                    //         }
                    //         break;
                    //     case 12:
                    //         { // Content-Format - uint - 0-2
                    //             byte[] value = new byte[optionLength];
                    //             Array.Copy(bytes, pos, value, 0, optionLength);
                    //         }
                    //         break;
                    //     case 14:
                    //         { // Max-Age - uint - 0-4
                    //             byte[] value = new byte[optionLength];
                    //             Array.Copy(bytes, pos, value, 0, optionLength);
                    //         }
                    //         break;
                    //     case 15:
                    //         { // Uri-Query - string - 0-255
                    //             byte[] value = new byte[optionLength];
                    //             Array.Copy(bytes, pos, value, 0, optionLength);
                    //         }
                    //         break;
                    //     case 17:
                    //         { // Accept - uint - 0-2
                    //             byte[] value = new byte[optionLength];
                    //             Array.Copy(bytes, pos, value, 0, optionLength);
                    //         }
                    //         break;
                    //     case 20:
                    //         { // Location-Query - string - 0-255
                    //             byte[] value = new byte[optionLength];
                    //             Array.Copy(bytes, pos, value, 0, optionLength);
                    //         }
                    //         break;
                    //     case 35:
                    //         { // Proxy-Uri - string - 1-1034
                    //             byte[] value = new byte[optionLength];
                    //             Array.Copy(bytes, pos, value, 0, optionLength);
                    //         }
                    //         break;
                    //     case 39:
                    //         { // Proxy-Scheme - string - 1-255
                    //             byte[] value = new byte[optionLength];
                    //             Array.Copy(bytes, pos, value, 0, optionLength);
                    //         }
                    //         break;
                    //     case 60:
                    //         { // Size1 - uint - 0-4
                    //             byte[] value = new byte[optionLength];
                    //             Array.Copy(bytes, pos, value, 0, optionLength);
                    //         }
                    //         break;
                    // }

                    byte[] value = new byte[optionLength];
                    Array.Copy(bytes, pos, value, 0, optionLength);
                    pos += optionLength;

                    // add value as option
                    if (message.Options == null)
                    {
                        message.Options = new SortedDictionary<CoapOptionNumber, List<CoapOption>>();
                    }

                    if (!message.Options.ContainsKey((CoapOptionNumber)optionNumber))
                    {
                        message.Options[(CoapOptionNumber)optionNumber] = new List<CoapOption>();
                    }

                    CoapOption option = new CoapOption();
                    option.Number = optionNumber;
                    option.Length = optionLength;
                    option.Value = value;

                    message.Options[(CoapOptionNumber)optionNumber].Add(option);
                }
            }

            // construct URL for easier processing by consumers
            message.Uri = message.ComposeUri();

            return message;
        }

        // TODO: ToByteArray
        public byte[] ToByteArray()
        {
            MemoryStream ms = new MemoryStream();

            // TODO: compose and write header
            byte headerByte = (byte)((byte)Version << 6 | (byte)Type << 4 | (byte)TokenLength);
            ms.WriteByte(headerByte);

            ms.WriteByte((byte)Code);

            ms.WriteByte((byte)(MessageID >> 8));
            ms.WriteByte((byte)(MessageID & 0xff));

            if (TokenValue != null)
            {
                ms.Write(TokenValue, 0, TokenValue.Length);
            }

            // write the options out
            int lastNumber = 0;
            if (Options != null)
            {
                foreach (CoapOptionNumber number in Options.Keys)
                {
                    foreach (CoapOption option in Options[number])
                    {
                        int delta = (int)number - lastNumber;
                        byte optionHeader = 0;
                        byte optionDelta = 0;
                        byte optionLength = 0;

                        if (delta <= 12)
                        {
                            optionDelta = (byte)(delta);
                        }
                        else if (delta <= 255 + 13)
                        {
                            optionDelta = (byte)(13);
                        }
                        else if (delta <= 65535 + 269)
                        {
                            optionDelta = (byte)(14);
                        }

                        if (option.Length <= 12)
                        {
                            optionLength = (byte)option.Length;
                        }
                        else if (option.Length <= 255 + 13)
                        {
                            optionLength = 13;
                        }
                        else if (option.Length <= 65535 + 269)
                        {
                            optionLength = 14;
                        }

                        optionHeader = (byte)((optionDelta << 4) | optionLength);
                        ms.WriteByte(optionHeader);

                        if (optionDelta == 13)
                        {
                            ms.WriteByte((byte)(delta - 13));
                        }
                        else if (optionDelta == 14)
                        {
                            int val = optionDelta - 69;
                            ms.WriteByte((byte)(val >> 8));
                            ms.WriteByte((byte)(val & 0xff));
                        }

                        // write extended option length field (0 - 2 bytes)
                        if (optionLength == 13)
                        {
                            ms.WriteByte((byte)(option.Length - 13));
                        }
                        else if (optionLength == 14)
                        {
                            int val = option.Length - 269;
                            ms.WriteByte((byte)(val >> 8));
                            ms.WriteByte((byte)(val & 0xff));
                        }

                        // write the value in raw byte format
                        ms.Write(option.Value, 0, option.Value.Length);

                        lastNumber = (int)number;
                    }
                }
            }

            if (Payload != null)
            {
                ms.WriteByte(0xff);
                ms.Write(Payload, 0, Payload.Length);
            }

            byte[] packet = new byte[ms.Length];
            byte[] buffer = ms.GetBuffer();

            Array.Copy(buffer, 0, packet, 0, ms.Length);

            return packet;
        }

        // decompose URI into internal options
        public void DecomposeUri(Uri uri)
        {
            int start = 0;
            int idx = 0;

            // get the scheme
            string url = uri.ToString();
            idx = url.IndexOf("://", start);
            if (idx == -1) throw new InvalidOperationException("You must specify a scheme in the uri, the uri must be fully qualified");

            string scheme = url.Substring(start, idx - start).ToLower();
            start = idx + 3;
            if (scheme != "coap" && scheme != "coaps")
            {
                throw new InvalidOperationException("You must specify coap or coaps as a scheme");
            }

            // is there a port?
            string hostname = string.Empty;
            int port = 0;
            if (scheme == "coap")
            {
                port = 5683;
            }
            else if (scheme == "coaps")
            {
                port = 5684;
            }

            int colonpos = url.IndexOf(':', start);
            int soliduspos = url.IndexOf('/', start);
            if (colonpos == -1)
            {
                // extract the hostname
                hostname = url.Substring(start, soliduspos - start);
                start = soliduspos + 1;
            }
            else
            {
                // port is specified, extract hostname and port
                hostname = url.Substring(start, colonpos - start);
                string portstring = url.Substring(colonpos + 1, soliduspos - colonpos - 1);
                port = int.Parse(portstring);
                start = soliduspos + 1;
            }

            // check hostname
            // IPAddress remote = null;
            // if (hostname.StartsWith('[') == false && IPAddress.TryParse(hostname, out remote) == false)
            // {
                // add a Uri-Host option
                AddOption(CoapOptionNumber.UriHost, hostname);
            // }

            // check port number
            if (port != 5683 && port != 5684)
            {
                // add a Uri-Port option
                AddOption(CoapOptionNumber.UriPort, port);
            }

            // process the path, looking for optional query
            string path = url.Substring(start);
            string query = string.Empty;
            idx = path.IndexOf('?');
            if (idx != -1)
            {
                query = path.Substring(idx + 1);
                path = path.Substring(0, idx);
            }

            // now parse path and query if they are not empty
            if (string.IsNullOrEmpty(path) == false && path != "/")
            {
                string[] parts = path.Split('/');
                foreach (string part in parts)
                {
                    // add Uri-Path option
                    AddOption(CoapOptionNumber.UriPath, part);
                }
            }

            if (string.IsNullOrEmpty(query) == false)
            {
                string[] parts = query.Split('&');
                foreach (string part in parts)
                {
                    // add Uri-Query option
                    AddOption(CoapOptionNumber.UriQuery, part);
                }
            }
        }

        public void AddOption(CoapOptionNumber type, string value)
        {
            CoapOption option = new CoapOption((int)type, value);
            AddOption(type, option);
        }

        public void AddOption(CoapOptionNumber type, byte[] value)
        {
            CoapOption option = new CoapOption((int)type, value);
            AddOption(type, option);
        }

        public void AddOption(CoapOptionNumber type, int value)
        {
            CoapOption option = new CoapOption((int)type, value);
            AddOption(type, option);
        }

        private void AddOption(CoapOptionNumber number, CoapOption option)
        {
            if (Options == null) 
            {
                Options = new SortedDictionary<CoapOptionNumber, List<CoapOption>>();
            }

            if (Options.ContainsKey(number) == false)
            {
                Options[number] = new List<CoapOption>();
            }

            Options[number].Add(option);
        }

        // compose URI from internal options
        public Uri ComposeUri()
        {
            // TODO: handle coap and coaps
            string uri = "coap://";

            if (Options.ContainsKey(CoapOptionNumber.UriHost))
            {
                uri += Options[CoapOptionNumber.UriHost][0].ToString();
            }
            else
            {
                uri += Destination.Address.ToString();
            }

            if (Options.ContainsKey(CoapOptionNumber.UriPort))
            {
                uri += ":" + Options[CoapOptionNumber.UriPort][0].ToString();
            }

            string resourceName = string.Empty;
            if (Options.ContainsKey(CoapOptionNumber.UriPath))
            {
                foreach (CoapOption pathPart in Options[CoapOptionNumber.UriPath])
                {
                    resourceName += "/" + pathPart.ToString().Replace(":", "%3A").Replace("@", "%40");
                }
            }
            if (string.IsNullOrEmpty(resourceName)) resourceName = "/";

            bool first = true;
            if (Options.ContainsKey(CoapOptionNumber.UriQuery))
            {
                foreach (CoapOption queryPart in Options[CoapOptionNumber.UriQuery])
                {
                    if (first)
                    {
                        resourceName += "?";
                        first = false;
                    }
                    else
                    {
                        resourceName += "&";
                    }

                    resourceName += queryPart.ToString().Replace("&", "%26").Replace(":", "%3A").Replace("@", "%40").Replace("/", "%2F").Replace("?", "%3F");
                }
            }

            uri += resourceName;

            return new Uri(uri);
        }
    }
}