
using OliverHine.LakeLapseBot;

using System.CommandLine;

var settings = new ProgramSettings();

var alwaysCaptureOption = new Option<bool>("--always-capture", description: "capture images 24/7, default is to capture images during daylight hours and pause at night.");
var montageOption = new Option<bool>("--montage", description: "update montage image, when --date is provided it will step in 1 day increments. otherwise it will just process today.");
var forceMontageOption = new Option<bool>("--force-montage", description: "allow incomplete rows");
var generateVideoOption = new Option<bool>("--generate-video");
var tweetOption = new Option<bool>("--post-tweet");
var instagramOption = new Option<bool>("--post-instagram");

var dryRunOption = new Option<bool>("--dry-run");
var verboseOption = new Option<bool>("--verbose");
var dateOption = new Option<string>(
        "--date",
        getDefaultValue: () => DateTime.Now.ToShortDateString(),
        description: "Override the date, default is today.");

var showConfigOption = new Option<bool>("--show-config", description: "Override the path, default is read from the config file.");

verboseOption.AddAlias("-v");





var rootCommand = new RootCommand
{
    dateOption,

    alwaysCaptureOption,
    montageOption,
    forceMontageOption,
    generateVideoOption,
    tweetOption,
    instagramOption,
    showConfigOption,
    dryRunOption,
    verboseOption
};


rootCommand.Description = "Lake Lapse Bot by Oliver Hine";

rootCommand.SetHandler((string d, bool ac, bool m, bool fm, bool tl, bool t, bool i, bool sc, bool dr, bool v) => StartApplication(d, ac, m, fm, tl, t, i, sc, dr, v), dateOption, alwaysCaptureOption, montageOption, forceMontageOption, generateVideoOption, tweetOption, instagramOption, showConfigOption, dryRunOption, verboseOption);

// Parse the incoming args and invoke the handler
return rootCommand.Invoke(args);


void StartApplication(string startDate, bool alwaysCapture, bool montage, bool forceMontage, bool outputVideo, bool tweet, bool instagram, bool showConfig, bool dryRun, bool verbose)
{
    if (showConfig)
    {
        Console.WriteLine("Showing Configuration");
        Console.WriteLine("Path: " + settings.savePath);
    }
    else
    {
        DateOnly dateOnly = DateOnly.FromDateTime(DateTime.Now);

        DateOnly.TryParse(startDate, out dateOnly);

        var updateCurrentTime = false;
        if (dateOnly == DateOnly.FromDateTime(DateTime.Now))
        {
            updateCurrentTime = true;
            settings.CurrentDateTime = dateOnly.ToDateTime(new TimeOnly(DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second));
        }
        else
        {
            settings.CurrentDateTime = dateOnly.ToDateTime(new TimeOnly(0, 0, 0));
        }


        if (verbose)
        {
            Console.WriteLine("Starting Application with a start date of: " + settings.CurrentDateTime.ToShortDateString());
        }

        settings.verbose = verbose;


        calcSunTimes();

        if (!dryRun)
        {
            setTimer();
        }

        for (; ; )
        {
            if (updateCurrentTime)
            {
                settings.CurrentDateTime = DateTime.Now;
            }
            else if (montage && settings.CurrentDateTime.Date <= DateTime.Now.Date)
            {
                Console.WriteLine("Montage");
                Console.WriteLine(settings.CurrentDateTime.Date);
                ImageTools.GatherSunrisePhotos(settings);

                if (settings.CurrentDateTime.Date == DateTime.Now.Date)
                {
                    Console.WriteLine(settings.CurrentDateTime.Date);
                    ImageTools.CreateMontage(settings, forceMontage);
                }

                settings.CurrentDateTime = settings.CurrentDateTime.AddDays(1);
            }
            else if (instagram && settings.CurrentDateTime.Date <= DateTime.Now.Date)
            {
                Console.WriteLine("instagram: not ready yet.");
                //Console.WriteLine(ImageTools.PostDailyVideoInstagram(settings, true));
            }

            Thread.Sleep(1000);

            if (settings.currentDayOfMonth != settings.CurrentDateTime.Day)
            {
                calcSunTimes();
            }
        }

    }
}

void cancelTimer()
{
    if (settings.theTimer != null) settings.theTimer.Dispose();
}

