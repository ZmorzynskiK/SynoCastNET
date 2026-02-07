using SynoCastNET;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using YoutubeExplode;
using YoutubeExplode.Channels;
using YoutubeExplode.Common;
using YoutubeExplode.Playlists;
using YoutubeExplode.Videos;
using YoutubeExplode.Videos.Streams;

Console.WriteLine("SynoCASTNET - YouTube audio only podcast downloader for Synology NAS");
var buildInfo = GetBuildInfo();
Console.WriteLine($"Build version: {buildInfo.Version}, date (UTC): {buildInfo.BuildDateUTC}");

var youtube = new YoutubeClient();

// Get JSON file from command line or use default
string jsonFile = args.Length > 0 ? args[0] : "config.json";

// Read and parse the configuration file
var config = JsonSerializer.Deserialize(
    File.ReadAllText(jsonFile),
    ConfigSerializationContext.Default.Config ) ?? throw new Exception($"Failed to load configuration file: {jsonFile}");

await ProcessSourcesAsync(config, youtube);

static async Task ProcessSourcesAsync(Config config, YoutubeClient youtube)
{
    foreach (var source in config.sources)
    {
        Console.WriteLine("\n***************************************************************");
        Console.WriteLine($"Processing source: {source.name} (Language: {source.language}, Container: {source.container ?? "any"})");
        IEnumerable<PlaylistVideo> videos = Enumerable.Empty<PlaylistVideo>();
        try
        {
            switch (source.type.ToLower())
            {
                case "channel_handle":
                    var handle = source.url.TrimStart('@');
                    var channel = await youtube.Channels.GetByHandleAsync($"@{handle}");
                    videos = (await youtube.Channels.GetUploadsAsync(channel.Id)).Take(source.maxItems);
                    break;
                case "playlist_url":
                    videos = (await youtube.Playlists.GetVideosAsync(source.url)).Take(source.maxItems);
                    break;
                default:
                    Console.WriteLine($"Unsupported source type: {source.type}");
                    continue;
            }
            await ProcessVideosAsync(videos, config, source, youtube);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing source {source.name}: {ex.Message}");
        }
    }
}

static char[] GetUniversalInvalidFileNameChars() =>
    [
        '\"', '<', '>', '|', '\0',
        (char)1, (char)2, (char)3, (char)4, (char)5, (char)6, (char)7, (char)8, (char)9, (char)10,
        (char)11, (char)12, (char)13, (char)14, (char)15, (char)16, (char)17, (char)18, (char)19, (char)20,
        (char)21, (char)22, (char)23, (char)24, (char)25, (char)26, (char)27, (char)28, (char)29, (char)30,
        (char)31, ':', '*', '?', '\\', '/'
    ];

