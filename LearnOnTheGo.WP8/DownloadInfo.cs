﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Common.WP8;
using Microsoft.Phone.BackgroundTransfer;
using Microsoft.Phone.Controls;

namespace LearnOnTheGo.WP8
{
    public class DownloadInfo : INotifyPropertyChanged, IDownloadInfo
    {
        private readonly int _courseId;
        private readonly string _courseTopicName;
        private readonly int _lectureId;
        private readonly string _lectureTitle;
        private readonly int _index;

        private const string TransfersFolder = "shared/transfers";
        private const string DoneSuffix = ".done";
        private const string CourseTopicNameSuffix = ".courseTopicName";
        private const string LectureTitleSuffix = ".lectureTitle";
        private const string IndexSuffix = ".index";
        private const string StateSuffix = ".state";

        public static string GetStateFile(string videoFile) 
        {
            return videoFile.Replace(DoneSuffix, StateSuffix);
        }

        public static IDictionary<string, BackgroundTransferRequest> GetBackgroundTransferRequests()
        {
            return BackgroundTransferService.Requests.ToDictionary(req => req.Tag);
        }

        private DownloadInfo(int courseId, string courseTopicName, int lectureId, string lectureTitle, int index, IDictionary<string, BackgroundTransferRequest> backgroundTransferRequests)
        {
            _courseId = courseId;
            _courseTopicName = courseTopicName;
            _lectureId = lectureId;
            _lectureTitle = lectureTitle;
            _index = index;
            RefreshStatus();

            var filename = GetBaseFilename();
            BackgroundTransferRequest existingRequest;
            if (backgroundTransferRequests.TryGetValue(filename, out existingRequest))
            {
                StartDownload(existingRequest);
            }
        }

        public static IDownloadInfo Create(int courseId, string courseTopicName, int lectureId, string lectureTitle, int index)
        {
            return new DownloadInfo(courseId, courseTopicName, lectureId, lectureTitle, index, GetBackgroundTransferRequests());
        }

        public int CourseId { get { return _courseId; } }
        public string CourseTopicName { get { return _courseTopicName; } }
        public int LectureId { get { return _lectureId; } }
        public string LectureTitle { get { return _lectureTitle; } }
        public int Index { get { return _index; } }

        public override int GetHashCode()
        {
            return CourseId ^ LectureId;
        }

        public override bool Equals(object obj)
        {
            var other = obj as DownloadInfo;
            return other != null && CourseId == other.CourseId && LectureId == other.LectureId;
        }

        private TransferMonitor _monitor;
        public TransferMonitor Monitor
        {
            get { return _monitor; }
            set
            {
                SetAndNotify(ref _monitor, value);
                Downloading = value != null;
            }
        }

        private bool _downloading;
        public bool Downloading
        {
            get { return _downloading; }
            set { SetAndNotify(ref _downloading, value); }
        }

        private bool _downloaded;
        public bool Downloaded
        {
            get { return _downloaded; }
            set { SetAndNotify(ref _downloaded, value); Notify("NotDownloaded"); }
        }

