using System;
using System.Text;

namespace coap.core
{
    public class CoapOption
    {
        public int Number { get; set; }
        public int Length { get; set; }
        public byte[] Value { get; set; }

        public CoapOption()
        {
            
        }

        public CoapOption(int number, string value)
        {
            Number = number;
            Length = value.Length;
            Value = Encoding.UTF8.GetBytes(value);
        }

        public CoapOption(int number, byte[] value)
        {
            Number = number;
            Length = value.Length;
            Value = value;
        }

        public CoapOption(int number, int value)
        {
            int len = 0;
            for (int i = 0; i < 4; i++)
            {
                if (value >= 1 << (i * 8) || value < 0)
                    len++;
                else
                    break;
            }

            Length = len;
            Value = new byte[len];
            for (int i = 0; i < len; i++)
            {
                Value[len - i - 1] = (byte)(value >> i * 8);
            }
        }

        public override string ToString()
        {
            switch (Number)
            {
                // case 1:
                //     { // If-Match - opaque - 0-8
                //         byte[] value = new byte[optionLength];
                //         Array.Copy(bytes, pos, value, 0, optionLength);
                //     }
                //     break;
                case 3:
                    { // Uri-Host - string - 1-255
                        return Encoding.UTF8.GetString(Value);
                    }
                // case 4:
                //     { // ETag - opaque - 1-8
                //         byte[] value = new byte[optionLength];
                //         Array.Copy(bytes, pos, value, 0, optionLength);
                //     }
                //     break;
                // case 5:
                //     { // If-None-Match - empty - 0
                //         byte[] value = new byte[optionLength];
                //         Array.Copy(bytes, pos, value, 0, optionLength);
                //     }
                //     break;
                case 7:
                    { // Uri-Port - uint - 0-2
                        int pos = 0;
                        int num = 0;
                        while (pos < Length)
                        {
                            num += Value[pos];
                            pos++;
                            if (pos < Length) num <<= 8;
                        }
                        
                        return num.ToString();
                    }
                case 8:
                    { // Location-Path - string - 0-255
                        return Encoding.UTF8.GetString(Value);
                    }
                case 11:
                    { // Uri-Path - string - 0-255
                        return Encoding.UTF8.GetString(Value);
                    }
                case 12:
                    { // Content-Format - uint - 0-2
                        int pos = 0;
                        int num = 0;
                        while (pos < Length)
                        {
                            num += Value[pos];
                            pos++;
                            if (pos < Length) num <<= 8;
                        }
                        
                        return num.ToString();
                    }
                case 14:
                    { // Max-Age - uint - 0-4
                        int pos = 0;
                        int num = 0;
                        while (pos < Length)
                        {
                            num += Value[pos];
                            pos++;
                            if (pos < Length) num <<= 8;
                        }
                        
                        return num.ToString();
                    }
                case 15:
                    { // Uri-Query - string - 0-255
                        return Encoding.UTF8.GetString(Value);
                    }
                case 17:
                    { // Accept - uint - 0-2
                        int pos = 0;
                        int num = 0;
                        while (pos < Length)
                        {
                            num += Value[pos];
                            pos++;
                            if (pos < Length) num <<= 8;
                        }
                        
                        return num.ToString();
                    }
                case 20:
                    { // Location-Query - string - 0-255
                        return Encoding.UTF8.GetString(Value);
                    }
                case 35:
                    { // Proxy-Uri - string - 1-1034
                        return Encoding.UTF8.GetString(Value);
                    }
                case 39:
                    { // Proxy-Scheme - string - 1-255
                        return Encoding.UTF8.GetString(Value);
                    }
                case 60:
                    { // Size1 - uint - 0-4
                        int pos = 0;
                        int num = 0;
                        while (pos < Length)
                        {
                            num += Value[pos];
                            pos++;
                            if (pos < Length) num <<= 8;
                        }
                        
                        return num.ToString();
                    }
            }
            return string.Empty;
        }
    }
}