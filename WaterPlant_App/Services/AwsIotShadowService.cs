using Amazon;
using Amazon.IotData;
using Amazon.IotData.Model;
using Amazon.Runtime;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace WaterPlant_App.Services
{
    internal class AwsIotShadowService
    {
        private readonly AmazonIotDataClient _client;
        private readonly string _thingName = "";        //fill your thing name here

        public AwsIotShadowService()                    //fill needed credentials and endpoint info here
        {
            var credentials = new BasicAWSCredentials(
                "",
                ""
            );

            var config = new AmazonIotDataConfig
            {
                RegionEndpoint = RegionEndpoint.USEast2,
                ServiceURL = ""
            };

            _client = new AmazonIotDataClient(credentials, config);
        }

        public async Task UpdateSettingsAsync(
            int[] minHumidity,
            int[] targetHumidity,
            int[] pumpDuration,
            bool[] plantEnabled)
        {
            var shadow = new
            {
                state = new
                {
                    desired = new
                    {
                        minHumidity,
                        targetHumidity,
                        pumpDuration,
                        plantEnabled

                    }
                }
            };

            string json = JsonSerializer.Serialize(shadow);

            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

            var request = new UpdateThingShadowRequest
            {
                ThingName = _thingName,
                Payload = stream
            };

            await _client.UpdateThingShadowAsync(request);
        }

        public async Task<int[]> GetHumidityAsync()
        {
            var request = new GetThingShadowRequest
            {
                ThingName = _thingName
            };

            var response = await _client.GetThingShadowAsync(request);

            using var reader = new StreamReader(response.Payload);
            string json = await reader.ReadToEndAsync();

            using var doc = JsonDocument.Parse(json);

            var humidityJson = doc.RootElement
                .GetProperty("state")
                .GetProperty("reported")
                .GetProperty("humidity");


            return humidityJson
                .EnumerateArray()
                .Select(x => x.GetInt32())
                .ToArray();
        }

        public async Task WaterPlantAsync(int plantIndex)
        {
            var shadow = new
            {
                state = new
                {
                    desired = new
                    {
                        waterNow = plantIndex
                    }
                }
            };

            string json = JsonSerializer.Serialize(shadow);

            using var stream = new MemoryStream(
                Encoding.UTF8.GetBytes(json));

            await _client.UpdateThingShadowAsync(
                new UpdateThingShadowRequest
                {
                    ThingName = _thingName,
                    Payload = stream
                });
        }

        public class PlantShadowState
        {
            public DateTime? LastDeviceUpdate { get; set; }
            public int[] Humidity { get; set; } = [0, 0, 0, 0];
            public int[] MinHumidity { get; set; } = [35, 35, 35, 35];

            public int[] TargetHumidity { get; set; } = [80, 80, 80, 80];

            public bool[] PlantEnabled { get; set; } = { false, false, false, false};
            public int[] PumpDuration { get; set; } = [1000, 1000, 1000, 1000];
        }

        public async Task<PlantShadowState> GetPlantShadowStateAsync()
        {
            var request = new GetThingShadowRequest
            {
                ThingName = _thingName
            };

            var response = await _client.GetThingShadowAsync(request);

            using var reader = new StreamReader(response.Payload);
            string json = await reader.ReadToEndAsync();

            using var doc = JsonDocument.Parse(json);

            var state = new PlantShadowState();

            var rootState = doc.RootElement.GetProperty("state");

            var reported = rootState.GetProperty("reported");

            JsonElement settingsSource = reported;

            if (rootState.TryGetProperty("desired", out var desiredJson))
            {
                settingsSource = desiredJson;
            }

            if (doc.RootElement.TryGetProperty("metadata", out var metadata) &&
    metadata.TryGetProperty("reported", out var reportedMetadata))
            {
                long latestTimestamp = 0;

                foreach (var property in reportedMetadata.EnumerateObject())
                {
                    if (property.Value.ValueKind == JsonValueKind.Object)
                    {
                        if (property.Value.TryGetProperty("timestamp", out var timestampJson))
                        {
                            latestTimestamp = Math.Max(latestTimestamp, timestampJson.GetInt64());
                        }
                    }
                    else if (property.Value.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in property.Value.EnumerateArray())
                        {
                            if (item.ValueKind == JsonValueKind.Object &&
                                item.TryGetProperty("timestamp", out var timestampJson))
                            {
                                latestTimestamp = Math.Max(latestTimestamp, timestampJson.GetInt64());
                            }
                        }
                    }
                }

                if (latestTimestamp > 0)
                {
                    state.LastDeviceUpdate =
                        DateTimeOffset.FromUnixTimeSeconds(latestTimestamp)
                            .LocalDateTime;
                }
            }

            // Humidity should always come from reported
            if (reported.TryGetProperty("humidity", out var humidityJson))
            {
                state.Humidity = humidityJson
                    .EnumerateArray()
                    .Select(x => x.GetInt32())
                    .ToArray();
            }

            if (settingsSource.TryGetProperty("targetHumidity", out var targetHumidityJson))
            {
                state.TargetHumidity = targetHumidityJson
                    .EnumerateArray()
                    .Select(x => x.GetInt32())
                    .ToArray();
            }

            // Settings should come from desired if available, otherwise reported
            if (settingsSource.TryGetProperty("minHumidity", out var minHumidityJson))
            {
                state.MinHumidity = minHumidityJson
                    .EnumerateArray()
                    .Select(x => x.GetInt32())
                    .ToArray();
            }

            if (settingsSource.TryGetProperty("pumpDuration", out var pumpDurationJson))
            {
                state.PumpDuration = pumpDurationJson
                    .EnumerateArray()
                    .Select(x => x.GetInt32())
                    .ToArray();
            }

            if (settingsSource.TryGetProperty("plantEnabled", out var plantEnabledJson))
            {
                state.PlantEnabled = plantEnabledJson
                    .EnumerateArray()
                    .Select(x => x.GetBoolean())
                    .ToArray();
            }

            return state;
        }

    }
}
