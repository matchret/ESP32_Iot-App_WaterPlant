using WaterPlant_App.Services;


namespace WaterPlant_App
{
    public partial class MainPage : ContentPage
    {
        private int[] humidity = { 42, 65, 31, 55 };
        private int[] pumpDuration = { 1000, 1000, 1000, 1000 };
        private int[] minHumidity = { 30, 30, 30, 30 };
        private int[] targetHumidity = { 80, 80, 80, 80 };
        private bool[] plantEnabled = { false,false,false,false};

        private readonly AwsIotShadowService awsShadowService = new();
        private bool isLoadingFromAws = false;

        private bool hasPendingChanges = false;

        private DateTime? lastDeviceUpdate;

        private bool isUpdatingUi = false;

        public MainPage()
        {
            InitializeComponent();
            _ = InitializeAsync();
        }
        //--------------  Iot AWS  ----------------
        private void MarkPendingChanges()
        {
            hasPendingChanges = true;
            ApplyChangesButton.Text = "* Apply Changes *";

            UpdateButtonStates();
        }
        private async Task InitializeAsync()
        {
            UpdateUI(); // affiche les valeurs par défaut immédiatement
            await Task.Delay(1000);
            await LoadShadowAsync();

            MainContent.IsVisible = true;
            LoadingOverlay.IsVisible = false;

            StartPolling();
        }

        private void StartPolling()
        {
            Dispatcher.StartTimer(TimeSpan.FromSeconds(10), () =>
            {
                _ = LoadShadowAsync();
                return true; // continue looping
            });
        }
        private async Task LoadShadowAsync()
        {
            if (hasPendingChanges && !isLoadingFromAws)
                return;

            #if ANDROID || IOS
            if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
            {
                LastUpdateLabel.Text = "No internet connection";
                return;
            }
            #endif

            try
            {
                isLoadingFromAws = true;

                var state = await awsShadowService.GetPlantShadowStateAsync();

                humidity = state.Humidity;
                minHumidity = state.MinHumidity;
                targetHumidity = state.TargetHumidity;
                plantEnabled = state.PlantEnabled;
                pumpDuration = state.PumpDuration;
                lastDeviceUpdate = state.LastDeviceUpdate;

                UpdateUI();

                if (!hasPendingChanges)
                {
                    ApplyChangesButton.Text = "Apply Changes";
                    UpdateButtonStates();
                }
            }
            catch (Exception ex)
            {
                LastUpdateLabel.Text = "AWS sync failed. Retrying...";
                Console.WriteLine(ex.ToString());
            }
            finally
            {
                isLoadingFromAws = false;
            }
        }

        //-----------------  UI  ------------------
        private void UpdateUI()
        {
            isUpdatingUi = true;

            Humidity1Label.Text = $"Humidity: {humidity[0]}%";
            Humidity2Label.Text = $"Humidity: {humidity[1]}%";
            Humidity3Label.Text = $"Humidity: {humidity[2]}%";
            Humidity4Label.Text = $"Humidity: {humidity[3]}%";

            Humidity1Bar.Progress = humidity[0] / 100.0;
            Humidity2Bar.Progress = humidity[1] / 100.0;
            Humidity3Bar.Progress = humidity[2] / 100.0;
            Humidity4Bar.Progress = humidity[3] / 100.0;

            PumpDuration1Label.Text = $"Pump duration: {pumpDuration[0]} ms";
            PumpDuration2Label.Text = $"Pump duration: {pumpDuration[1]} ms";
            PumpDuration3Label.Text = $"Pump duration: {pumpDuration[2]} ms";
            PumpDuration4Label.Text = $"Pump duration: {pumpDuration[3]} ms";

            PumpDuration1Slider.Value = pumpDuration[0];
            PumpDuration2Slider.Value = pumpDuration[1];
            PumpDuration3Slider.Value = pumpDuration[2];
            PumpDuration4Slider.Value = pumpDuration[3];

            Plant1EnabledSwitch.IsToggled = plantEnabled[0];
            Plant2EnabledSwitch.IsToggled = plantEnabled[1];
            Plant3EnabledSwitch.IsToggled = plantEnabled[2];
            Plant4EnabledSwitch.IsToggled = plantEnabled[3];

            HumidityRange1Label.Text = $"Min: {minHumidity[0]}% | Target: {targetHumidity[0]}%";
            HumidityRange2Label.Text = $"Min: {minHumidity[1]}% | Target: {targetHumidity[1]}%";
            HumidityRange3Label.Text = $"Min: {minHumidity[2]}% | Target: {targetHumidity[2]}%";
            HumidityRange4Label.Text = $"Min: {minHumidity[3]}% | Target: {targetHumidity[3]}%";

            HumidityRange1Slider.RangeStart = minHumidity[0];
            HumidityRange1Slider.RangeEnd = targetHumidity[0];

            HumidityRange2Slider.RangeStart = minHumidity[1];
            HumidityRange2Slider.RangeEnd = targetHumidity[1];

            HumidityRange3Slider.RangeStart = minHumidity[2];
            HumidityRange3Slider.RangeEnd = targetHumidity[2];

            HumidityRange4Slider.RangeStart = minHumidity[3];
            HumidityRange4Slider.RangeEnd = targetHumidity[3];

            MasterControlSwitch.IsToggled = plantEnabled.All(x => x);

            LastUpdateLabel.Text = lastDeviceUpdate.HasValue
    ? $"Last IoT update: {lastDeviceUpdate:yyyy-MM-dd HH:mm:ss}"
    : "Last IoT update: Unknown";

            isUpdatingUi = false;
        }

