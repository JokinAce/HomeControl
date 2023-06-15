using System;
using System.Text;

namespace HomeController {
    public static class Obfuscator {
        private static readonly int XORConstant = 0x38;

        private static string ToBase64(this string input) {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(input));
        }

        private static string FromBase64(this string input) {
            return Encoding.UTF8.GetString(Convert.FromBase64String(input));
        }

        private static string XOREncrypt(this string input) {
            byte[] data = Encoding.UTF8.GetBytes(input);

            for (int i = 0; i < data.Length; i++) {
                data[i] = (byte)(data[i] ^ XORConstant);
            }

            return Encoding.UTF8.GetString(data);
        }

        private static string XORDecrypt(this string input) {
            byte[] data = Encoding.UTF8.GetBytes(input);

            for (int i = 0; i < data.Length; i++) {
                data[i] = (byte)(data[i] ^ XORConstant);
            }

            return Encoding.UTF8.GetString(data);
        }


        public static string Encrypt(string input) {
            input = input.ToBase64();
            input += $",{DateTime.UtcNow.ToBinary()}";

            input = input.ToBase64();
            input = input.XOREncrypt(); // Perhaps go to AES

            return input.ToBase64();
        }

        public static Content Decrypt(string input) {
            input = input.FromBase64();
            input = input.XORDecrypt();
            input = input.FromBase64();

            string[] decryptedInput = input.Split(',');

            Content parsedContent = new Content(decryptedInput[0].FromBase64(),
                DateTime.FromBinary(long.Parse(decryptedInput[1])));

            return parsedContent;
        }

        public static bool IsReplay(this Content content) {
            return (DateTime.UtcNow - content.DateTime).Minutes > 2;
        }

        //public record Content(string ContentMessage, DateTime DateTime);

        public readonly struct Content {
            public string ContentMessage { get; }
            public DateTime DateTime { get; }

            public Content(string contentMessage, DateTime dateTime) {
                ContentMessage = contentMessage;
                DateTime = dateTime;
            }
        }
    }
}
