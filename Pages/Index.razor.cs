using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using System.Net.Http.Json;
using System.Web;
using System.Collections.Specialized;

using MudBlazor;

using RandomDataApp.Models;

namespace RandomDataApp.Pages
{
    public partial class Index : ComponentBase
    {
        public string SelectedRegion { get; set; } = default!;
        public double ErrorsCount { get; set; } = 0.0;
        public int Seed { get; set; } = 0;

        [Inject]
        IJSRuntime JS { get; set; } = default!;

        [Inject]
        HttpClient httpClient { get; set; } = default!;

        [Inject]
        ISnackbar Snackbar { get; set; } = default!;

        private int Page { get; set; } = 0;
        private int Limit { get; set; } = 10;

        private bool Loading { get; set; } = false;

        public class Region
        {

            public const string USA = "The United States of America (English language)";
            public const string Spain = "España (Idioma español)";
            public const string Sweden = "Sverige (Svenska)";
        }

        private List<RandomUserData> Users = new List<RandomUserData>();

        private bool UsersEmpty => Users.Count() == 0;

        private void ResetOffset()
        {
            Page = 0;
        }

        private void ResetUsers()
        {
            Users = new List<RandomUserData>();
        }

        private async Task ResetData()
        {
            if (Loading)
            {
                return;
            }
            ResetUsers();
            ResetOffset();
            await ValidateRegionAndGetRandomData();
        }

        public async Task OnRegionChange(string region)
        {
            SelectedRegion = region switch
            {
                Region.USA => "USA",
                Region.Spain => "Spain",
                Region.Sweden => "Sweden",
                _ => throw new ArgumentException("Invalid value for command", nameof(region)),
            };
            await ResetData();
        }

        private async Task ErrorsCountRangeChanged(double errorsCount)
        {
            ErrorsCount = errorsCount;
            await ResetData();

        }

        private async Task ErrorsCountInputChanged(double errorsCount)
        {
            ErrorsCount = errorsCount;
            await ResetData();
        }

        private async Task SeedValueChanged(int seed)
        {
            Seed = seed;
            await ResetData();
        }

        public async Task OnRandomClick(MouseEventArgs args)
        {
            var generatedSeed = new Random().Next(0, int.MaxValue);
            Seed = generatedSeed;
            await ResetData();
        }

        private NameValueCollection AddParams(string query)
        {
            var paramValues = HttpUtility.ParseQueryString(query);
            paramValues.Add("page", Page.ToString());
            paramValues.Add("limit", Limit.ToString());
            paramValues.Add("region", SelectedRegion);
            paramValues.Add("errorsCount", ErrorsCount.ToString().Replace(',', '.'));
            paramValues.Add("seed", Seed.ToString());
            return paramValues;
        }

        private Uri BuildRequestUri()
        {
            var uriBuilder = new UriBuilder($"{httpClient.BaseAddress}api/RandomData");
            var addedParams = AddParams(uriBuilder.Query);
            uriBuilder.Query = addedParams.ToString();
            return uriBuilder.Uri;
        }

        private async Task ValidateRegionAndGetRandomData()
        {
            if (SelectedRegion == null)
            {
                Snackbar.Add("Please select the region to get the values", Severity.Info);
                return;
            }
            await SetRandomData();
        }
        public async Task<List<RandomUserData>> GetRandomData()
        {
            Loading = true;
            var result = await httpClient.GetAsync(BuildRequestUri());
            if (result.IsSuccessStatusCode)
            {
                List<RandomUserData> users = await result.Content.ReadFromJsonAsync<List<RandomUserData>>() ?? new List<RandomUserData>();
                Page += 1;
                Loading = false;
                return users;
            }
            else
            {
                string msg = await result.Content.ReadAsStringAsync();
                Snackbar.Add(msg, Severity.Error);
                return new List<RandomUserData>();
            }
        }

        public async Task SetRandomData()
        {
            Users = await GetRandomData();
            var secondPageUsers = await GetRandomData();
            Users.AddRange(secondPageUsers);
        }

        private async Task OnScroll(ScrollEventArgs args)
        {
            if (args.FirstChildBoundingClientRect.Height - args.ScrollTop <= 500)
            {
                Users.AddRange(await GetRandomData());
            }
        }

        private async Task ExportToCSV(MouseEventArgs args)
        {
            var response = await httpClient.PostAsJsonAsync($"{httpClient.BaseAddress}api/RandomData/ExportToCSV", Users);
            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadAsByteArrayAsync();
                await JS.InvokeVoidAsync("downloadCSV", data, SelectedRegion, Seed, ErrorsCount);
            }
            else
            {
                Snackbar.Add("An error occurred while saving the file", Severity.Error);
            }
        }
    }
}