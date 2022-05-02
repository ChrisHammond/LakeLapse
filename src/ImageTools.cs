using ImageMagick;

namespace OliverHine.LakeLapseBot
{
    internal class ImageTools
    {
        public static decimal SunriseRating(string file)
        {
            ImageMagick.IMagickColor<ushort> color;
            using (MagickImage image = new MagickImage(file))
            {
                image.Resize(128, 128);

                // Get average color
                return calcRedLevel(image);
            }
        }

        internal static decimal calcRedLevel(MagickImage img)
        {
            var inPercent = false;

            int redCount = 0;
            int totalPixels = (img.Width * img.Height);

            int[] color = new int[3];
            for (int y = 0; y < img.Height; y++)
            {
                var pc = img.GetPixels();
                int channelsCount = pc.Channels;
                var pcv = pc.GetValues();
                for (int x = 0; x < pcv.Length; x += channelsCount)
                {
                    color[0] = (pcv[x] / 255) - 2;
                    color[1] = (pcv[x + 1] / 255) - 2;
                    color[2] = (pcv[x + 2] / 255) - 2;

                    if (color[0] > 150 && (color[0] > color[1] && color[0] > color[2]))
                    {
                        //red?
                        redCount = redCount + 1;
                    }
                }
            }

            var outcome = (1 - (redCount / totalPixels)) * (inPercent ? 100 : 1);



            return outcome < 0 ? outcome * -1 : outcome;
        }

        public static void CreateMontage(ProgramSettings settings)
        {
            Console.WriteLine("Montage");
            var path = settings.savePath + settings.MontageFolderName;

            var files = new DirectoryInfo(path).GetFiles("*.jpg").OrderByDescending(i => i.FullName);
            string timestampForFile = settings.CurrentDateTime.ToString("yyyyMMdd");

            Console.WriteLine(files.Count());

            using (MagickImageCollection images = new MagickImageCollection())
            {
                foreach (var item in files)
                {
                    if (!item.Name.ToLower().StartsWith("montage"))
                    {
                        images.Add(item.FullName);
                    }
                }

                //we want full rows only
                if (images.Count % settings.MontageImagesPerRow == 0)
                {
                    MontageSettings ms = new MontageSettings();
                    ms.Geometry = new MagickGeometry(string.Format("{0}x{1}", 200, 113));
                    ms.TileGeometry = new MagickGeometry(string.Format("{0}x", settings.MontageImagesPerRow));

                    using (MagickImage montageResult = (MagickImage)images.Montage(ms))
                    {
                        montageResult.Write(path + Path.DirectorySeparatorChar.ToString() + "montage" + timestampForFile + ".jpg");
                    }
                }
                else
                {
                    Console.WriteLine("Incomplete row, trying again tomorrow.");
                }
            }
        }

        public static void downloadFrame(ProgramSettings settings)
        {
            var imagePath = settings.CameraJpgUrl;

            using (var webClient = new HttpClient())
            {
                byte[] dataArr = webClient.GetByteArrayAsync(imagePath).Result;

                var timestampForFile = DateTime.Now.ToString("yyyyMMdd-HHmmss");

                File.WriteAllBytes(string.Format(@"{0}snap-{1}.jpg", settings.savePathImage, timestampForFile), dataArr);

                if (settings.verbose) Console.Write(settings._photoInterval == settings.PhotoIntervalUpdated ? "." : string.Concat(settings._photoInterval / 1000, "."));
            }
        }

        public static void ProcessFiles(ProgramSettings settings)
        {
            var timestampForFile = settings.CurrentDateTime.ToString("yyyyMMdd");
            string[] fileEntries = Directory.GetFiles(settings.savePathImage, String.Format("snap-{0}-*.jpg", timestampForFile));

            string savePath = Path.Combine(settings.savePathImage, timestampForFile);

            if (!Directory.Exists(savePath))
            {
                Directory.CreateDirectory(savePath);
            }

            foreach (string fileName in fileEntries)
            {
                var newFileName = fileName.Replace(settings.savePathImage, (settings.savePathImage + timestampForFile + Path.DirectorySeparatorChar.ToString()));

                if (File.Exists(fileName))
                {
                    File.Move(fileName, newFileName);
                }
            }
        }

        public static int ProcessDailyVideo(ProgramSettings settings, int totalFrameCount)
        {
            string timestampForFile = settings.CurrentDateTime.ToString("yyyyMMdd");
            string savePath = settings.savePathImage + timestampForFile + Path.DirectorySeparatorChar.ToString();

            string command = @"ffmpeg";
            string commandArgs = string.Format(@"-r 24 -pattern_type glob -i ""{1}snap-{0}-*.jpg"" -s hd1080 -vcodec libx264 ""{2}daily{0}-{3}.mp4""", timestampForFile, savePath, settings.savePathImage, totalFrameCount);

            var proc = System.Diagnostics.Process.Start(command, commandArgs);

            proc.WaitForExit();

            return proc.ExitCode;
        }

