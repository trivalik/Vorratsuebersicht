﻿using System;
using System.IO;
using System.Threading;
using System.Collections.Generic;
using Android.Content;
using Android.App;
using Android.OS;
using Android.Widget;

// Anhand von
// http://www.c-sharpcorner.com/UploadFile/1e050f/creating-splash-screen-for-android-app-in-xamarin/
// 
namespace VorratsUebersicht  
{
    using static Tools;

    [Activity(Label="Vorratsübersicht",MainLauncher=true,Theme="@style/Theme.Splash",NoHistory=true,Icon="@drawable/ic_launcher")]  
    public class SplashScreenActivity : Activity  
    {  
        private TextView    progressText;
        private ProgressBar progressBar;

        protected override void OnCreate(Bundle bundle)  
        {  
            base.OnCreate(bundle);  

            SetContentView(Resource.Layout.SplashScreen);

            this.progressText = FindViewById<TextView>(Resource.Id.SplashScreen_ProgressText);
            this.progressBar  = FindViewById<ProgressBar>(Resource.Id.SplashScreen_ProgressBar);

            bool ok = this.InitializeApp();
            if (ok)
            {
                this.CheckAndMoveArticleImages();
                StartActivity(typeof(MainActivity));
            }
        }

        private bool InitializeApp() 
        {
            if (Android_Database.SQLiteConnection != null)
                return true;
            
            List<string> fileList;

            try
            {
                fileList = this.GetFileList();
            }
            catch(Exception ex)
            {
                string text = "Bitte ggf. den Eintrag 'Zusätzlicher Datenbankpfad' in den Einstellungen prüfen.";
                TRACE("SplashScreen: {0}", ex.Message);
                TRACE("SplashScreen: {0}", text);

                text = ex.Message + "\n\n" + text;

                Toast.MakeText(this, text, ToastLength.Long).Show();

                this.ConvertAndStartMainScreen();
                
                /*
                 * Die Meldung kam bei Neuinstallation und könnte den Benutzer verwirren.
                 * 
                var message = new AlertDialog.Builder(this);
                message.SetMessage(text);
                message.SetIcon(Resource.Drawable.ic_launcher);
                message.SetPositiveButton("Ok", (s, e) => 
                {
                    this.ConvertAndStartMainScreen();
                });
                message.Create().Show();
                */
                return false;
            }

            if (fileList.Count == 1)
            {
                Android_Database.SelectedDatabaseName = fileList[0];
                return true;
            }

            if (fileList.Count == 0)
            {
                return true;
            }

            string[] databaseNames = new string[fileList.Count];

            for(int i = 0; i < fileList.Count; i++)
            {
                databaseNames[i] = Path.GetFileNameWithoutExtension(fileList[i]);
            }

            AlertDialog.Builder builder = new AlertDialog.Builder(this);
            builder.SetTitle("Datenbank auswählen:");
            builder.SetItems(databaseNames, (sender2, args) =>
            {
                Android_Database.SelectedDatabaseName = fileList[args.Which];

                this.ConvertAndStartMainScreen();
            });

            builder.SetOnCancelListener(new OnDismissListener(() =>
            {
                Android_Database.SelectedDatabaseName = fileList[0];        // Ersten Eintrag auswählen

                this.ConvertAndStartMainScreen();
            }));

            builder.Show();

            return false;

            /*
            bool emulator = Android.OS.Environment.IsExternalStorageEmulated;
            string status = Android.OS.Environment.ExternalStorageState;
            bool canWrite = Android.OS.Environment.ExternalStorageDirectory.CanWrite();
            string sdCardPath = Android.OS.Environment.ExternalStorageDirectory.AbsolutePath;
            string databasePath = Android_Database.Instance.GetDatabasePath();
            
            TRACE("********************************************");
            TRACE("Is SD card emulated : {0}", emulator);
            TRACE("SD card state       : {0}", status);
            TRACE("Can write SD card   : {0}", canWrite);
            TRACE("SD card path        : {0}", sdCardPath);
            TRACE("Database path       : {0}", databasePath);
            TRACE("Database on SD card : {0}", Android_Database.IsDatabaseOnSdCard);
            TRACE("********************************************");

            for (int progress=0; progress<100; progress+=10) 
            {
                System.Threading.Thread.Sleep(1000);

                RunOnUiThread(() =>
                {
                    this.progressText.SetText(progress.ToString(), TextView.BufferType.Normal);
                });
            }
            */
        }

