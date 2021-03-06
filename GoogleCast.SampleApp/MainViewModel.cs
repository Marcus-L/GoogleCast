﻿using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Threading;
using GoogleCast.Channels;
using GoogleCast.Models.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GoogleCast.SampleApp
{
    /// <summary>
    /// Main view model
    /// </summary>
    class MainViewModel : ViewModelBase
    {
        /// <summary>
        /// Initializes a new instance of MainViewModel class
        /// </summary>
        /// <param name="deviceLocator">GoogleCast device locator</param>
        /// <param name="sender">GoogleCast sender</param>
        public MainViewModel(IDeviceLocator deviceLocator, ISender sender)
        {
            DeviceLocator = deviceLocator;
            Sender = sender;
            sender.GetChannel<IMediaChannel>().StatusChanged += MediaChannelStatusChanged;
            sender.GetChannel<IReceiverChannel>().StatusChanged += ReceiverChannelStatusChanged;
            PlayCommand = new RelayCommand(async () => await Try(PlayAsync), () => AreButtonsEnabled);
            PauseCommand = new RelayCommand(async () => await Try(PauseAsync), () => AreButtonsEnabled);
            StopCommand = new RelayCommand(async () => await Try(StopAsync), () => AreButtonsEnabled);
            RefreshCommand = new RelayCommand(async () => await Try(RefreshAsync), () => IsLoaded);

            if (!IsInDesignMode)
            {
                Task.Run(RefreshAsync);
            }
        }

        private IDeviceLocator DeviceLocator { get; }
        private ISender Sender { get; }

        private IEnumerable<IReceiver> _receivers;
        /// <summary>
        /// Gets the available receivers
        /// </summary>
        public IEnumerable<IReceiver> Receivers
        {
            get { return _receivers; }
            private set { Set(nameof(Receivers), ref _receivers, value); }
        }

        private bool IsInitialized { get; set; }

        private IReceiver _selectedReceiver;
        /// <summary>
        /// Gets or sets the selected receiver
        /// </summary>
        public IReceiver SelectedReceiver
        {
            get { return _selectedReceiver; }
            set
            {
                if (_selectedReceiver != null && !_selectedReceiver.Equals(value) ||
                    _selectedReceiver == null && value != null)
                {
                    _selectedReceiver = value;
                    IsInitialized = false;
                    RaisePropertyChanged(nameof(SelectedReceiver));
                    RaiseButtonsCommandsCanExecuteChanged();
                }
            }
        }

        private bool _isLoaded;
        /// <summary>
        /// Gets a value indicating whether the list of the GoogleCast devices is loaded or not 
        /// </summary>
        public bool IsLoaded
        {
            get { return _isLoaded; }
            private set
            {
                if (_isLoaded != value)
                {
                    _isLoaded = value;
                    RaisePropertyChanged(nameof(IsLoaded));
                    RefreshCommand.RaiseCanExecuteChanged();
                    RaiseButtonsCommandsCanExecuteChanged();
                }
            }
        }

        /// <summary>
        /// Gets a value indicating whether the Play, Pause and Stop buttons must be enabled or not
        /// </summary>
        public bool AreButtonsEnabled
        {
            get { return IsLoaded && SelectedReceiver != null && !String.IsNullOrWhiteSpace(Link); }
        }

        private string _playerState;
        /// <summary>
        /// Gets the player state
        /// </summary>
        public string PlayerState
        {
            get { return _playerState; }
            private set { Set(nameof(PlayerState), ref _playerState, value); }
        }

        private string _link = "http://commondatastorage.googleapis.com/gtv-videos-bucket/sample/BigBuckBunny.mp4";
        /// <summary>
        /// Gets or sets the link to play
        /// </summary>
        public string Link
        {
            get { return _link; }
            set
            {
                if (_link != value)
                {
                    _link = value;
                    IsInitialized = false;
                    RaisePropertyChanged(nameof(Link));
                    RaiseButtonsCommandsCanExecuteChanged();
                }
            }
        }

        private bool _isMuted;
        /// <summary>
        /// Gets or sets a value indicating whether the audio is muted
        /// </summary>
        public bool IsMuted
        {
            get { return _isMuted; }
            set
            {
                if (_isMuted != value)
                {
                    _isMuted = value;
                    RaisePropertyChanged(nameof(IsMuted));
                    Task.Run(SetIsMutedAsync);
                }
            }
        }

        public float _volume = 1;
        /// <summary>
        /// Gets or sets the volume
        /// </summary>
        public float Volume
        {
            get { return _volume; }
            set
            {
                if (_volume != value)
                {
                    if (value > _volume)
                    {
                        IsMuted = false;
                    }
                    _volume = value;
                    RaisePropertyChanged(nameof(Volume));
                    Task.Run(SetVolumeAsync);
                }
            }
        }

        private bool IsStopped
        {
            get
            {
                var mediaChannel = Sender.GetChannel<IMediaChannel>();
                return (mediaChannel.Status == null || !String.IsNullOrEmpty(mediaChannel.Status.FirstOrDefault()?.IdleReason));
            }
        }

        /// <summary>
        /// Get the play command
        /// </summary>
        public RelayCommand PlayCommand { get; }
        /// <summary>
        /// Gets the pause command
        /// </summary>
        public RelayCommand PauseCommand { get; }
        /// <summary>
        /// Gets the stop command
        /// </summary>
        public RelayCommand StopCommand { get; }
        /// <summary>
        /// Gets the refresh command
        /// </summary>
        public RelayCommand RefreshCommand { get; }

        private void RaiseButtonsCommandsCanExecuteChanged()
        {
            RaisePropertyChanged(nameof(AreButtonsEnabled));
            PlayCommand.RaiseCanExecuteChanged();
            PauseCommand.RaiseCanExecuteChanged();
            StopCommand.RaiseCanExecuteChanged();
        }

        private async Task Try(Func<Task> action)
        {
            try
            {
                await action();
            }
            catch (Exception ex)
            {
                PlayerState = ex.GetBaseException().Message;
                IsInitialized = false;
            }
        }

        private async Task InvokeAsync<TChannel>(Func<TChannel, Task> action) where TChannel : IChannel
        {
            if (action != null)
            {
                await action.Invoke(Sender.GetChannel<TChannel>());
            }
        }

        private async Task SendChannelCommandAsync<TChannel>(bool condition, Func<TChannel, Task> action, Func<TChannel, Task> otherwise) where TChannel : IChannel
        {
            await InvokeAsync<TChannel>(condition ? action : otherwise);
        }

        private async Task PlayAsync()
        {
            await SendChannelCommandAsync<IMediaChannel>(!IsInitialized || IsStopped,
                async c =>
                {
                    var selectedReceiver = SelectedReceiver;
                    var link = Link;
                    if (selectedReceiver != null && !string.IsNullOrWhiteSpace(link))
                    {
                        var sender = Sender;
                        await sender.ConnectAsync(selectedReceiver);
                        var mediaChannel = sender.GetChannel<IMediaChannel>();
                        await sender.LaunchAsync(mediaChannel);
                        await mediaChannel.LoadAsync(new Media() { ContentId = link, });
                        IsInitialized = true;
                    }
                }, c => c.PlayAsync());
        }

        private async Task PauseAsync()
        {
            await SendChannelCommandAsync<IMediaChannel>(IsStopped, null, async c => await c.PauseAsync());
        }

        private async Task StopAsync()
        {
            await SendChannelCommandAsync<IMediaChannel>(IsStopped, null, async c => await c.StopAsync());
        }

        private async Task RefreshAsync()
        {
            await DispatcherHelper.RunAsync(async () =>
            {
                IsLoaded = false;
                Receivers = await DeviceLocator.FindReceiversAsync();
                SelectedReceiver = Receivers.FirstOrDefault();
                IsLoaded = true;
            });
        }

        private async Task SetVolumeAsync()
        {
            await SendChannelCommandAsync<IReceiverChannel>(IsStopped, null, async c => await c.SetVolumeAsync(Volume));
        }

        private async Task SetIsMutedAsync()
        {
            await SendChannelCommandAsync<IReceiverChannel>(IsStopped, null, async c => await c.SetIsMutedAsync(IsMuted));
        }

        private void MediaChannelStatusChanged(object sender, EventArgs e)
        {
            var status = ((IMediaChannel)sender).Status?.FirstOrDefault();
            var playerState = status?.PlayerState;
            if (playerState == "IDLE" && !String.IsNullOrEmpty(status.IdleReason))
            {
                playerState = status.IdleReason;
            }
            PlayerState = playerState;
        }

        private void ReceiverChannelStatusChanged(object sender, EventArgs e)
        {
            if (!IsInitialized)
            {
                var status = ((IReceiverChannel)sender).Status;
                if (status.Volume.Level != null)
                {
                    Volume = (float)status.Volume.Level;
                }
                if (status.Volume.IsMuted != null)
                {
                    IsMuted = (bool)status.Volume.IsMuted;
                }
            }
        }
    }
}
