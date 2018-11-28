﻿using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using CatalogueLibrary;
using Renci.SshNet;
using ReusableLibraryCode.Progress;

namespace LoadModules.Generic.FTP
{
    /// <summary>
    /// load component which downloads files from a remote SFTP (Secure File Transfer Protocol) server to the ForLoading directory
    /// 
    /// <para>Operates in the same way as <see cref="FTPDownloader"/> except that it uses SSH.  In addition this 
    /// class will not bother downloading any files that already exist in the forLoading directory (have the same name - file size is NOT checked)</para>
    /// </summary>
    public class SFTPDownloader:FTPDownloader
    {
        protected override void Download(string file, IHICProjectDirectory destination,IDataLoadEventListener job)
        {
            if (file.Contains("/") || file.Contains("\\"))
                throw new Exception("Was not expecting a relative path here");
            
            Stopwatch s = new Stopwatch();
            s.Start();
            
            using(var sftp = new SftpClient(_host,_username,_password))
            {
                sftp.ConnectionInfo.Timeout = new TimeSpan(0, 0, 0, TimeoutInSeconds);
                sftp.Connect();
                
                //if there is a specified remote directory then reference it otherwise reference it locally (or however we were told about it from GetFileList())
                string fullFilePath = !string.IsNullOrWhiteSpace(RemoteDirectory) ? Path.Combine(RemoteDirectory, file) : file;
                
                string destinationFilePath = Path.Combine(destination.ForLoading.FullName, file);

                //register for events
                Action<ulong> callback = (totalBytes) => job.OnProgress(this, new ProgressEventArgs(destinationFilePath, new ProgressMeasurement((int)(totalBytes * 0.001), ProgressType.Kilobytes), s.Elapsed));

                using (var fs = new FileStream(destinationFilePath, FileMode.CreateNew))
                {
                    //download
                    sftp.DownloadFile(fullFilePath, fs, callback);
                    fs.Close();
                }
                _filesRetrieved.Add(fullFilePath);

            }
            s.Stop();
        }


        public override void LoadCompletedSoDispose(ExitCodeType exitCode, IDataLoadEventListener postLoadEventListener)
        {
            if(exitCode == ExitCodeType.Success)
            {
                using (var sftp = new SftpClient(_host, _username, _password))
                {
                    sftp.ConnectionInfo.Timeout = new TimeSpan(0, 0, 0, TimeoutInSeconds);
                    sftp.Connect();
                    
                    foreach (string retrievedFiles in _filesRetrieved)
                        try
                        {
                            sftp.DeleteFile(retrievedFiles);
                            postLoadEventListener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Information, "Deleted SFTP file " + retrievedFiles + " from SFTP server"));
                        }
                        catch (Exception e)
                        {
                            postLoadEventListener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Error, "Could not delete SFTP file " + retrievedFiles + " from SFTP server", e));
                        }
                }
                
            }
        }


        protected override string[] GetFileList()
        {
            using (var sftp = new SftpClient(_host, _username, _password))
            {
                sftp.ConnectionInfo.Timeout = new TimeSpan(0, 0, 0, TimeoutInSeconds);
                sftp.Connect();

                string directory = RemoteDirectory;

                if (string.IsNullOrWhiteSpace(directory))
                    directory = ".";
                
                return sftp.ListDirectory(directory).Select(d=>d.Name).ToArray();
            }

        }
    }
}
