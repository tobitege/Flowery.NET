using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Flowery.Controls;
using Flowery.Controls.Custom.Weather;
using Flowery.Controls.Custom.Weather.Models;
using Flowery.Controls.Custom.Weather.Services;

namespace Flowery.NET.Gallery.Examples;

public partial class CustomControls : UserControl, IScrollableExample
{
    private OpenWeatherMapService? _weatherService;
    private Dictionary<string, Visual>? _sectionTargetsById;

    public CustomControls()
    {
        InitializeComponent();
        InitializeForecastData();
    }

    private void InitializeForecastData()
    {
        var fullCard = this.FindControl<DaisyWeatherCard>("FullWeatherCard");
        if (fullCard != null)
        {
            fullCard.Forecast = new List<ForecastDay>
            {
                new ForecastDay { Date = new DateTime(2025, 7, 16), Condition = WeatherCondition.PartlyCloudy, HighTemperature = 16, LowTemperature = 8 },
                new ForecastDay { Date = new DateTime(2025, 7, 17), Condition = WeatherCondition.Rain, HighTemperature = 15, LowTemperature = 9 },
                new ForecastDay { Date = new DateTime(2025, 7, 18), Condition = WeatherCondition.Sunny, HighTemperature = 15, LowTemperature = 5 },
                new ForecastDay { Date = new DateTime(2025, 7, 19), Condition = WeatherCondition.Sunny, HighTemperature = 15, LowTemperature = 6 },
                new ForecastDay { Date = new DateTime(2025, 7, 20), Condition = WeatherCondition.PartlyCloudy, HighTemperature = 15, LowTemperature = 7 }
            };
        }

        var compactCard = this.FindControl<DaisyWeatherCard>("CompactWeatherCard");
        if (compactCard != null)
        {
            compactCard.Forecast = new List<ForecastDay>
            {
                new ForecastDay { Date = new DateTime(2025, 7, 16), Condition = WeatherCondition.Sunny, HighTemperature = 24, LowTemperature = 18 },
                new ForecastDay { Date = new DateTime(2025, 7, 17), Condition = WeatherCondition.PartlyCloudy, HighTemperature = 23, LowTemperature = 17 },
                new ForecastDay { Date = new DateTime(2025, 7, 18), Condition = WeatherCondition.Cloudy, HighTemperature = 21, LowTemperature = 16 }
            };
        }

        var forecastStrip = this.FindControl<DaisyWeatherForecast>("ForecastStrip");
        if (forecastStrip != null)
        {
            forecastStrip.ItemsSource = new List<ForecastDay>
            {
                new ForecastDay { Date = new DateTime(2025, 7, 16), Condition = WeatherCondition.PartlyCloudy, HighTemperature = 16, LowTemperature = 8 },
                new ForecastDay { Date = new DateTime(2025, 7, 17), Condition = WeatherCondition.Rain, HighTemperature = 15, LowTemperature = 9 },
                new ForecastDay { Date = new DateTime(2025, 7, 18), Condition = WeatherCondition.Thunderstorm, HighTemperature = 14, LowTemperature = 10 },
                new ForecastDay { Date = new DateTime(2025, 7, 19), Condition = WeatherCondition.Sunny, HighTemperature = 18, LowTemperature = 11 },
                new ForecastDay { Date = new DateTime(2025, 7, 20), Condition = WeatherCondition.Sunny, HighTemperature = 20, LowTemperature = 12 },
                new ForecastDay { Date = new DateTime(2025, 7, 21), Condition = WeatherCondition.PartlyCloudy, HighTemperature = 19, LowTemperature = 13 }
            };
        }
    }

    public void ScrollToSection(string sectionName)
    {
        var scrollViewer = this.FindControl<ScrollViewer>("MainScrollViewer");
        if (scrollViewer == null) return;

        var target = GetSectionTarget(sectionName);
        if (target == null) return;

        var transform = target.TransformToVisual(scrollViewer);
        if (transform.HasValue)
        {
            var point = transform.Value.Transform(new Point(0, 0));
            // Add current scroll offset to get absolute position in content
            scrollViewer.Offset = new Vector(0, point.Y + scrollViewer.Offset.Y);
        }
    }

    private Visual? GetSectionTarget(string sectionId)
    {
        if (_sectionTargetsById == null)
        {
            _sectionTargetsById = new Dictionary<string, Visual>(StringComparer.OrdinalIgnoreCase);
            foreach (var header in this.GetVisualDescendants().OfType<SectionHeader>())
            {
                if (!string.IsNullOrWhiteSpace(header.SectionId))
                    _sectionTargetsById[header.SectionId] = header.Parent as Visual ?? header;
            }
        }

        return _sectionTargetsById.TryGetValue(sectionId, out var target) ? target : null;
    }

