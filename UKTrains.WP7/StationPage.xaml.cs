﻿using Microsoft.Phone.Controls;
#if WP8
using Microsoft.Phone.Maps.Controls;
using Microsoft.Phone.Maps.Services;
using Microsoft.Phone.Maps.Toolkit;
using System.Windows;
#endif
using NationalRail;
using System;
using System.Linq;
using System.Windows.Navigation;
using Microsoft.Phone.Shell;
using Microsoft.Phone.Tasks;
using System.Device.Location;

namespace UKTrains
{
    public partial class StationPage : PhoneApplicationPage
    {
        public StationPage()
        {
            InitializeComponent();
        }

        private Station station;
        private Station callingAt;
        private bool busy;

        private Uri GetUriForThisPage()
        {
            return callingAt == null ?
                new Uri("/StationPage.xaml?stationCode=" + station.Code, UriKind.Relative) :
                new Uri("/StationPage.xaml?stationCode=" + station.Code + "&callingAt=" + callingAt.Name, UriKind.Relative);
        }

        private string GetTitle()
        {
            return station.Name + (callingAt == null ? "" : " calling at " + callingAt.Name);
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            busy = true;

            station = LiveDepartures.getStation(NavigationContext.QueryString["stationCode"]);
            callingAt = NavigationContext.QueryString.ContainsKey("callingAt") ? LiveDepartures.getStation(NavigationContext.QueryString["callingAt"]) : null;

            pivot.Title = GetTitle();

            if (callingAt != null)
            {
                NavigationService.RemoveBackEntry();
                var menuItem = new ApplicationBarMenuItem("Clear filter");
                menuItem.Click += OnClearFilterClick;
                ApplicationBar.MenuItems.Add(menuItem);
            }

            Load();

            bool createDirectionsButton;
#if WP8
            var currentPosition = LocationService.CurrentPosition;
            if (currentPosition != null && !currentPosition.IsUnknown && Settings.GetBool(Setting.LocationServicesEnabled))
            {
                var routeQuery = new RouteQuery
                {
                    RouteOptimization = RouteOptimization.MinimizeTime,
                    TravelMode = TravelMode.Walking,
                    Waypoints = new[] { 
                    currentPosition, 
                    new GeoCoordinate(station.LatLong.Lat, station.LatLong.Long) },
                };

                routeQuery.QueryCompleted += OnRouteQueryCompleted;
                routeQuery.QueryAsync();
                createDirectionsButton = false;
            }
            else
            {
                createDirectionsButton = true;
            }
#else
            createDirectionsButton = true;
#endif
            if (ApplicationBar.Buttons.Count == 2)
            {

                if (createDirectionsButton)
                {
                    var mapButton = new ApplicationBarIconButton(new Uri("/Icons/dark/appbar.map.png", UriKind.Relative))
                    {
                        Text = "Directions",
                    };
                    mapButton.Click += OnDirectionsClick;
                    ApplicationBar.Buttons.Add(mapButton);

                }

                var uri = GetUriForThisPage();
                if (!ShellTile.ActiveTiles.Any(tile => tile.NavigationUri == uri))
                {
                    var pinButton = new ApplicationBarIconButton(new Uri("/Icons/dark/appbar.pin.png", UriKind.Relative))
                    {
                        Text = "Pin to Start",
                    };
                    pinButton.Click += OnPinClick;
                    ApplicationBar.Buttons.Add(pinButton);
                }
            }
        }

        private void OnClearFilterClick(object sender, EventArgs e)
        {
            var uri = new Uri("/StationPage.xaml?stationCode=" + station.Code, UriKind.Relative);
            NavigationService.Navigate(uri);
        }

        private void Load()
        {
            bool refreshing = this.departures.ItemsSource != null;
            LiveDepartures.getDepartures(callingAt, station).Display(
                this,
                refreshing ? "Refreshing departures..." : "Loading departures...",
                refreshing,
                "No more departures from this station today",
                messageTextBlock,
                departures => this.departures.ItemsSource = departures,
                () => busy = false);
        }

#if WP8
        private void OnRouteQueryCompleted(object sender, QueryCompletedEventArgs<Route> e)
        {
            if (e.Error == null)
            {
                var geometry = e.Result.Legs[0].Geometry;
                var start = geometry[0];
                var end = geometry[geometry.Count - 1];
                var center = new GeoCoordinate((start.Latitude + end.Latitude) / 2, (start.Longitude + end.Longitude) / 2);
                var map = new Map
                {
                    Center = center,
                    ZoomLevel = 15,
                    PedestrianFeaturesEnabled = true,
                };

                var mapLayer = new MapLayer();
                mapLayer.Add(new MapOverlay
                {
                    GeoCoordinate = start,
                    PositionOrigin = new Point(0, 1),
                    Content = new Pushpin
                    {
                        Content = "Me",
                    }
                });
                mapLayer.Add(new MapOverlay
                {
                    GeoCoordinate = end,
                    PositionOrigin = new Point(0, 1),
                    Content = new Pushpin
                    {
                        Content = station.Name + " Station",
                    }
                });
                map.Layers.Add(mapLayer);

                map.AddRoute(new MapRoute(e.Result));

                pivot.Items.Add(new PivotItem
                {
                    Header = "Directions",
                    Content = map
                });
            }
        }
#endif
        
        private void OnRefreshClick(object sender, EventArgs e)
        {
            if (busy)
            {
                return;
            }
            busy = true;

            Load();
        }

        private void OnDirectionsClick(object sender, EventArgs e)
        {
            var task = new BingMapsDirectionsTask();
            task.End = new LabeledMapLocation(station.Name + " Station", new GeoCoordinate(station.LatLong.Lat, station.LatLong.Long));
            task.Show();
        }

        private void OnPinClick(object sender, EventArgs e)
        {
#if WP8
            var tileData = new FlipTileData()
            {
                Title = GetTitle(),
                SmallBackgroundImage = new Uri("/Assets/Tiles/FlipCycleTileSmall.png", UriKind.Relative),
                BackgroundImage = new Uri("/Assets/Tiles/FlipCycleTileMedium.png", UriKind.Relative),
                WideBackgroundImage = new Uri("/Assets/Tiles/FlipCycleTileLarge.png", UriKind.Relative),
            };
            ShellTile.Create(new Uri("/StationPage.xaml?stationCode=" + station.Code, UriKind.Relative), tileData, true);
#else
            var tileData = new StandardTileData()
            {
                Title = GetTitle(),
                BackgroundImage = new Uri("Tile.png", UriKind.Relative),
            };
            ShellTile.Create(GetUriForThisPage(), tileData);
#endif
        }

        private void OnFilterClick(object sender, EventArgs e)
        {
            NavigationService.Navigate(new Uri("/MainAndFilterPage.xaml?fromStation=" + station.Code, UriKind.Relative));
        }
    }
}