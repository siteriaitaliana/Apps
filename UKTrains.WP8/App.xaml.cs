﻿using System.Windows;
using Trains;
using Trains.WP8;

namespace UKTrains.WP8
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();

            new AppMetadata(this, "UK Trains", "uktrains@codebeside.org");
            Stations.Country = Country.UK;
        }
    }
}