        private void UpdateButtonStates()
        {
            ApplyChangesButton.IsEnabled = hasPendingChanges;
            DiscardChangesButton.IsEnabled = hasPendingChanges;

            ApplyChangesButton.BackgroundColor =
                hasPendingChanges
                ? Color.FromArgb("#28A745")
                : Colors.Gray;

            DiscardChangesButton.BackgroundColor =
                hasPendingChanges
                ? Color.FromArgb("#D9534F")
                : Colors.Gray;
        }

        private async void OnApplyChangesClicked(object sender, EventArgs e)
        {
            if (!hasPendingChanges)
                return;

            ApplyChangesButton.IsEnabled = false;
            ApplyChangesButton.Text = "Applying...";

            try
            {
                await awsShadowService.UpdateSettingsAsync(
                    minHumidity,
                    targetHumidity,
                    pumpDuration,
                    plantEnabled
                );

                hasPendingChanges = false;
                ApplyChangesButton.Text = "✓ Applied";

                UpdateButtonStates();

                await DisplayAlertAsync(
                    "Success",
                    "Settings sent to AWS.",
                    "OK");

                _ = Task.Run(async () =>
                {
                    await Task.Delay(1500);
                    await MainThread.InvokeOnMainThreadAsync(async () =>
                    {
                        await LoadShadowAsync();
                    });
                });
            }
            catch (Exception ex)
            {
                hasPendingChanges = true;
                ApplyChangesButton.IsEnabled = true;
                ApplyChangesButton.Text = "* Apply Changes *";

                await DisplayAlertAsync("AWS Error", ex.Message, "OK");
            }
        }

        private void OnPumpDuration1Changed(object sender, ValueChangedEventArgs e)
        {
            if (isLoadingFromAws || isUpdatingUi) return;

            pumpDuration[0] = (int)e.NewValue;

            PumpDuration1Label.Text = $"Pump duration: {pumpDuration[0]} ms";

            MarkPendingChanges();
        }
        private void OnPumpDuration2Changed(object sender, ValueChangedEventArgs e)
        {
            if (isLoadingFromAws || isUpdatingUi) return;

            pumpDuration[1] = (int)e.NewValue;

            PumpDuration2Label.Text = $"Pump duration: {pumpDuration[1]} ms";

            MarkPendingChanges();
        }

        private void OnPumpDuration3Changed(object sender, ValueChangedEventArgs e)
        {
            if (isLoadingFromAws || isUpdatingUi) return;

            pumpDuration[2] = (int)e.NewValue;

            PumpDuration3Label.Text = $"Pump duration: {pumpDuration[2]} ms";

            MarkPendingChanges();
        }

        private void OnPumpDuration4Changed(object sender, ValueChangedEventArgs e)
        {
            if (isLoadingFromAws || isUpdatingUi) return;

            pumpDuration[3] = (int)e.NewValue;

            PumpDuration4Label.Text = $"Pump duration: {pumpDuration[3]} ms";

            MarkPendingChanges();
        }

        private void OnPlant1EnabledToggled(object sender, ToggledEventArgs e)
        {
            if (isLoadingFromAws || isUpdatingUi) return;
            plantEnabled[0] = e.Value;
            MarkPendingChanges();
        }

        private void OnPlant2EnabledToggled(object sender, ToggledEventArgs e)
        {
            if (isLoadingFromAws || isUpdatingUi) return;
            plantEnabled[1] = e.Value;
            MarkPendingChanges();
        }