        private List<string> GetFileList()
        {
            var fileList = new List<string>();

            string addPath = Settings.GetString("AdditionslDatabasePath", string.Empty);
            
            if (!string.IsNullOrEmpty(addPath))
            {
                fileList.AddRange(Directory.GetFiles(addPath, "*.db3"));
            }
            
            string sdCardPath = Android_Database.Instance.GetSdCardPath();
            if (Directory.Exists(sdCardPath))
            {
                fileList.AddRange(Directory.GetFiles(sdCardPath, "*.db3"));
            }

            return fileList;
        }

        private void ConvertAndStartMainScreen()
        {
            new System.Threading.Thread(new ThreadStart(delegate             
            {
                this.CheckAndMoveArticleImages();
                StartActivity(typeof(MainActivity));
            })).Start();
        }

        //
        // Bilder in die neue Tabelle übertragen
        //
        private bool CheckAndMoveArticleImages()
        {
            SQLite.SQLiteConnection databaseConnection;

            try
            {
                databaseConnection = Android_Database.Instance.GetConnection();

            }
            catch
            {
                return true;
            }

            // Nur, wenn bereits eine Datenbank vorhanden ist
            if (databaseConnection == null)
                return true;

            // Artikelbilder ermitteln, die noch nicht übertragen wurden.
            var articleImagesToCopy = databaseConnection.Query<ArticleData>(
                "SELECT ArticleId, Name" +
                " FROM Article" +
                " WHERE Image IS NOT NULL" +
                " AND ArticleId NOT IN (SELECT ArticleId FROM ArticleImage)" +
                " ORDER BY Name COLLATE NOCASE");

            if (articleImagesToCopy.Count == 0)
                return true;

            string message = string.Format("Übertrage {0} Artikelbilder...", articleImagesToCopy.Count);
            TRACE(message);
            RunOnUiThread(() =>
            {
                this.progressText.SetText(message, TextView.BufferType.Normal);
                Thread.Sleep(500);
            });

            // Einzelne Bilder kopieren, damit die Datenbankgröße nicht zu stark wächst.
            string cmdCopyImages = 
                "INSERT INTO ArticleImage (ArticleId, Type, ImageSmall, ImageLarge, CreatedAt)" +
                " SELECT ArticleId, 0,  Image AS ImageSmall, ImageLarge, DATETIME('now')" +
                " FROM Article" +
                " WHERE ArticleId = ?";

            // Bilder löschen, da sie schon übernommen wurden
            string cmdClearImages = 
                "UPDATE Article" +
                " SET Image = NUll, ImageLarge = NULL" +
                " WHERE ArticleId IN (SELECT ArticleId FROM ArticleImage)";

            int count = 0;
            int max = articleImagesToCopy.Count;

            Exception exception = null;
            this.progressBar.Max = max;

            foreach(ArticleData article in articleImagesToCopy)
            {
                try
                {
                    databaseConnection.Execute(cmdCopyImages, article.ArticleId);
                    databaseConnection.Execute(cmdClearImages);
                }
                catch(Exception ex)
                {
                    exception = ex;
                    break;
                }
                count++;

                RunOnUiThread(() =>
                {
                    message = string.Format("Übertrage Artikelbild {0} von {1}\n{2}",
                        count,
                        max,
                        article.Name);

                    this.progressBar.Progress = count;
                    this.progressText.SetText(message, TextView.BufferType.Normal);
                    Thread.Sleep(200);
                });
            }

            if (exception != null)
            {
                RunOnUiThread(() =>
                {
                    message = string.Format("Fehler '{0}' beim Übertragen der {1} Bilder in die neue Tabelle.\n\n" +
                        "Bitte beenden Sie die Anwendung, verschaffen mehr Platz auf der SD Karte und dem internen Speicher " +
                        "und starten Sie die App erneut",
                        exception.Message,
                        articleImagesToCopy.Count);

                    this.progressBar.Visibility = Android.Views.ViewStates.Invisible;

                    var dialog = new AlertDialog.Builder(this);
                    dialog.SetMessage(message);
                    dialog.SetTitle(Resource.String.App_Name);
                    dialog.SetIcon(Resource.Drawable.ic_launcher);
                    dialog.SetPositiveButton("OK", (s, e) => 
                    { 
                        StartActivity(typeof(MainActivity));
                        
                    });
                    dialog.Create().Show();
                });

                return false;
            }

            return true;
        }

        private class OnDismissListener : Java.Lang.Object, IDialogInterfaceOnCancelListener
        {
            private readonly Action action;

            public OnDismissListener(Action action)
            {
                this.action = action;
            }

            public void OnCancel(IDialogInterface dialog)
            {
                this.action();
            }
        }
    }
}   