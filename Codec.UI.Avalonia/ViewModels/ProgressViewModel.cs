// <copyright file="ProgressViewModel.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Codec.UI.Avalonia.ViewModels
{
    using System;
    using System.Threading;
    using CommunityToolkit.Mvvm.ComponentModel;
    using CommunityToolkit.Mvvm.Input;

    public partial class ProgressViewModel : ObservableObject, IDisposable
    {
        private readonly CancellationTokenSource cts = new();

        [ObservableProperty]
        private double progress;

        [ObservableProperty]
        private string? progressText;

        /// <summary>
        /// Gets the token that is cancelled when the user closes the window
        /// or clicks Cancel.
        /// </summary>
        public CancellationToken Cancel => this.cts.Token;

        [RelayCommand]
        private void CancelExport()
        {
            this.cts.Cancel();
        }

        public void Dispose()
        {
            this.cts.Dispose();
        }
    }
}
