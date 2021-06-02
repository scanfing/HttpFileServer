using System.Net;
using System.Web;
using HttpFileServer.Infrastructure;

namespace HttpFileServer.Models
{
    public class RequestModel : BindableBase
    {
        #region Fields

        private string _status;

        #endregion Fields

        #region Constructors

        public RequestModel(string url, EndPoint ep, string method)
        {
            RequestUrl = url;
            EndPoint = ep;
            HttpMethod = method;
        }

        #endregion Constructors

        #region Properties

        public EndPoint EndPoint
        {
            get;
        }

        public string HttpMethod { get; }

        public string RequestUrl
        {
            get;
        }

        public string Status
        {
            get => _status;
            set => SetProperty(ref _status, value);
        }

        #endregion Properties

        #region Methods

        public override string ToString()
        {
            return $"{EndPoint} {HttpUtility.UrlDecode(RequestUrl)}";
        }

        #endregion Methods
    }
}