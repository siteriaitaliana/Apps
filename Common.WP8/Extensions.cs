﻿using System;
using System.Linq;
using Microsoft.Phone.Controls;
using Microsoft.Xna.Framework.GamerServices;

namespace Common.WP8
{
    public static class Extensions
    {
        public static Uri GetUri<T>(this PhoneApplicationPage currentPage) where T : PhoneApplicationPage
        {
            var targetPageType = typeof(T);
            return new Uri("/" + targetPageType.Namespace + ";component/" + targetPageType.Name + ".xaml", UriKind.Relative);
        }

        public static Uri WithParameters(this Uri originalUri, params string[] args)
        {            
            string uri = originalUri.OriginalString;
            bool hadQuery = uri.Contains("?");
            for (int i = 0; i < args.Length; ++i)
            {
                uri += i == 0 && !hadQuery ? "?" : i % 2 == 0 ? "&" : "=";
                uri += args[i];
            }
            return new Uri(uri, UriKind.Relative);
        }

        public static Uri WithParametersIf(this Uri originalUri, bool condition, params string[] args)
        {
            return condition ? originalUri.WithParameters(args) : originalUri;
        }

        public static Uri WithParametersIf(this Uri originalUri, bool condition, params Func<string>[] args)
        {
            return condition ? originalUri.WithParameters(args.Select(arg => arg()).ToArray()) : originalUri;
        }

        public static bool ShowMessageBox(string title, string text, string yesButton = "Yes", string noButton = "No")
        {
            IAsyncResult result = Guide.BeginShowMessageBox(
                title,
                text,
                new[] { yesButton, noButton },
                0,
                Microsoft.Xna.Framework.GamerServices.MessageBoxIcon.None,
                null,
                null);

            result.AsyncWaitHandle.WaitOne();

            int? choice = Guide.EndShowMessageBox(result);
            return choice.HasValue && choice.Value == 0;
        }
    }
}
