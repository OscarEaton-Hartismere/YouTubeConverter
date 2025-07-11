﻿using Microsoft.Playwright;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using Xabe.FFmpeg;  // Make sure you have this NuGet package installed

namespace PlayWrightYouTube;

class Program
{
    public static async Task Main()
    {
        var urlsFilePath = "urls.txt";

        if (!File.Exists(urlsFilePath))
        {
            Console.WriteLine($"File not found: {urlsFilePath}");
            return;
        }

        var urls = new List<string>();
        foreach (var line in File.ReadLines(urlsFilePath))
        {
            var trimmed = line.Trim();
            if (!string.IsNullOrEmpty(trimmed))
                urls.Add(trimmed);
        }

        if (urls.Count == 0)
        {
            Console.WriteLine("No URLs found in the file.");
            return;
        }

        string downloadsFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Downloads", "youtube"
        );

        Directory.CreateDirectory(downloadsFolder);

        // Set FFmpeg executables folder (download FFmpeg executables if you haven't)
        FFmpeg.SetExecutablesPath("ffmpeg");

        using var playwright = await Playwright.CreateAsync();
        var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = false });
        var context = await browser.NewContextAsync();
        var page = await context.NewPageAsync();

        foreach (var youtubeUrl in urls)
        {
            Console.WriteLine($"Processing: {youtubeUrl}");

            await page.GotoAsync("https://ezmp3.to/");

            await page.FillAsync("input[placeholder='Please paste the YouTube video URL here']", youtubeUrl);
            await page.ClickAsync("button:has-text('Convert')");
            await page.WaitForSelectorAsync("button:has-text('Download MP3')", new PageWaitForSelectorOptions { Timeout = 60000 });

            var downloadTask = page.WaitForDownloadAsync();
            await page.ClickAsync("button:has-text('Download MP3')");
            var download = await downloadTask;

            var tempFilePath = await download.PathAsync();
            var suggestedFileName = download.SuggestedFilename;
            var finalPath = GetUniqueFilePath(downloadsFolder, suggestedFileName);

            File.Move(tempFilePath, finalPath);

            Console.WriteLine($"Downloaded to: {finalPath}");

            // Normalize audio loudness after download
            string normalizedPath = Path.Combine(downloadsFolder, Path.GetFileNameWithoutExtension(finalPath) + "_normalized.mp3");
            await NormalizeLoudNorm(finalPath, normalizedPath);

            // Optionally, replace original with normalized
            File.Delete(finalPath);
            File.Move(normalizedPath, finalPath);

            Console.WriteLine($"Normalized and replaced original file at: {finalPath}");
        }

        await browser.CloseAsync();

        Console.WriteLine("All downloads completed.");
    }

    static string GetUniqueFilePath(string folder, string fileName)
    {
        string filePath = Path.Combine(folder, fileName);

        if (!File.Exists(filePath))
            return filePath;

        string nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
        string ext = Path.GetExtension(fileName);

        int count = 1;
        while (true)
        {
            string newFileName = $"{nameWithoutExt}({count}){ext}";
            string newFilePath = Path.Combine(folder, newFileName);

            if (!File.Exists(newFilePath))
                return newFilePath;

            count++;
        }
    }

    static async Task NormalizeLoudNorm(string inputPath, string outputPath)
    {
        FFmpeg.SetExecutablesPath(@"C:\FFMpeg\ffmpeg-7.1.1-essentials_build\ffmpeg-7.1.1-essentials_build\bin");
        var conversion = FFmpeg.Conversions.New()
            .AddParameter($"-i \"{inputPath}\"", ParameterPosition.PreInput)
            .AddParameter("-af loudnorm")
            .SetOutput(outputPath)
            .SetOverwriteOutput(true);

        await conversion.Start();
    }
}