        async public static Task PostDailyVideo(ProgramSettings settings, bool isLive)
        {
            var timestampForFile = settings.CurrentDateTime.ToString("yyyyMMdd");
            string savePath = settings.savePathImage + timestampForFile + Path.DirectorySeparatorChar.ToString();

            var tweetDateFormat = "M/d";
            var tweetTimeFormat = "h:mmtt";

            int totalFrameCount = Directory.GetFiles(savePath, String.Format("snap-{0}-*.jpg", timestampForFile)).Length;

            var videoFilename = string.Format("{2}daily{0}-{3}.mp4", timestampForFile, savePath, settings.savePathImage, totalFrameCount);

            if (settings.verbose) Console.WriteLine("Looking For: " + videoFilename);
            if (File.Exists(videoFilename))
            {
                var fileList = new DirectoryInfo(savePath).EnumerateFiles().OrderBy(f => f.CreationTime);

                var startTime = fileList.First().CreationTime.ToString(tweetTimeFormat).ToLower();
                var endTime = fileList.Last().CreationTime.ToString(tweetTimeFormat).ToLower();
                var displayDate = settings.CurrentDateTime.ToString(tweetDateFormat);

                var tweetMessage = String.Format(settings.TwitterTweet, displayDate, startTime, endTime, settings.sunrise.ToString(tweetTimeFormat).ToLower(), settings.sunset.ToString(tweetTimeFormat).ToLower());

                var userClient = new Tweetinvi.TwitterClient(settings.TwitterConsumerKey, settings.TwitterConsumerSecret, settings.TwitterAccessToken, settings.TwitterAccessSecret);

                var videoBinary = File.ReadAllBytes(videoFilename);

                if (isLive)
                {
                    if (settings.verbose) Console.Write("Uploading");

                    var uploadedVideo = await userClient.Upload.UploadTweetVideoAsync(videoBinary);

                    if (settings.verbose) Console.Write(". Processing");

                    //// IMPORTANT: you need to wait for Twitter to process the video
                    await userClient.Upload.WaitForMediaProcessingToGetAllMetadataAsync(uploadedVideo);
                    if (settings.verbose) Console.Write(". Tweeting");

                    var tweetWithVideo = await userClient.Tweets.PublishTweetAsync(new Tweetinvi.Parameters.PublishTweetParameters(tweetMessage)
                    {
                        Medias = { uploadedVideo }
                    });
                }

                if (settings.verbose) Console.Write(". Finished! " + tweetMessage);
            }
        }

        public static void GatherSunrisePhotos(ProgramSettings settings)
        {
            string timestampForFile = settings.CurrentDateTime.ToString("yyyyMMdd");
            string savePath = settings.savePathImage + timestampForFile + Path.DirectorySeparatorChar.ToString();

            if (settings.verbose)
            {
                Console.WriteLine("Sunrise: " + settings.sunrise.ToString());
                Console.WriteLine("Checking path: " + savePath);
            }

            if (Directory.Exists(savePath))
            {
                var files = new DirectoryInfo(savePath).GetFiles("*.jpg").OrderByDescending(i => i.LastWriteTime);

                var topRanked = string.Empty;
                decimal topRankedAmount = decimal.Zero;

                var rankings = string.Empty;

                foreach (var item in files)
                {
                    if (item.LastWriteTime >= settings.sunrise.AddMinutes(13) && item.LastWriteTime <= settings.sunrise.AddMinutes(14))
                    {
                        var redAmount = ImageTools.SunriseRating(item.FullName);

                        if (redAmount > topRankedAmount)
                        {
                            topRanked = item.FullName;
                            topRankedAmount = redAmount;
                        }

                        rankings = string.Concat(rankings, redAmount, ", ");
                    }

                }

                if (settings.verbose)
                {
                    Console.Write(topRankedAmount + " - " + topRanked + ". ");
                    Console.WriteLine(rankings);
                }

                if (!string.IsNullOrEmpty(topRanked) && File.Exists(topRanked))
                {
                    var montagePath = string.Concat(settings.savePath, settings.MontageFolderName);

                    if (!Directory.Exists(montagePath))
                    {
                        Directory.CreateDirectory(montagePath);
                    }

                    File.Copy(topRanked, montagePath + Path.DirectorySeparatorChar.ToString() + timestampForFile + ".jpg", true);
                }
            }

        }

    }
}
