using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json.Linq;

namespace loginAPI
{
    public partial class frm_Login_out : Form
    {
        private string clientId = "NMS_Mobile";
        private string issuer = "https://apinpm.egov.phutho.vn";
        private string redirectUri = "http://localhost:4000";
        private string accessToken;
        private string logoutUrl;
        private DateTime tokenExpiryTime;

        public frm_Login_out()
        {
            InitializeComponent();
        }

        private void frm_Login_out_Load(object sender, EventArgs e)
        {
            // Khởi tạo trạng thái ban đầu
            lblStatus.Text = "Chưa đăng nhập";
            rtbInfo.Text = "Thông tin mã và token sẽ hiển thị tại đây.\n";
        }

        private async void btnLogin_Click(object sender, EventArgs e)
        {
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    // Khám phá issuer
                    var discoveryUrl = $"{issuer}/.well-known/openid-configuration";
                    var discoveryResponse = await client.GetStringAsync(discoveryUrl);
                    var discoveryData = JObject.Parse(discoveryResponse);
                    var authEndpoint = discoveryData["authorization_endpoint"].ToString();
                    var tokenEndpoint = discoveryData["token_endpoint"].ToString();

                    // Redirect user to login
                    var authUrl = $"{authEndpoint}?client_id={clientId}&redirect_uri={redirectUri}&response_type=code&scope=openid profile email offline_access";
                    System.Diagnostics.Process.Start(authUrl);

                    // Nhận mã code qua callback
                    string code = await StartLocalHttpServer();
                    rtbInfo.AppendText($"Mã nhận được: {code}\n");

                    // Trao đổi code để lấy token
                    var tokenResponse = await client.PostAsync(tokenEndpoint, new FormUrlEncodedContent(new[]
                    {
                        new KeyValuePair<string, string>("grant_type", "authorization_code"),
                        new KeyValuePair<string, string>("code", code),
                        new KeyValuePair<string, string>("redirect_uri", redirectUri),
                        new KeyValuePair<string, string>("client_id", clientId)
                    }));

                    tokenResponse.EnsureSuccessStatusCode();

                    var tokenData = JObject.Parse(await tokenResponse.Content.ReadAsStringAsync());
                    accessToken = tokenData["access_token"].ToString();
                    int expiresIn = tokenData["expires_in"].ToObject<int>();
                    tokenExpiryTime = DateTime.Now.AddSeconds(expiresIn);

                    rtbInfo.AppendText($"Access Token: {accessToken}\n");
                    rtbInfo.AppendText($"Token Expiry Time: {tokenExpiryTime}\n");

                    // Cập nhật trạng thái
                    lblStatus.Text = "Đăng nhập thành công!";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi đăng nhập: {ex.Message}");
            }
        }

        private async void btnLogout_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(logoutUrl))
            {
                MessageBox.Show("Bạn chưa đăng nhập!");
                return;
            }

            try
            {
                // Redirect user to logout
                System.Diagnostics.Process.Start(logoutUrl);
                accessToken = null;
                logoutUrl = null;
                lblStatus.Text = "Đã đăng xuất.";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi đăng xuất: {ex.Message}");
            }
        }

        private async Task<string> StartLocalHttpServer()
        {
            using (HttpListener listener = new HttpListener())
            {
                listener.Prefixes.Add("http://localhost:4000/");
                listener.Start();
                Console.WriteLine("Listening for callbacks...");

                // Chờ request từ OpenID Connect provider
                HttpListenerContext context = await listener.GetContextAsync();
                string code = context.Request.QueryString["code"]; // Lấy mã code từ query string

                // Gửi phản hồi tới trình duyệt
                string responseString = "<html><body>Login successful. You can close this window.</body></html>";
                byte[] buffer = Encoding.UTF8.GetBytes(responseString);
                context.Response.ContentLength64 = buffer.Length;
                context.Response.OutputStream.Write(buffer, 0, buffer.Length);
                context.Response.OutputStream.Close();

                listener.Stop();
                return code;
            }
        }
    }
}
