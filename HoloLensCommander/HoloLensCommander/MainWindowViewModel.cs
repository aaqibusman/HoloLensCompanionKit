﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using Windows.Storage;
using Windows.UI.Core;

namespace HoloLensCommander
{
    /// <summary>
    /// The view model for the MainPage.
    /// </summary>
    partial class MainWindowViewModel : INotifyPropertyChanged, IDisposable
    {
        /// <summary>
        /// Values used to store and retrieve settings data.
        /// </summary>
        private static readonly string DefaultUserNameKey = "defaultUserName";
        private static readonly string DefaultPasswordKey = "defaultPassword";

        /// <summary>
        /// The default address used when connecting to a HoloLens. This address assumes
        /// a USB connection.
        /// </summary>
        private static readonly string DefaultConnectionAddress = "localhost:10080";

        /// <summary>
        /// Name of the folder which will contain mixed reality files from the registered HoloLens devices.
        /// </summary>
        private static readonly string MixedRealityFilesFolderName = "HoloLensCommander";

        /// <summary>
        /// Dispatcher used to ensure notifications happen on the correct thread.
        /// </summary>
        private CoreDispatcher dispatcher;

        /// <summary>
        /// The application settings container.
        /// </summary>
        private ApplicationDataContainer appSettings;

        /// <summary>
        /// Value indicating whether or not we have attempted to reconnect to previous HoloLens devices.
        /// </summary>
        private bool reconnected;

        /// <summary>
        /// Value indicating whether or not connection changes are to be saved.
        /// </summary>
        private bool suppressSave;

        /// <summary>
        /// The local application folder.
        /// </summary>
        private StorageFolder localFolder;

        /// <summary>
        /// Event that is notified when a property value has changed.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Initializes a new instance of the <see cref="MainWindowViewModel" /> class.
        /// </summary>
        public MainWindowViewModel(CoreDispatcher coreDispatcher)
        {
            this.dispatcher = coreDispatcher;

            this.reconnected = false;
            this.suppressSave = false;

            this.CommonDeviceApps = new ObservableCollection<string>();
            this.RegisteredDevices = new ObservableCollection<HoloLensMonitorControl>();

            this.localFolder = ApplicationData.Current.LocalFolder;

            // Fetch stored settings.
            this.appSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
            this.UseDefaultCredentials();
            
            this.UpdateCanReconnect();

            this.RegisterCommands();
        }

        /// <summary>
        /// Finalizer so that we are assured we clean up all encapsulated resources.
        /// </summary>
        /// <remarks>Call Dispose on this object to avoid running the finalizer.</remarks>
        ~MainWindowViewModel()
        {
            Debug.WriteLine("[~MainWindowViewModel]");
            this.Dispose();
        }

        /// <summary>
        /// Cleans up objects managed by the MainWindowViewModel.
        /// </summary>
        /// <remarks>
        /// Failure to call this method will result in the object not being collected until
        /// finalization occurs.
        /// </remarks>
        public void Dispose()
        {
            Debug.WriteLine("[MainWindowViewModel.Dispose]");

            this.ClearRegisteredDevices();
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Bulk removes HoloLens devices from application management.
        /// </summary>
        private void ClearRegisteredDevices()
        {
            List<HoloLensMonitorControl> monitors = GetCopyOfRegisteredDevices();

            foreach (HoloLensMonitorControl monitor in monitors)
            {
                monitor.Dispose();
            }

            monitors.Clear();

            this.RegisteredDevices.Clear();
        }

        /// <summary>
        /// Get a copy of the registerd HoloLens devices.
        /// </summary>
        /// <returns>List of HoloLensMonitorControl objects.</returns>
        private List<HoloLensMonitorControl> GetCopyOfRegisteredDevices()
        {
            List<HoloLensMonitorControl> registeredDevicesCopy = new List<HoloLensMonitorControl>();

            registeredDevicesCopy.AddRange(this.RegisteredDevices);

            return registeredDevicesCopy;
        }

        /// <summary>
        /// Sends the PropertyChanged events to registered handlers.
        /// </summary>
        /// <param name="propertyName">The name of property that has changed.</param>
        private void NotifyPropertyChanged(string propertyName)
        {
            this.PropertyChanged?.Invoke(
                this, 
                new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Registers commands supported by this object.
        /// </summary>
        private void RegisterCommands()
        {
            this.CloseAllAppsCommand = new Command(
                (parameter) =>
                {
                    this.CloseAllApps();
                });

            this.ConnectToDeviceCommand = new Command(
                async (parameter) =>
                {

                    ConnectOptions connectOptions = new ConnectOptions(
                        string.Empty,
                        this.UserName,
                        this.Password,
                        true);

                    await this.ConnectToDeviceAsync(
                        connectOptions,
                        string.Empty);
                });

            this.DeselectAllDevicesCommand = new Command(
                (parameter) =>
                {
                    this.DeselectAllDevices();
                });

            this.ForgetConnectionsCommand = new Command(
                async (parameter) =>
                {
                    await this.ForgetAllConnectionsAsync();
                });

            this.InstallAppCommand = new Command(
                async (parameter) =>
                {
                    await this.InstallAppAsync();
                });

            this.LaunchAppCommand = new Command(
                (parameter) =>
                {
                    this.LaunchApp();
                });

            this.RefreshCommonAppsCommand = new Command(
                async (parameter) =>
                {
                    await this.RefreshCommonAppsAsync();
                });

            this.RebootDevicesCommand = new Command(
                async (parameter) =>
                {
                    await this.RebootDevicesAsync();
                });

            this.ReconnectToDevicesCommand= new Command(
                async (parameter) =>
                {
                    await this.ReconnectToDevicesAsync();
                });

            this.SaveMixedRealityFilesCommand = new Command(
                async (parameter) =>
                {
                    await this.SaveMixedRealityFiles();
                });

            this.SelectAllDevicesCommand = new Command(
                (parameter) =>
                {
                    this.SelectAllDevices();
                });

            this.ShowConnectContextMenuCommand = new Command(
                async (parameter) =>
                {
                    await this.ShowConnectContextMenuAsync(parameter);
                });

            this.ShutdownDevicesCommand = new Command(
                async (parameter) =>
                {
                    await this.ShutdownDevicesAsync();
                });

            this.StartMixedRealityRecordingCommand = new Command(
                async (parameter) =>
                {
                    await this.StartMixedRealityRecording();
                });

            this.StopMixedRealityRecordingCommand = new Command(
                async (parameter) =>
                {
                    await this.StopMixedRealityRecording();
                });

            this.UninstallAppCommand = new Command(
                async (parameter) =>
                {
                    await this.UninstallApp();
                });
        }

        /// <summary>
        /// Updates the UI with the list of applications installed on all registered HoloLens devices.
        /// </summary>
        /// <param name="appNames">List of application names.</param>
        private void UpdateCommonAppsCollection(List<string> appNames)
        {
            // Get the currently selected application.
            string currentSelection = this.SelectedApp as string;

            this.CommonDeviceApps.Clear();
            foreach (string name in appNames)
            {
                this.CommonDeviceApps.Add(name);
            }

            this.CanManageApps = false;
            if (this.CommonDeviceApps.Count > 0)
            {
                // Set the selected item.
                if ((currentSelection != null) &&
                    this.CommonDeviceApps.Contains(currentSelection))
                {
                    this.SelectedApp = currentSelection;
                }
                else
                {
                    this.SelectedApp = this.CommonDeviceApps[0];
                }

                this.CanManageApps = true;
            }
        }
    }
}
