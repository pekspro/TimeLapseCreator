using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TimeLapseCreator
{
    class Program
    {
        static async Task Main(string[] args)
        {
            await new VideoCreator().CreateVideoAsync();
        }
    }

    public class VideoCreator
    {
        private const string OriginalImagesPath = @".\video\original\";
        private const string FramesPath = @".\video\frames\";
        private const string TitleFramesPath = @".\video\titleframes\";
        private const string OutputPath = @".\video\";

        private const string FFmpgPath = @".\";
        private const string AssetsPath = @".\video\assets";

        private const string Title = "Hello world!";
        private const string SubTitle = "Have a nice day";

        private FontCollection MyFontCollection = new FontCollection();
        private FontFamily MyFontFamily = null;

        public const int FramesPerSecond = 6;

        public async Task CreateVideoAsync()
        {
            Stopwatch totalStopWatch = Stopwatch.StartNew();

            Directory.CreateDirectory(OriginalImagesPath);
            Directory.CreateDirectory(FramesPath);
            Directory.CreateDirectory(TitleFramesPath);
            Directory.CreateDirectory(AssetsPath);
            Directory.CreateDirectory(FFmpgPath);

            MyFontFamily ??= MyFontCollection.Install(
                            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "verdana.ttf")
                        );

            // Get all images that should be converted to a video.
            Console.WriteLine($"Getting images...");
            Stopwatch getImagesStopWatch = Stopwatch.StartNew();
            List<string> orgimages = await GetOriginalImagesAsync();
            Console.WriteLine($"Done. {getImagesStopWatch.Elapsed}");
            Console.WriteLine($"");

            // Read the first video to setup the dimensions.
            int width, weight;

            using (Image image = Image.Load(orgimages[0]))
            {
                width = image.Width;
                weight = image.Height;
            }

            // Create title images
            Console.WriteLine($"Creating title frames...");
            Stopwatch titlesStopWatch = Stopwatch.StartNew();
            var titleimages = await CreateTitleScreenAsync(width, weight);
            Console.WriteLine($"Done. {titlesStopWatch.Elapsed}");

            // Create overlays. This is the original images but with a
            // timestamp on each image. The first images will also be
            // fading from the last title image.
            Console.WriteLine();
            Console.WriteLine($"Creating frames...");
            Stopwatch overlaysStopWatch = Stopwatch.StartNew();
            var overlayimages = await CreateOverlayImagesAsync(orgimages, titleimages.Last());

            // Creat a pretty thumbnail image.
            string thumbnailImagePath = Path.Combine(OutputPath, "thumbnail.png");
            await CreateThumbnailImageAsync(orgimages[orgimages.Count / 2], thumbnailImagePath).ConfigureAwait(false);

            Console.WriteLine($"Done. {overlaysStopWatch.Elapsed}");


            Console.WriteLine();
            Console.WriteLine($"Rendering...");
            Console.WriteLine();

            // Combine all images
            List<string> images = new List<string>();
            images.AddRange(titleimages);
            images.AddRange(overlayimages);

            string audioPath = Path.Combine(AssetsPath, "background.mp3");
            Stopwatch renderStopWatch = Stopwatch.StartNew();

            // Render the video
            await RenderVideoAsync(FramesPerSecond, images,
                Path.Combine(FFmpgPath, "ffmpeg.exe"),
                audioPath,
                thumbnailImagePath,
                Path.Combine(OutputPath, "out.mp4")
                ).ConfigureAwait(false);

            Console.WriteLine();
            Console.WriteLine($"Done. {renderStopWatch.Elapsed}");

            Console.WriteLine();
            Console.WriteLine($"All steps completed in {totalStopWatch.Elapsed}");
        }

        // Render video from a list of images, add background audio and a thumbnail image.
        private async Task RenderVideoAsync(int framesPerSecond, List<string> images, string ffmpgPath,
                string audioPath, string thumbnailImagePath, string outPath,
                double audioFadeInDuration = 0, double audioFadeOutDuration = 0)
        {
            string fileListName = Path.Combine(OutputPath, "framelist.txt");
            var fileListContent = images.Select(a => $"file '{a}'{Environment.NewLine}duration 1");

            await File.WriteAllLinesAsync(fileListName, fileListContent);

            TimeSpan vidLengthCalc = TimeSpan.FromSeconds(images.Count / ((double)framesPerSecond));
            int coverId = -1;
            int audioId = -1;
            int framesId = 0;
            int nextId = 1;

            StringBuilder inputParameters = new StringBuilder();
            StringBuilder outputParameters = new StringBuilder();

            inputParameters.Append($"-r {framesPerSecond} -f concat -safe 0 -i {fileListName} ");

            outputParameters.Append($"-map {framesId} ");

            if (thumbnailImagePath != null)
            {
                coverId = nextId;
                nextId++;

                inputParameters.Append($"-i {thumbnailImagePath} ");

                outputParameters.Append($"-map {coverId} ");
                outputParameters.Append($"-c:v:{coverId} copy -disposition:v:{coverId} attached_pic ");
            }

            if (audioPath != null)
            {
                audioId = nextId;
                nextId++;

                inputParameters.Append($"-i {audioPath} ");
                outputParameters.Append($"-map {audioId} ");

                if(audioFadeInDuration <= 0 && audioFadeOutDuration <= 0)
                {
                    // If no audio fading, just copy as it is.
                    outputParameters.Append($"-c:a copy ");
                }
                else
                {
                    List<string> audioEffectList = new List<string>();
                    if(audioFadeInDuration > 0)
                    {
                        //Assume we fade in from first second.
                        audioEffectList.Add($"afade=in:start_time={0}s:duration={audioFadeInDuration.ToString("0", NumberFormatInfo.InvariantInfo)}s");
                    }

                    if (audioFadeInDuration > 0)
                    {
                        //Assume we fade out to last second.
                        audioEffectList.Add($"afade=out:start_time={(vidLengthCalc.TotalSeconds - audioFadeOutDuration).ToString("0.000", NumberFormatInfo.InvariantInfo)}s:duration={audioFadeInDuration.ToString("0.000", NumberFormatInfo.InvariantInfo)}s");
                    }

                    string audioFilterString = string.Join(',', audioEffectList);

                    outputParameters.Append($"-af \"{audioFilterString}\" ");
                }
            }

            int milliseconds = vidLengthCalc.Milliseconds;
            int seconds = vidLengthCalc.Seconds;
            int minutes = vidLengthCalc.Minutes;
            var hours = (int)vidLengthCalc.TotalHours;

            string durationString = $"{hours:D}:{minutes:D2}:{seconds:D2}.{milliseconds:D3}";

            outputParameters.Append($"-c:v:{framesId} libx264 -pix_fmt yuv420p -to {durationString} {outPath} -y ");
            
            string parameters = inputParameters.ToString() + outputParameters.ToString();

            try
            {
                await Task.Factory.StartNew(() =>
                {
                    var outputLog = new List<string>();

                    using (var process = new Process
                    {
                        StartInfo =
                        {
                        FileName = ffmpgPath,
                        Arguments = parameters,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        // ffmpeg send everything to the error output, standard output is not used.
                        RedirectStandardError = true
                        },
                        EnableRaisingEvents = true
                    })
                    {
                        process.ErrorDataReceived += (sender, e) =>
                        {
                            if (string.IsNullOrEmpty(e.Data))
                            {
                                return;
                            }

                            outputLog.Add(e.Data.ToString());
                            Console.WriteLine(e.Data.ToString());
                        };

                        process.Start();

                        process.BeginErrorReadLine();

                        process.WaitForExit();

                        if (process.ExitCode != 0)
                        {
                            throw new Exception($"ffmpeg failed error exit code {process.ExitCode}. Log: {string.Join(Environment.NewLine, outputLog)}");
                        }
                        Console.WriteLine($"Exit code: {process.ExitCode}");
                    }
                });
            }
            catch(Win32Exception )
            {
                Console.WriteLine("Oh no, failed to start ffmpeg. Have you downloaded and copied ffmpeg.exe to the output folder?");
            }

            Console.WriteLine();
            Console.WriteLine("Video was successfully created. It is availible at: " + Path.GetFullPath(outPath));
        }


        // Get the original images to use.
        // In real life you will probably just do something like this:
        //
        // return Directory.EnumerateFiles(OriginalImagesPath, "*.jpg", enumerationOptions: new EnumerationOptions()
        // {
        //     RecurseSubdirectories = true
        // }).OrderBy(a => a).ToList();
        //
        // But since this is just demo code, we will create some images
        // instead with an advanced animation.
        private async Task<List<string>> GetOriginalImagesAsync()
        {
            List<string> originalImages = new List<string>();

            int width = 320;
            int height = 200;

            Color objectsColor = Color.ParseHex("191D7C");

            for (int i = 0; i < FramesPerSecond * 10; i++)
            {
                using (var image = new Image<Rgba32>(width, height, Rgba32.ParseHex("6D8AFF")))
                {
                    SixLabors.ImageSharp.Drawing.IPath yourPolygon =
                        new SixLabors.ImageSharp.Drawing.Star
                        (
                            width - height * 0.15f,
                            height * 0.85f,
                            prongs: 4,
                            innerRadii: height * 0.05f,
                            outerRadii: height * 0.10f,
                            (float)-(Math.PI * 2 * i / FramesPerSecond / 6 )
                            ); ;

                    image.Mutate(x => x.Fill(objectsColor, yourPolygon));


                    int objectWidth = width / 3;

                    // Math is fun!
                    const int framesToPassTheScreen = FramesPerSecond * 2;
                    int x = -objectWidth + (width + objectWidth) * ((i + 1) % framesToPassTheScreen) / framesToPassTheScreen;

                    if (x > -objectWidth)
                    {
                        var rectangle = new Rectangle(x, height / 3, objectWidth, height / 3);

                        image.Mutate(x => x.Fill(objectsColor, rectangle));
                    }

                    string fullFileName = Path.Combine(OriginalImagesPath, $@"{i:0000000}.png");

                    await image.SaveAsync(fullFileName, new PngEncoder());

                    originalImages.Add(fullFileName);
                }
            }

            return originalImages;
        }

        // Creates "overlay" images. That is the original image but with a time stamp.
        // The first images will also be faded from the fadiInImage parameter.
        public async Task<List<string>> CreateOverlayImagesAsync(List<string> orgimages, string fadeInImage)
        {
            List<string> overlayImages = new List<string>();

            using (FileStream fs = new FileStream(fadeInImage, FileMode.Open))
            {
                using (Image lastTitleImage = await Image.LoadAsync(fs))
                {
                    float opacity = 1;

                    foreach (var orgImagePath in orgimages)
                    {
                        opacity = Math.Max(opacity - 1.0f / FramesPerSecond, 0);

                        string newfilename = Path.Combine(FramesPath, Path.GetFileName(orgImagePath));

                        await CreateOverlayAsync(orgImagePath, newfilename, lastTitleImage, opacity);

                        overlayImages.Add(newfilename);
                    }
                }
            }

            return overlayImages;
        }

        DateTime ImageTimeStamp = DateTime.Now;

        // Create a single "overlay" image.
        async Task CreateOverlayAsync(string inputPath, string outputPath, Image fadedImage, float fadeRatio)
        {
            IBrush backgroundbrush = Brushes.Solid(Color.FromRgba(0, 0, 0, 224));
            IPen backgroundpen = Pens.Solid(Color.Blue, 1);

            // Creates a new image with empty pixel data. 
            using (var image = Image.Load(inputPath))
            {
                Rectangle rectangle = new Rectangle(4, 5, 139, 20);

                image.Mutate(x => x.Fill(backgroundbrush, rectangle)
                                    .Draw(backgroundpen, rectangle)
                                    );

                // You should find a better way to get the timestamp :)
                string text = ImageTimeStamp.ToString("yyyy-MM-dd HH:mm");
                ImageTimeStamp = ImageTimeStamp.AddMinutes(1);

                Color fontColor = Color.White;
                Font OverlayFont = MyFontFamily.CreateFont(14, FontStyle.Regular);
                TextGraphicsOptions textoptions = new TextGraphicsOptions()
                {
                    GraphicsOptions = new GraphicsOptions()
                    {
                        Antialias = true
                    }
                };

                // Draw the text
                image.Mutate(x => x.DrawText(textoptions, text, OverlayFont, fontColor, new PointF(8, 7)));

                // Fade in the other image
                if (fadedImage != null && fadeRatio > 0)
                {
                    image.Mutate(x => x.DrawImage(fadedImage, fadeRatio));
                }

                await image.SaveAsync(outputPath, new PngEncoder()).ConfigureAwait(false);
            }
        }

        // Creates a thumbnail image. That is created from a background image, 
        // and then some text is added to that image.
        async Task CreateThumbnailImageAsync(string backgroundImagePath, string thumbnailImagePath)
        {
            using (FileStream fs = new FileStream(backgroundImagePath, FileMode.Open))
            {
                using (Image image = await Image.LoadAsync(fs))
                {
                    int width = image.Width;
                    int height = image.Height;

                    var rectangle = new Rectangle(0, height * 26 / 100, width, height * 48 / 100);

                    image.Mutate(x => x.Fill(Color.FromRgba(0, 0, 0, 204), rectangle));

                    TextGraphicsOptions textOptions = new TextGraphicsOptions()
                    {
                        TextOptions = new TextOptions()
                        {
                            WrapTextWidth = width,
                            HorizontalAlignment = HorizontalAlignment.Center
                        },
                        GraphicsOptions = new GraphicsOptions()
                        {
                            Antialias = true
                        }
                    };

                    Color fontColor = Color.White;

                    Font titlefont = MyFontFamily.CreateFont(height / 200.0f * 32, FontStyle.Regular);
                    string title = Title;
                    image.Mutate(x => x.DrawText(textOptions, title, titlefont, fontColor, new PointF(0, height / 3)));


                    Font subtitlefont = MyFontFamily.CreateFont(height / 200.0f * 20, FontStyle.Regular);
                    string subtitle = SubTitle;
                    image.Mutate(x => x.DrawText(textOptions, subtitle, subtitlefont, fontColor, new PointF(0, height / 100 * 55)));

                    await image.SaveAsync(thumbnailImagePath, new PngEncoder()).ConfigureAwait(false);
                }
            }
        }

        // Create a title screen. That is text that fades in on a black background.
        async Task<List<string>> CreateTitleScreenAsync(int width, int height)
        {
            List<string> titleFramesFileNames = new List<string>();

            TextGraphicsOptions textOptions = new TextGraphicsOptions()
            {
                TextOptions = new TextOptions()
                {
                    WrapTextWidth = width,
                    HorizontalAlignment = HorizontalAlignment.Center
                },
                GraphicsOptions = new GraphicsOptions()
                {
                    Antialias = true
                }
            };

            const int fadeFramesCount = FramesPerSecond;

            // The text in in title screens will fade in in one second.
            for (int i = 1; i <= fadeFramesCount; i++)
            {
                using (var image = new Image<Rgba32>(width, height, Rgba32.ParseHex("000000")))
                {
                    // Calculate the color. The first will be very transparent, the last not.
                    Color fontColor = Color.FromRgba(255, 255, 255, (byte)(255.0f * i / fadeFramesCount));

                    Font titlefont = MyFontFamily.CreateFont(height / 200.0f * 32, FontStyle.Regular);
                    string title = Title;
                    image.Mutate(x => x.DrawText(textOptions, title, titlefont, fontColor, new PointF(0, height / 3)));


                    Font subtitlefont = MyFontFamily.CreateFont(height / 200.0f * 20, FontStyle.Regular);
                    string subtitle = SubTitle;
                    image.Mutate(x => x.DrawText(textOptions, subtitle, subtitlefont, fontColor, new PointF(0, height / 100 * 55)));

                    string fullname = Path.Combine(TitleFramesPath, @$"{i:000}.png");
                    await image.SaveAsync(fullname, new PngEncoder());
                    titleFramesFileNames.Add(fullname);
                }
            }

            // We should have more frames where the nothing changes.
            // We are just reusing that last images seveal times.
            for (int i = 0; i < FramesPerSecond; i++)
            {
                titleFramesFileNames.Add(titleFramesFileNames.Last());
            }

            return titleFramesFileNames;
        }
    }
}