void setTimer()
{
    var timerNeedsReset = false;
    var timerInProcessingMode = false;

    if (((settings.CurrentDateTime > settings.sunset.AddMinutes(settings.StopAfterSunset) && settings.CurrentDateTime < settings.sunrise.AddDays(1).AddMinutes(settings.StartBeforeSunrise * -1))
        || settings.CurrentDateTime < settings.sunrise.AddMinutes(settings.StartBeforeSunrise * -1))
                && settings._photoInterval != settings.photodelayProcessing || settings.timerNeedsResetting)
    {
        cancelTimer();

        timerInProcessingMode = true;

        settings.PhotoIntervalUpdated = settings.photodelayProcessing;
        settings._photoInterval = settings.photodelayProcessing;
        timerNeedsReset = true;

        if (settings.verbose) Console.WriteLine("outside of daylight.");
    }

    if (settings.CurrentDateTime > settings.sunrise.AddMinutes(settings.postSunrise) && settings.CurrentDateTime < settings.sunset.AddMinutes(settings.preSunset) && settings.PhotoIntervalUpdated != settings.photodelayDay)
    {
        settings.PhotoIntervalUpdated = settings.photodelayDay;
        timerNeedsReset = true;
        if (settings.verbose) Console.WriteLine("needs switching to day: " + settings.photodelayDay);
    }

    if (settings.CurrentDateTime > settings.sunrise.AddMinutes(settings.preSunrise) && settings.CurrentDateTime < settings.sunrise.AddMinutes(settings.postSunrise) && settings.PhotoIntervalUpdated != settings.photodelaySunrise)
    {
        if (settings.PhotoIntervalUpdated > settings.photodelayNight)
        {
            settings.PhotoIntervalUpdated = settings.photodelayNight;
            settings._photoInterval = settings.photodelayNight;
        }
        else
        {
            settings.PhotoIntervalUpdated = settings.photodelaySunrise;
        }

        timerNeedsReset = true;
        if (settings.verbose) Console.WriteLine("needs switching to sunrise: " + settings.photodelaySunrise);
    }

    if (settings.CurrentDateTime > settings.sunset.AddMinutes(settings.preSunset) && settings.CurrentDateTime < settings.sunset.AddMinutes(settings.postSunset) && settings.PhotoIntervalUpdated != settings.photodelaySunset)
    {
        settings.PhotoIntervalUpdated = settings.photodelaySunset;
        timerNeedsReset = true;
        if (settings.verbose) Console.WriteLine("needs switching to sunset: " + settings.photodelaySunset);
    }

    if ((settings.CurrentDateTime > settings.sunset.AddMinutes(settings.postSunset) ||
            settings.CurrentDateTime < settings.sunrise.AddMinutes(settings.preSunrise)) &&
            settings.PhotoIntervalUpdated <= settings.photodelayNight &&
            settings.PhotoIntervalUpdated != settings.photodelayProcessing &&
            !timerNeedsReset &&
            !timerInProcessingMode)
    {
        if (settings.PhotoIntervalUpdated > settings.photodelayNight)
        {
            settings.PhotoIntervalUpdated = settings.photodelayNight;
            settings._photoInterval = settings.photodelayNight;
        }
        else
        {
            settings.PhotoIntervalUpdated = settings.photodelayNight;
        }

        timerNeedsReset = true;
        if (settings.verbose) Console.WriteLine("needs switching to night: " + settings.photodelayNight);
    }


    if (settings.PhotoIntervalUpdated != settings._photoInterval && timerNeedsReset == false)
    {
        settings.theTimer.Change(settings._photoInterval, settings.PhotoInterval);
    }

    if (timerNeedsReset == true || settings.boolFirstRun == true)
    {
        if (settings.boolFirstRun)
        {
            settings.boolFirstRun = false;
        }

        cancelTimer();

        settings.theTimer = new Timer(callback: timerInProcessingMode ? ProcessImagesTask : DownloadImagesTask, state: 5, dueTime: settings._photoInterval, period: settings.PhotoInterval);

        if (settings.verbose) Console.WriteLine("interval switched to " + (settings.PhotoIntervalUpdated / 1000) + ", it was " + (settings._photoInterval / 1000) + " @ " + settings.CurrentDateTime.ToLongTimeString());
    }
}

void DownloadImagesTask(Object? state)
{
    try
    {
        ImageTools.downloadFrame(settings);
    }
    catch (Exception err)
    {
        if (settings.verbose) Console.Write("!");

        Thread.Sleep(500);

        ImageTools.downloadFrame(settings);
    }

    setTimer();

    Thread.Sleep(500);
}

