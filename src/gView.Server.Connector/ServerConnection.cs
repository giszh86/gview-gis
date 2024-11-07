using gView.Framework.Core.Common;
using gView.Framework.Web;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Xml;


// wsdl /namespace:gView.MapServer.Connector /protocol:soap /out:MapServerProxy.cs /language:cs http://localhost:8001/MapServer?wsdl
namespace gView.Server.Connector
{
    public class ServerConnection : IErrorMessage
    {
        private string _url = String.Empty;

        public ServerConnection(string url)
        {
            _url = url;
            if (!_url.ToLower().StartsWith("http://") && !_url.ToLower().StartsWith("https://"))
            {
                _url = "http://" + _url;
            }

            this.Timeout = 0;
        }

        public int Timeout { get; set; }
        async public Task<string> SendAsync(string service, string request, string InterpreterGUID, string user, string pwd)
        {
            string ret = await WebFunctions.HttpSendRequestAsync(_url + "/MapRequest/" + InterpreterGUID + "/" + service, "POST",
                Encoding.UTF8.GetBytes(request), user, pwd, this.Timeout * 1000);

            return Encoding.UTF8.GetString(Encoding.Default.GetBytes(ret));
        }

        public string LastErrorMessage { get; set; }


        public void Dispose()
        {
        }
    }
}
