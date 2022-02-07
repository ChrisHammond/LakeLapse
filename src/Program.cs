
using OliverHine.LakeLapseBot;

using System.CommandLine;

var settings = new ProgramSettings();

var alwaysCaptureOption = new Option<bool>("--always-capture", description: "capture images 24/7, default is to capture images during daylight hours and pause at night.");
var montageOption = new Option<bool>("--montage");
var generateVideoOption = new Option<bool>("--generate-video");
var tweetOption = new Option<bool>("--post-tweet");


var dryRunOption = new Option<bool>("--dry-run");
var verboseOption = new Option<bool>("--verbose");
var dateOption = new Option<string>(
        "--date",
        getDefaultValue: () => DateTime.Now.ToShortDateString(),
        description: "Override the date, default is today."); 


verboseOption.AddAlias("-v");





var rootCommand = new RootCommand
{
    dateOption,

    alwaysCaptureOption,
    montageOption,
    generateVideoOption,
    tweetOption,

    dryRunOption,
    verboseOption
};


rootCommand.Description = "Lake Lapse Bot by Oliver Hine";

rootCommand.SetHandler((string d, bool ac, bool m, bool tl, bool t, bool dr, bool v) => StartApplication(d, ac, m, tl, t, dr, v), dateOption, alwaysCaptureOption, montageOption, generateVideoOption, tweetOption, dryRunOption, verboseOption);

// Parse the incoming args and invoke the handler
return rootCommand.Invoke(args);




void StartApplication(string startDate, bool alwaysCapture, bool montage, bool outputVideo, bool tweet, bool dryRun, bool verbose)
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
        if (updateCurrentTime) settings.CurrentDateTime = DateTime.Now;

        Thread.Sleep(1000);

        if (settings.currentDayOfMonth != settings.CurrentDateTime.Day)
        {
            calcSunTimes();
        }
    }
}


void setTimer()
{
    var timerNeedsReset = false;
    var timerInProcessingMode = false;

    if (((settings.CurrentDateTime > settings.sunset.AddMinutes(settings.StopAfterSunset) && settings.CurrentDateTime < settings.sunrise.AddDays(1).AddMinutes(settings.StartBeforeSunrise * -1))
        || settings.CurrentDateTime < settings.sunrise.AddMinutes(settings.StartBeforeSunrise * -1))
                && settings._photoInterval != settings.photodelayProcessing || settings.timerNeedsResetting)
    {
        if (settings.theTimer != null) settings.theTimer.Dispose();

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
            //_photoInterval = PhotoIntervalUpdated;
            settings.boolFirstRun = false;
        }

        if (settings.theTimer != null) settings.theTimer.Dispose(); //cancel teh timer. 

        settings.theTimer = new Timer(callback: timerInProcessingMode ? ProcessImagesTask : DownloadImagesTask, state: 5, dueTime: settings._photoInterval, period: settings.PhotoInterval);

        if (settings.verbose) Console.WriteLine("switched to: " + settings.PhotoIntervalUpdated + ":" + settings._photoInterval + ":" + settings.CurrentDateTime.ToLongTimeString());
    }
    //if (theTimer != null) theTimer.Dispose(); //cancel teh timer. 
    //theTimer = new Timer(ComputeBoundOp, 5, 0, PhotoInterval);
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
            Console.WriteLine("PFrames: " + processedframeCount);
            Console.WriteLine("UFrames: " + unprocessedframeCount);
        }

        if (!File.Exists(string.Format("{2}daily{0}-{3}.mp4", timestampForFile, savePath, settings.savePathImage, processedframeCount)) && unprocessedframeCount > 250)
        {
            if (settings.theTimer != null) settings.theTimer.Dispose(); //cancel the timer.

            ImageTools.ProcessFiles(settings);

            int totalFrameCount = Directory.GetFiles(savePath, String.Format("snap-{0}-*.jpg", timestampForFile)).Length;

            if (settings.verbose) Console.Write("-");
            ImageTools.ProcessDailyVideo(settings, totalFrameCount);

            if (settings.verbose) Console.WriteLine("Video Fully Processed.");
            settings.timerNeedsResetting = true;

            await ImageTools.PostDailyVideo(settings, true);

            ImageTools.GatherSunrisePhotos(settings);
            ImageTools.CreateMontage(settings);
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
        Console.WriteLine();
    }
}