        private void OnPlant3EnabledToggled(object sender, ToggledEventArgs e)
        {
            if (isLoadingFromAws || isUpdatingUi) return;
            plantEnabled[2] = e.Value;
            MarkPendingChanges();
        }

        private void OnPlant4EnabledToggled(object sender, ToggledEventArgs e)
        {
            if (isLoadingFromAws || isUpdatingUi) return;
            plantEnabled[3] = e.Value;
            MarkPendingChanges();
        }

        private void OnHumidityRange1Changed(object sender, EventArgs e)
        {
            if (isLoadingFromAws || isUpdatingUi) return;

            minHumidity[0] = (int)HumidityRange1Slider.RangeStart;
            targetHumidity[0] = (int)HumidityRange1Slider.RangeEnd;

            HumidityRange1Label.Text = $"Min: {minHumidity[0]}% | Target: {targetHumidity[0]}%";

            MarkPendingChanges();
        }

        private void OnHumidityRange2Changed(object sender, EventArgs e)
        {
            if (isLoadingFromAws || isUpdatingUi) return;

            minHumidity[1] = (int)HumidityRange2Slider.RangeStart;
            targetHumidity[1] = (int)HumidityRange2Slider.RangeEnd;

            HumidityRange2Label.Text = $"Min: {minHumidity[1]}% | Target: {targetHumidity[1]}%";

            MarkPendingChanges();
        }

        private void OnHumidityRange3Changed(object sender, EventArgs e)
        {
            if (isLoadingFromAws || isUpdatingUi) return;

            minHumidity[2] = (int)HumidityRange3Slider.RangeStart;
            targetHumidity[2] = (int)HumidityRange3Slider.RangeEnd;

            HumidityRange3Label.Text = $"Min: {minHumidity[2]}% | Target: {targetHumidity[2]}%";

            MarkPendingChanges();
        }

        private void OnHumidityRange4Changed(object sender, EventArgs e)
        {
            if (isLoadingFromAws || isUpdatingUi) return;

            minHumidity[3] = (int)HumidityRange4Slider.RangeStart;
            targetHumidity[3] = (int)HumidityRange4Slider.RangeEnd;

            HumidityRange4Label.Text = $"Min: {minHumidity[3]}% | Target: {targetHumidity[3]}%";

            MarkPendingChanges();
        }
        private void OnMasterControlToggled(object sender, ToggledEventArgs e)
        {
            if (isLoadingFromAws || isUpdatingUi)
                return;

            for (int i = 0; i < 4; i++)
                plantEnabled[i] = e.Value;

            UpdateUI();
            MarkPendingChanges();
        }

        private async void WaterPlant(int index)
        {
            if (!plantEnabled[index])
            {
                await DisplayAlertAsync(
                    "Plant Disabled",
                    $"Plant {index + 1} is disabled.",
                    "OK");
                return;
            }

            bool confirm = await DisplayAlertAsync(
                "Manual Watering",
                $"Do you want to water Plant {index + 1}?",
                "Yes",
                "No");

            if (!confirm)
                return;

            try
            {
                await awsShadowService.WaterPlantAsync(index);

                await DisplayAlertAsync(
                    "Success",
                    $"Watering request sent for Plant {index + 1}.",
                    "OK");
            }
            catch (Exception ex)
            {
                await DisplayAlertAsync(
                    "Error",
                    $"Failed to send watering request.\n\n{ex.Message}",
                    "OK");
            }
        }

        private void OnWaterPlant1Clicked(object sender, EventArgs e)
            => WaterPlant(0);

        private void OnWaterPlant2Clicked(object sender, EventArgs e)
            => WaterPlant(1);

        private void OnWaterPlant3Clicked(object sender, EventArgs e)
            => WaterPlant(2);

        private void OnWaterPlant4Clicked(object sender, EventArgs e)
            => WaterPlant(3);

        private async void OnDiscardChangesClicked(object sender, EventArgs e)
        {
            try
            {
                hasPendingChanges = false;
                await LoadShadowAsync();
                ApplyChangesButton.Text = "Apply Changes";

                UpdateButtonStates();
            }
            catch (Exception ex)
            {
                await DisplayAlertAsync(
                    "Error",
                    ex.Message,
                    "OK");
            }
        }

        private void OnThemeClicked(object sender, EventArgs e)
        {
            Application.Current!.UserAppTheme =
                Application.Current.UserAppTheme == AppTheme.Dark
                ? AppTheme.Light
                : AppTheme.Dark;
        }
    }
}
