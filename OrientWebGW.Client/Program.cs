using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Threading;
using System.Net.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net;

using Microsoft.AspNet.SignalR.Client;

using System.Runtime.InteropServices;

using System.Configuration;
using System.Net.Http.Headers;
using System.Reflection;
using System.IO;
using System.Xml;

namespace OrientWebGW.Client
{
    class Program
    {

        static string PublickKey = "";
        static string SecretCode = "";

        static void Main(string[] args)
        {
            //Thread.Sleep(15000);
            List<optionDataFormat> myData = new List<optionDataFormat>();
            Dictionary<string, List<optionDataFormat>> optionData = new Dictionary<string, List<optionDataFormat>>();
            int total = 0;
            int today = Convert.ToInt32(DateTime.Now.ToString("yyyyMMdd"));
            OptionInformation myOption = new OptionInformation(today);
            int[] optionList = myOption.GetOptionNameByDate(today);
            string time = "";
            int recordStart = 0;
            int recordEnd = 0;
            string optionString = "";
            for (int i = 0; i < optionList.Length; i++)
            {
                optionString += optionList[i].ToString() + ".SH,";
            }
            int thisMonth = Convert.ToInt32(DateTime.Now.ToString("yyMM"));
            for (int i = 0; i <=9; i++)
            {
                string month= DateTime.Now.AddMonths(i).ToString("yyMM");
                optionString += "IH" + month + ".CF,";
            }
            optionString += "510050.SH";

            bool bRet = SetConsoleCtrlHandler(commandLineDelegate, true);

            var baseAddress = ConfigurationManager.AppSettings["OrientWebGWAddress"];

            var userName = ConfigurationManager.AppSettings["UserName"];
            var userPassword = ConfigurationManager.AppSettings["UserPassword"];

            GetPublicKey().Wait();

            var accessToken = GetAccessToken(userName, userPassword).Result;

            Console.WriteLine("GetToken:{0}", accessToken);

            if (string.IsNullOrEmpty(accessToken))
            {
                Console.WriteLine("用户名密码错误");
                Console.ReadLine();
                return;
            }


            using (var hubConnection = new HubConnection(baseAddress))
            {
                hubConnection.Headers.Add("Authorization", "Bearer " + accessToken);

                IHubProxy proxy = hubConnection.CreateHubProxy("DataCenterHub");

                proxy.On<dynamic, string>("onPushDynamicData", (data, stkcd) =>
                {
                    //DateTime currentTime = System.DateTime.Now;
                    // Console.WriteLine(currentTime+currentTime.Millisecond.ToString());
                    optionDataFormat data0 = new optionDataFormat();
                    data0.code = data["c"];
                    data0.ask = new int[5];
                    data0.askv = new int[5];
                    data0.bid = new int[5];
                    data0.bidv = new int[5];
                    for (int i = 0; i < 5; i++)
                    {
                        data0.ask[i] = data["a"][i];
                        data0.askv[i] = data["av"][i];
                        data0.bid[i] = data["b"][i];
                        data0.bidv[i] = data["bv"][i];
                    }
                    data0.high = data["h"];
                    data0.low = data["l"];
                    data0.last = data["p"];
                    data0.openInterest = data["oi"];
                    data0.status = data["s"];
                    data0.time = data["t"];
                    data0.turnover = data["tt"];
                    data0.volume = data["deal"]["v"];
                    data0.count = data["deal"]["c"];
                    //if (optionData.ContainsKey(data0.code) == false)
                    //{
                    //    List<optionDataFormat> myData0 = new List<optionDataFormat>();
                    //    myData0.Add(data0);
                    //    optionData.Add(data0.code, myData0);
                    //}
                    //else
                    //{
                    //    optionData[data0.code].Add(data0);
                    //}
                    myData.Add(data0);
                    total += 1;
                });

                proxy.On("onPushStaticData", data =>
                {
                    Console.WriteLine(data["c"] + " " + data["pc"]);

                });

                proxy.On("onPushBasisData", data =>
                {
                    Console.WriteLine(data["c"] + " " + data["cn"]);
                });

                proxy.On("onPushOrderQueueData", data =>
                {
                    Console.WriteLine(data["c"] + " " + data["t"] + " " + data["a"].ToString());
                });


                DateTime firstStamp = DateTime.MinValue;
                DateTime firstNow = DateTime.MinValue;

                proxy.On("onPushTickData", data =>
                {

                });


                proxy.On("OnNotify", data =>
               {
                   Console.WriteLine(data["c"] + " " + data["cn"]);
               });

                proxy.On("OnRequest", data =>
                {
                    Console.WriteLine(data);
                });

                hubConnection.Start().ContinueWith(c =>
                {
                    if (c.IsFaulted)
                    {
                        //连接失败，暂时无法在客户端区分错误类别，只能识别出500内部错误                        
                    }
                    else
                    {

                        proxy.Invoke("RequestSubscribe", optionString);

                        //proxy.Invoke("RequestSubscribe", "002341.SZ", "OQ,TI,SO");

                        //proxy.Invoke("RequestGetBasisData", "11000021.SH");

                        //proxy.Invoke("RequestGetStaticData", "11000021.SH");

                        //proxy.Invoke("RequestGetSecurityList", "1100");

                        //proxy.Invoke("RequestGetMinuteData", "11000021.SH");
                    }
                });

                while (true)
                {
                    Thread.Sleep(3 * 1000);
                    time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    Console.WriteLine(total);
                    recordEnd = total - 1;
                    if (recordEnd > recordStart)
                    {
                        StoreData store = new StoreData(myData, recordStart, recordEnd);
                        Console.WriteLine("Time: {2}, record data! form {0} to {1}", recordStart, recordEnd, time);
                    }
                    recordStart = recordEnd + 1;
                }
              }
        }