    private async void FetchWeatherBtn_Click(object? sender, RoutedEventArgs e)
    {
        var apiKeyInput = this.FindControl<DaisyInput>("ApiKeyInput");
        var locationInput = this.FindControl<DaisyInput>("LocationInput");
        var statusAlert = this.FindControl<DaisyAlert>("StatusAlert");
        var statusMessage = this.FindControl<TextBlock>("StatusMessage");
        var liveResultPanel = this.FindControl<StackPanel>("LiveResultPanel");
        var liveWeatherCard = this.FindControl<DaisyWeatherCard>("LiveWeatherCard");
        var fetchBtn = this.FindControl<DaisyButton>("FetchWeatherBtn");

        if (apiKeyInput == null || locationInput == null || statusAlert == null ||
            statusMessage == null || liveResultPanel == null || liveWeatherCard == null)
            return;

        var apiKey = apiKeyInput.Text?.Trim();
        var location = locationInput.Text?.Trim();

        if (string.IsNullOrEmpty(apiKey))
        {
            ShowStatus(statusAlert, statusMessage, "Please enter your OpenWeatherMap API key.", DaisyAlertVariant.Warning);
            return;
        }

        if (string.IsNullOrEmpty(location))
        {
            ShowStatus(statusAlert, statusMessage, "Please enter a location (e.g., London,UK).", DaisyAlertVariant.Warning);
            return;
        }

        try
        {
            if (fetchBtn != null) fetchBtn.IsEnabled = false;
            ShowStatus(statusAlert, statusMessage, $"Fetching weather for {location}...", DaisyAlertVariant.Info);

            _weatherService = new OpenWeatherMapService(apiKey);

            var currentWeather = await _weatherService.GetCurrentWeatherAsync(location);
            var forecast = await _weatherService.GetForecastAsync(location, 5);
            var metrics = await _weatherService.GetMetricsAsync(location);

            liveWeatherCard.Temperature = currentWeather.Temperature;
            liveWeatherCard.FeelsLike = currentWeather.FeelsLike;
            liveWeatherCard.Condition = currentWeather.Condition;
            liveWeatherCard.Date = currentWeather.Date;
            liveWeatherCard.Sunrise = currentWeather.Sunrise;
            liveWeatherCard.Sunset = currentWeather.Sunset;
            liveWeatherCard.Forecast = forecast.ToList();
            liveWeatherCard.UvIndex = metrics.UvIndex;
            liveWeatherCard.UvMax = metrics.UvMax;
            liveWeatherCard.WindSpeed = metrics.WindSpeed;
            liveWeatherCard.WindMax = metrics.WindMax;
            liveWeatherCard.WindUnit = metrics.WindUnit;
            liveWeatherCard.Humidity = metrics.Humidity;
            liveWeatherCard.HumidityMax = metrics.HumidityMax;

            liveResultPanel.IsVisible = true;
            ShowStatus(statusAlert, statusMessage, $"Successfully fetched weather for {currentWeather.Location ?? location}!", DaisyAlertVariant.Success);
        }
        catch (Exception ex)
        {
            liveResultPanel.IsVisible = false;
            var errorMsg = ex.Message;

            if (ex.Message.Contains("401") || ex.Message.Contains("Invalid API key"))
                errorMsg = "Invalid API key. New keys can take up to 2 hours to activate. Please wait and try again.";
            else if (ex.Message.Contains("404") || ex.Message.Contains("city not found"))
                errorMsg = $"Location '{location}' not found. Try format: 'City,CountryCode' (e.g., 'London,GB').";
            else if (ex.Message.Contains("429"))
                errorMsg = "API rate limit exceeded. Please wait a minute and try again.";

            ShowStatus(statusAlert, statusMessage, errorMsg, DaisyAlertVariant.Error);
        }
        finally
        {
            if (fetchBtn != null) fetchBtn.IsEnabled = true;
        }
    }

    private async void TestApiKeyBtn_Click(object? sender, RoutedEventArgs e)
    {
        var apiKeyInput = this.FindControl<DaisyInput>("ApiKeyInput");
        var statusAlert = this.FindControl<DaisyAlert>("StatusAlert");
        var statusMessage = this.FindControl<TextBlock>("StatusMessage");
        var testBtn = this.FindControl<DaisyButton>("TestApiKeyBtn");

        if (apiKeyInput == null || statusAlert == null || statusMessage == null)
            return;

        var apiKey = apiKeyInput.Text?.Trim();

        if (string.IsNullOrEmpty(apiKey))
        {
            ShowStatus(statusAlert, statusMessage, "Please enter your API key first.", DaisyAlertVariant.Warning);
            return;
        }

        try
        {
            if (testBtn != null) testBtn.IsEnabled = false;
            ShowStatus(statusAlert, statusMessage, "Testing API key...", DaisyAlertVariant.Info);

            using var httpClient = new System.Net.Http.HttpClient();
            var testUrl = $"https://api.openweathermap.org/data/2.5/weather?q=London&appid={apiKey}";
            var response = await httpClient.GetAsync(testUrl);
            var content = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                ShowStatus(statusAlert, statusMessage, "API key is valid and active!", DaisyAlertVariant.Success);
            }
            else if ((int)response.StatusCode == 401)
            {
                ShowStatus(statusAlert, statusMessage, "API key is not yet active. New keys take up to 2 hours to activate.", DaisyAlertVariant.Warning);
            }
            else
            {
                ShowStatus(statusAlert, statusMessage, $"API returned: {content}", DaisyAlertVariant.Error);
            }
        }
        catch (Exception ex)
        {
            ShowStatus(statusAlert, statusMessage, $"Connection error: {ex.Message}", DaisyAlertVariant.Error);
        }
        finally
        {
            if (testBtn != null) testBtn.IsEnabled = true;
        }
    }

    private void ClearResultsBtn_Click(object? sender, RoutedEventArgs e)
    {
        var statusAlert = this.FindControl<DaisyAlert>("StatusAlert");
        var liveResultPanel = this.FindControl<StackPanel>("LiveResultPanel");

        if (statusAlert != null) statusAlert.IsVisible = false;
        if (liveResultPanel != null) liveResultPanel.IsVisible = false;
    }

    private void LocationInput_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            FetchWeatherBtn_Click(sender, e);
        }
    }

    private static void ShowStatus(DaisyAlert alert, TextBlock message, string text, DaisyAlertVariant variant)
    {
        message.Text = text;
        alert.Variant = variant;
        alert.IsVisible = true;
    }
}
