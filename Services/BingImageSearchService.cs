using System;
using System.Configuration;
using System.Net.Http;
using System.Threading.Tasks;
using CustomVisionEnd2End.Models;
using Newtonsoft.Json;

namespace CustomVisionEnd2End.Services
{
    public interface IBingImageSearchService : IDisposable
    {
        Task<BingImageSearchModel> ImageSearch(string searchQuery, int count = 30);
    }

    public class BingImageSearchService : IBingImageSearchService
    {

        /// <summary>
        ///     Performs a Bing Image search and return the results as a SearchResult.
        /// </summary>
        public async Task<BingImageSearchModel> ImageSearch(string searchQuery, int count = 30)
        {
            var subscriptionKey = ConfigurationManager.AppSettings["BingImageSearch_Key"];
            var uriBase = ConfigurationManager.AppSettings["BingImageSearch_Url"];

            if (subscriptionKey.Length != 32)
                throw new Exception("Invalid access key");
            //\\domain: www.wikipedia.org - &license=Public
            // Construct the URI of the search request

            var requestParameters = $"q=\"{Uri.EscapeDataString(searchQuery)}\"&count={count}&imageType=Photo&license=Public";

            var client = new HttpClient();

            // Request headers.
            client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", subscriptionKey);


            // Assemble the URI for the REST API Call.
            var uri = uriBase + "?" + requestParameters;


            var response = await client.GetAsync(uri);

            var json = await response.Content.ReadAsStringAsync();

            var result = JsonConvert.DeserializeObject<BingImageSearchModel>(json);


            return result;
        }

        public void Dispose()
        {

        }
    }
}
