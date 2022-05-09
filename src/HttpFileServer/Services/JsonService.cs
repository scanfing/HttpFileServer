using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading.Tasks;

namespace HttpFileServer.Services
{
    public class JsonService
    {        /// <summary>
        #region Methods

        /// 反序列化
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="value"></param>
        /// <returns></returns>
        public T DeserializeObject<T>(string value)
        {
            var serializer = new DataContractJsonSerializer(typeof(T));
            try
            {
                var buff = Encoding.UTF8.GetBytes(value);
                var memStream = new MemoryStream(buff);

                object obj = serializer.ReadObject(memStream);
                return (T)obj;
            }
            catch (Exception)
            {
                return default;
            }
        }

        /// <summary>
        /// 序列化
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public string SerializeObject<T>(T value)
        {
            DataContractJsonSerializer jsonSerializer = new DataContractJsonSerializer(typeof(T));
            var stream = new MemoryStream();
            jsonSerializer.WriteObject(stream, value);
            var buff = stream.GetBuffer();
            var content = Encoding.UTF8.GetString(buff, 0, buff.Length);
            return content;
        }

        #endregion Methods
    }
}