async void ProcessImagesTask(Object? state)
{
    settings.timerNeedsResetting = false;

    try
    {
        var timestampForFile = settings.CurrentDateTime.ToString("yyyyMMdd");
        string savePath = settings.savePathImage + timestampForFile + Path.DirectorySeparatorChar.ToString();

        int unprocessedframeCount = Directory.GetFiles(settings.savePathImage, String.Format("snap-{0}-*.jpg", timestampForFile)).Length;

        if (!Directory.Exists(savePath))
        {
            Directory.CreateDirectory(savePath);
        }

        int processedframeCount = Directory.GetFiles(savePath, String.Format("snap-{0}-*.jpg", timestampForFile)).Length;

        if (settings.verbose)
        {
            Console.WriteLine("Processed Frames: " + processedframeCount);
            Console.WriteLine(string.Concat(savePath, String.Format("snap-{0}-*.jpg", timestampForFile)));
            Console.WriteLine("Unprocessed Frames: " + unprocessedframeCount);
            Console.WriteLine(string.Concat(settings.savePathImage, String.Format("snap-{0}-*.jpg", timestampForFile)));
        }

        if (!File.Exists(string.Format("{2}daily{0}-{3}.mp4", timestampForFile, savePath, settings.savePathImage, processedframeCount)) && unprocessedframeCount > 250)
        {
            if (settings.theTimer != null) settings.theTimer.Dispose(); //cancel the timer.

            ImageTools.ProcessFiles(settings);

            int totalFrameCount = Directory.GetFiles(savePath, String.Format("snap-{0}-*.jpg", timestampForFile)).Length;

            ImageTools.ProcessDailyVideo(settings, totalFrameCount);

            if (settings.verbose) Console.WriteLine("Video Fully Processed.");
            settings.timerNeedsResetting = true;

            await ImageTools.PostDailyVideo(settings, true);

            //ImageTools.GatherSunrisePhotos(settings);
            //ImageTools.CreateMontage(settings);
        }
        else
        {
            if (settings.verbose) Console.Write("~");
        }
    }
    catch (Exception err)
    {
        if (settings.verbose) Console.Write("!");
        settings.timerNeedsResetting = true;
    }

    if (settings.currentDayOfMonth != DateTime.Now.Day)
    {
        calcSunTimes();
        settings.timerNeedsResetting = true;
    }

    if (settings.timerNeedsResetting || DateTime.Now > settings.sunrise.AddMinutes(settings.StartBeforeSunrise * -1))
    {
        setTimer();
    }
}


void calcSunTimes()
{
    if (settings.verbose)
    {
        Console.Write("Calculating important events for the day. ");
    }

    settings.currentDayOfMonth = settings.CurrentDateTime.Day;
    settings.date = new DateTime(settings.CurrentDateTime.Year, settings.CurrentDateTime.Month, settings.CurrentDateTime.Day, 12, 0, 0, DateTimeKind.Utc);

    var sunPosition = SunCalcNet.SunCalc.GetSunPosition(settings.date, settings.latitude, settings.longitude);
    var sunPhases = SunCalcNet.SunCalc.GetSunPhases(settings.date, settings.latitude, settings.longitude);

    var moonPhases = SunCalcNet.MoonCalc.GetMoonPhase(settings.date, settings.latitude, settings.longitude);

    foreach (var sunPhase in sunPhases)
    {
        switch (sunPhase.Name.Value.ToLower())
        {
            case "sunrise":
                settings.sunrise = sunPhase.PhaseTime.ToLocalTime();
                break;
            case "sunset":
                settings.sunset = sunPhase.PhaseTime.ToLocalTime();
                break;
            default:
                break;
        }
    }

    if (settings.verbose)
    {
        Console.Write(string.Format("Sunrise: {0} - Sunset: {1}", settings.sunrise, settings.sunset));
        if (moonPhases.AlwaysUp)
        {
            Console.Write("Moon is always visible.");
        }
        else if (moonPhases.AlwaysDown)
        {
            Console.Write("Moon isn't visible");
        }
        else if (moonPhases.Rise.HasValue && moonPhases.Set.HasValue)
        {

            var moonPostion = SunCalcNet.MoonCalc.GetMoonPosition(moonPhases.Rise.Value, settings.latitude, settings.longitude);

            if (moonPhases.Rise > moonPhases.Set)
            {
                var moonSetPhase = SunCalcNet.MoonCalc.GetMoonPhase(settings.date.AddDays(1), settings.latitude, settings.longitude);
                //(this * 180 / PI);
                Console.Write(string.Format("Moonrise: {0} - Moonset: {1} - Azimuth: {2}", moonPhases.Rise, moonSetPhase.Set, moonPostion.Azimuth * 180 / Math.PI));
            } else
            {
                Console.Write(string.Format("Moonrise: {0} - Moonset: {1} - Azimuth: {2}", moonPhases.Rise, moonPhases.Set, moonPostion.Azimuth * 180 / Math.PI));
            }
        }
        
        Console.WriteLine();
    }
}




