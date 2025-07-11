using Microsoft.Playwright;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;

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
}