        static async Task GetCodeTable()
        {

            var url = ConfigurationManager.AppSettings["OrientWebGWAddress"] + "/api/OrientWebAPI/GetCodeTable";

            using (var httpClient = new HttpClient())
            {
                var response = await httpClient.GetAsync(url);

                var result = response.Content.ReadAsStringAsync();
            }
        }

        static async Task<string> GetAccessToken(string userName, string userPassword)
        {
            using (var _httpClient = new HttpClient())
            {

                var publicKey = PublickKey;

                var clientId = EncryptHelper.RSAEncrypt(publicKey, "v1.0.3");                            //需加密
                var clientSecret = SecretCode;

                _httpClient.BaseAddress = new Uri(ConfigurationManager.AppSettings["OrientWebGWAddress"].ToString());

                var parameters = new Dictionary<string, string>();
                parameters.Add("grant_type", "password");
                parameters.Add("username", EncryptHelper.RSAEncrypt(publicKey, userName));         //需加密
                parameters.Add("password", EncryptHelper.RSAEncrypt(publicKey, userPassword));     //需加密

                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                    "Basic",
                    Convert.ToBase64String(Encoding.ASCII.GetBytes(clientId + ":" + clientSecret))
                    );

                var response = await _httpClient.PostAsync("OAuth/Token", new FormUrlEncodedContent(parameters));
                var responseValue = await response.Content.ReadAsStringAsync();
                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    var jsonO = JObject.Parse(responseValue);

                    return jsonO["access_token"].Value<string>();
                }
            }

            return "";
        }

        public static async Task GetPublicKey()
        {

            var url = ConfigurationManager.AppSettings["OrientWebGWAddress"] + "/api/OrientWebAPI/GetSecretKey";

            using (var httpClient = new HttpClient())
            {
                var response = await httpClient.GetAsync(url);

                var result = await response.Content.ReadAsStringAsync();

                var jsonO = JObject.Parse(result);

                PublickKey = jsonO["PublicKey"].Value<string>();
                SecretCode = jsonO["SecretCode"].Value<string>();

                Console.WriteLine(result);
            }
        }


        static async Task ChangePassword()
        {
            var url = ConfigurationManager.AppSettings["OrientWebGWAddress"] + "/api/OrientWebAPI/ChangePassword";

            var publicKey = PublickKey;


            using (var httpClient = new HttpClient())
            {
                var parameters = new Dictionary<string, string>();
                parameters.Add("UserName", EncryptHelper.RSAEncrypt(publicKey, "stress"));
                parameters.Add("OldPassword", EncryptHelper.RSAEncrypt(publicKey, "54321"));
                parameters.Add("NewPassword", EncryptHelper.RSAEncrypt(publicKey, "54321"));
                parameters.Add("ConfirmPassword", EncryptHelper.RSAEncrypt(publicKey, "54321"));
                parameters.Add("ClientID", EncryptHelper.RSAEncrypt(publicKey, "v1.0.2"));

                var response = await httpClient.PostAsync(url, new FormUrlEncodedContent(parameters));

                var result = await response.Content.ReadAsStringAsync();

                Console.WriteLine(result);
            }
        }


        #region CommandLine Control
        public delegate bool ControlCtrlDelegate(int CtrlType);

        [DllImport("kernel32.dll")]
        private static extern bool SetConsoleCtrlHandler(ControlCtrlDelegate HandlerRoutine, bool Add);

        static ControlCtrlDelegate commandLineDelegate = new ControlCtrlDelegate(HandlerRoutine);

        public static bool HandlerRoutine(int CtrlType)
        {
            switch (CtrlType)
            {
                case 0:
                    //ctrl + C 退出
                    CloseConsole();
                    break;
                case 2:
                    //右上角大叉退出
                    CloseConsole();
                    break;
            }
            return false;
        }

        static void CloseConsole()
        {

            Console.WriteLine("exiting...");

            Environment.Exit(0);
        }

        #endregion

    }
}