static async Task ProcessVideosAsync(IEnumerable<PlaylistVideo> videos, Config config, Source source, YoutubeClient youtube)
{
    var safeSourceName = string.Join("_", source.name.Split(GetUniversalInvalidFileNameChars()));
    var finalDataDir = Path.Combine(config.outputDirectory ?? Directory.GetCurrentDirectory(), safeSourceName);
    Directory.CreateDirectory(finalDataDir);

    foreach (var video in videos.Reverse()) // Reverse to process oldest first
    {
        if (video.Duration is null)
        {
            Console.WriteLine($"Video {video.Url} has no duration information (live?). Skipping.");
            continue;
        }

        try
        {
            using var manifestTimeout = new CancellationTokenSource();
            manifestTimeout.CancelAfter(TimeSpan.FromSeconds(30)); // Set a timeout for each manifest retrieval

            var v = await youtube.Videos.GetAsync(video.Url);
            Console.WriteLine($"Title: {v.Title}");
            Console.WriteLine($"Url: {v.Url}");
            Console.WriteLine($"Duration: {video.Duration}");

            // check duration limits if set
            if(source.minDurationSecs.HasValue && video.Duration.Value.TotalSeconds < source.minDurationSecs)
            {
                Console.WriteLine($"Video duration {video.Duration} is shorter than minimum {source.minDurationSecs} seconds. Skipping.");
                continue;
            }

            var streamManifest = await youtube.Videos.Streams.GetManifestAsync(v.Url, manifestTimeout.Token);
            var audioStreams = streamManifest.GetAudioOnlyStreams();
            var streamInfo = audioStreams
                                .Where(s => string.Equals(s.AudioLanguage?.Code, source.language, StringComparison.OrdinalIgnoreCase)
                                    || (s.AudioLanguage is not null && s.AudioLanguage.Value.Code.StartsWith($"{source.language}-", StringComparison.OrdinalIgnoreCase)));
            // check if we have any stream - if not then get streams for any language
            if (!streamInfo.Any())
            {
                Console.WriteLine($"No audio streams found for language '{source.language}'. Trying to get streams without language set.");
                streamInfo = audioStreams.Where(s => s.AudioLanguage is null || string.IsNullOrWhiteSpace(s.AudioLanguage.Value.Code));
            }
            if (!string.IsNullOrWhiteSpace(source.container))
                streamInfo = streamInfo.Where(s => string.Equals(s.Container.Name, source.container, StringComparison.OrdinalIgnoreCase));

            var bestStream = streamInfo.OrderByDescending(s => s.Bitrate).FirstOrDefault();
            if (bestStream != null)
            {
                Console.WriteLine($"Best Stream: {bestStream.Container}, {bestStream.Bitrate} bps, {bestStream.Size} bytes, codec: {bestStream.AudioCodec}, lang: {bestStream.AudioLanguage} ({bestStream.IsAudioLanguageDefault})");
                // Download the best stream
                var safeTitle = string.Join("_", v.Title.Split(GetUniversalInvalidFileNameChars()));
                var fileExt = bestStream.Container.Name;
                var filePath = Path.Combine(finalDataDir, $"{safeTitle}.{fileExt}");
                if (File.Exists(filePath))
                {
                    Console.WriteLine($"File already exists: {filePath}. Skipping download.");
                }
                else
                {
                    Console.WriteLine($"Downloading to: {filePath}");
                    using var progress = new ConsoleStarProgress();
                    await youtube.Videos.Streams.DownloadAsync(bestStream, filePath, progress);
                    Console.WriteLine("\nDownload complete.");
                }
            }
            else
            {
                Console.WriteLine($"No audio stream found for language '{source.language}' and container '{source.container ?? "any"}'.");
            }
            Console.WriteLine();
        }
        catch(OperationCanceledException)
        {
            Console.WriteLine($"Processing of video {video.Url} was cancelled due to timeout.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing video {video.Url}: {ex.Message}");
        }
    }
    // Remove oldest files if maxDownloads is set (or use maxItems*2 if null)
    int effectiveMaxDownloads = source.maxDownloads ?? (source.maxItems * 2);
    if (effectiveMaxDownloads > 0)
    {
        var files = Directory.GetFiles(finalDataDir)
            .Select(f => new FileInfo(f))
            .OrderBy(f => f.LastWriteTimeUtc)
            .ToList();
        if (files.Count > effectiveMaxDownloads)
        {
            var toDelete = files.Take(files.Count - effectiveMaxDownloads);
            foreach (var file in toDelete)
            {
                try
                {
                    file.Delete();
                    Console.WriteLine($"Deleted old file: {file.FullName}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to delete file {file.FullName}: {ex.Message}");
                }
            }
        }
    }
}

static (string Version, string BuildDateUTC) GetBuildInfo()
{
    var assembly = Assembly.GetExecutingAssembly();
    const string BuildVersionMetadataPrefix = "+buildUTC";

    var attribute = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
    if (attribute?.InformationalVersion != null)
    {
        var value = attribute.InformationalVersion;
        var index = value.IndexOf(BuildVersionMetadataPrefix);
        if (index > 0)
        {
            string version = value.Substring(0, index);
            string buildDate = value.Substring(index + BuildVersionMetadataPrefix.Length);
            return (version, buildDate);
        }
    }

    return ("?", "?");
}