using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dropbox.Api;
using Dropbox.Api.Files;
using Plugin.FilePicker;
using Plugin.FilePicker.Abstractions;
using Xamarin.Forms;

namespace DropBoxDemo
{
    public partial class MainPage : ContentPage
    {
        private const string AppKeyDropboxtoken = "k0avuwqk6lh9fs3";

        private const string ClientId = "k0avuwqk6lh9fs3";

        private const string RedirectUri = "https://www.anysite.se/";

        public Action OnAuthenticated;

        private string oauth2State;
        private string AccessToken { get; set; }
        public MainPage()
        {
            InitializeComponent();
        }

        private async void ButtonAuthen_OnClicked(object sender, EventArgs e)
        {
            await Authorize();
        }

        public async Task Authorize()
        {
            if (string.IsNullOrWhiteSpace(this.AccessToken) == false)
            {
                // Already authorized
                this.OnAuthenticated?.Invoke();
                return;
            }

            if (this.GetAccessTokenFromSettings())
            {
                // Found token and set AccessToken 
                return;
            }

            // Run Dropbox authentication
            this.oauth2State = Guid.NewGuid().ToString("N");
            var authorizeUri = DropboxOAuth2Helper.GetAuthorizeUri(OAuthResponseType.Token, ClientId, new Uri(RedirectUri), this.oauth2State);
            var webView = new WebView { Source = new UrlWebViewSource { Url = authorizeUri.AbsoluteUri } };
            webView.Navigating += this.WebViewOnNavigating;
            var contentPage = new ContentPage { Content = webView };
            await Application.Current.MainPage.Navigation.PushModalAsync(contentPage);
        }

        private async void WebViewOnNavigating(object sender, WebNavigatingEventArgs e)
        {
            if (!e.Url.StartsWith(RedirectUri, StringComparison.OrdinalIgnoreCase))
            {
                // we need to ignore all navigation that isn't to the redirect uri.
                return;
            }

            try
            {
                var result = DropboxOAuth2Helper.ParseTokenFragment(new Uri(e.Url));

                if (result.State != this.oauth2State)
                {
                    return;
                }

                this.AccessToken = result.AccessToken;

                await SaveDropboxToken(this.AccessToken);
                this.OnAuthenticated?.Invoke();
            }
            catch (ArgumentException)
            {
                // There was an error in the URI passed to ParseTokenFragment
            }
            finally
            {
                e.Cancel = true;
                await Application.Current.MainPage.Navigation.PopModalAsync();
            }
        }

        private async Task SaveDropboxToken(string token)
        {
            if (token == null)
            {
                return;
            }

            try
            {
                Application.Current.Properties.Add(AppKeyDropboxtoken, token);
                await Application.Current.SavePropertiesAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Save DropBox token error: " + ex.Message);
            }
        }

        private bool GetAccessTokenFromSettings()
        {
            try
            {
                if (!Application.Current.Properties.ContainsKey(AppKeyDropboxtoken))
                {
                    return false;
                }

                this.AccessToken = Application.Current.Properties[AppKeyDropboxtoken]?.ToString();
                if (this.AccessToken != null)
                {
                    this.OnAuthenticated.Invoke();
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Dropbox error: " + ex.Message);
                return false;
            }
        }

        private async void ButtonGetFiles_OnClicked(object sender, EventArgs e)
        {
            using (var client = new DropboxClient(this.AccessToken))
            {
                var list = await client.Files.ListFolderAsync(string.Empty);

                // show folders then files
                foreach (var item in list.Entries.Where(i => i.IsFolder))
                {
                    Debug.WriteLine("D  {0}/", item.Name);
                }

                foreach (var item in list.Entries.Where(i => i.IsFile))
                {
                    Debug.WriteLine("F{0,8} {1}", item.AsFile.Size, item.Name);
                }
            }
        }

        private FileData file;
        private async void ButtonUpload_OnClicked(object sender, EventArgs e)
        {
            file = await CrossFilePicker.Current.PickFile();

            using (var client = new DropboxClient(this.AccessToken))
            {
                using (var mem = new MemoryStream(file.DataArray))
                {
                    var updated = await client.Files.UploadAsync(file.FilePath + "/" + file, WriteMode.Overwrite.Instance, body: file.GetStream());
                    Console.WriteLine("Saved {0}/{1} rev {2}", file.FilePath, file, updated.Rev);
                }
            }
        }

        private async void ButtonDownload_OnClicked(object sender, EventArgs e)
        {
            using (var client = new DropboxClient(this.AccessToken))
            {
                using (var response = await client.Files.DownloadAsync(file.FilePath + "/" + file))
                {
                    var pickedFile = await response.GetContentAsByteArrayAsync();
                    //FileImagePreview.Source = ImageSource.FromStream(() => pickedFile);
                }
            }
        }
    }
}
