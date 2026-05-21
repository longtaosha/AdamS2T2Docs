using System;
using System.Security.Cryptography;
using System.Text;

namespace AdamS2T2Docs
{
    class XunFeiEncription
    {
        private static string timeStamp;
        private static string baseString;
        private static string baseStringtoMd5;
        private static string signa;
        private static string uri;
        private string appid;
        private string apiKey;

        public XunFeiEncription (string appid, string apiKey)
        {
            this.appid = appid;
            this.apiKey = apiKey; 

        }

        public string getUri()
        {
            timeStamp = getTimeStamp();
            baseString = appid + timeStamp;
            baseStringtoMd5 = toMD5(baseString);
            signa = toHmacSHA1(baseStringtoMd5, apiKey);
       
            string requestUrl = string.Format("wss://rtasr.xfyun.cn/v1/ws?appid={0}&ts={1}&signa={2}&lang=en&pd=tech&roleType=2", appid,
            timeStamp, urlEncode(signa));

            return requestUrl;
        }

        private static string getTimeStamp()
        {
            TimeSpan ts = DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, 0);
            return Convert.ToInt64(ts.TotalSeconds).ToString();
        }


        private static string toMD5(string txt)
        {
            using (MD5 mi = MD5.Create())
            {
                byte[] buffer = Encoding.Default.GetBytes(txt);
                byte[] newBuffer = mi.ComputeHash(buffer);
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < newBuffer.Length; i++)
                {
                    sb.Append(newBuffer[i].ToString("x2"));
                }
                return sb.ToString();
            }
        }

        private static string toHmacSHA1(string baseStringtoMd5, string appkey)
        {
            HMACSHA1 hmacsha1 = new HMACSHA1();
            hmacsha1.Key = Encoding.UTF8.GetBytes(appkey);
            byte[] dataBuffer = Encoding.UTF8.GetBytes(baseStringtoMd5);
            byte[] hashBytes = hmacsha1.ComputeHash(dataBuffer);
            return Convert.ToBase64String(hashBytes);
        }

        private static string urlEncode(string str)
        {
            StringBuilder sb = new StringBuilder();
            byte[] byStr = System.Text.Encoding.UTF8.GetBytes(str);
            for (int i = 0; i < byStr.Length; i++)
            {
                sb.Append(@"%" + Convert.ToString(byStr[i], 16));
            }
            return (sb.ToString());
        }
    }
}
