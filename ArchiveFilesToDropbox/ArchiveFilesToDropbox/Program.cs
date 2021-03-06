﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Dropbox.Api;
using Dropbox.Api.Files;
using Serilog;

namespace ArchiveFilesToDropbox
{
    class Program
    {
        private static ILogger Logger;
        static void Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;

            Environment.SetEnvironmentVariable("BASEDIR", AppDomain.CurrentDomain.BaseDirectory);
            Logger = new LoggerConfiguration().ReadFrom.AppSettings().CreateLogger();

            try
            {
                var archiveFolderPath = ConfigurationManager.AppSettings["ArchiveFolder"];
                var trashFolder = ConfigurationManager.AppSettings["TrashFolder"];
                bool simulateOnly = bool.Parse(ConfigurationManager.AppSettings["SimulateOnly"] ?? "true");

                int noOfFileToTake = int.Parse(ConfigurationManager.AppSettings["NoOfLatestFileToArchive"] ?? "1");
                int noOfFileToKeep = int.Parse(ConfigurationManager.AppSettings["NoOfLatestFileToKeep"] ?? "5");
                string dropboxAccessToken = ConfigurationManager.AppSettings["DropboxAccessToken"];
                string dropboxFolderToArchive = ConfigurationManager.AppSettings["DropboxFolderToArchive"];


                var archiveFolder = new DirectoryInfo(archiveFolderPath);
                Logger.Information("Getting archive folder:{archiveFolder}", archiveFolder);
                var archiveFiles = archiveFolder.GetFiles().OrderByDescending(f => f.LastWriteTime).Take(noOfFileToTake);

                int noOfFileToDelete = Math.Max(0, archiveFolder.GetFiles().Length - noOfFileToKeep);
                var deleteFiles = archiveFolder.GetFiles().OrderBy(f => f.LastWriteTime).Take(noOfFileToDelete);

                foreach (var fileToAchive in archiveFiles)
                {
                    Logger.Information("File to be archived:{0}", fileToAchive.FullName);

                    if (!simulateOnly)
                    {
                        var dbx = new DropboxClient(dropboxAccessToken);
                        var dbFileMetaData = UploadFileToDropbox(dbx, fileToAchive.FullName, dropboxFolderToArchive, fileToAchive.Name).Result;
                        Logger.Information("Uploaded File Success");
                    }
                    //Logger.Information("Uploaded File Info:{@dbFileMetaData}", dbFileMetaData);

                }

                if (!Directory.Exists(trashFolder))
                {
                    Directory.CreateDirectory(trashFolder);
                }
                foreach (var fileToDelete in deleteFiles)
                {
                    Logger.Warning("File to be trashed:{0}", fileToDelete.FullName);
                    if (!simulateOnly)
                    {
                        string moveToLocation = Path.Combine(trashFolder, fileToDelete.Name);
                        fileToDelete.MoveTo(moveToLocation);
                    }
                }



                //Console.ReadLine();

            }
            catch (Exception ex)
            {
                Logger.Error(ex, "");
                //Console.ReadLine();

            }
        }




        private static async Task<FileMetadata> UploadFileToDropbox(DropboxClient dbx, string localFilePath, string dropboxFolder, string fileName)
        {

            using (MemoryStream fileStream = new MemoryStream(File.ReadAllBytes(localFilePath)))
            {
                string filterdFileName = fileName.Replace("-", "");
                Logger.Information("Uploading:{0} to Dropbox", localFilePath);

                string targetLocation = $"/{dropboxFolder}/{filterdFileName}";
                if (string.IsNullOrEmpty(dropboxFolder))
                {
                    targetLocation = $"/{filterdFileName}";
                }
                Logger.Information("Target Dropbox path:{0}", targetLocation);
                var uploadedFileInformation = await dbx.Files.UploadAsync(targetLocation, WriteMode.Overwrite.Instance, body: fileStream);
                return uploadedFileInformation;
            }

        }




    }

}

