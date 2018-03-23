using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Text;
using System.Web;
using System.Windows.Forms;

namespace SendTool
{
    public partial class Form1 : Form
    {
        #region Varialbes
        /// <summary>
        /// 请求的URL
        /// </summary>
        private  string URL = string.Empty;
        /// <summary>
        /// 系统唯一标识
        /// </summary>
        private  string AppKey = string.Empty;
        /// <summary>
        /// 加密的盐
        /// </summary>
        private  string AppSecret = string.Empty;
        /// <summary>
        /// 是否启用签章
        /// </summary>
        private bool EnableSignature = false;
        /// <summary>
        /// 是否上传文件
        /// </summary>
        private bool EnableUploadFile = false;
        #endregion

        public Form1()
        {
            InitializeComponent();
            this.textURL.Text = @"http://[IP]:8765/";
            this.textFilePath.Text = @"上传文件的全路径路径";
            this.textFilePath.Enabled = false;
            this.textAppKey.Enabled = false;
            this.textAppSecret.Enabled = false;
        }

        #region Events
        private void SendBtn_Click(object sender, EventArgs e)
        {
            URL = textURL.Text;
            AppKey = textAppKey.Text;
            AppSecret = textAppSecret.Text;
            if(EnableUploadFile)
                richTextRes.Text=UploadFile(textFilePath.Text);
            else
            {
                string requestJson = richTextReq.Text;
                if (!string.IsNullOrEmpty(requestJson))
                {
                    Byte[] bytes = Encoding.UTF8.GetBytes(requestJson);
                    richTextRes.Text = Post(bytes, "application/json; charset=UTF-8", EnableSignature);
                }
                else
                {
                    richTextRes.Text = Get("application/json; charset=UTF-8", EnableSignature);
                }
            }
        }
        private void checkBox2_CheckedChanged(object sender, EventArgs e)
        {
            EnableUploadFile = checkBox2.Checked;            
            this.textFilePath.Enabled = EnableUploadFile;
        }
        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            EnableSignature = checkBox1.Checked;
            this.textAppKey.Enabled = EnableSignature;
            this.textAppSecret.Enabled = EnableSignature;
        }
        #endregion

        /// <summary>
        /// 上传PDF报告
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns></returns>
        private string UploadFile(string filePath)
        {
            string result = string.Empty;
            Byte[] bytes = File.ReadAllBytes(filePath);
            result = Post(bytes, "application/x-www-form-urlencoded", EnableSignature);

            return result;
        }

        #region Send http request
        private string Post(Byte[] requestBytes,string contentType,bool enableSignature)
        {
            string result = string.Empty;

            HttpWebRequest httpWebRequest = CreateWebRequest(contentType,"POST", requestBytes);

            try
            {
                //get Response
                HttpWebResponse httpWebResponse = httpWebRequest.GetResponse() as HttpWebResponse;
                if (httpWebResponse.StatusCode == HttpStatusCode.OK)
                {
                    using (Stream recieveStream = httpWebResponse.GetResponseStream())
                    {
                        StreamReader reader = new StreamReader(recieveStream, Encoding.UTF8);
                        result = reader.ReadToEnd();
                    }
                }
            }
            catch (Exception e)
            {
                result = e.ToString();
            }
            return result;
        }
        private string Get(string contentType, bool enableSignature)
        {
            string result = string.Empty;

            HttpWebRequest httpWebRequest = CreateWebRequest(contentType, "GET", null);

            try
            {
                //get Response
                HttpWebResponse httpWebResponse = httpWebRequest.GetResponse() as HttpWebResponse;
                if (httpWebResponse.StatusCode == HttpStatusCode.OK)
                {
                    using (Stream recieveStream = httpWebResponse.GetResponseStream())
                    {
                        StreamReader reader = new StreamReader(recieveStream, Encoding.UTF8);
                        result = reader.ReadToEnd();
                    }
                }
            }
            catch (Exception e)
            {
                result = e.ToString();
            }
            return result;
        }

