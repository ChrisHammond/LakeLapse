using System.Configuration;

namespace OliverHine.LakeLapseBot
{
    class ProgramSettings
    {
        public bool verbose = false;

        public double latitude = double.Parse(ConfigurationManager.AppSettings["Latitude"]);
        public double longitude = double.Parse(ConfigurationManager.AppSettings["Longitude"]);
        public string CameraJpgUrl = ConfigurationManager.AppSettings["CameraJpgUrl"];

        public string savePath = ConfigurationManager.AppSettings["SavePath"];
        public string savePathImage = ConfigurationManager.AppSettings["SavePathImage"];

        public string TwitterConsumerKey = ConfigurationManager.AppSettings["TwitterConsumerKey"];
        public string TwitterConsumerSecret = ConfigurationManager.AppSettings["TwitterConsumerSecret"];
        public string TwitterAccessToken = ConfigurationManager.AppSettings["TwitterAccessToken"];
        public string TwitterAccessSecret = ConfigurationManager.AppSettings["TwitterAccessSecret"];

        public string TwitterTweet = ConfigurationManager.AppSettings["TwitterTweet"];

        public string MontageFolderName = "DailySunrise";

        public DateTime date = DateTime.Today;
        public DateTime CurrentDateTime = DateTime.Now;
        public int currentDayOfMonth = -1;

        //note: even numbers required due to ramping function stepping in 2s intervals. 
        public int photodelayProcessing = 90 * 1000;
        public int photodelayNight = 60 * 1000;
        public int photodelayDay = 40 * 1000;
        public int photodelaySunrise = 10 * 1000;
        public int photodelaySunset = 20 * 1000;

        public int preSunrise = -75;
        public int postSunrise = 35;

        public int preSunset = -75;
        public int postSunset = 5;

        public int StopAfterSunset = 60;
        public int StartBeforeSunrise = 70;

        public int MontageImagesPerRow = 7;

        public Timer? theTimer;


        

        public DateTime sunrise;
        public DateTime sunset;

        public bool boolFirstRun = true;
        public bool timerNeedsResetting = false;

        public int _photoInterval;
        public int PhotoInterval
        {
            get
            {
                if (PhotoIntervalUpdated > _photoInterval)
                {
                    _photoInterval = _photoInterval + 2000;
                }
                if (PhotoIntervalUpdated < _photoInterval)
                {
                    _photoInterval = _photoInterval - 2000;
                }

                return _photoInterval;
            }
            set
            {
                _photoInterval = value;
            }
        }

        public int PhotoIntervalUpdated
        {
            get; set;
        }
        
    }
}
