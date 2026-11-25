using System;
using System.Linq;
using Bit.App.Controls;
using Bit.Core.Models.View;
using Bit.Core.Utilities;
using Microsoft.Maui.Controls;
using Microsoft.Maui;

namespace Bit.App.Pages
{
    public partial class FoldersPage : BaseContentPage
    {
        private FoldersPageViewModel _vm;

        public FoldersPage()
        {
            InitializeComponent();
            SetActivityIndicator(_mainContent);
            _vm = BindingContext as FoldersPageViewModel;
            _vm.Page = this;
            // OAHU: the native screen sends row taps and its own add button (Android) to the view model, which
            // hands them back here so the page navigates as before.
            _vm.FolderSelected += folder => OpenFolderAsync(folder).FireAndForget();
            _vm.AddRequested += () => AddButton_Clicked(this, EventArgs.Empty);

            // TODO Xamarin.Forms.Device.RuntimePlatform is no longer supported. Use Microsoft.Maui.Devices.DeviceInfo.Platform instead. For more details see https://learn.microsoft.com/en-us/dotnet/maui/migration/forms-projects#device-changes
            if (Device.RuntimePlatform == Device.iOS)
            {
                ToolbarItems.Add(_closeItem);
                ToolbarItems.Add(_addItem);
            }
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await LoadOnAppearedAsync(_mainLayout, true, async () =>
            {
                await _vm.InitAsync();
            }, _mainContent);
        }

        private async Task OpenFolderAsync(FolderView folder)
        {
            if (!DoOnce())
            {
                return;
            }
            var page = new FolderAddEditPage(folder.Id);
            await Navigation.PushModalAsync(new NavigationPage(page));
        }

        private async void AddButton_Clicked(object sender, EventArgs e)
        {
            if (DoOnce())
            {
                var page = new FolderAddEditPage();
                await Navigation.PushModalAsync(new NavigationPage(page));
            }
        }

        private async void Close_Clicked(object sender, System.EventArgs e)
        {
            if (DoOnce())
            {
                await Navigation.PopModalAsync();
            }
        }
    }
}
