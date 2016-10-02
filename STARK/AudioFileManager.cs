﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;

namespace STARK {
    public class AudioFileManager {

        string watchFolder;
        bool directoryExists = false;
        FileSystemWatcher watcher;
        ObservableCollection<AudioPlaybackItem> collection;
        ObservableCollection<string> tagsTracker;

        public AudioFileManager(string watchFolder) {
            this.watchFolder = watchFolder;
            collection = new ObservableCollection<AudioPlaybackItem>();
            tagsTracker = new ObservableCollection<string>();
            SetupWatcher();
        }

        private void SetupWatcher() {
            if (watcher != null) watcher.Dispose();
            if (Directory.Exists(watchFolder)) {
                directoryExists = true;
                watcher = new FileSystemWatcher();
                watcher.Path = watchFolder;
                watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName;
                watcher.Filter = "*.*"; //watch for all files, we filter audio files out in the event.
                watcher.Created += Watcher_Created;
                watcher.Deleted += Watcher_Deleted;
                watcher.Renamed += Watcher_Renamed;
                watcher.EnableRaisingEvents = true;
            }
        }

        public void LoadCurrentFiles() {
            if (watchFolder != null && directoryExists) {
                var files = Directory.GetFiles(watchFolder);
                foreach (string file in files) {
                    if (IsAudioFile(Path.GetExtension(file))) {
                        AddToCollection(new AudioPlaybackItem("", file, 100));
                    }
                }
            }
        }

        public void ChangeWatchFolder(string watchFolder) {
            if (Directory.Exists(watchFolder)) {
                this.watchFolder = watchFolder;
                watcher.Path = this.watchFolder;
                directoryExists = true;

                if (watcher == null) {
                    SetupWatcher();
                }
            } else {
                directoryExists = false;
            }
        }

        private bool IsAudioFile(string ext) {
            //Only load the actual files we can play.
            return Regex.IsMatch(ext, @"\.mp3|\.wav|\.aac|\.wma|\.m4a|\.mp4|\.wmv|\.avi|\.m4v|\.mov", RegexOptions.IgnoreCase);
        }

        //COLLECTION METHODS
        #region "Collection Methods"
        //generates an id for the file and adds to the collection
        public void AddToCollection(AudioPlaybackItem item) {
            App.Current.Dispatcher.Invoke(delegate {
                item.ChangeId(collection.Count);
                collection.Add(item);
            });
        }

        public void RemoveFromCollection(string path) {
            App.Current.Dispatcher.Invoke(delegate {
                foreach (AudioPlaybackItem item in collection) {
                    if (item.GetPath() == path) {
                        collection.Remove(item);
                        RecalculateIDs();
                        break;
                    }
                }
            });
        }

        public void RecalculateIDs() {
            int currentID = 0;
            foreach (AudioPlaybackItem item in collection) {
                item.ChangeId(currentID);
                currentID++;
            }
        }
        #endregion

        //EVENTS
        #region "Events"
        private void Watcher_Renamed(object sender, RenamedEventArgs e) {
            string ext = Path.GetExtension(e.FullPath);

            if (IsAudioFile(ext)) {
                //yes i know i could just change the name, but i'm wayyyy to lazy and tired
                RemoveFromCollection(e.FullPath);
                AddToCollection(new AudioPlaybackItem("", e.FullPath, 100));
            }
        }

        private void Watcher_Deleted(object sender, FileSystemEventArgs e) {
            string ext = Path.GetExtension(e.FullPath);

            if (IsAudioFile(ext)) {
                RemoveFromCollection(e.FullPath);
            }
        }

        private void Watcher_Created(object sender, FileSystemEventArgs e) {
            string ext = Path.GetExtension(e.FullPath);

            if (IsAudioFile(ext)) {
                AddToCollection(new AudioPlaybackItem("", e.FullPath, 100));
            }
        }
        #endregion

        //GETTERS
        #region "Getters"
        public ObservableCollection<AudioPlaybackItem> getCollection() {
            return collection;
        }

        public ObservableCollection<string> getTagsTracker() {
            return tagsTracker;
        }
        #endregion
    }
}