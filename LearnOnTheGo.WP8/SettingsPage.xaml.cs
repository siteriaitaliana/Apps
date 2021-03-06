﻿using System;
using System.Windows;
using Common.WP8;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Shell;
using Microsoft.Phone.Tasks;

namespace LearnOnTheGo.WP8
{
    public partial class SettingsPage : PhoneApplicationPage
    {
        public SettingsPage()
        {
            InitializeComponent();
            email.Text = Settings.GetString(Setting.Email);
            password.Password = Settings.GetString(Setting.Password);
            runUnderLockScreen.IsChecked = Settings.GetBool(Setting.RunUnderLockScreen);
        }

        private void OnSaveClick(object sender, EventArgs e)
        {
            ErrorReporting.Log("OnSaveClick");
            if (email.Text != Settings.GetString(Setting.Email))
            {
                Cache.DeleteAllFiles();
                App.Crawler = null;
            }
            else if (password.Password != Settings.GetString(Setting.Password))
            {
                App.Crawler = null;
            }
            Settings.Set(Setting.Email, email.Text);
            Settings.Set(Setting.Password, password.Password);
            Settings.Set(Setting.RunUnderLockScreen, runUnderLockScreen.IsChecked == true);
            EnableOrDisableLockScreen();
            NavigationService.GoBack();
        }

        public static void EnableOrDisableLockScreen()
        {
            try
            {
                if (Settings.GetBool(Setting.RunUnderLockScreen))
                {
                    PhoneApplicationService.Current.ApplicationIdleDetectionMode = IdleDetectionMode.Disabled;
                }
                else
                {
                    PhoneApplicationService.Current.ApplicationIdleDetectionMode = IdleDetectionMode.Enabled;
                }
            }
            catch { }
        }

        private void OnCreateAccountClick(object sender, RoutedEventArgs e)
        {
            ErrorReporting.Log("OnCreateAccountClick");
            new WebBrowserTask { Uri = new Uri("https://accounts.coursera.org/signup") }.Show();
        }
    }
}