        public bool NotDownloaded
        {
            get { return !_downloaded; }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public DownloadInfo Self { get { return this; } }

        private void SetAndNotify<T>(ref T prop, T value, [CallerMemberName] string propertyName = "")
        {
            if (!Equals(prop, value))
            {
                prop = value;
                Notify(propertyName);
            }
        }

        private void Notify(string propertyName)
        {
            var propertyChanged = PropertyChanged;
            if (propertyChanged != null)
            {
                propertyChanged(this, new PropertyChangedEventArgs(propertyName));
                propertyChanged(this, new PropertyChangedEventArgs("Self"));
            }
        }

        public Uri VideoLocation
        {
            get { return new Uri(GetBaseFilename() + DoneSuffix, UriKind.Relative); }
        }

        private static string GetBaseFilename(int courseId, int lectureId)
        {
            return TransfersFolder + "/" + courseId + "_" + lectureId;
        }

        private string GetBaseFilename()
        {
            return GetBaseFilename(CourseId, LectureId);
        }

        public void RefreshStatus()
        {
            Downloaded = IsolatedStorage.FileExists(GetBaseFilename() + DoneSuffix);
        }

        private static void SafeRemoveRequest(BackgroundTransferRequest request)
        {
            try
            {
                BackgroundTransferService.Remove(request);
            }
            catch { }
            try
            {
                request.Dispose();
            }
            catch { }
        }

        public void DeleteVideo()
        {
            IsolatedStorage.Delete(GetBaseFilename() + DoneSuffix);
            IsolatedStorage.Delete(GetBaseFilename() + CourseTopicNameSuffix);
            IsolatedStorage.Delete(GetBaseFilename() + LectureTitleSuffix);
            IsolatedStorage.Delete(GetBaseFilename() + IndexSuffix);
            IsolatedStorage.Delete(GetBaseFilename() + StateSuffix);
            RefreshStatus();
        }

        // this might be called more than once
        private void OnCompletion(BackgroundTransferRequest request)
        {
            lock (typeof(DownloadInfo))
            {
                Monitor = null;
                IsolatedStorage.Move(GetBaseFilename(), GetBaseFilename() + DoneSuffix);
                RefreshStatus();
                SafeRemoveRequest(request);
            }
        }

        // this might be called more than once
        private void OnFailure(BackgroundTransferRequest request)
        {
            lock (typeof(DownloadInfo))
            {
                Monitor = null;
                IsolatedStorage.Delete(GetBaseFilename());
                IsolatedStorage.Delete(GetBaseFilename() + CourseTopicNameSuffix);
                IsolatedStorage.Delete(GetBaseFilename() + LectureTitleSuffix);
                IsolatedStorage.Delete(GetBaseFilename() + IndexSuffix);
                SafeRemoveRequest(request);
            }
        }

        private void StartDownload(BackgroundTransferRequest request)
        {
            if (request.TransferStatus == TransferStatus.Completed)
            {
                OnCompletion(request);
            }
            else if (request.TransferStatus == TransferStatus.Unknown)
            {
                OnFailure(request);
            }
            else
            {
                Monitor = new TransferMonitor(request);
                Monitor.Complete += (_, args) => { OnCompletion(request); };
                Monitor.Failed += (_, args) =>
                {
                    OnFailure(request);
                    if (args.Request.StatusCode == 206 && args.Request.TotalBytesToReceive > 100000000 && args.Request.TransferPreferences == TransferPreferences.AllowCellularAndBattery)
                    {
                        var newRequest = new BackgroundTransferRequest(args.Request.RequestUri, args.Request.DownloadLocation)
                        {
                            Tag = args.Request.Tag,
                            TransferPreferences = TransferPreferences.None,
                        };
                        StartDownload(newRequest);
                    }
                };
                if (request.TransferStatus == TransferStatus.None)
                {
                    IsolatedStorage.WriteAllText(GetBaseFilename() + CourseTopicNameSuffix, CourseTopicName);
                    IsolatedStorage.WriteAllText(GetBaseFilename() + LectureTitleSuffix, LectureTitle);
                    IsolatedStorage.WriteAllText(GetBaseFilename() + IndexSuffix, Index.ToString());
                    Monitor.RequestStart();
                }
            }
        }

        public void QueueDowload(string videoUrl)
        {
            if (!Downloaded)
            {
                var filename = GetBaseFilename();
                var request = new BackgroundTransferRequest(new Uri(videoUrl), new Uri(filename, UriKind.Relative))
                {
                    Tag = filename,
                    TransferPreferences = TransferPreferences.AllowCellularAndBattery,
                };
                StartDownload(request);
            }
        }

        private static IDownloadInfo Get(string filename, IDictionary<string, BackgroundTransferRequest> backgroundTransferRequests)
        {
            var parts = filename.Replace(DoneSuffix, null).Substring(filename.LastIndexOf('/') + 1).Split('_');
            var courseId = int.Parse(parts[0]);
            var lectureId = int.Parse(parts[1]);
            var courseTopicName = IsolatedStorage.ReadAllText(GetBaseFilename(courseId, lectureId) + CourseTopicNameSuffix) ?? "<Unknown Course>";
            var lectureTitle = IsolatedStorage.ReadAllText(GetBaseFilename(courseId, lectureId) + LectureTitleSuffix) ?? "<Unknown Lecture>";
            var indexStr = IsolatedStorage.ReadAllText(GetBaseFilename(courseId, lectureId) + IndexSuffix);
            int index;
            int.TryParse(indexStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out index);
            return new DownloadInfo(courseId, courseTopicName, lectureId, lectureTitle, index, backgroundTransferRequests);
        }

        public static void SetupBackgroundTransfers()
        {
            var backgroundTransferRequests = GetBackgroundTransferRequests();
            foreach (var request in backgroundTransferRequests.Values)
            {
                // this will subscribe to completion and failure events
                Get(request.Tag, backgroundTransferRequests);
            }
        }

        public static IEnumerable<IDownloadInfo> GetAll()
        {
            var backgroundTransferRequests = GetBackgroundTransferRequests();
            foreach (var request in backgroundTransferRequests.Values)
            {
                var downloadInfo = Get(request.Tag, backgroundTransferRequests);
                if (downloadInfo.Downloading && !downloadInfo.Downloaded)
                {
                    // if it is already downloaded, the Get removed it from the BackgroundTransferService,
                    // but this time it's still present as a duplicate
                    yield return downloadInfo;
                }
            }
            foreach (var filename in IsolatedStorage.GetFiles(TransfersFolder, DoneSuffix))
            {
                yield return Get(filename, backgroundTransferRequests);
            }
        }
    }
}