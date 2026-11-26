using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows;
using Microsoft.Win32;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace Zuzify;
public sealed class ZuzifyWindow : Window
{
    readonly WebView2 view = new();
    readonly HttpClient http = new(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(20) };
    const string Origin = "https://zuzify.local";
    string? token, selectedFile;
    readonly Uri endpoint;
    bool processing;
    [STAThread] public static void Main() => new Application().Run(new ZuzifyWindow());
    public ZuzifyWindow()
    {
        Title = "Zuzify"; Width = 1280; Height = 850; MinWidth = 820; MinHeight = 640;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        endpoint = new Uri(Environment.GetEnvironmentVariable("ZUZIFY_API_URL") ?? "http://127.0.0.1:8787/");
        if (endpoint.Scheme != "https" && !(endpoint.Scheme == "http" && endpoint.IsLoopback))
            throw new InvalidOperationException("License server requires HTTPS except on loopback.");
        Content = view; Loaded += async (_, _) => {
            try {
                var dir = Path.Combine(AppContext.BaseDirectory, "ui", "dist");
                if (!File.Exists(Path.Combine(dir, "index.html"))) throw new Exception("Build the React UI with npm run build in ui before building Zuzify.");
                var profile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Zuzify", "WebView");
                await view.EnsureCoreWebView2Async(await CoreWebView2Environment.CreateAsync(null, profile));
                var core = view.CoreWebView2;
                core.Settings.AreDevToolsEnabled = false; core.Settings.AreDefaultContextMenusEnabled = false; core.Settings.AreHostObjectsAllowed = false;
                core.SetVirtualHostNameToFolderMapping("zuzify.local", dir, CoreWebView2HostResourceAccessKind.Deny);
                core.NavigationStarting += (_, e) => { if (!Trusted(e.Uri)) e.Cancel = true; };
                core.NewWindowRequested += (_, e) => e.Handled = true;
                core.PermissionRequested += (_, e) => e.State = CoreWebView2PermissionState.Deny;
                core.WebMessageReceived += Receive;
                core.Navigate(Origin + "/index.html");
            } catch (Exception ex) { MessageBox.Show(ex.Message, "Zuzify startup"); Close(); }
        };
    }
    static bool Trusted(string raw) => Uri.TryCreate(raw, UriKind.Absolute, out var u) && u.GetLeftPart(UriPartial.Authority) == Origin;
    async void Receive(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        if (!Trusted(e.Source)) return;
        int id = 0;
        try {
            using var doc = JsonDocument.Parse(e.WebMessageAsJson);
            var root = doc.RootElement; id = root.GetProperty("id").GetInt32();
            if(processing) { Reply(id,false,null,"Another operation is running."); return; }
            processing = true;
            try {
                var action = root.GetProperty("action").GetString();
                var data = root.GetProperty("data");
                object? result = action switch {
                    "register" or "login" => await Authenticate(action!, data),
                    "me" => await Api("GET", "me", null),
                    "redeem" => await Api("POST", "redeem", new { code = Read(data,"code") }),
                    "logout" => await Logout(),
                    "checkout" => await Checkout(Read(data,"plan")),
                    "chooseFile" => await ChooseFile(),
                    "hash" => await Hash(Read(data,"expected")),
                    "password" => await Password(data.GetProperty("length").GetInt32()),
                    "device" => new { OS = Environment.OSVersion.ToString(), Architecture = Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit", LogicalProcessors = Environment.ProcessorCount, Runtime = Environment.Version.ToString() },
                    "export" => await Export(Read(data,"text")),
                    _ => throw new Exception("Unknown operation.")
                };
                Reply(id,true,result,null);
            } finally { processing = false; }
        } catch(Exception ex) { Reply(id,false,null,ex.Message); }
    }
    void Reply(int id,bool ok,object? data,string? error) => view.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new {id,ok,data,error}));
    static string Read(JsonElement data,string name) => data.GetProperty(name).GetString() ?? "";
    async Task<JsonElement> Api(string method,string path,object? body)
    {
        using var req = new HttpRequestMessage(new HttpMethod(method),new Uri(endpoint,path));
        if(token!=null) req.Headers.Authorization=new AuthenticationHeaderValue("Bearer",token);
        if(body!=null) req.Content=new StringContent(JsonSerializer.Serialize(body),Encoding.UTF8,"application/json");
        using var res=await http.SendAsync(req);
        using var doc=JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        var root=doc.RootElement;
        if(!res.IsSuccessStatusCode) throw new Exception(root.TryGetProperty("error",out var error)?error.GetString():"License service request failed.");
        return root.Clone();
    }
    async Task<object> Authenticate(string action,JsonElement data)
    {
        var res=await Api("POST",action,new {username=Read(data,"username"),password=Read(data,"password")});
        token=res.GetProperty("token").GetString(); selectedFile=null;
        return new { username=res.GetProperty("username"), licenses=res.GetProperty("licenses") };
    }
    async Task<object> Logout(){try{await Api("POST","logout",new {});}finally{token=null;selectedFile=null;}return new {ok=true};}
    async Task RequireLicense(){var r=await Api("GET","me",null);if(r.GetProperty("licenses").GetArrayLength()==0)throw new Exception("An active Zuzify license is required.");}
    async Task<object> Checkout(string plan)
    {
        var r=await Api("POST","checkout",new {plan});
        if(!Uri.TryCreate(r.GetProperty("url").GetString(),UriKind.Absolute,out var url)||url.Scheme!="https"||url.Host!="checkout.stripe.com")throw new Exception("Invalid checkout URL.");
        Process.Start(new ProcessStartInfo(url.AbsoluteUri){UseShellExecute=true}); return new {ok=true};
    }
    async Task<object> ChooseFile()
    {
        await RequireLicense(); var d=new OpenFileDialog();
        if(d.ShowDialog(this)==true)selectedFile=d.FileName;
        return new {name=selectedFile==null?"":Path.GetFileName(selectedFile)};
    }
    async Task<object> Hash(string expected)
    {
        await RequireLicense();
        if(selectedFile==null)throw new Exception("Choose a file first.");
        expected=expected.Trim();
        if(expected.Length!=0&&(expected.Length!=64||!expected.All(Uri.IsHexDigit)))throw new Exception("Enter a 64-character SHA-256.");
        await using var stream=File.OpenRead(selectedFile);
        var hash=Convert.ToHexString(await SHA256.HashDataAsync(stream)).ToLowerInvariant();
        return new {result=hash+(expected.Length==0?"":string.Equals(hash,expected,StringComparison.OrdinalIgnoreCase)?"\nMATCH":"\nMISMATCH")};
    }
    async Task<object> Password(int length)
    {
        await RequireLicense(); if(length<16||length>64)throw new Exception("Choose 16–64 characters.");
        const string alphabet="ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%&*+-_";
        return new {secret=new string(Enumerable.Range(0,length).Select(_=>alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)]).ToArray())};
    }
    async Task<object> Export(string text)
    {
        if(text.Length>1000000)throw new Exception("Report too large.");
        var d=new SaveFileDialog{Filter="Text file|*.txt",FileName="zuzify-activity.txt"};
        if(d.ShowDialog(this)==true)await File.WriteAllTextAsync(d.FileName,text);
        return new {ok=true};
    }
}