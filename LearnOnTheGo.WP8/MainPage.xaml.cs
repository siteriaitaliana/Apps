﻿using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using Common.WP8;
using FSharp.Control;
using Microsoft.Phone.Controls;

namespace LearnOnTheGo.WP8
{
    public partial class MainPage : PhoneApplicationPage
    {
        public MainPage()
        {
            InitializeComponent();
#if DEBUG
            if (string.IsNullOrEmpty(Settings.GetString(Setting.Email)) || string.IsNullOrEmpty(Settings.GetString(Setting.Password)))
            {
                Settings.Set(Setting.Email, "ovatsus@outlook.com");
                Settings.Set(Setting.Password, "abc123");
            }
#endif
            CommonApplicationBarItems.Init(this);
            adControl.AppId = AppMetadata.Current.AdDuplexId;
        }

        private LazyBlock<Course[]> coursesLazyBlock;
        private string lastEmail;

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (e.NavigationMode == NavigationMode.New)
            {
                ErrorReporting.CheckForPreviousException(true);
                AppMetadata.CheckForNewVersion();
                AppMetadata.CheckForReview(this);
            }

            // settings changed
            if (lastEmail != null && lastEmail != Settings.GetString(Setting.Email))
            {
                ErrorReporting.Log("Settings changed");
                activeCourses.ItemsSource = null;
                upcomingCourses.ItemsSource = null;
                finishedCourses.ItemsSource = null;
            }

            var email = Settings.GetString(Setting.Email);
            var password = Settings.GetString(Setting.Password);

            lastEmail = email;

            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                if (e.NavigationMode != NavigationMode.Back)
                {
                    this.NavigateToSettings();
                }
            }
            else if (activeCourses.ItemsSource == null)
            {
                LoadCourses(email, password, false);
            }
        }

        private void LoadCourses(string email, string password, bool refresh)
        {
            if (App.Crawler == null)
            {
                App.Crawler = new Crawler(email, password, Cache.GetFiles(), Cache.SaveFile, DownloadInfo.Create);
            }
            if (refresh)
            {
                App.Crawler.RefreshCourses();
            }
            if (coursesLazyBlock != null)
            {
                coursesLazyBlock.Cancel();
            }
            coursesLazyBlock = new LazyBlock<Course[]>(
                "courses",
                "No courses",
                App.Crawler.Courses,
                a => a.Length == 0,
                new LazyBlockUI<Course[]>(
                    this,
                    courses =>
                    {
                        var active = new List<Course>();
                        var upcoming = new List<Course>();
                        var finished = new List<Course>();
                        foreach (var course in courses)
                        {
                            if (course.HasFinished)
                                finished.Add(course);
                            else if (course.Active)
                                active.Add(course);
                            else
                                upcoming.Add(course);
                        }
                        activeCourses.ItemsSource = active;
                        upcomingCourses.ItemsSource = upcoming;
                        finishedCourses.ItemsSource = finished;
                        activeCoursesEmptyMessage.Visibility = active.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
                        upcomingCoursesEmptyMessage.Visibility = upcoming.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
                        finishedCoursesEmptyMessage.Visibility = finished.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
                        if (active.Count == 0)
                        {
                            if (upcoming.Count == 0)
                            {
                                pivot.SelectedIndex = 2;
                            }
                            else
                            {
                                pivot.SelectedIndex = 1;
                            }
                        }
                    },
                    () => activeCourses.ItemsSource != null,
                    messageTextBlock),
                false,
                _ =>
                {
                    activeCoursesEmptyMessage.Visibility = Visibility.Collapsed;
                    upcomingCoursesEmptyMessage.Visibility = Visibility.Collapsed;
                    finishedCoursesEmptyMessage.Visibility = Visibility.Collapsed;
                },
                success =>
                {
                    if (!success)
                    {
                        ErrorReporting.Log("Failed to get courses");
                    }
                },
                null);
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            base.OnNavigatingFrom(e);
            if (coursesLazyBlock != null)
            {
                coursesLazyBlock.Cancel();
            }          
        }

        private void OnRefreshClick(object sender, EventArgs e)
        {
            ErrorReporting.Log("OnRefreshClick");

            var email = Settings.GetString(Setting.Email);
            var password = Settings.GetString(Setting.Password);

            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                this.NavigateToSettings();
            }
            else
            {
                LoadCourses(email, password, true);
            }
        }

        private void OnCourseClick(object sender, RoutedEventArgs e)
        {
            ErrorReporting.Log("OnCourseClick");
            var course = (Course)((Button)sender).DataContext;
            NavigationService.Navigate(new Uri("/CoursePage.xaml?courseId=" + course.Id, UriKind.Relative));
        }
    }
}