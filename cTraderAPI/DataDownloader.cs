using System;
using System.Collections.Generic;
using System.Net;
using Newtonsoft.Json;

namespace cTraderAPI
{
    public delegate void PriceDataDownloadedEventHandler(object sender, DataDownloaderEventArgs e);

    public class DataDownloader
    {
        public PriceDataDownloadedEventHandler OnComplete;
        public ErrorHandler OnError;
        public int BarPeriod { get; set; }
        public string AssetName { get; set; }
        public UserConfig Config { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public object PostRunStrategies { get; set; }


        public DataDownloader(UserConfig config, string assetName, int barPeriod, DateTime startDate, DateTime endDate, PriceDataDownloadedEventHandler onComplete)
        {
            OnComplete = onComplete;
            AssetName = assetName;
            BarPeriod = barPeriod;
            Config = config;
            StartDate = startDate;
            EndDate = endDate;
        }

        //request a download of minute based data
        public void GetPriceDataAsync()
        {
            //5000 is the maximum number of trendbars returned.
            //To and from tiem pattern YYYYMMDDHHMMSS
            //Dates are in UTC 0


            string timeframe = "m1";
            if (BarPeriod < 60)
                timeframe = "m" + BarPeriod;
            else if (BarPeriod >= 60 && BarPeriod < 1440)
                timeframe = "h" + (int)(BarPeriod / 60);
            else
                timeframe = "d" + (int)(BarPeriod / 1440);

            //compile the url to retrieve the price data from
            string start = StartDate.ToString("yyyyMMddHHmmss");
            string end = EndDate.ToString("yyyyMMddHHmmss");
            string url = string.Format("https://api.spotware.com/connect/tradingaccounts/{0}/symbols/{1}/trendbars/{2}?from={3}&to={4}&oauth_token={5}",
                Config.AccountId, AssetName, timeframe, start, end, Config.Token);

            using (WebClient wc = new WebClient())
            {
                wc.DownloadStringCompleted += new DownloadStringCompletedEventHandler(DownloadStringCompleted);
                wc.DownloadStringAsync(new Uri(url));
        

            }

        }

        void DownloadStringCompleted(object sender, DownloadStringCompletedEventArgs e)
        {
            if (e.Error != null)
            {
                OnError?.Invoke("Can't download price data: " + e.Error.Message);
            }
            else
            {
                DataDownloaderEventArgs args = new DataDownloaderEventArgs();

                args.BarData = JsonConvert.DeserializeObject<OpenAPIBarData>(e.Result);                
                args.Timeframe = BarPeriod;
                args.AssetName = AssetName;
                args.PostRunStrategies = PostRunStrategies;
                OnComplete?.Invoke(this, args);
            }
        }
    }
}
