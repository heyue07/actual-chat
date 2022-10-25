using System;
using System.Diagnostics;
using Android.Util;
using Android.Webkit;
using Java.Interop;
using Stl.Fusion.Authentication;
using WebView = Android.Webkit.WebView;

namespace ActualChat.App.Maui;

public partial class MauiBlazorWebViewHandler
{
    protected override void ConnectHandler(Android.Webkit.WebView platformView)
    {
        Log.Debug(AndroidConstants.LogTag, $"MauiBlazorWebViewHandler.ConnectHandler");

        base.ConnectHandler(platformView);
        var baseUri = UrlMapper.BaseUri;
        var sessionId = AppSettings.SessionId;

        platformView.Settings.JavaScriptEnabled = true;
        var cookieManager = CookieManager.Instance!;
        // May be will be required https://stackoverflow.com/questions/2566485/webview-and-cookies-on-android
        cookieManager.SetAcceptCookie(true);
        cookieManager.SetAcceptThirdPartyCookies(platformView, true);
        var sessionCookieValue = $"FusionAuth.SessionId={sessionId}; path=/; secure; samesite=none; httponly";
        cookieManager.SetCookie("https://" + "0.0.0.0", sessionCookieValue);
        cookieManager.SetCookie("https://" + baseUri.Host, sessionCookieValue);
        var jsInterface = new JavascriptInterface(this, platformView);
        platformView.AddJavascriptInterface(jsInterface, "Android");
        platformView.SetWebViewClient(new WebViewClientOverride(platformView.WebViewClient));
    }

    private class JavascriptInterface : Java.Lang.Object
    {
        private readonly MauiBlazorWebViewHandler _handler;
        private readonly Android.Webkit.WebView _webView;

        public event Action<string> MessageReceived = m => { };

        public JavascriptInterface(MauiBlazorWebViewHandler handler, Android.Webkit.WebView webView)
        {
            _handler = handler;
            _webView = webView;
        }

        [JavascriptInterface]
        [Export("DOMContentLoaded")]
        public void OnDOMContentLoaded()
        {
            _webView.Post(() => {
                try {
                    var sessionHash = new Session(_handler.AppSettings.SessionId).Hash;
                    var script = $"window.App.initPage('{_handler.UrlMapper.BaseUrl}', '{sessionHash}')";
                    _webView.EvaluateJavascript(script, null);
                }
                catch (Exception ex) {
                    Debug.WriteLine(ex.ToString());
                }
            });
        }

        [JavascriptInterface]
        [Export("postMessage")]
        public void OnPostMessage(string data)
        {
            _webView.Post(() => {
                MessageReceived.Invoke(data);
            });
        }
    }

    private class WebViewClientOverride : WebViewClient
    {
        private WebViewClient Original { get; }

        public WebViewClientOverride(WebViewClient original)
            => Original = original;

        public override bool ShouldOverrideUrlLoading(WebView? view, IWebResourceRequest? request)
            => Original.ShouldOverrideUrlLoading(view, request);

        public override WebResourceResponse? ShouldInterceptRequest(WebView? view, IWebResourceRequest? request)
        {
            var resourceResponse = Original.ShouldInterceptRequest(view, request);
            if (resourceResponse == null)
                return null;

            const string contentTypeKey = "Content-Type";
            var contentType = resourceResponse.ResponseHeaders?[contentTypeKey];
            if (contentType == resourceResponse.MimeType && contentType == "application/wasm")
                resourceResponse.ResponseHeaders?.Remove(contentTypeKey);
            return resourceResponse;
        }

        public override void OnPageFinished(WebView? view, string? url)
            => Original.OnPageFinished(view, url);

        protected override void Dispose(bool disposing)
        {
            if (!disposing)
                return;

            Original.Dispose();
        }
    }
}