        /// <summary>
        /// Create HttpWebRequest object
        /// </summary>
        /// <param name="enableSignature"></param>
        /// <returns></returns>
        private HttpWebRequest CreateWebRequest(string contentType,string method, Byte[] contentBytes)
        {
            HttpWebRequest httpWebRequest = (HttpWebRequest)WebRequest.Create(URL);
            
            if (EnableSignature)
            {
                //Add request hearder
                httpWebRequest.Headers.Add("HiAuthVersion", "1.1");//Add Version
                httpWebRequest.Headers.Add("HiAuthAppKey", AppKey);//Add AppKey
                string requestBody;
                if (EnableUploadFile)
                requestBody = contentBytes != null?PercentEncode(Convert.ToBase64String(contentBytes)):"";
                else
                {
                    requestBody = contentBytes != null ? Encoding.UTF8.GetString(contentBytes):"";
                }
                string appSignature = CreateDigitalSignature(URL, method, requestBody, AppSecret);
                httpWebRequest.Headers.Add("HiAuthAppSignature", appSignature);//Add AppSecret
            }

            httpWebRequest.ContentType = contentType;
            httpWebRequest.ContentLength = contentBytes != null ? contentBytes.Length : 0;
            httpWebRequest.Method = method;
            httpWebRequest.KeepAlive = true;            

            //add resquest data
            if(contentBytes !=null)
                using (Stream stream = httpWebRequest.GetRequestStream())
                {
                    stream.Write(contentBytes, 0, contentBytes.Length);
                    stream.Flush();
                }

            return httpWebRequest;
        }
        #endregion

        #region 生成签章方法
        private string PercentEncode(string s)
        {
            string t = HttpUtility.UrlEncode(s);
            t = t.Replace("+", "%20");
            t = t.Replace("!", "%21");
            t = t.Replace("(", "%28");
            t = t.Replace(")", "%29");
            t = t.Replace("*", "%2a");
            return t;
        }
        /// <summary>
        /// 生成数字签章
        /// </summary>
        /// <param name="url"></param>
        /// <param name="method"></param>
        /// <param name="requestBody"></param>
        /// <param name="secret">加密的salt</param>
        /// <returns></returns>
        private string CreateDigitalSignature(string url, string method, string requestBody, string secret)
        {
            List<string> combined = new List<string>();

            // request method
            combined.Add(method.ToUpper());

            Uri uri = new Uri(url);
            // scheme
            combined.Add(uri.Scheme.ToLower());
            // host
            combined.Add(uri.Host.ToLower());
            // port
            combined.Add(uri.Port.ToString());
            // path
            string path = uri.AbsolutePath.ToLower();
            path = path.Replace("\\", "/");
            if (path.EndsWith("/"))
                path = path.Substring(0, path.Length - 1);
            combined.Add(PercentEncode(path));

            // query string
            string q = (uri.Query ?? "").Trim();
            if (q.Length > 0)
            {
                if (q.StartsWith("?"))
                    q = q.Substring(1);
                string[] itemStrs = q.Split(new char[] { '&' }, StringSplitOptions.RemoveEmptyEntries);
                List<KeyValuePair<string, string>> items = new List<KeyValuePair<string, string>>();
                foreach (string itemStr in itemStrs)
                {
                    if (itemStr.Trim().Length == 0) continue;
                    string key = "", value = "";

                    int index = itemStr.IndexOf("=");
                    if (index <= 0) // = is missing or key is missing, ignore
                    {
                        continue;
                    }
                    else
                    {
                        key = HttpUtility.UrlDecode(itemStr.Substring(0, index)).Trim().ToLower();
                        value = HttpUtility.UrlDecode(itemStr.Substring(index + 1)).Trim();
                        items.Add(new KeyValuePair<string, string>(key, value));
                    }
                }

                // query
                List<string> paramArray = new List<string>();
                foreach (KeyValuePair<string, string> item in items)
                {
                    paramArray.Add(string.Format("{0}={1}", PercentEncode(item.Key), PercentEncode(item.Value)));
                }
                combined.Add(String.Join("&", paramArray.ToArray()));
            }
            else
                combined.Add("");

            // body
            combined.Add(PercentEncode(requestBody ?? ""));
            // salt
            combined.Add(secret);

            string baseString = String.Join("|", combined.ToArray());

            System.Security.Cryptography.SHA256Managed s256 = new System.Security.Cryptography.SHA256Managed();
            byte[] buff;
            buff = s256.ComputeHash(Encoding.UTF8.GetBytes(baseString));
            s256.Clear();
            return Convert.ToBase64String(buff);
        }

        #endregion

    }
}
